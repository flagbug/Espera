using Caliburn.Micro;
using Espera.Core.Management;
using Espera.Core.Settings;
using System;
using System.IO;

namespace Espera.View.ViewModels
{
    internal class DesignTimeShellViewModel : ShellViewModel
    {
        private static readonly Library Library;

        static DesignTimeShellViewModel()
        {
            if (Execute.InDesignMode)
            {
                string directoryPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), @"Espera\");
                string filePath = Path.Combine(directoryPath, "Library.xml");

                Library = new Library(new RemovableDriveWatcher(), new LibraryFileReader(filePath), new LibraryFileWriter(filePath), new LibrarySettingsWrapper());
            }
        }

        public DesignTimeShellViewModel()
            : base(Library, new WindowManager())
        { }
    }
}