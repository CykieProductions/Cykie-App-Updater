using System.Threading.Tasks;
using Avalonia.Android;
using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.Net;
using Android.OS;
using Java.IO;
using CykieAppLauncher.ViewModels;
using AndroidX.Core.Content;
using static Android.Telephony.CarrierConfigManager;

namespace CykieAppLauncher.Android
{
    //https://stackoverflow.com/questions/4604239/install-application-programmatically-on-android
    //https://developer.android.com/reference/android/content/Intent#ACTION_INSTALL_PACKAGE
    //https://stackoverflow.com/questions/58240297/xamarin-android-10-install-apk-no-activity-found-to-handle-intent
    //https://support.google.com/googleplay/android-developer/answer/12085295?hl=en

    internal static class APKLauncher
    {
        static bool hasInitialized = false;
        internal static void Init()
        {
            if (hasInitialized) return;
            MainViewModel.AndroidLaunchAction += InstallAndRunAPK;
            MainViewModel.RequestAndroidDataPath += GetPublicDataPath; ;
            hasInitialized = true;
        }

        private static string GetPublicDataPath()
        {
            var path = System.IO.Path.Combine(Environment.ExternalStorageDirectory.AbsolutePath, "Android", "data", Application.Context.PackageName, "files");
            return path;
        }

        /// <summary>
        /// Create an intent to install and launch the APK file
        /// </summary>
        /// <param name="apkPath"></param>
        public static void InstallAndRunAPK(string apkPath)
        {
            var context = Application.Context;
            File file = new(apkPath);
            file.SetReadable(true);

            Intent intent;/* = TryGetLaunchIntent(apkPath);

            //! Set up Launch Intent
            //if (IsAlreadyInstalled(context.PackageName))
            if (intent != null)
            {
                //Uri uri = FileProvider.GetUriForFile(context, context.ApplicationInfo.PackageName + ".provider", file);
            }
            else*/
            {
                intent = new Intent(Intent.ActionView);
                Uri uri = FileProvider.GetUriForFile(context, context.ApplicationInfo.PackageName + ".provider", file);

                intent.PutExtra(Intent.ExtraNotUnknownSource, true);
                intent.SetDataAndType(uri, "application/vnd.android.package-archive");
                intent.SetFlags(ActivityFlags.NewTask);
                intent.AddFlags(ActivityFlags.GrantReadUriPermission);
            }
            
            context.StartActivity(intent);//*/
        }

        /// <summary>
        /// Checks if an app is already installed and gets an Intent to launch it
        /// </summary>
        /// <returns>Null if the app wasn't installed</returns>
        public static Intent? TryGetLaunchIntent(string apkPath)
        {
            var context = Application.Context;
            using var packageManager = context.PackageManager;
            var apps = packageManager.GetInstalledApplications(0);
            foreach (var app in apps)
            {
                if (app.Name == MainViewModel.AppName)
                {
                    return packageManager.GetLaunchIntentForPackage(app.PackageName);
                }
            }

            return null;
        }

        [System.Obsolete]
        public static bool IsAlreadyInstalled(string apkPath)
        {
            var context = Application.Context;
            using var packageManager = context.PackageManager;
            var apps = packageManager.GetInstalledApplications(0);

            foreach (var app in apps)
            {
                if (app.Name == MainViewModel.AppName)
                    return true;
            }

            return false;
        }
    }
}
