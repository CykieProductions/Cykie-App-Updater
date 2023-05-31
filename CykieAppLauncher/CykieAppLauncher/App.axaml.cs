using Avalonia;
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
        public override void Initialize()
        {
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
            }
            else if (ApplicationLifetime is ISingleViewApplicationLifetime singleViewPlatform)
            {
                singleViewPlatform.MainView = new MainView
                {
                    DataContext = new MainViewModel()
                };
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