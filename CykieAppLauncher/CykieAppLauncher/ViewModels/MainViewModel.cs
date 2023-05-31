using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Reactive;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls.Notifications;
using CykieAppLauncher.Models;
using CykieAppLauncher.Views;
using ReactiveUI;
using Notification = Avalonia.Controls.Notifications.Notification;
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
                    MainView.Current.BtnUpdate.Content = _statusStr;
            }
        }

        public enum LauncherState
        {
            Ready, Updating, Launching
        }

        LauncherState launcherState;
        //public Version LocalVersion { get; private set; }
        private Version _localVersion = Version.Default;
        public Version LocalVersion { get => _localVersion; private set
            {
                _localVersion = value;
                if (MainView.Current != null)
                    MainView.Current.VersionTxt.Text = "v" + _localVersion.ToString();
            }
        }
        Version latestVersion = Version.Default;

        public static string AppName { get; private set; } = "PROGRAM NAME";
        public static bool AutoLaunch { get; private set; }

        public string RootPath { get; private set; } = "";
        public string SettingsPath { get; private set; } = "";
        //public static string VersionFile { get; private set; } = "";
        public static string ConfigFile { get; private set; } = "";
        public static string ProgramZipDest { get; private set; } = "";
        public static string LaunchFile { get; private set; } = "";

        public static ConfigurationInfo Config { get; private set; } = new(new string[6]);

        public MainViewModel()
        {
            InitConfig();
            Header = AppName;

            UpdateCommand = ReactiveCommand.Create(PressedUpdate);
            LaunchCommand = ReactiveCommand.Create(PressedLaunch);

            if (AutoLaunch)
            {
                if (MainView.Current != null)
                    MainView.Current.BtnLaunch.IsEnabled = false;

                OnLaunchClicked(true);
            }
        }
        private void InitConfig()
        {
            try
            {
                RootPath = AppContext.BaseDirectory;
                SettingsPath = Path.Combine(RootPath, "Settings");
                Directory.CreateDirectory(SettingsPath);
                ConfigFile = Path.Combine(RootPath, "Settings", "Config.txt");
                if (!File.Exists(ConfigFile))
                {
                    var fs = File.Create(ConfigFile);
                    fs.Close();
                }

                var info = ParseConfigFile(out var config);
                Config = config;

                AppName = Config.Name;
                //Set Auto Launch
                if (bool.TryParse(info[^1].Split('=', 2)[1], out bool r))
                    AutoLaunch = r;

                //Where to download the Zip to
                if (Config.ZipPath == "auto")
                    ProgramZipDest = Path.Combine(RootPath, $"{AppName} v{Config.Version}.zip");
                else 
                    ProgramZipDest = Config.ZipPath;
                //Where will the app launch from
                if (Config.LaunchPath == "auto")
                    LaunchFile = Path.Combine(RootPath, "Build", $"{AppName}.exe");
                else
                    LaunchFile = Config.LaunchPath;
            }
            catch (Exception ex)
            {
                throw new Exception($"Error: {ex}");
            }
        }

        private string[] ParseConfigFile(out ConfigurationInfo config)
        {
            var info = File.ReadAllLines(ConfigFile);
            if (info.Length < 7)
            {
                info = ("App Name=PROGRAM\n" +
                    "Version=\n" +
                    "Zip Path=auto\n" +
                    "Launch Path=auto\n" +
                    "Version Link=\n" +
                    "Build Link=\n" +
                    "Auto Launch=True").Split('\n');
                File.WriteAllLines(ConfigFile, info);
            }
            //https://sites.google.com/site/gdocs2direct/home

            /*x Version is now included in Config.txt 
             * VersionFile = Path.Combine(SettingsPath, "Version.txt");
            if (!File.Exists(VersionFile))
            {
                var fileStream = File.Create(VersionFile);//Will be Version.Invalid
                fileStream.Close();
            }*/

            //Set Config from file content
            config = new(info[0].Split('=', 2)[1].Trim(), info[1].Split('=', 2)[1].Trim(),
                info[2].Split('=', 2)[1].Trim(), info[3].Split('=', 2)[1].Trim(),
                info[4].Split('=', 2)[1].Trim(), info[5].Split('=', 2)[1].Trim());

            /*x Version.txt method
             * Config = new(info[0].Split('=', 2)[1].Trim(),
                new Version(File.ReadAllText(VersionFile).Trim()).ToString(),
                info[1].Split('=', 2)[1].Trim(), info[2].Split('=', 2)[1].Trim(),
                info[3].Split('=', 2)[1].Trim(), info[4].Split('=', 2)[1].Trim());*/
            LocalVersion = new(config.Version);

            return info;
        }

        public ReactiveCommand<Unit, Unit> UpdateCommand { get; }
        async void PressedUpdate()
        {
            //Don't attempt another update before finishing
            if (launcherState != LauncherState.Ready)
                return;

            launcherState = LauncherState.Updating;
            try
            {

                if (!LocalVersion.IsValid() || await IsUpdateAvailable())
                {
                    InstallProgramFiles();
                }
                else
                {
                    launcherState = LauncherState.Ready;
                    StatusStr = "Up to Date";
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
            if (launcherState != LauncherState.Ready)
                return;

            OnLaunchClicked();
        }

        private async void OnLaunchClicked(bool forceUpdate = false)
        {
            launcherState = LauncherState.Launching;

            if (await IsUpdateAvailable())
            {
                launcherState = LauncherState.Updating;
                if (forceUpdate)
                    InstallProgramFiles(true);
                else
                    AskToUpdate();

                return;
            }

            LaunchProgram();
        }

        public static void LaunchProgram()
        {
            if (!File.Exists(LaunchFile))
                return;

            if (MainView.Current != null)
                MainView.Current.IsEnabled = false;
            ProcessStartInfo info = new(LaunchFile)
            {
                WorkingDirectory = Path.GetDirectoryName(LaunchFile)
            };
            Process.Start(info);
            App.Quit();
        }

        //todo FIX THIS
        private void AskToUpdate()
        {
            bool? result = null;

            Task.Run(async () =>
            {
                Notification notification = new(null, "There is an update available. Would you like to install it?", NotificationType.Information,
                TimeSpan.Zero, () => { InstallProgramFiles(true); result = true; }, () => { LaunchProgram(); result = false; });

                WindowNotificationManager notificationManager = new(MainWindow.Current);
                notificationManager.Show(notification);
            });


            /*x bool result = 
                //await DisplayAlert("Alert", "There is an update available. Would you like to install it?", "Yes", "No");

            if (result)
            {
                InstallProgramFiles(true);
            }
            else
                launcherState = LauncherState.Ready;*/
        }



        private async Task<bool> IsUpdateAvailable()
        {
            ParseConfigFile(out var config);
            Config = config;

            HttpClient httpClient = new();
            string onlineVersionStr = "";

            await Task.Run(async () =>
            {
                onlineVersionStr = (await httpClient.GetStringAsync(Config.VersionLink)).Trim();
            });

            var onlineVersion = new Version(onlineVersionStr);
            latestVersion = LocalVersion;

            if (LocalVersion.Compare(onlineVersion) >= 0)
                return false;

            latestVersion = onlineVersion;
            return true;
        }

        //https://gist.github.com/yasirkula/d0ec0c07b138748e5feaecbd93b6223c
        public async void InstallProgramFiles(bool launchAfter = false, Version? version = null)
        {
            StatusStr = "Updating...";
            launcherState = LauncherState.Updating;

            try
            {
                HttpClient httpClient = new();

                if (version == null)
                {
                    await Task.Run(async () =>
                    {
                        version = new(
                            (await httpClient.GetStringAsync(Config.VersionLink)).Trim());
                    });
                }

                //! Ensures that the actual .zip is downloaded from Google Drive
                int attempts = 0;
                var link = new Uri(Config.BuildLink);
                do
                {
                    var download = Task.Run(async () =>
                    {
                        await httpClient.DownloadFileTaskAsync(link, ProgramZipDest);
                    });

                    string dots = "...";
                    while (!download.IsCompleted)
                    {
                        StatusStr = "Downloading Files" + dots;
                        Thread.Sleep(1000);
                        dots = dots == "..." ? "." : (dots == "." ? dots = ".." : "...");
                    }

                    ProcessDriveDownload(ProgramZipDest, out link);
                    attempts++;
                }
                while (!OnDownloadFileCompleted() && attempts < 4);

                //x webClient.DownloadFileCompleted += OnDownloadFileCompleted;
                //x webClient.DownloadFileAsync(new Uri(AppShell.Config.BuildLink), AppShell.ProgramZipDest, version);
            }
            catch (Exception)
            {
                throw;
            }

            if (launchAfter)
                LaunchProgram();
        }

        // Downloading large files from Google Drive prompts a warning screen and requires manual confirmation
        // Consider that case and try to confirm the download automatically if warning prompt occurs
        // Returns true, if no more download requests are necessary
        private bool ProcessDriveDownload(string fileName, out Uri newLink)
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
                    newLink = new Uri(/*"https://drive.google.com" + */content[linkIndex..linkEnd].Replace("&amp;", "&"));
                    return false;
                }
            }

            return true;
        }

        private bool OnDownloadFileCompleted()
        {
            try
            {
                StatusStr = "Installing...";
                ZipFile.ExtractToDirectory(ProgramZipDest,
                    //.Replace(AppShell.Config.Version, onlineVersion), 
                    Path.GetDirectoryName(LaunchFile), true);
                File.Delete(ProgramZipDest);
            }
            catch (Exception)
            {
                return false;
            }

            UpdateVersionInfo(latestVersion.ToString());
            launcherState = LauncherState.Ready;
            StatusStr = "Up to Date";

            return true;
        }

        void UpdateVersionInfo(string version)
        {
            LocalVersion = new(version);
            //if (MainView.Current != null)
                //MainView.Current.VersionTxt.Text = LocalVersion.ToString();
            var contents = File.ReadAllLines(ConfigFile);
            contents[1] = "Version=" + LocalVersion.ToString();
            File.WriteAllLines(ConfigFile, contents);

            //File.WriteAllText(VersionFile, version);
        }
    }
}