using System;
using System.Collections.Generic;
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
        private readonly DirectoryScanner scanner;

        /// <summary>
        /// Gets the supported extension.
        /// </summary>
        /// <value>The supported extension.</value>
        public List<string> SupportedExtensions { get; private set; }

        /// <summary>
        /// Gets the songs that have been found.
        /// </summary>
        /// <value>The songs that have been found.</value>
        public List<Song> SongsFound { get; private set; }

        /// <summary>
        /// Gets the songs that are corrupt and could not be read.
        /// </summary>
        public List<string> CorruptSongs { get; private set; }

        /// <summary>
        /// Occurs when a song has been found.
        /// </summary>
        public event EventHandler<SongEventArgs> SongFound;

        /// <summary>
        /// Occurs when the song crawler has finished.
        /// </summary>
        public event EventHandler Finished;

        /// <summary>
        /// Initializes a new instance of the <see cref="LocalSongFinder"/> class.
        /// </summary>
        /// <param name="path">The path of the directory where the recursive search should start.</param>
        public LocalSongFinder(string path)
        {
            SupportedExtensions = new List<string>(new[] { ".mp3", ".wav" });
            this.SongsFound = new List<Song>();
            this.CorruptSongs = new List<string>();
            this.scanner = new DirectoryScanner(path);
            this.scanner.FileFound += scanner_FileFound;
        }

        /// <summary>
        /// Starts the song crawler.
        /// </summary>
        public void Start()
        {
            this.scanner.Start();

            this.Finished.RaiseSafe(this, EventArgs.Empty);
        }

        /// <summary>
        /// Handles the FileFound event of the directory scanner.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="FlagLib.IO.FileEventArgs"/> instance containing the event data.</param>
        private void scanner_FileFound(object sender, FileEventArgs e)
        {
            if (this.SupportedExtensions.Contains(e.File.Extension))
            {
                try
                {
                    Tag tag = null;
                    TimeSpan duration = TimeSpan.Zero;
                    AudioType? audioType = null; // Use a nullable value so that we don't have to assign a enum value

                    File file = null;

                    switch (e.File.Extension)
                    {
                        case ".mp3":
                            file = new TagLib.Mpeg.AudioFile(e.File.FullName);
                            audioType = AudioType.Mp3;
                            break;

                        case ".wav":
                            file = new TagLib.WavPack.File(e.File.FullName);
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
                        var song = new LocalSong(new Uri(e.File.FullName), audioType.Value, duration, DateTime.Now)
                        {
                            Album = tag.Album ?? String.Empty,
                            Artist = tag.FirstPerformer ?? "Unknown Artist",
                            Genre = tag.FirstGenre ?? String.Empty,
                            Title = tag.Title ?? String.Empty
                        };

                        this.SongsFound.Add(song);
                        this.SongFound.RaiseSafe(this, new SongEventArgs(song));
                    }
                }

                catch (CorruptFileException)
                {
                    this.CorruptSongs.Add(e.File.FullName);
                }
            }
        }
    }
}