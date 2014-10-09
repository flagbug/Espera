using System;
using System.IO;
using System.Reflection;

namespace Espera.Core
{
    public static class AppInfo
    {
        public static readonly string AppName;
        public static readonly string BlobCachePath;
        public static readonly string DirectoryPath;
        public static readonly string LibraryFilePath;
        public static readonly string LogFilePath;
        public static readonly string OverridenBasePath;
        public static readonly Version Version;

        static AppInfo()
        {
            AppName = "Espera";

#if DEBUG
            // Set and uncomment this if you want to change the app data folder for debugging

            // OverridenBasePath = "D://AppData";

            AppName = "EsperaDebug";
#endif

            DirectoryPath = Path.Combine(OverridenBasePath ?? Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), AppName);
            BlobCachePath = Path.Combine(DirectoryPath, "BlobCache");
            LibraryFilePath = Path.Combine(DirectoryPath, "Library.json");
            LogFilePath = Path.Combine(DirectoryPath, "Log.txt");
            Version = Assembly.GetExecutingAssembly().GetName().Version;
        }
    }
}