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
        public enum LauncherState
        {
            Ready, Updating, Launching, Self_Updating, Finalizing_Self_Update
        }

        static LauncherState _launcherStatus = LauncherState.Ready;
        public static LauncherState LauncherStatus
        {
            get => _launcherStatus; private set
            {
                _launcherStatus = value;
                if (MainView.Current != null)
                {
                    var view = MainView.Current;

                    Task task = new(() =>
                    {
                        switch (_launcherStatus)
                        {
                            case LauncherState.Ready:
                                view.BtnLaunch.IsEnabled = true;
                                view.BtnUpdate.IsEnabled = true;
                                break;
                            case LauncherState.Updating:
                                view.BtnLaunch.IsEnabled = false;
                                view.BtnUpdate.IsEnabled = false;
                                break;
                            case LauncherState.Launching:
                                view.BtnLaunch.IsEnabled = false;
                                view.BtnUpdate.IsEnabled = false;
                                break;
                            case LauncherState.Self_Updating:
                                view.BtnLaunch.IsEnabled = false;
                                view.BtnUpdate.IsEnabled = false;
                                break;
                        }
                    });

                    task.RunSynchronously(view.SyncedScheduler);
                }
            }
        }

        private Version _localVersion = Version.Default;
        public Version LocalVersion { get => _localVersion; private set
            {
                _localVersion = value;
                if (MainView.Current != null)
                {
                    Task task = new(() =>
                    {
                        MainView.Current.VersionTxt.Text = "v" + _localVersion.ToString();
                    });

                    task.RunSynchronously(MainView.Current.SyncedScheduler);
                }
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
        public static string BuildPath { get => Path.GetDirectoryName(LaunchFile); }

        private static string? SelfConfigFile { get; set; }
        private string DefaultSelfUpdateZip { get; init; }

        public static ConfigurationInfo Config { get; private set; } = new(new string[6]);

        public static event Action<string>? AndroidLaunchAction;
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
        public static event Func<string> RequestAndroidDataPath;
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.

        public MainViewModel()
        {
            httpClient = new HttpClient();
            

            InitConfig();
            Header = AppName;

            DefaultSelfUpdateZip = Path.Combine(RootPath, "self-update.zip");
            SelfConfigFile = Path.Combine(RootPath, "Settings", "Launcher.config");
            if (!File.Exists(SelfConfigFile))
                SelfConfigFile = null;//use hard coded values


            if (File.Exists(ProgramZipDest))
                File.Delete(ProgramZipDest);
            //todo REMOVE
            /*if (Directory.Exists(Path.GetDirectoryName(SettingsPath)))
                Directory.Delete(Path.GetDirectoryName(SettingsPath), true);
            if (Directory.Exists(Path.GetDirectoryName(LaunchFile)))
                Directory.Delete(Path.GetDirectoryName(LaunchFile), true);//*/

            UpdateCommand = ReactiveCommand.Create(PressedUpdate);
            LaunchCommand = ReactiveCommand.Create(PressedLaunch);

            Task startup = new(async () =>
            {
                LauncherStatus = LauncherState.Self_Updating;
                if (await TryUpdateSelf())
                {
                    while (LauncherStatus != LauncherState.Finalizing_Self_Update) ;

                    //*https://andreasrohner.at/posts/Programming/C%23/A-platform-independent-way-for-a-C%23-program-to-update-itself/#:~:text=A%20platform%20independent%20way%20for%20a%20C%23%20program,...%203%20Demo%20Project%20...%204%20References%20

                    var updatePath = Path.Combine(RootPath, "self-update");

                    //Exclude Settings
                    if (Directory.Exists(SettingsPath.Replace(RootPath, updatePath)))
                        Directory.Delete(SettingsPath.Replace(RootPath, updatePath), true);
                    //Exclude Builds
                    if (Directory.Exists(BuildPath.Replace(RootPath, updatePath)))
                        Directory.Delete(BuildPath.Replace(RootPath, updatePath), true);

                    if (App.TargetPlatform != App.PlatformType.Windows && App.TargetPlatform != App.PlatformType.Android)
                    {
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
                    else if (App.TargetPlatform == App.PlatformType.Windows)
                    {
                        var batFilePath = RootPath + "self-update.bat";
                        var launcherName = Path.GetFileName(Environment.ProcessPath);

                        string content =
$@"TIMEOUT /t 1 /nobreak > NUL
robocopy {updatePath} {RootPath} /MOVE /E
DEL ""%~f0"" & START """" /B ""{launcherName}""";

                        File.WriteAllText(batFilePath, content);

                        ProcessStartInfo info = new(batFilePath)
                        {
                            WorkingDirectory = Path.GetDirectoryName(RootPath)
                        };
                        Process.Start(info);
                    }


                    App.Quit();

                    return;
                }
                else
                    LauncherStatus = LauncherState.Ready;

                if (AutoLaunch)
                {
                    OnLaunchClicked(true);
                }
            });

            startup.RunSynchronously(MainView.Current.SyncedScheduler);
        }
        ~MainViewModel()
        {
            httpClient.Dispose();
        }
        private void InitConfig()
        {
            try
            {
                RootPath = Environment.CurrentDirectory;
                RootPath = AppContext.BaseDirectory;
                if (!App.IsDesktop)
                    RootPath = RequestAndroidDataPath.Invoke();

                //var files = Directory.GetFiles(RootPath, "*", SearchOption.AllDirectories);
                SettingsPath = Path.Combine(RootPath, "Settings");
                Directory.CreateDirectory(SettingsPath);
                ConfigFile = Path.Combine(RootPath, "Settings", "Config.txt");
                if (!File.Exists(ConfigFile))
                {
                    var fs = File.Create(ConfigFile);
                    fs.Close();
                }

                var info = ParseConfigFile(ConfigFile, out var config);
                Config = config;
            }
            catch (Exception ex)
            {
                throw new Exception($"Error: {ex}");
            }
        }

        private string[] ParseConfigFile(string? filePath, out ConfigurationInfo config)
        {
            filePath ??= ConfigFile;
            var info = File.ReadAllLines(filePath);
            if (info.Length < 7)
            {
                //Default Links
                string defName = "Blooming Darkness";
                string vLink = "https://drive.google.com/uc?export=download&id=1yTkCTalsDPKeJ2kpAgIAZMGvv82qZ9dh";
                string bLink = "https://drive.google.com/uc?export=download&id=1FpTxX1L3-CBd5lTM29Wzm3G_JUpXxNAK";

                if (!App.IsDesktop)
                {
                    defName = "Meeting Time";
                    vLink = "https://drive.google.com/uc?export=download&id=1LW56nibL44OpIqPrDrwMlG3pY4QvcjgZ";
                    bLink = "https://drive.google.com/uc?export=download&id=14Y_iXKvY9tfoBdRZFiCj__vrt6pZHUZX";
                }

                info = ($"App Name={defName}\n" +
                    "Version=\n" +
                    "Zip Path=auto\n" +
                    "Launch Path=auto\n" +
                    $"Version Link={vLink}\n" +
                    $"Build Link={bLink}\n" +
                    "Auto Launch=False").Split('\n');
                File.WriteAllLines(filePath, info);
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
            config = new(info[0].Split('=', 2)[1].Trim(), new Version(info[1].Split('=', 2)[1].Trim()).ToString(),
                info[2].Split('=', 2)[1].Trim(), info[3].Split('=', 2)[1].Trim(),
                info[4].Split('=', 2)[1].Trim(), info[5].Split('=', 2)[1].Trim());

            /*x Version.txt method
             * Config = new(info[0].Split('=', 2)[1].Trim(),
                new Version(File.ReadAllText(VersionFile).Trim()).ToString(),
                info[1].Split('=', 2)[1].Trim(), info[2].Split('=', 2)[1].Trim(),
                info[3].Split('=', 2)[1].Trim(), info[4].Split('=', 2)[1].Trim());*/
            LocalVersion = new(config.Version);

            AppName = config.Name;
            //Set Auto Launch
            if (bool.TryParse(info[^1].Split('=', 2)[1], out bool r))
                AutoLaunch = r;

            //Where to download the Zip to
            if (config.ZipPath == "auto")
                ProgramZipDest = Path.Combine(RootPath, $"{AppName}.zip");
            else
                ProgramZipDest = config.ZipPath;
            //Where will the app launch from
            if (config.LaunchPath == "auto")
                LaunchFile = Path.Combine(RootPath, $"Build - {AppName}", $"{AppName}{App.RunnableExtension}");
            else
                LaunchFile = config.LaunchPath;

            return info;
        }

        public ReactiveCommand<Unit, Unit> UpdateCommand { get; }
        async void PressedUpdate()
        {
            //Don't attempt another update before finishing
            if (LauncherStatus != LauncherState.Ready)
                return;

            LauncherStatus = LauncherState.Updating;
            try
            {
                //Check for an update first so that latest version can be set properly
                if (await IsUpdateAvailable() || !LocalVersion.IsValid() || !File.Exists(LaunchFile))
                {
                    InstallProgramFiles(false, Config, latestVersion);
                }
                else
                {
                    LauncherStatus = LauncherState.Ready;
                    StatusStr = "Up to Date";
                    SetUpdateText("Check For Updates", 5f, () =>
                    {
                        return LauncherStatus != LauncherState.Ready;
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
            if (LauncherStatus != LauncherState.Ready)
                return;

            OnLaunchClicked();
        }
        private async void OnLaunchClicked(bool forceUpdate = false)
        {
            LauncherStatus = LauncherState.Launching;
            //Check for an update first so that latest version can be set properly
            if (await IsUpdateAvailable() || !LocalVersion.IsValid() || !File.Exists(LaunchFile))
            {
                if (forceUpdate == false)
                    forceUpdate = !LocalVersion.IsValid() || !File.Exists(LaunchFile);

                LauncherStatus = LauncherState.Updating;
                if (forceUpdate)
                    InstallProgramFiles(true, Config, latestVersion);
                else
                    _ = await AskToUpdate();
                
                return;
            }

            LaunchProgram();
        }

        public static void LaunchProgram()
        {
            if (!File.Exists(LaunchFile))
                return;
            LauncherStatus = LauncherState.Launching;

            if (App.TargetPlatform == App.PlatformType.Android)
            {
                AndroidLaunchAction?.Invoke(LaunchFile);
            }
            else
            {
                ProcessStartInfo info = new(LaunchFile)
                {
                    WorkingDirectory = Path.GetDirectoryName(LaunchFile)
                };

                Process.Start(info);
            }

            if (MainView.Current != null)
                MainView.Current.IsEnabled = true;

            App.Quit();
        }

        private async Task<MsgBox.MessageBoxResult> AskToUpdate(ConfigurationInfo? config = null, bool allowCancel = true, bool handleResultManually = false)
        {
            config ??= Config;
            var buttonSet = allowCancel ? MsgBox.MessageBoxButtons.YesNoCancel : MsgBox.MessageBoxButtons.YesNo;

            var result = await MsgBox.MessageBox.Show(App.MainScreen as Window, 
                "Notice", $"There is an update available for {config.Name}. Would you like to install it?", buttonSet);

            if (handleResultManually)
                return result;

            if (result == MsgBox.MessageBoxResult.Accept)
                InstallProgramFiles(true, config, latestVersion);
            else if (result == MsgBox.MessageBoxResult.Decline)
                LaunchProgram();
            //Otherwise cancel
            else
                LauncherStatus = LauncherState.Ready;

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


        private async Task<bool> IsUpdateAvailable()
        {
            ParseConfigFile(ConfigFile, out var config);
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
        public void InstallProgramFiles(bool launchAfter = false, ConfigurationInfo? config = null, Version? version = null, bool isSelfUpdate = false)
        {
            config ??= Config;

            var zipDest = config.ZipPath;
            if (string.IsNullOrEmpty(zipDest))
                zipDest = ProgramZipDest;

            StatusStr = "Updating...";
            LauncherStatus = LauncherState.Updating;

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
                    while (!OnDownloadFileCompleted(config, isSelfUpdate ? Path.Combine(RootPath, "self-update") : BuildPath) && attempts < 4);
                }
                catch (Exception ex)
                {
                    throw new Exception("Installation error: " + ex);
                }

                if (isSelfUpdate)
                {
                    LauncherStatus = LauncherState.Finalizing_Self_Update;
                    return;
                }

                if (launchAfter)
                    LaunchProgram();
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
                    newLink = new Uri(/*"https://drive.google.com" + */content[linkIndex..linkEnd].Replace("&amp;", "&"));
                    return false;
                }
            }

            return true;
        }

        private bool OnDownloadFileCompleted(ConfigurationInfo? config = null, string? buildPath = null)
        {
            config ??= Config;
            buildPath ??= BuildPath;
            var zipDest = config.ZipPath;

            try
            {
                if (!HelperUtils.IsZipValid(zipDest))
                    return false;

                StatusStr = "Installing...";

                using (var zipContent = ZipFile.OpenRead(zipDest))
                {
                    //var files = Directory.GetFiles(Path.GetDirectoryName(LaunchFile), $"*{App.RunnableExtension}", SearchOption.AllDirectories);

                    //If the target file is found in the zip the extract it
                    if (zipContent.HasFile(App.RunnableExtension))
                    {
                        ZipFile.ExtractToDirectory(zipDest, buildPath, true);
                        Thread.Sleep(1000);
                        try
                        {
                            File.Delete(zipDest);
                        }
                        catch { }
                    }
                    else //The zip itself is likely the executable
                    {
                        File.Move(zipDest,
                            Path.Combine(buildPath, Path.GetFileName(zipDest).Replace(".zip", App.RunnableExtension)));
                    }
                }
            }
            catch (Exception ex)
            {
                return false;
            }

            if (config.Name == AppName)
                UpdateVersionInfo(latestVersion.ToString());
            LauncherStatus = LauncherState.Ready;

            StatusStr = "Up to Date";
            SetUpdateText("Check For Updates", 10f, () =>
            {
                return LauncherStatus != LauncherState.Ready;
            });

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

        private async Task<bool> TryUpdateSelf()
        {
            Version onlineVersion = new();
            ConfigurationInfo config;
            var assembly = System.Reflection.Assembly.GetExecutingAssembly();
            Version curVersion = new(assembly.GetName().Version?.ToString());

            if (SelfConfigFile == null)
            {
                var myZip = DefaultSelfUpdateZip;
                var vLink = "https://drive.google.com/uc?id=1-gMLfa0JpO1ui-UHlI3OwgDVAR0I2kuW";
                var bLink = "https://drive.google.com/uc?id=1CAC74wNYPJq5TBhr9VBY_5dwz3j6lrN8";

                config = new(assembly.GetName().Name, curVersion.ToString(), myZip, assembly.Location, vLink, bLink);
            }
            else
            {
                ParseConfigFile(SelfConfigFile, out config);
                config = new ConfigurationInfo(config.Name, config.Version, config.ZipPath, assembly.Location, config.VersionLink, config.BuildLink);
            }

            await Task.Run(async () =>
            {
                string onlineVersionStr = (await httpClient.GetStringAsync(config.VersionLink)).Trim();
                onlineVersion = new Version(onlineVersionStr);
            });

            if (onlineVersion.Compare(curVersion) <= 0)
                return false;

            var result = await AskToUpdate(config, false, true);

            if (result == MsgBox.MessageBoxResult.Accept)
                InstallProgramFiles(false, config, onlineVersion, true);

            return result == MsgBox.MessageBoxResult.Accept;
        }

    }
}