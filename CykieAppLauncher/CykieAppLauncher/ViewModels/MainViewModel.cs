using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Reactive;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using CykieAppLauncher.Models;
using CykieAppLauncher.Views;
using ReactiveUI;

using Version = CykieAppLauncher.Models.Version;

namespace CykieAppLauncher.ViewModels
{
    public class MainViewModel : ViewModelBase
    {
        public string Header { get; private set; } = "Main Page";

        private string _statusStr = "Check For Updates";
        public string StatusStr { get => _statusStr; private set
            {
                _statusStr = value;
                if (MainView.Current != null)
                {
                    Task task = new(() =>
                    {
                        MainView.Current.BtnUpdate.Content = _statusStr;
                    });

                    task.RunSynchronously(MainView.Current.SyncedScheduler);
                }
            }
        }

#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
        private static HttpClient httpClient;
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.

        static Launcher.State _launcherStatus = Launcher.State.Ready;
        public static Launcher.State LauncherStatus
        {
            get => CurLauncher == null ? Launcher.State.Finalizing : CurLauncher.LauncherStatus; 
            private set
            {
                _launcherStatus = value;
                if (MainView.Current != null)
                {
                    var view = MainView.Current;

                    Task task = new(() =>
                    {
                        switch (_launcherStatus)
                        {
                            case Launcher.State.Ready:
                                view.BtnLaunch.IsEnabled = true;
                                view.BtnUpdate.IsEnabled = true;
                                break;
                            case Launcher.State.Updating:
                                view.BtnLaunch.IsEnabled = false;
                                view.BtnUpdate.IsEnabled = false;
                                break;
                            case Launcher.State.Installing:
                                view.BtnLaunch.IsEnabled = false;
                                view.BtnUpdate.IsEnabled = false;
                                break;
                            case Launcher.State.Launching:
                                view.BtnLaunch.IsEnabled = false;
                                view.BtnUpdate.IsEnabled = false;
                                break;
                            case Launcher.State.Self_Updating:
                                view.BtnLaunch.IsEnabled = false;
                                view.BtnUpdate.IsEnabled = false;
                                break;
                        }
                    });

                    task.RunSynchronously(view.SyncedScheduler);
                }
            }
        }

        public static Version LocalVersion { get => CurLauncher == null ? Version.Invalid : CurLauncher.LocalVersion; }
        public static Launcher CurLauncher;
        private readonly Launcher SelfUpdater;

        public static bool AutoLaunch { get; private set; }

        public string RootPath { get; private set; } = "";
        public string SettingsPath { get; private set; } = "";

        //private static string? SelfConfigFile { get; set; }
        //private string DefaultSelfUpdateZip { get; init; }

        //public static ConfigurationInfo Config { get; private set; } = new(new string[6]);

        public static event Action<string>? AndroidLaunchAction;
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
        public static event Func<string> RequestAndroidDataPath;
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.

        public MainViewModel()
        {
            httpClient = new HttpClient();
            Launcher.RequestMainScheduler += GetMainScheduler;

            RootPath = AppContext.BaseDirectory;
            if (!App.IsDesktop)
                RootPath = RequestAndroidDataPath.Invoke();

            SettingsPath = Path.Combine(RootPath, "Settings");
            Directory.CreateDirectory(SettingsPath);

            CurLauncher = new Launcher(Path.Combine(SettingsPath, $"Profile 1.txt"));
            CurLauncher.UpdateCompleteAction += OnUpdateComplete;
            CurLauncher.OnChangedVersionAction += OnChangedVersion;
            CurLauncher.RefreshVersion();

            Header = CurLauncher.AppName;

            SelfUpdater = new Launcher(Path.Combine(RootPath, "Settings", "Launcher.config"), Path.Combine(RootPath, Launcher.SELF_UPDATE_TEXT + ".zip"),
                Path.Combine(RootPath, Launcher.SELF_UPDATE_TEXT), isSelfUpdater: true);

            UpdateCommand = ReactiveCommand.Create(PressedUpdate);
            LaunchCommand = ReactiveCommand.Create(PressedLaunch);

            Task startup = new(async () =>
            {
                LauncherStatus = Launcher.State.Self_Updating;
                if (await TryUpdateSelf())
                {
                    int maxWait = 20;
                    while (SelfUpdater.LauncherStatus != Launcher.State.Finalizing && maxWait-- > 0)
                    {
                        Thread.Sleep(1000);
                    }

                    SelfUpdater.UpdateSelfAndRestart();

                    return;
                }
                else
                    LauncherStatus = Launcher.State.Ready;

                if (AutoLaunch)
                {
                    OnLaunchClicked(true);
                }
            });

            startup.RunSynchronously(MainView.Current.SyncedScheduler);
        }

        private TaskScheduler GetMainScheduler()
        {
            return MainView.Current?.SyncedScheduler ?? TaskScheduler.Default;
        }

        private void OnChangedVersion(Version version)
        {
            if (MainView.Current != null)
            {
                Task task = new(() =>
                {
                    MainView.Current.VersionTxt.Text = "v" + version.ToString();
                });

                task.RunSynchronously(MainView.Current.SyncedScheduler);
            }
        }

