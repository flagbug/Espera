using System;
using System.IO;
using System.Threading.Tasks;

namespace Espera.Core.Mobile
{
    public class MobileSong : Song
    {
        private readonly AsyncSubject<Unit> dataGate;

        private MobileSong(string path, TimeSpan duration)
            : base(path, duration)
        {
            dataGate = new AsyncSubject<Unit>();
        }

        public override bool IsVideo => false;

        public override NetworkSongSource NetworkSongSource => NetworkSongSource.Mobile;

        public override string PlaybackPath => OriginalPath;

        internal static MobileSong Create(NetworkSong metaData, IObservable<byte[]> data, IFileSystem fileSystem = null)
        {
            fileSystem = fileSystem ?? new FileSystem();

            var tempPath = fileSystem.Path.GetTempFileName();

            // Lol, MediaElement is too stupid to play a file with a .tmp extension
            var newName = Path.ChangeExtension(tempPath, ".mp3");
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
            return dataGate.ToTask();
        }
    }
}