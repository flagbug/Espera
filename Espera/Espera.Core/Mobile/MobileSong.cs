using System;
using System.IO;
using System.IO.Abstractions;
using System.Reactive.Linq;
using Espera.Network;

namespace Espera.Core.Mobile
{
    public class MobileSong : Song
    {
        private MobileSong(string path, TimeSpan duration)
            : base(path, duration)
        { }

        internal static MobileSong Create(NetworkSong metaData, IObservable<byte[]> data, IFileSystem fileSystem = null)
        {
            fileSystem = fileSystem ?? new FileSystem();

            string tempPath = fileSystem.Path.GetTempFileName();

            // Lol, MediaElement is too stupid to play a file with a .tmp extension
            string newName = Path.ChangeExtension(tempPath, ".mp3");
            fileSystem.File.Move(tempPath, newName);
            tempPath = newName;

            data.FirstAsync().Subscribe(x => fileSystem.File.WriteAllBytes(tempPath, x));

            var song = new MobileSong(tempPath, metaData.Duration)
            {
                Album = metaData.Album,
                Artist = metaData.Artist,
                Genre = metaData.Genre,
                Title = metaData.Title
            };

            return song;
        }
    }
}