        ~MainViewModel()
        {
            httpClient.Dispose();
            Launcher.RequestMainScheduler -= GetMainScheduler;
        }

        public ReactiveCommand<Unit, Unit> UpdateCommand { get; }
        async void PressedUpdate()
        {
            //Don't attempt another update before finishing
            if (LauncherStatus != Launcher.State.Ready)
                return;

            LauncherStatus = Launcher.State.Updating;
            try
            {
                //Check for an update first so that latest version can be set properly
                if (await CurLauncher.IsUpdateAvailable() || !CurLauncher.LocalVersion.IsValid() || !File.Exists(CurLauncher.LaunchFilePath))
                {
                    CurLauncher.InstallProgramFiles(false);//, Config, latestVersion);
                }
                else
                {
                    LauncherStatus = Launcher.State.Ready;
                    StatusStr = "Up to Date";
                    SetUpdateText("Check For Updates", 5f, () =>
                    {
                        return LauncherStatus != Launcher.State.Ready;
                    });
                }
            }
            catch (Exception)
            {

                throw;
            }
        }

        public ReactiveCommand<Unit, Unit> LaunchCommand { get; }

        void PressedLaunch()
        {
            if (LauncherStatus != Launcher.State.Ready)
                return;

            OnLaunchClicked();
        }
        private static async void OnLaunchClicked(bool forceUpdate = false)
        {
            LauncherStatus = Launcher.State.Launching;
            //Check for an update first so that latest version can be set properly
            if (await CurLauncher.IsUpdateAvailable() || !CurLauncher.LocalVersion.IsValid() || !File.Exists(CurLauncher.LaunchFilePath))
            {
                if (forceUpdate == false)
                    forceUpdate = !LocalVersion.IsValid() || !File.Exists(CurLauncher.LaunchFilePath);

                LauncherStatus = Launcher.State.Updating;
                if (forceUpdate)
                    CurLauncher.InstallProgramFiles(true);//, Config, latestVersion);
                else
                    _ = await AskToUpdate();
                
                return;
            }

            CurLauncher.LaunchProgram();
        }

        private static async Task<MsgBox.MessageBoxResult> AskToUpdate(ConfigurationInfo? config = null, bool allowCancel = true, bool handleResultManually = false)
        {
            config ??= CurLauncher.Config;
            var buttonSet = allowCancel ? MsgBox.MessageBoxButtons.YesNoCancel : MsgBox.MessageBoxButtons.YesNo;

            var result = await MsgBox.MessageBox.Show(App.MainScreen as Window, 
                "Notice", $"There is an update available for {config.Name}. Would you like to install it?", buttonSet);

            if (handleResultManually)
                return result;

            if (result == MsgBox.MessageBoxResult.Accept)
                CurLauncher.InstallProgramFiles(true);//, config, latestVersion);
            else if (result == MsgBox.MessageBoxResult.Decline)
                CurLauncher.LaunchProgram();
            //Otherwise cancel
            else
                LauncherStatus = Launcher.State.Ready;

            return result;
        }


        /// <summary>
        /// Changes the update text with an optional delay
        /// </summary>
        /// <param name="text">Text to display</param>
        /// <param name="secDelay">How many seconds to wait before changing the text</param>
        /// <param name="cancelCondition">A condition that will be checked just before setting the text</param>
        public async void SetUpdateText(string text, float secDelay, Func<bool>? cancelCondition = null)
        {
            if (secDelay > 0)
            {
                await Task.Run(() =>
                {
                    Thread.Sleep(TimeSpan.FromSeconds(secDelay));
                });
            }

            bool shouldCancel = cancelCondition?.Invoke() ?? false;

            if (shouldCancel)
                return;
            StatusStr = text;
        }

        private async Task<bool> TryUpdateSelf()
        {
            Version onlineVersion = new();
            ConfigurationInfo config = SelfUpdater.Config;
            var assembly = System.Reflection.Assembly.GetExecutingAssembly();
            Version curVersion = new(assembly.GetName().Version?.ToString());

            await Task.Run(async () =>
            {
                try
                {
                    string onlineVersionStr = (await httpClient.GetStringAsync(config.VersionLink)).Trim();
                    onlineVersion = new Version(onlineVersionStr);
                }
                catch { onlineVersion = Version.Invalid; }
            });

            //If the online version is invalid or if it isn't new then ignore it
            if (!onlineVersion.IsValid() || onlineVersion.CompareTo(curVersion) <= 0)
                return false;

            var result = await AskToUpdate(config, false, true);

            if (result == MsgBox.MessageBoxResult.Accept)
                SelfUpdater.InstallProgramFiles(false, config, onlineVersion);

            return result == MsgBox.MessageBoxResult.Accept;
        }

        void OnUpdateComplete()
        {
            LauncherStatus = Launcher.State.Ready;

            StatusStr = "Up to Date";
            SetUpdateText("Check For Updates", 10f, () =>
            {
                return LauncherStatus != Launcher.State.Ready;
            });
        }

    }
}