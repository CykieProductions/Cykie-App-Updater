using Android.App;
using Android.Content;
using Android.OS;
using Java.IO;
using Avalonia;
using Avalonia.Android;
using Avalonia.ReactiveUI;
using Application = Android.App.Application;
using CykieAppLauncher.ViewModels;

namespace CykieAppLauncher.Android
{
    [Activity(Theme = "@style/MyTheme.Splash", MainLauncher = true, NoHistory = true)]
    public class SplashActivity : AvaloniaSplashActivity<App>
    {
        protected override AppBuilder CustomizeAppBuilder(AppBuilder builder)
        {
            APKLauncher.Init();
            return base.CustomizeAppBuilder(builder)
                .WithInterFont()
                .UseReactiveUI();
        }

        protected override void OnCreate(Bundle? savedInstanceState)
        {
            base.OnCreate(savedInstanceState);
            
        }

        protected override void OnResume()
        {
            base.OnResume();

            StartActivity(new Intent(Application.Context, typeof(MainActivity)));
        }
    }
}