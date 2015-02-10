using Espera.Network;
using ReactiveMarrow;
using System;
using System.IO;
using System.IO.Abstractions;
using System.Reactive;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Reactive.Threading.Tasks;
using System.Threading.Tasks;

namespace Espera.Core.Mobile
{
    public class MobileSong : Song
    {
        private readonly AsyncSubject<Unit> dataGate;

        private MobileSong(string path, TimeSpan duration)
            : base(path, duration)
        {
            this.dataGate = new AsyncSubject<Unit>();
        }

        public override bool IsVideo
        {
            get { return false; }
        }

        public override NetworkSongSource NetworkSongSource
        {
            get { return NetworkSongSource.Mobile; }
        }

        public override string PlaybackPath
        {
            get { return this.OriginalPath; }
        }

        internal static MobileSong Create(NetworkSong metaData, IObservable<byte[]> data, IFileSystem fileSystem = null)
        {
            fileSystem = fileSystem ?? new FileSystem();

            string tempPath = fileSystem.Path.GetTempFileName();

            // Lol, MediaElement is too stupid to play a file with a .tmp extension
            string newName = Path.ChangeExtension(tempPath, ".mp3");
            fileSystem.File.Move(tempPath, newName);
            tempPath = newName;

            var song = new MobileSong(tempPath, metaData.Duration)
            {
                Album = metaData.Album,
                Artist = metaData.Artist,
                Genre = metaData.Genre,
                Title = metaData.Title
            };

            var conn = data.FirstAsync()
                .Do(x => fileSystem.File.WriteAllBytes(tempPath, x))
                .ToUnit()
                .Multicast(song.dataGate);
            conn.Connect();

            return song;
        }

        internal override Task PrepareAsync(YoutubeStreamingQuality qualityHint)
        {
            return this.dataGate.ToTask();
        }
    }
}