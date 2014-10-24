using System;
using System.IO;
using System.IO.Abstractions.TestingHelpers;
using Espera.Core.Management;
using Espera.Core.Settings;

namespace Espera.View.DesignTime
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
                string filePath = Path.Combine(directoryPath, "Library.json");

                library = new Library(new LibraryFileReader(filePath), new LibraryFileWriter(filePath),
                    new CoreSettings(), new MockFileSystem());

                library.Initialize();
            }

            return library;
        }
    }
}