using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using CykieAppLauncher.ViewModels;
using CykieAppLauncher.Views;
using CykieAppLauncher.Models;
using System;

namespace CykieAppLauncher;

public partial class App : Application
{
    public static bool IsDesktop { get => Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime; }
    public static ContentControl MainScreen { get; private set; }

    public override void Initialize()
    {
        AppInfo.TryInit();
        Launcher.QuitAction += Quit;
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow
            {
                DataContext = new MainViewModel()
            };
            MainScreen = desktop.MainWindow;
        }
        else if (ApplicationLifetime is ISingleViewApplicationLifetime singleViewPlatform)
        {
            var view = new MainView
            {
                DataContext = new MainViewModel()
            };
            singleViewPlatform.MainView = view;
            MainScreen = view;
        }

        base.OnFrameworkInitializationCompleted();
    }

    /// <summary>
    /// Exits the program
    /// </summary>
    /// <param name="exitCode">0 means normal exit; 1 means launch exit; 2 means self-update exit; -1 means generic forced crash</param>
    public static void Quit(int exitCode = 0)
    {
        if (Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktopApp)
        {
            desktopApp.Shutdown();
        }
        else if (Current?.ApplicationLifetime is ISingleViewApplicationLifetime viewApp)
        {
            //This just hangs the app
            //viewApp.MainView = null;

            if (viewApp.MainView != null)
            {
                viewApp.MainView.IsEnabled = false;
                Environment.Exit(exitCode);
            }
        }
    }
}