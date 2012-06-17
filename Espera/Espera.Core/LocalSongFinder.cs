using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Espera.Core.Audio;
using Rareform.IO;
using Rareform.Validation;
using TagLib;

namespace Espera.Core
{
    /// <summary>
    /// Encapsulates a recursive call through the local filesystem that reads the tags of all WAV and MP3 files and returns them.
    /// </summary>
    internal sealed class LocalSongFinder : SongFinder<LocalSong>
    {
        private static readonly string[] AllowedExtensions = new[] { ".mp3", ".wav" };
        private readonly List<string> corruptFiles;
        private readonly Queue<string> pathQueue;
        private readonly DirectoryScanner scanner;
        private readonly object songListLock;
        private readonly object tagLock;
        private volatile bool abort;
        private volatile bool isSearching;
        private volatile bool isTagging;

        /// <summary>
        /// Initializes a new instance of the <see cref="LocalSongFinder"/> class.
        /// </summary>
        /// <param name="path">The path of the directory where the recursive search should start.</param>
        public LocalSongFinder(string path)
        {
            if (path == null)
                Throw.ArgumentNullException(() => path);

            this.tagLock = new object();
            this.songListLock = new object();

            this.pathQueue = new Queue<string>();
            this.corruptFiles = new List<string>();
            this.scanner = new DirectoryScanner(path);
            this.scanner.FileFound += ScannerFileFound;
        }

        /// <summary>
        /// Gets the files that are corrupt and could not be read.
        /// </summary>
        public IEnumerable<string> CorruptFiles
        {
            get { return this.corruptFiles; }
        }

        /// <summary>
        /// Gets the total number of songs that are counted yet.
        /// </summary>
        public int CurrentTotalSongs
        {
            get
            {
                int pathCount;

                lock (this.tagLock)
                {
                    pathCount = this.pathQueue.Count;
                }

                lock (this.songListLock)
                {
                    pathCount += this.InternSongsFound.Count;
                }

                return pathCount;
            }
        }

        /// <summary>
        /// Gets the number of tags that are processed yet.
        /// </summary>
        public int TagsProcessed
        {
            get
            {
                int songCount;

                lock (this.songListLock)
                {
                    songCount = this.InternSongsFound.Count;
                }

                return songCount;
            }
        }

        public void Abort()
        {
            this.abort = true;
        }

        /// <summary>
        /// Starts the <see cref="LocalSongFinder"/>.
        /// </summary>
        public override void Start()
        {
            var fileScanTask = Task.Factory.StartNew(this.StartFileScan);

            this.isSearching = true;

            var tagScanTask = Task.Factory.StartNew(this.StartTagScan);

            Task.WaitAll(fileScanTask, tagScanTask);

            this.OnFinished(EventArgs.Empty);
        }

        private static LocalSong CreateSong(Tag tag, TimeSpan duration, AudioType audioType, string filePath)
        {
            return new LocalSong(filePath, audioType, duration)
                       {
                           Album = tag.Album ?? String.Empty,
                           Artist = tag.FirstPerformer ?? String.Empty,
                           Genre = tag.FirstGenre ?? String.Empty,
                           Title = tag.Title ?? Path.GetFileNameWithoutExtension(filePath),
                           TrackNumber = (int)tag.Track
                       };
        }

        private void AddSong(TagLib.File file, AudioType audioType)
        {
            var song = CreateSong(file.Tag, file.Properties.Duration, audioType, file.Name);

            lock (this.songListLock)
            {
                this.InternSongsFound.Add(song);
            }

            this.OnSongFound(new SongEventArgs(song));
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
                this.corruptFiles.Add(filePath);
            }

            catch (IOException)
            {
                this.corruptFiles.Add(filePath);
            }
        }

        private void ScannerFileFound(object sender, FileEventArgs e)
        {
            if (this.abort || !AllowedExtensions.Contains(e.File.Extension))
                return;

            lock (this.tagLock)
            {
                this.pathQueue.Enqueue(e.File.FullName);
            }
        }

        private void StartFileScan()
        {
            this.scanner.Start();
            this.isSearching = false;
        }

        private void StartTagScan()
        {
            this.isTagging = true;

            while ((this.isSearching || this.isTagging) && !this.abort)
            {
                string filePath = null;

                lock (this.tagLock)
                {
                    if (this.pathQueue.Any())
                    {
                        filePath = this.pathQueue.Dequeue();
                    }

                    else if (!this.isSearching)
                    {
                        this.isTagging = false;
                    }
                }

                if (filePath != null)
                {
                    this.ProcessFile(filePath);
                }
            }
        }
    }
}