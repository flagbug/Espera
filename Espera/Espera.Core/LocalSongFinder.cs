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

        public override void Execute()
        {
            var fileScanTask = Task.Factory.StartNew(this.StartFileScan);

            this.isSearching = true;

            var tagScanTask = Task.Factory.StartNew(this.StartTagScan);

            Task.WaitAll(fileScanTask, tagScanTask);

            this.OnCompleted();
        }

        private static LocalSong CreateSong(Tag tag, TimeSpan duration, AudioType audioType, string filePath)
        {
            return new LocalSong(filePath, audioType, duration)
            {
                Album = tag.Album ?? String.Empty,
                Artist = tag.FirstPerformer ?? "Unknown Artist",
                Genre = tag.FirstGenre ?? String.Empty,
                Title = tag.Title ?? Path.GetFileNameWithoutExtension(filePath),
                TrackNumber = (int)tag.Track
            };
        }

        private void AddSong(TagLib.File file, AudioType audioType)
        {
            var song = CreateSong(file.Tag, file.Properties.Duration, audioType, file.Name);

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
                .ForEach(file =>
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