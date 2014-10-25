using System.IO.Abstractions.TestingHelpers;
using Espera.Core;
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
                library = new Library(new LibraryFileReader(AppInfo.LibraryFilePath), new LibraryFileWriter(AppInfo.LibraryFilePath),
                    new CoreSettings(), new MockFileSystem());

                library.Initialize();
            }

            return library;
        }
    }
}