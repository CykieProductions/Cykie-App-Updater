using System;
using System.Runtime.InteropServices;

namespace CykieAppLauncher.Models
{
    public static class AppInfo
    {
        public enum PlatformType
        {
            Windows, Linux, MacOS, Android, iOS, Web
        }

        public static PlatformType TargetPlatform { get; private set; } = PlatformType.Windows;
        public static bool IsDesktop
        {
            get => TargetPlatform == PlatformType.Windows
                || TargetPlatform == PlatformType.Linux
                || TargetPlatform == PlatformType.MacOS;
        }
        public static string RunnableExtension { get; private set; } = ".exe";
        static bool isInitialized = false;


        public static void TryInit()
        {
            if (isInitialized) return;

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                TargetPlatform = PlatformType.Windows;
                RunnableExtension = ".exe";
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                TargetPlatform = PlatformType.Linux;
                RunnableExtension = ".run";
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                TargetPlatform = PlatformType.MacOS;
                RunnableExtension = ".app";
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Create("ANDROID")))
            {
                TargetPlatform = PlatformType.Android;
                RunnableExtension = ".apk";
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Create("IOS")))
            {
                TargetPlatform = PlatformType.iOS;
                RunnableExtension = ".ipa";
            }
            else if (false)//Web is disabled for now
            {
                TargetPlatform = PlatformType.Web;
                RunnableExtension = ".html";
            }

            isInitialized = true;
        }
    }
}