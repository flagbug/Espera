using Akavache;
using Espera.Core.Management;
using Espera.Core.Settings;
using System;
using System.IO;
using System.IO.Abstractions.TestingHelpers;

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
                                      new LibraryFileWriter(filePath), new CoreSettings(BlobCache.InMemory), new MockFileSystem());
            }

            return library;
        }
    }
}