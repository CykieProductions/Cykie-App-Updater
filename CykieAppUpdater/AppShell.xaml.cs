using System.Diagnostics.CodeAnalysis;

namespace CykieAppUpdater;

public partial class AppShell : Shell
{
    public static string AppName { get; private set; }
    public static bool AutoLaunch { get; private set; }

    public static string RootPath { get; private set; }
    public static string VersionFile { get; private set; }
    public static string ConfigFile { get; private set; }
    public static string ProgramZipDest { get; private set; }
    public static string LaunchFile { get; private set; }

    public static ConfigurationInfo Config { get; private set; }


    public AppShell()
	{
        AppName = "[ERROR]";
		InitializeComponent();

        try
        {
            RootPath = AppContext.BaseDirectory;
            ConfigFile = Path.Combine(RootPath, "Config.txt");
            if (!File.Exists(ConfigFile))
            {
                var fs = File.Create(ConfigFile);
                fs.Close();
            }

            var info = File.ReadAllLines(ConfigFile);
            if (info.Length == 0)
            {
                info = ("App Name=PROGRAM\n" +
                    "Zip Path=auto\n" +
                    "Launch Path=auto\n" +
                    "Version Link=\n" +
                    "Build Link=\n" +
                    "Auto Launch=True").Split('\n');
                File.WriteAllLines(ConfigFile, info);
            }
            //https://sites.google.com/site/gdocs2direct/home

            VersionFile = Path.Combine(RootPath, "Version.txt");
            if (!File.Exists(VersionFile))
            {
                var fs = File.Create(VersionFile);//Will be Version.Invalid
                fs.Close();
            }

            Config = new(info[0].Split('=', 2)[1].Trim(), 
                new Version(File.ReadAllText(VersionFile).Trim()).ToString(),
                info[1].Split('=', 2)[1].Trim(), info[2].Split('=', 2)[1].Trim(),
                info[3].Split('=', 2)[1].Trim(), info[4].Split('=', 2)[1].Trim());

            if (bool.TryParse(info[^1].Split('=', 2)[1], out bool r))
                AutoLaunch = r;
            AppName = Config.Name;

            ProgramZipDest = Path.Combine(RootPath, $"{AppName} v{Config.Version}.zip");
            LaunchFile = Path.Combine(RootPath, "Build", $"{AppName}.exe");
        }
        catch (Exception ex)
        {
            throw new Exception($"Error: {ex}");
        }

        MainTitleView.Text = AppName;
    }

}

public record ConfigurationInfo(string Name, string Version, string ZipPath, string LaunchPath,
    string VersionLink, string BuildLink)
{
    public ConfigurationInfo(string[] info) : this(info[0], info[1], info[2], info[3],
        info[4], info[5]) { }
}

public struct Version
{
    public static Version Invalid { get; } = new Version(-1, -1, -1);
    public static Version Default { get; } = new Version(0, 1, 0);

    short major = 0, minor = 1, patch = 0;

    public Version()
    {
        Set(Default);
    }
    public Version(short major, short minor, short patch)
    {
        Set(major, minor, patch);
    }
    public Version(string versionStr)
    {
        var parts = versionStr.Split('.');

        if (parts.Length != 3)
        {
            Set(Invalid);
            return;
        }

        try
        {
            major = short.Parse(parts[0]);
            minor = short.Parse(parts[1]);
            patch = short.Parse(parts[2]);
        }
        catch (Exception)
        {
            Set(Invalid);
        }
    }

    public void Set(short inMajor, short inMinor, short inPatch)
    {
        major = inMajor;
        minor = inMinor;
        patch = inPatch;
    }
    public void Set(Version inVersion)
    {
        major = inVersion.major;
        minor = inVersion.minor;
        patch = inVersion.patch;
    }

    public bool IsValid()
    {
        var v = new Version(ToString());

        if (v.Equals(Invalid))
            return false;
        return true;
    }

    public override string ToString()
    {
        return $"{major}.{minor}.{patch}";
    }
}

//https://stackoverflow.com/questions/45711428/download-file-with-webclient-or-httpclient
public static class HttpClientUtils
{
    public static async Task DownloadFileTaskAsync(this HttpClient client, Uri uri, string fileName)
    {
        using (var downloadStream = await client.GetStreamAsync(uri))
        {
            using (var fileStream = File.Create(fileName))
            {
                await downloadStream.CopyToAsync(fileStream);
            }
        }
    }
}
