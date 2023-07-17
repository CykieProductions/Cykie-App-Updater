//Required Model files get copied over to this project before each build
//https://stackoverflow.com/questions/49531513/adding-pre-build-commands-to-copy-files-to-a-project-in-vs-2017
using CykieAppLauncher.Models;
using Version = CykieAppLauncher.Models.Version;

namespace CykieSelfUpdater;

public abstract class SelfUpdaterBase : Launcher
{
    public SelfUpdaterBase(string configPath, string? zipPath = null, string? buildPath = null, string? settingsPath = null) 
        : base(configPath, zipPath, buildPath, settingsPath, true)
    {
        if (!LatestVersion.IsValid())
        {
            _ = IsUpdateAvailable();
        }

        UpdateCompleteAction += OnInstallComplete;
    }

    private void OnInstallComplete()
    {
        UpdateSelfAndRestart();
    }

    public bool TryUpdateSelf()
    {
        var assembly = System.Reflection.Assembly.GetExecutingAssembly();
        Version curVersion = new(assembly.GetName().Version?.ToString());

        //If the latest version is invalid or if it isn't new then ignore it
        if (!LatestVersion.IsValid() || LatestVersion.CompareTo(curVersion) <= 0)
            return false;

        AskToUpdate();
        return true;
    }

    /// <summary>
    /// Will simply update without asking by default! Override this with an environment-specific confirmation.
    /// </summary>
    protected virtual void AskToUpdate()
    {
        OnDecideToUpdate(true);
    }

    /// <summary>
    /// This must be called in response to the confirmation
    /// </summary>
    /// <param name="shouldUpdate">Did the user decide to update</param>
    public virtual void OnDecideToUpdate(bool shouldUpdate)
    {
        if (shouldUpdate)
            InstallProgramFiles(false, Config, LatestVersion);
    }
}