using Espera.Core.Management;
using Espera.Core.Settings;
using System;
using System.IO;

namespace Espera.View
{
    internal static class DesignTime
    {
        private static Library library;

        public static Library LoadLibrary()
        {
            if (library == null)
            {
                string directoryPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"Espera\");
                string filePath = Path.Combine(directoryPath, "Library.xml");

                library = new Library(new RemovableDriveWatcher(), new LibraryFileReader(filePath),
                                      new LibraryFileWriter(filePath), new LibrarySettingsWrapper());
            }

            return library;
        }
    }
}