using System;
using System.IO;

namespace Espera.Core
{
    public static class ApplicationHelper
    {
        /// <summary>
        /// The installation folder path of VLC media player
        /// </summary>
        public static readonly string VlcFolderPath;

        static ApplicationHelper()
        {
            const string vlcPath = @"VideoLAN\VLC";

            VlcFolderPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), vlcPath);
        }

        public static string DetectVlcFolderPath()
        {
            if (File.Exists(Path.Combine(VlcFolderPath, "vlc.exe")))
                return VlcFolderPath;

            return null;
        }

        public static bool IsVlcInstalled()
        {
            return DetectVlcFolderPath() != null;
        }
    }
}