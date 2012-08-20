using System;
using System.IO;

namespace Espera.Core
{
    public static class ApplicationHelper
    {
        /// <summary>
        /// The installation folder path of VLC media player on a 32-Bit operation system
        /// </summary>
        public static readonly string Vlc32FolderPath;

        /// <summary>
        /// The installation folder path of VLC media player on a 64-Bit operation system when VLC is installed as 32-Bit version
        /// </summary>
        public static readonly string Vlc6432FolderPath;

        /// <summary>
        /// The installation folder path of VLC media player on a 64-Bit operation system
        /// </summary>
        public static readonly string Vlc64FolderPath;

        static ApplicationHelper()
        {
            const string vlcPath = @"VideoLAN\VLC";

            if (Environment.Is64BitOperatingSystem)
            {
                Vlc6432FolderPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), vlcPath);
                Vlc64FolderPath = Path.Combine(Environment.GetEnvironmentVariable("ProgramW6432"), vlcPath);
            }

            else
            {
                Vlc32FolderPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), vlcPath);
            }
        }

        public static bool IsVlcInstalled()
        {
            if (Environment.Is64BitOperatingSystem)
            {
                if (File.Exists(Path.Combine(Vlc64FolderPath, "vlc.exe")))
                    return true;

                if (File.Exists(Path.Combine(Vlc6432FolderPath, "vlc.exe")))
                    return true;
            }

            else
            {
                if (File.Exists(Path.Combine(Vlc32FolderPath, "vlc.exe")))
                    return true;
            }

            return false;
        }
    }
}