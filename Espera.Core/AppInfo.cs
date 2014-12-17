using System;
using System.IO;
using System.Reflection;

namespace Espera.Core
{
    public static class AppInfo
    {
        /// <summary>
        /// Returns a value whether this application is portable or not. The application is portable
        /// if a file with the name "PORTABLE" is present in the <see cref="AppRootPath" /> directory.
        /// </summary>
        public static readonly bool IsPortable;

        public static readonly string AppName;
        public static readonly string BlobCachePath;
        public static readonly string ApplicationDataPath;
        public static readonly string LibraryFilePath;
        public static readonly string LogFilePath;
        public static readonly string OverridenApplicationDataPath;
        public static readonly Version Version;
        public static readonly string AppRootPath;
        public static readonly string UpdatePath;

        static AppInfo()
        {
            AppName = "Espera";

#if DEBUG
            // Set and uncomment this if you want to change the app data folder for debugging

            // OverridenApplicationDataPath = "D://AppData";

            AppName = "EsperaDebug";
#endif

            var baseDirectory = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);

            IsPortable = File.Exists(Path.Combine(baseDirectory.Parent.FullName, "PORTABLE"));

            ApplicationDataPath = Path.Combine(OverridenApplicationDataPath ?? Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), AppName);
            BlobCachePath = Path.Combine(ApplicationDataPath, "BlobCache");
            LibraryFilePath = Path.Combine(ApplicationDataPath, "Library.json");
            LogFilePath = Path.Combine(ApplicationDataPath, "Log.txt");
            UpdatePath = "http://getespera.com/releases/squirrel/";
            Version = Assembly.GetExecutingAssembly().GetName().Version;

            // Directory.GetParent doesn't work here, it has problems when
            // AppDomain.CurrentDomain.BaseDirectory returns a path with a backslash and returns the
            // same directory instead of the parent
            AppRootPath = baseDirectory.Parent.Parent.FullName;

            if (!IsPortable)
            {
                // If we're a portable app, let Squirrel figure out the path for us
                AppRootPath = null;
            }
        }
    }
}