using Espera.Core.Audio;
using Rareform.IO;
using Rareform.Validation;
using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading;
using System.Threading.Tasks;
using TagLib;

namespace Espera.Core
{
    /// <summary>
    /// Encapsulates a recursive call through the local filesystem that reads the tags of all WAV and MP3 files and returns them.
    /// </summary>
    internal sealed class LocalSongFinder : SongFinder<LocalSong>
    {
        private static readonly string[] AllowedExtensions = new[] { ".mp3", ".wav" };
        private readonly string directoryPath;
        private readonly ConcurrentQueue<string> pathQueue;
        private readonly Subject<int> songsFound;
        private volatile bool abort;
        private DriveType driveType;
        private volatile bool isSearching;
        private volatile bool isTagging;
        private int songCount;

        public LocalSongFinder(string directoryPath)
        {
            if (directoryPath == null)
                Throw.ArgumentNullException(() => directoryPath);

            this.pathQueue = new ConcurrentQueue<string>();
            this.directoryPath = directoryPath;
            this.songsFound = new Subject<int>();

            this.driveType = new DriveInfo(Path.GetPathRoot(directoryPath)).DriveType;
        }

        /// <summary>
        /// Gets the number of songs that have been found.
        /// </summary>
        public IObservable<int> SongsFound
        {
            get { return this.songsFound.AsObservable(); }
        }

        public void Abort()
        {
            this.abort = true;
        }

        public async override Task ExecuteAsync()
        {
            Task.Factory.StartNew(this.StartFileScan);

            this.isSearching = true;

            await Task.Factory.StartNew(this.StartTagScan);

            this.OnCompleted();
        }

        private static LocalSong CreateSong(Tag tag, TimeSpan duration, AudioType audioType, string filePath, DriveType driveType)
        {
            return new LocalSong(filePath, audioType, duration, driveType)
            {
                Album = PrepareTag(tag.Album, String.Empty),
                Artist = PrepareTag(tag.FirstPerformer, "Unknown Artist"), //HACK: In the future retrieve the string for an unkown artist from the view if we want to localize it
                Genre = PrepareTag(tag.FirstGenre, String.Empty),
                Title = PrepareTag(tag.Title, Path.GetFileNameWithoutExtension(filePath)),
                TrackNumber = (int)tag.Track
            };
        }

        private static string PrepareTag(string tag, string replacementIfNull)
        {
            return tag == null ? replacementIfNull : TagSanitizer.Sanitize(tag);
        }

        private void AddSong(TagLib.File file, AudioType audioType)
        {
            var song = CreateSong(file.Tag, file.Properties.Duration, audioType, file.Name, this.driveType);

            this.OnSongFound(song);
        }

        private void ProcessFile(string filePath)
        {
            try
            {
                AudioType? audioType = null; // Use a nullable value so that we don't have to assign a enum value

                TagLib.File file = null;

                switch (Path.GetExtension(filePath))
                {
                    case ".mp3":
                        file = new TagLib.Mpeg.AudioFile(filePath);
                        audioType = AudioType.Mp3;
                        break;

                    case ".wav":
                        file = new TagLib.WavPack.File(filePath);
                        audioType = AudioType.Wav;
                        break;
                }

                if (file != null)
                {
                    if (file.Tag != null)
                    {
                        this.AddSong(file, audioType.Value);
                    }

                    file.Dispose();
                }
            }

            catch (CorruptFileException)
            {
                Interlocked.Decrement(ref this.songCount);
            }

            catch (IOException)
            {
                Interlocked.Decrement(ref this.songCount);
            }
        }

        private void StartFileScan()
        {
            var scanner = new DirectoryScanner(this.directoryPath);

            Observable.FromEventPattern<FileEventArgs>(
                handler => scanner.FileFound += handler,
                handler => scanner.FileFound -= handler)
                .Select(x => x.EventArgs.File)
                .Where(file => AllowedExtensions.Contains(file.Extension))
                .Subscribe(file =>
                {
                    if (this.abort)
                    {
                        scanner.Stop();
                    }

                    else
                    {
                        this.pathQueue.Enqueue(file.FullName);

                        this.songsFound.OnNext(Interlocked.Increment(ref this.songCount));
                    }
                });

            scanner.Start();

            this.isSearching = false;
        }

        private void StartTagScan()
        {
            this.isTagging = true;

            while (this.isTagging && !this.abort)
            {
                string filePath;

                bool hasPath = this.pathQueue.TryDequeue(out filePath);

                if (hasPath)
                {
                    this.ProcessFile(filePath);
                }

                if (!this.isSearching && this.pathQueue.IsEmpty)
                {
                    this.isTagging = false;
                }
            }
        }
    }
}