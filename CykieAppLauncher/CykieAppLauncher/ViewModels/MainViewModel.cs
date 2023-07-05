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

        public static ConfigurationInfo Config { get; private set; } = new(new string[6]);

        public static event Action<string>? AndroidLaunchAction;
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
        public static event Func<string> RequestAndroidDataPath;
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.

        public MainViewModel()
        {
            httpClient = new HttpClient();

            RootPath = AppContext.BaseDirectory;
            if (!App.IsDesktop)
                RootPath = RequestAndroidDataPath.Invoke();

            SettingsPath = Path.Combine(RootPath, "Settings");
            Directory.CreateDirectory(SettingsPath);

            CurLauncher = new Launcher(Path.Combine(SettingsPath, $"Profile 1.txt"));
            CurLauncher.BeginDownloadAction += OnBeginDownload;
            CurLauncher.UpdateCompleteAction += OnUpdateComplete;
            CurLauncher.OnChangedVersionAction += OnChangedVersion;
            CurLauncher.RefreshVersion();

            Header = CurLauncher.AppName;

            SelfUpdater = new Launcher(Path.Combine(RootPath, "Settings", "Launcher.config"), Path.Combine(RootPath, "self-update.zip"),
                Path.Combine(RootPath, "self-update"), true);

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

                    //*https://andreasrohner.at/posts/Programming/C%23/A-platform-independent-way-for-a-C%23-program-to-update-itself/#:~:text=A%20platform%20independent%20way%20for%20a%20C%23%20program,...%203%20Demo%20Project%20...%204%20References%20

                    var updatePath = Path.Combine(RootPath, "self-update");

                    //Exclude Settings
                    var tmpSettingsPath = Path.Combine(updatePath, new DirectoryInfo(SettingsPath).Name);
                    if (Directory.Exists(tmpSettingsPath))
                        Directory.Delete(tmpSettingsPath, true);
                    //Exclude Builds
                    var buildPaths = Directory.GetDirectories(updatePath, $"{Launcher.BUILD_DIR_PREFIX}*", SearchOption.TopDirectoryOnly);
                    for (int i = 0; i < buildPaths.Length; i++)
                    {
                        try
                        {
                            Directory.Delete(buildPaths[i], true);
                        }
                        catch { }
                    }

                    //Windows update
                    if (App.TargetPlatform == App.PlatformType.Windows)
                    {
                        var batFilePath = RootPath + "self-update.bat";
                        var launcherName = Path.GetFileName(Environment.ProcessPath);

                        string content =
$@"TIMEOUT /t 1 /nobreak > NUL
robocopy {updatePath} {RootPath} /MOVE /E
RD /S /Q {updatePath}
DEL ""%~f0"" & START """" /B ""{launcherName}""";

                        File.WriteAllText(batFilePath, content);

                        ProcessStartInfo info = new(batFilePath)
                        {
                            WorkingDirectory = Path.GetDirectoryName(RootPath)
                        };
                        Process.Start(info);
                    }
                    //TODO add self update logic for all platforms
                    else if (App.TargetPlatform == App.PlatformType.Linux || App.TargetPlatform == App.PlatformType.Web)
                    {
                        //TODO this is untested
                        var files = Directory.GetFiles(updatePath);
                        List<string> replacedFiles = new();

                        foreach (var file in files)
                        {
                            var name = Path.GetFileName(file);
                            if (name == Environment.ProcessPath)
                                continue;
                            replacedFiles.Add(name);

                            try
                            {
                                File.Move(file, Path.Combine(RootPath, name), true);
                            }
                            catch (Exception)
                            {
                                File.Copy(file, Path.Combine(RootPath, name), true);
                            }
                        }

                        var directories = Directory.GetDirectories(updatePath);
                        foreach (var directory in directories)
                        {
                            var name = Path.GetDirectoryName(directory);
                            replacedFiles.Add(name);

                            try
                            {
                                Directory.Move(directory, Path.Combine(RootPath, name));
                            }
                            catch (IOException ex)
                            {
                                Directory.Delete(directory, true);
                                Directory.Move(directory, Path.Combine(RootPath, name));
                            }
                        }

                        var self = System.Reflection.Assembly.GetExecutingAssembly().Location;
                        Process.Start(self);

                        // Sleep for half a second to avoid an exception
                        Thread.Sleep(500);
                        App.Quit();

                    }


                    App.Quit();

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
        private async void OnLaunchClicked(bool forceUpdate = false)
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
            config ??= Config;
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

        /*x private async Task<bool> _IsUpdateAvailable()
        {
            ParseConfigFile(ConfigFilePath, out var config);
            Config = config;

            Version onlineVersion = Version.Invalid;

            try
            {
                await Task.Run(async () =>
                {
                    string onlineVersionStr = (await httpClient.GetStringAsync(Config.VersionLink)).Trim();
                    onlineVersion = new Version(onlineVersionStr);
                });
            }
            catch { return false; }

            latestVersion = LocalVersion;

            if (LocalVersion.Compare(onlineVersion) >= 0)
                return false;

            latestVersion = onlineVersion;
            return true;
        }

        //https://gist.github.com/yasirkula/d0ec0c07b138748e5feaecbd93b6223c
        public void _InstallProgramFiles(bool launchAfter = false, ConfigurationInfo? config = null, Version? version = null, bool isSelfUpdate = false)
        {
            config ??= Config;

            var zipDest = config.ZipPath;
            if (string.IsNullOrEmpty(zipDest) || zipDest == "auto")
                zipDest = ProgramZipDest;

            StatusStr = "Updating...";
            LauncherStatus = Launcher.State.Updating;

            Task installTask = new(async () =>
            {
                try
                {
                    version ??= new((await httpClient.GetStringAsync(config.VersionLink)).Trim());

                    //! Ensures that the actual .zip is downloaded from Google Drive
                    int attempts = 0;
                    var link = new Uri(config.BuildLink);
                    do
                    {
                        var download = Task.Run(async () =>
                        {
                            await httpClient.DownloadFileTaskAsync(link, zipDest);
                        });

                        string dots = StatusStr.Split('.', 2)[1] + ".";
                        //StatusStr = "Downloading Files" + dots;
                        while (!download.IsCompleted)
                        {
                            Thread.Sleep(500);
                            dots = dots == "..." ? "." : (dots == "." ? dots = ".." : "...");
                            StatusStr = "Downloading Files" + dots;
                        }

                        ProcessDriveDownload(zipDest, out link);
                        attempts++;
                    }
                    while (!WasDownloadSuccessful(config, isSelfUpdate ? Path.Combine(RootPath, "self-update") : BuildPath) && attempts < 4);

                    _ = OnDownloadComplete(config);
                }
                catch (Exception ex)
                {
                    throw new Exception("Installation error: " + ex);
                }

                if (isSelfUpdate)
                {
                    LauncherStatus = Launcher.State.Finalizing_Self_Update;
                    return;
                }

                if (launchAfter)
                    CurLauncher.LaunchProgram();
            });

            installTask.RunSynchronously(MainView.Current.SyncedScheduler);
        }

        /// <summary>
        /// Downloading large files from Google Drive prompts a warning screen and requires manual confirmation.
        /// Consider that case and try to confirm the download automatically if warning prompt occurs
        /// </summary>
        /// <param name="fileName"></param>
        /// <param name="newLink"></param>
        /// <returns>true, if no more download requests are necessary</returns>
        private static bool ProcessDriveDownload(string fileName, out Uri newLink)
        {
            newLink = new(fileName);
            FileInfo downloadedFile = new FileInfo(fileName);
            if (downloadedFile == null)
                return true;

            // Confirmation page is around 50KB, shouldn't be larger than 60KB
            if (downloadedFile.Length > 60000L)
                return true;

            // Downloaded file might be the confirmation page, check it
            string content;
            using (var reader = downloadedFile.OpenText())
            {
                // Confirmation page starts with <!DOCTYPE html>, which can be preceded by a newline
                char[] header = new char[20];
                int readCount = reader.ReadBlock(header, 0, 20);
                if (readCount < 20 || !(new string(header).Contains("<!DOCTYPE html>")))
                    return true;

                content = reader.ReadToEnd();
            }

            //int linkIndex = content.LastIndexOf("href=\"/uc?");
            int linkIndex = content.LastIndexOf("action=\"");
            if (linkIndex >= 0)
            {
                linkIndex += "action=\"".Length;
                int linkEnd = content.IndexOf('"', linkIndex);
                if (linkEnd >= 0)
                {
                    newLink = new Uri(content[linkIndex..linkEnd].Replace("&amp;", "&"));
                    return false;
                }
            }

            return true;
        }

        private bool WasDownloadSuccessful(ConfigurationInfo? config = null, string? buildPath = null)
        {
            config ??= Config;
            var zipDest = config.ZipPath;

            try
            {
                if (!HelperUtils.IsZipValid(zipDest))
                    return false;

                StatusStr = "Installing...";
            }
            catch (Exception)
            {
                return false;
            }

            return true;
        }

        private async Task OnDownloadComplete(ConfigurationInfo? config = null, string? buildPath = null)
        {
            config ??= Config;
            buildPath ??= BuildPath;
            var zipDest = config.ZipPath;

            bool removedZip = false;
            using (var zipContent = ZipFile.OpenRead(zipDest))
            {
                //The zip itself is likely the executable
                if (!zipContent.HasFile(App.RunnableExtension))
                {
                    File.Move(zipDest,
                        Path.Combine(buildPath, Path.GetFileName(zipDest).Replace(".zip", App.RunnableExtension)));
                    removedZip = true;
                }
            }

            //The target file was found in the zip, so extract it
            if (!removedZip)
            {
                ZipFile.ExtractToDirectory(zipDest, buildPath, true);
                await Task.Run(() =>
                {

                    //Attempt to delete the zip a max of [i] times
                    int i = 12;
                    while (i-- > 0)
                    {
                        Thread.Sleep(1000);
                        try
                        {
                            //The extraction must be complete for this to work
                            File.Delete(zipDest);
                            removedZip = true;
                            break;
                        }
                        catch (Exception ex)
                        {
                            _ = $"Error: {ex}";
                        }
                    }
                });
            }

            if (config.Name == AppName)
                UpdateVersionInfo(latestVersion.ToString());
            LauncherStatus = Launcher.State.Ready;

            StatusStr = "Up to Date";
            SetUpdateText("Check For Updates", 10f, () =>
            {
                return LauncherStatus != Launcher.State.Ready;
            });
        }

        void UpdateVersionInfo(string version)
        {
            LocalVersion = new(version);
            //if (MainView.Current != null)
                //MainView.Current.VersionTxt.Text = LocalVersion.ToString();
            var contents = File.ReadAllLines(ConfigFilePath);
            contents[1] = "Version=" + LocalVersion.ToString();
            File.WriteAllLines(ConfigFilePath, contents);

            //File.WriteAllText(VersionFile, version);
        }
        */

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

        void OnBeginDownload(Task download)
        {
            if (!StatusStr.Contains("Downloading Files")) return;

            string dots = StatusStr.Split('.', 2)[1] + ".";
            //StatusStr = "Downloading Files" + dots;
            while (!download.IsCompleted)
            {
                Thread.Sleep(500);
                dots = dots == "..." ? "." : (dots == "." ? dots = ".." : "...");
                StatusStr = "Downloading Files" + dots;
            }
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