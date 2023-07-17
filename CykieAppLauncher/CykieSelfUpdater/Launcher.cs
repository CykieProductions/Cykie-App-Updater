using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace CykieAppLauncher.Models;

public class Launcher
{
    //! STATIC //
    public string RootPath { get; private set; } = "";
    public string SettingsPath { get; private set; } = "";

    private const int MAX_DOWNLOAD_ATTEMPTS = 4;
    private const int MAX_ZIP_DELETE_ATTEMPTS = 12;
    public const string BUILD_DIR_PREFIX = "Build - ";
    private static readonly HttpClient httpClient = new();
    public static TaskScheduler? MainScheduler { get; private set; }

    public enum State
    {
        Ready, Updating, Launching, Self_Updating, Finalizing
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

    private Version _localVersion = Version.Default;

    public Version LocalVersion
    {
        get => _localVersion;
        private set
        {
            _localVersion = value;
            OnChangedVersionAction?.Invoke(value);
        }
    }

    public Version LatestVersion { get; protected set; } = Version.Invalid;
    public bool IsSelfUpdater { get; }

    //! Profile Config
    public ConfigurationInfo Config { get; private set; } = new(new string[6]);

    [Obsolete("Will reevaluate usefulness once refactoring is completely")]
    public static bool AutoLaunch { get; private set; }
    public string AppName { get => Config.Name; }
    public string ConfigFilePath { get; private set; } = "";
    public string? ProgramZipDest { get; private set; } = null;
    public string? LaunchFilePath { get; private set; } = null;
    public string BuildPath { get; private set; }

    //! EVENTS //
    /// <summary>
    /// 0 means normal exit; 1 means launch exit; 2 means self-update exit; -1 means generic forced crash
    /// </summary>
    public static event Action<int>? QuitAction;
    public static event Func<TaskScheduler> RequestMainScheduler;
    public static event Action<string>? AndroidLaunchAction;
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
    public static event Func<string> RequestAndroidDataPath;
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
    public event Action<State>? OnChangedStateAction;
    public event Action<Version>? OnChangedVersionAction;

    public event Action? BeginUpdateProcessAction;
    public event Action<Task>? BeginDownloadAction;
    public event Action? BeginInstallAction;
    public event Action? UpdateCompleteAction;


    public Launcher(string configPath, string? zipPath = null, string? buildPath = null, string? settingsPath = null, bool isSelfUpdater = false)
    {
        MainScheduler = RequestMainScheduler?.Invoke();

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
                Version curVersion = new(assembly.GetName().Version?.ToString());
                var myZip = ProgramZipDest;
                var vLink = "https://drive.google.com/uc?id=1-gMLfa0JpO1ui-UHlI3OwgDVAR0I2kuW";
                var bLink = "https://drive.google.com/uc?id=1CAC74wNYPJq5TBhr9VBY_5dwz3j6lrN8";

                Config = new(assembly.GetName().Name, curVersion.ToString(), myZip, assembly.Location, vLink, bLink);
                LocalVersion = new(Config.Version);
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
            //Default Links
            string defName = "Blooming Darkness";
            string vLink = "https://drive.google.com/uc?export=download&id=1yTkCTalsDPKeJ2kpAgIAZMGvv82qZ9dh";
            string bLink = "https://drive.google.com/uc?export=download&id=1FpTxX1L3-CBd5lTM29Wzm3G_JUpXxNAK";

            if (!AppInfo.IsDesktop)
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

        var name = info[0].Split('=', 2)[1].Trim();
        //set auto values
            ProgramZipDest ??= Path.Combine(RootPath, $"{name}.zip");
            LaunchFilePath ??= Path.Combine(RootPath, $"{BUILD_DIR_PREFIX}{name}", $"{name}{AppInfo.RunnableExtension}");

        //Set Config from file content
        config = new(name, new Version(info[1].Split('=', 2)[1].Trim()).ToString(),
            info[2].Split('=', 2)[1].Trim().Unless("auto", ProgramZipDest), info[3].Split('=', 2)[1].Trim().Unless("auto", LaunchFilePath),
            info[4].Split('=', 2)[1].Trim(), info[5].Split('=', 2)[1].Trim());

        LocalVersion = new(config.Version);

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
        LocalVersion = new(LocalVersion.ToString());
    }
    void UpdateVersionInfo(string version)
    {
        LocalVersion = new(version);
        if (File.Exists(ConfigFilePath))
        {
            var contents = File.ReadAllLines(ConfigFilePath);
            contents[1] = "Version=" + LocalVersion.ToString();
            File.WriteAllLines(ConfigFilePath, contents);
        }
    }

    public async Task<bool> IsUpdateAvailable()
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
                    BeginDownloadAction?.Invoke(download);

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

        installTask.RunSynchronously(MainScheduler ?? TaskScheduler.Default);
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
            ProcessStartInfo info = new(LaunchFilePath)
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
                UpdateCompleteAction?.Invoke();
            });
        }

        //? If this is a self update, this doesn't run until the app is starting to close
        //UpdateVersionInfo(latestVersion.ToString());

        //LauncherStatus = State.Ready;
        //UpdateCompleteAction?.Invoke();
    }

    /// <summary>
    /// Only works if this is a Self Updater
    /// </summary>
    public void UpdateSelfAndRestart()
    {
        if (!IsSelfUpdater) return;

        //*https://andreasrohner.at/posts/Programming/C%23/A-platform-independent-way-for-a-C%23-program-to-update-itself/#:~:text=A%20platform%20independent%20way%20for%20a%20C%23%20program,...%203%20Demo%20Project%20...%204%20References%20
        var updatePath = Path.Combine(RootPath, "self-update");

        //Exclude Settings
        var tmpSettingsPath = Path.Combine(updatePath, new DirectoryInfo(SettingsPath).Name);
        if (Directory.Exists(tmpSettingsPath))
            Directory.Delete(tmpSettingsPath, true);
        //Exclude Builds
        var buildPaths = Directory.GetDirectories(updatePath, $"{BUILD_DIR_PREFIX}*", SearchOption.TopDirectoryOnly);
        for (int i = 0; i < buildPaths.Length; i++)
        {
            try
            {
                Directory.Delete(buildPaths[i], true);
            }
            catch { }
        }

        //Windows update
        if (AppInfo.TargetPlatform == AppInfo.PlatformType.Windows)
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
        else if (AppInfo.TargetPlatform == AppInfo.PlatformType.Linux || AppInfo.TargetPlatform == AppInfo.PlatformType.Web)
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

        }

        QuitAction?.Invoke(1);
        //App.Quit();
    }
}
