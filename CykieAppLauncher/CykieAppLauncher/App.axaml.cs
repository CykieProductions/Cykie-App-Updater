using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using CykieAppLauncher.ViewModels;
using CykieAppLauncher.Views;
using System;

namespace CykieAppLauncher
{
    public partial class App : Application
    {
        public enum PlatformType
        {
            Windows, Linux, MacOS, Android, iOS, Web
        }

        public static bool IsDesktop { get => Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime; }
        public static PlatformType TargetPlatform { get; private set; } = PlatformType.Windows;
        public static string RunnableExtension { get; private set; } = ".exe";
        public static ContentControl MainScreen { get; private set; }

        public override void Initialize()
        {
            if (OperatingSystem.IsWindows())
            {
                TargetPlatform = PlatformType.Windows;
                RunnableExtension = ".exe";
            }
            else if (OperatingSystem.IsLinux())
            {
                TargetPlatform = PlatformType.Linux;
                RunnableExtension = ".run";
            }
            else if (OperatingSystem.IsMacOS())
            {
                TargetPlatform = PlatformType.MacOS;
                RunnableExtension = ".app";
            }
            else if (OperatingSystem.IsAndroid())
            {
                TargetPlatform = PlatformType.Android;
                RunnableExtension = ".apk";
            }
            else if (OperatingSystem.IsIOS())
            {
                TargetPlatform = PlatformType.iOS;
                RunnableExtension = ".ipa";
            }
            else if (false)//Web is disabled for now
            {
                TargetPlatform = PlatformType.Web;
                RunnableExtension = ".html";
            }

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

        public static void Quit()
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
                    Environment.Exit(0);
                }
            }
        }
    }
}