using Microsoft.Maui.Storage;
using System;
using System.Diagnostics;
using System.IO.Compression;
using System.Net;
using System.Net.Http;

namespace CykieAppUpdater;

public partial class MainPage : ContentPage
{
    public enum LauncherState
    {
        Ready, Updating, Launching
    }

    LauncherState launcherState;
    Version localVersion = Version.Default;
    Version latestVersion = Version.Default;

    public MainPage()
    {
        InitializeComponent();
        localVersion = new(AppShell.Config.Version);
        lblVersion.Text = localVersion.ToString();

        if (AppShell.AutoLaunch)
        {
            BtnPlay.IsVisible = false;
            launcherState = LauncherState.Ready;
            OnLaunchClicked(true);
        }
    }

    private void OnCounterClicked(object sender, EventArgs e)
    {
        SemanticScreenReader.Announce(BtnUpdate.Text);
    }

    private void BtnPlay_Clicked(object sender, EventArgs e)
    {
        if (launcherState != LauncherState.Ready)
            return;

        OnLaunchClicked();
    }

    private async void OnLaunchClicked(bool forceUpdate = false)
    {
        if (await IsUpdateAvailable())
        {
            launcherState = LauncherState.Updating;
            if (forceUpdate)
                InstallProgramFiles(true);
            else
                AskToUpdate();

            return;
        }

        launcherState = LauncherState.Launching;
        LaunchProgram();
    }
    async void AskToUpdate()
    {
        bool result = await DisplayAlert("Alert",
                    "There is an update available. Would you like to install it?", "Yes", "No");

        if (result)
        {
            InstallProgramFiles(true);
        }
        else
            launcherState = LauncherState.Ready;
    }

    private async void BtnUpdate_Clicked(object sender, EventArgs e)
    {
        //Don't attempt another update before finishing
        if (launcherState == LauncherState.Updating)
            return;

        launcherState = LauncherState.Updating;
        try
        {
            
            if (!localVersion.IsValid() || await IsUpdateAvailable())
            {
                InstallProgramFiles();
            }
            else
            {
                BtnUpdate.Text = "Up to Date";
            }
        }
        catch (Exception)
        {

            throw;
        }
    }

    private void LaunchProgram()
    {
        ProcessStartInfo info = new(AppShell.LaunchFile)
        {
            WorkingDirectory = Path.GetDirectoryName(AppShell.LaunchFile)
        };
        Process.Start(info);
        Application.Current.Quit();
    }

    private async Task<bool> IsUpdateAvailable()
    {
        HttpClient httpClient = new();
        string onlineVersionStr = "";

        await Task.Run(async () =>
        {
            onlineVersionStr = (await httpClient.GetStringAsync(AppShell.Config.VersionLink)).Trim();
        });

        latestVersion = new Version(onlineVersionStr);

        if (localVersion.Equals(latestVersion))
            return false;
        return true;
    }

    //https://gist.github.com/yasirkula/d0ec0c07b138748e5feaecbd93b6223c
    public async void InstallProgramFiles(bool launchAfter = false, Version? version = null)
    {
        BtnUpdate.Text = "Updating...";
        launcherState = LauncherState.Updating;

        try
        {
            HttpClient httpClient = new();

            if (version == null)
            {
                await Task.Run(async () =>
                {
                    version = new(
                        (await httpClient.GetStringAsync(AppShell.Config.VersionLink)).Trim());
                });
            }

            //! Ensures that the actual .zip is downloaded from Google Drive
            int attempts = 0;
            var link = new Uri(AppShell.Config.BuildLink);
            do
            {
                await Task.Run(async () =>
                {
                    await httpClient.DownloadFileTaskAsync(link, AppShell.ProgramZipDest);
                });
                ProcessDriveDownload(AppShell.ProgramZipDest, out link);
                attempts++;
            }
            while (!OnDownloadFileCompleted(version.ToString()) && attempts < 4);

            //webClient.DownloadFileCompleted += OnDownloadFileCompleted;
            //webClient.DownloadFileAsync(new Uri(AppShell.Config.BuildLink), AppShell.ProgramZipDest, version);
        }
        catch (Exception)
        {
            throw;
        }

        BtnUpdate.Text = "Up to Date";
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
            // Confirmation page starts with <!DOCTYPE html>, which can be preceeded by a newline
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

    private bool OnDownloadFileCompleted(string onlineVersionStr)
    {
        try
        {
            ZipFile.ExtractToDirectory(AppShell.ProgramZipDest,
                //.Replace(AppShell.Config.Version, onlineVersion), 
                Path.GetDirectoryName(AppShell.LaunchFile), true);
            File.Delete(AppShell.ProgramZipDest);
        }
        catch (Exception)
        {
            return false;
        }

        //! Update Version.txt
        UpdateVersionInfo(onlineVersionStr);
        launcherState = LauncherState.Ready;

        return true;
    }

    void UpdateVersionInfo(string version)
    {
        localVersion = new(version);
        File.WriteAllText(AppShell.VersionFile, version);
        lblVersion.Text = localVersion.ToString();
    }
}