using System;
using System.IO;
using System.Reflection;

namespace Espera.Core
{
    public static class AppInfo
    {
        public static readonly string BlobCachePath;
        public static readonly string LibraryFilePath;
        public static readonly string LogFilePath;
        public static readonly string OverridenApplicationDataPath;
        public static readonly Version Version;
        public static readonly string UpdatePath;
        public static readonly string ApplicationRootPath;

        static AppInfo()
        {
#if DEBUG
            // Set this if you want to change the app data folder for debugging

            OverridenApplicationDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "EsperaDebug");
#endif

            ApplicationRootPath = OverridenApplicationDataPath ?? Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "..");
            BlobCachePath = Path.Combine(ApplicationRootPath, "BlobCache");
            LibraryFilePath = Path.Combine(ApplicationRootPath, "Library.json");
            LogFilePath = Path.Combine(ApplicationRootPath, "Log.txt");
            UpdatePath = "http://getespera.com/releases/squirrel/";
            Version = Assembly.GetExecutingAssembly().GetName().Version;
        }
    }
}