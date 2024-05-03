using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace CykieAppLauncher.Models
{

    public class Launcher
    {
        public string RootPath { get; private set; } = "";
        public string SettingsPath { get; private set; } = "";

        private const int MAX_DOWNLOAD_ATTEMPTS = 4;
        private const int MAX_ZIP_DELETE_ATTEMPTS = 12;
        public const string BUILD_DIR_PREFIX = "Build - ";
        public const string SELF_UPDATE_TEXT = "self-update";
        private static readonly HttpClient httpClient = new HttpClient();

        public enum State
        {
            Ready, Updating, Installing, Launching, Self_Updating, Finalizing,
            Restarting,
            Failed,
        }


        private State _launcherStatus = State.Ready;
        public State LauncherStatus
        {
            get => _launcherStatus;
            private set
            {
                _launcherStatus = value;
                OnChangedStateAction?.Invoke(value);
            }
        }

        private Version _localVersion = Version.Invalid;

        public Version LocalVersion
        {
            get => _localVersion;
            protected set
            {
                _localVersion = value;
                OnChangedVersionAction?.Invoke(value);
            }
        }

        public Version LatestVersion { get; protected set; } = Version.Invalid;
        public bool IsSelfUpdater { get; }

        //! Profile Config
        public ConfigurationInfo Config { get; private set; } = ConfigurationInfo.Empty;

        [Obsolete("Will reevaluate usefulness once refactoring is completely")]
        public static bool AutoLaunch { get; private set; }
        public string AppName { get => Config.Name; }
        public string ConfigFilePath { get; private set; } = "";
        public string? ProgramZipDest { get; protected set; } = null;
        public string? LaunchFilePath { get; protected set; } = null;
        public string? BuildPath { get; protected set; }

        //! EVENTS //
        /// <summary>
        /// 0 means normal exit; 1 means launch exit; 2 means self-update exit; -1 means generic forced crash
        /// </summary>
        public static event Action<int>? QuitAction;
        public static event Func<TaskScheduler>? RequestMainScheduler;
        public static event Action<string>? AndroidLaunchAction;
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
        public static event Func<string> RequestAndroidDataPath;
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
        public event Action<State>? OnChangedStateAction;
        public event Action<Version>? OnChangedVersionAction;

        public event Action? BeginUpdateProcessAction;
        public event Action? BeginDownloadAction;
        public event Action? BeginInstallAction;
        public event Action? UpdateCompleteAction;


        public Launcher(string configPath, string? zipPath = null, string? buildPath = null, string? settingsPath = null, bool isSelfUpdater = false)
        {
            IsSelfUpdater = isSelfUpdater;

            RootPath = AppContext.BaseDirectory;
            if (!AppInfo.IsDesktop)
                RootPath = RequestAndroidDataPath.Invoke();

            SettingsPath = settingsPath ?? Path.Combine(RootPath, "Settings");
            ConfigFilePath = configPath;

            //null is accounted for in InitConfig()
            ProgramZipDest = zipPath;
            BuildPath = buildPath;


            InitConfig();
        }

        public Launcher(ConfigurationInfo config, string rootPath, bool isSelfUpdater = false)
        {
            RootPath = rootPath;
            IsSelfUpdater = isSelfUpdater;
            Config = config;
            LocalVersion = new Version(Config.Version);
            ProgramZipDest = Config.ZipPath;
            LaunchFilePath = Config.LaunchPath;
        }

        #region Config Setup
        public void InitConfig()
        {
            try
            {
                Directory.CreateDirectory(SettingsPath);

                if (!IsSelfUpdater || File.Exists(ConfigFilePath))
                {
                    ParseConfigFile(ConfigFilePath, out var config);
                    Config = config;
                }
                else
                {
                    //use hard coded values for Self Update
                    var assembly = System.Reflection.Assembly.GetExecutingAssembly();
                    Version curVersion = new Version(assembly.GetName().Version?.ToString());
                    var myZip = ProgramZipDest;
                    var vLink = "https://drive.google.com/uc?id=1-gMLfa0JpO1ui-UHlI3OwgDVAR0I2kuW";
                    var bLink = "https://drive.google.com/uc?id=1CAC74wNYPJq5TBhr9VBY_5dwz3j6lrN8";

                    Config = new ConfigurationInfo(assembly.GetName().Name, curVersion.ToString(), myZip, assembly.Location, vLink, bLink);
                    LocalVersion = new Version(Config.Version);
                    LaunchFilePath = Config.LaunchPath;
                }

                BuildPath ??= Path.GetDirectoryName(LaunchFilePath) ?? $"{BUILD_DIR_PREFIX}NAME ERROR";
            }
            catch (Exception ex)
            {
                throw new Exception($"Error: {ex}");
            }
        }
        private string[] ParseConfigFile(string? filePath, out ConfigurationInfo config)
        {
            filePath ??= ConfigFilePath;

            if (!File.Exists(filePath))
            {
                var fs = File.Create(filePath);
                fs.Close();
            }

            var info = File.ReadAllLines(filePath);
            if (info.Length < 7)
            {
                //Test Link - Windows
                string defName = "Blooming Darkness";
                string vLink = "https://drive.google.com/uc?export=download&id=1yTkCTalsDPKeJ2kpAgIAZMGvv82qZ9dh";
                string bLink = "https://drive.google.com/uc?export=download&id=1MgxwXPEIK3XOtdCWXOpUuMlAsIzZHBZ4";

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

            var name = info[0].Split('=', 2)[1].Trim();
            //set auto values
            ProgramZipDest ??= Path.Combine(RootPath, $"{name}.zip");
            LaunchFilePath ??= Path.Combine(RootPath, $"{BUILD_DIR_PREFIX}{name}", $"{name}{AppInfo.RunnableExtension}");

            //Set Config from file content
            config = new ConfigurationInfo(name, new Version(info[1].Split('=', 2)[1].Trim()).ToString(),
                info[2].Split('=', 2)[1].Trim().Unless("auto", ProgramZipDest), info[3].Split('=', 2)[1].Trim().Unless("auto", LaunchFilePath),
                info[4].Split('=', 2)[1].Trim(), info[5].Split('=', 2)[1].Trim());

            LocalVersion = new Version(config.Version);

            //Set Auto Launch
            if (bool.TryParse(info[^1].Split('=', 2)[1], out bool r))
                AutoLaunch = r;

            //Where to download the Zip to
            ProgramZipDest = config.ZipPath;
            //Where will the app launch from
            LaunchFilePath = config.LaunchPath;

            return info;
        }
        #endregion

        /// <summary>
        /// For manually updating version text
        /// </summary>
        public void RefreshVersion()
        {
            LocalVersion = new Version(LocalVersion.ToString());
        }
        void UpdateVersionInfo(string version)
        {
            LocalVersion = new Version(version);

            try
            {
                if (File.Exists(ConfigFilePath))
                {
                    var contents = File.ReadAllLines(ConfigFilePath);
                    contents[1] = "Version=" + LocalVersion.ToString();
                    File.WriteAllLines(ConfigFilePath, contents);
                }
            }
            catch { }
        }

        public async Task<bool> IsUpdateAvailable()
        {
            if (!string.IsNullOrWhiteSpace(ConfigFilePath) && Config.IsEmpty())
            {
                ParseConfigFile(ConfigFilePath, out var config);
                Config = config;
            }

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

            LatestVersion = LocalVersion;

            if (LocalVersion.CompareTo(onlineVersion) >= 0)
                return false;

            LatestVersion = onlineVersion;
            return true;
        }

        public void InstallProgramFiles(bool launchAfter = false, ConfigurationInfo? config = null, Version? version = null)
        {
            if (LauncherStatus != State.Ready) return;

            config ??= Config;

            var zipDest = config.ZipPath;
            if (string.IsNullOrEmpty(zipDest))
                zipDest = ProgramZipDest;

            //StatusStr = "Updating...";
            BeginUpdateProcessAction?.Invoke();
            LauncherStatus = State.Updating;

            Task installTask = new Task(async () =>
            {
                try
                {
                    version ??= new Version((await httpClient.GetStringAsync(config.VersionLink)).Trim());

                    //! Ensures that the actual .zip is downloaded from Google Drive
                    int attempts = 0;
                    var link = new Uri(config.BuildLink);


                    do
                    {
                        var download = Task.Run(async () =>
                        {
                            using var downloadStream = await httpClient.GetStreamAsync(link);
                            using var fileStream = File.Create(zipDest);

                            await downloadStream.CopyToAsync(fileStream);
                            //await httpClient.DownloadFileTaskAsync(link, zipDest);
                        });
                        BeginDownloadAction?.Invoke();

                        while (!download.IsCompleted) ;

                        ProcessDriveDownload(zipDest, out link);
                        attempts++;
                    }
                    while (!WasDownloadSuccessful(config) && attempts < MAX_DOWNLOAD_ATTEMPTS);

                    //Path.Combine(RootPath, "self-update")
                    _ = OnDownloadComplete();
                }
                catch (Exception ex)
                {
                    throw new Exception("Installation error: " + ex);
                }

                if (launchAfter)
                    LaunchProgram();
            });

            installTask.Start();//.RunSynchronously(RequestMainScheduler?.Invoke() ?? TaskScheduler.Default);
        }

        public void LaunchProgram()
        {
            if (!File.Exists(LaunchFilePath))
                return;
            LauncherStatus = State.Launching;

            if (AppInfo.TargetPlatform == AppInfo.PlatformType.Android)
            {
                AndroidLaunchAction?.Invoke(LaunchFilePath);
            }
            else
            {
                ProcessStartInfo info = new ProcessStartInfo(LaunchFilePath)
                {
                    WorkingDirectory = Path.GetDirectoryName(LaunchFilePath)
                };

                Process.Start(info);
            }

            QuitAction?.Invoke(0);
            //App.Quit();
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
            newLink = new Uri(fileName);
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

        private bool WasDownloadSuccessful(ConfigurationInfo? config = null)
        {
            config ??= Config;
            var zipDest = config.ZipPath;

            try
            {
                if (!HelperUtils.IsZipValid(zipDest))
                    return false;
                //StatusStr = "Installing...";
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

            LauncherStatus = State.Installing;
            BeginInstallAction?.Invoke();

            using (var zipContent = ZipFile.OpenRead(zipDest))
            {
                //The zip itself is likely the executable
                if (!zipContent.HasFile(AppInfo.RunnableExtension))
                {
                    File.Move(zipDest,
                        Path.Combine(buildPath, Path.GetFileName(zipDest).Replace(".zip", AppInfo.RunnableExtension)));
                    LauncherStatus = State.Finalizing;
                }
            }

            //The target file was found in the zip, so extract it
            if (LauncherStatus != State.Finalizing)
            {
                ZipFile.ExtractToDirectory(zipDest, buildPath, true);
                await Task.Run(() =>
                {

                    //Attempt to delete the zip a max of [i] times
                    int i = MAX_ZIP_DELETE_ATTEMPTS;
                    while (i-- > 0)
                    {
                        Thread.Sleep(1000);
                        try
                        {
                            //The extraction must be complete for this to work
                            File.Delete(zipDest);
                            LauncherStatus = State.Finalizing;
                            break;
                        }
                        catch (Exception ex)
                        {
                            _ = $"Error: {ex}";
                        }
                    }

                    //!Finalize
                    UpdateVersionInfo(LatestVersion.ToString());

                    LauncherStatus = State.Ready;
                });
            }

            //? If this is a Launcher self update, this doesn't run until the app is starting to close
            UpdateCompleteAction?.Invoke();
        }

        /// <summary>
        /// Only works if this is a Self Updater
        /// </summary>
        public void UpdateSelfAndRestart(string? programPath = null, bool hasSettingsPathToExclude = true, bool hasBuildPathsToExclude = true, bool debugMode = false)
        {
            if (!IsSelfUpdater) return;

            //*https://andreasrohner.at/posts/Programming/C%23/A-platform-independent-way-for-a-C%23-program-to-update-itself/#:~:text=A%20platform%20independent%20way%20for%20a%20C%23%20program,...%203%20Demo%20Project%20...%204%20References%20
            var updatePath = Path.Combine(RootPath, SELF_UPDATE_TEXT);

            //Exclude Settings
            if (hasSettingsPathToExclude)
            {
                var tmpSettingsPath = Path.Combine(updatePath, new DirectoryInfo(SettingsPath).Name);
                if (Directory.Exists(tmpSettingsPath))
                    Directory.Delete(tmpSettingsPath, true);
            }
            //Exclude Builds
            if (hasBuildPathsToExclude)
            {
                var buildPaths = Directory.GetDirectories(updatePath, $"{BUILD_DIR_PREFIX}*", SearchOption.TopDirectoryOnly);
                for (int i = 0; i < buildPaths.Length; i++)
                {
                    try
                    {
                        Directory.Delete(buildPaths[i], true);
                    }
                    catch { }
                }
            }

            //Windows update
            if (AppInfo.TargetPlatform == AppInfo.PlatformType.Windows)
            {
                var batFilePath = Path.Combine(RootPath, SELF_UPDATE_TEXT + ".bat");
                LauncherStatus = State.Restarting;
                var programName = Path.GetFileName(programPath ?? Process.GetCurrentProcess().MainModule?.FileName);

                if (string.IsNullOrWhiteSpace(programName))
                {
                    LauncherStatus = State.Failed;
                    return;
                }

                string logCommand = debugMode ? " /LOG:update.log" : "";
                //? Quotes around paths are needed if they have spaces
                string content =
$@"TIMEOUT /t 1 /nobreak > NUL
robocopy ""{updatePath}"" ""{RootPath}"" /MOVE /E /NJH /NJS /R:0 /W:0{logCommand}
RD /S /Q ""{updatePath}""
START """" /D ""{RootPath}"" ""{programName}""";
                if (!debugMode)//keep for analysis
                    content += "\nDEL \"%~f0\"";

                File.WriteAllText(batFilePath, content);

                ProcessStartInfo info = new ProcessStartInfo(batFilePath)
                {
                    WorkingDirectory = Path.GetDirectoryName(RootPath)
                };
                Process.Start(info);
            }
            //TODO add self update logic for all platforms
            else if (AppInfo.TargetPlatform == AppInfo.PlatformType.Linux || AppInfo.TargetPlatform == AppInfo.PlatformType.Web)
            {
                //TODO this is untested
                var files = Directory.GetFiles(updatePath);
                List<string> replacedFiles = new List<string>();

                foreach (var file in files)
                {
                    var name = Path.GetFileName(file);
                    if (name == Path.GetFileName(Process.GetCurrentProcess().MainModule.FileName))//Environment.ProcessPath)
                        continue;
                    replacedFiles.Add(name);

                    try
                    {
                        File.Move(file, Path.Combine(RootPath, name));
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

            }

            QuitAction?.Invoke(1);
            //App.Quit();
        }
    }
}