using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Espera.Core.Audio;
using FlagLib.Extensions;
using FlagLib.IO;
using TagLib;

namespace Espera.Core
{
    /// <summary>
    /// Encapsulates a recursive call through the local filesystem that reads the tags of all WAV and MP3 files and returns them.
    /// </summary>
    public sealed class LocalSongFinder
    {
        private static readonly string[] AllowedExtensions = new[] { ".mp3", ".wav" };

        private readonly DirectoryScanner scanner;
        private volatile bool isSearching;
        private volatile bool isTagging;
        private readonly Queue<string> pathQueue;
        private readonly object syncLock;
        private readonly object progressLock;
        private readonly List<Song> songsFound;
        private readonly List<string> corruptFiles;

        /// <summary>
        /// Occurs when a song has been found.
        /// </summary>
        public event EventHandler<SongEventArgs> SongFound;

        /// <summary>
        /// Occurs when the song crawler has finished.
        /// </summary>
        public event EventHandler Finished;

        /// <summary>
        /// Gets the songs that have been found.
        /// </summary>
        /// <value>The songs that have been found.</value>
        public IEnumerable<Song> SongsFound
        {
            get { return this.songsFound; }
        }

        /// <summary>
        /// Gets the files that are corrupt and could not be read.
        /// </summary>
        public IEnumerable<string> CorruptFiles
        {
            get { return this.corruptFiles; }
        }

        /// <summary>
        /// Gets the number of tags that are processed yet.
        /// </summary>
        public int TagsProcessed
        {
            get
            {
                int songCount;

                lock (this.progressLock)
                {
                    songCount = this.songsFound.Count;
                }

                return songCount;
            }
        }

        /// <summary>
        /// Gets the total number of songs that are counted yet.
        /// </summary>
        public int CurrentTotalSongs
        {
            get
            {
                int pathCount;

                lock (this.syncLock)
                {
                    pathCount = this.pathQueue.Count;
                }

                lock (this.progressLock)
                {
                    pathCount += this.songsFound.Count;
                }

                return pathCount;
            }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="LocalSongFinder"/> class.
        /// </summary>
        /// <param name="path">The path of the directory where the recursive search should start.</param>
        public LocalSongFinder(string path)
        {
            this.syncLock = new object();
            this.progressLock = new object();

            this.pathQueue = new Queue<string>();
            this.songsFound = new List<Song>();
            this.corruptFiles = new List<string>();
            this.scanner = new DirectoryScanner(path);
            this.scanner.FileFound += ScannerFileFound;
        }

        /// <summary>
        /// Starts the song crawler in an asynchronous manner.
        /// </summary>
        public void Start()
        {
            var fileScanTask = Task.Factory.StartNew(this.StartFileScan);
            var tagScanTask = Task.Factory.StartNew(this.StartTagScan);

            Task.WaitAll(fileScanTask, tagScanTask);

            this.Finished.RaiseSafe(this, EventArgs.Empty);
        }

        private void StartFileScan()
        {
            this.isSearching = true;
            this.scanner.Start();
            this.isSearching = false;
        }

        private void StartTagScan()
        {
            this.isTagging = true;

            while (this.isSearching || this.isTagging)
            {
                string filePath = null;

                lock (this.syncLock)
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
                    try
                    {
                        Tag tag = null;
                        TimeSpan duration = TimeSpan.Zero;
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
                            duration = file.Properties.Duration;
                            tag = file.Tag;

                            file.Dispose();
                        }

                        if (tag != null)
                        {
                            var song =
                                new LocalSong(new Uri(filePath), audioType.Value, duration)
                                    {
                                        Album = tag.Album ?? String.Empty,
                                        Artist = tag.FirstPerformer ?? "Unknown Artist",
                                        Genre = tag.FirstGenre ?? String.Empty,
                                        Title = tag.Title ?? String.Empty,
                                        TrackNumber = (int)tag.Track
                                    };

                            lock (this.progressLock)
                            {
                                this.songsFound.Add(song);
                            }

                            this.SongFound.RaiseSafe(this, new SongEventArgs(song));
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
            }
        }

        /// <summary>
        /// Handles the FileFound event of the directory scanner.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="FlagLib.IO.FileEventArgs"/> instance containing the event data.</param>
        private void ScannerFileFound(object sender, FileEventArgs e)
        {
            if (AllowedExtensions.Contains(e.File.Extension))
            {
                lock (this.syncLock)
                {
                    this.pathQueue.Enqueue(e.File.FullName);
                }
            }
        }
    }
}