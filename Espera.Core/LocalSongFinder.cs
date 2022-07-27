﻿using System;
using System.Collections.Generic;
using System.IO;
using File = TagLib.File;

namespace Espera.Core
{
    /// <summary>
    ///     Encapsulates a recursive call through the local filesystem that reads the tags of all WAV
    ///     and MP3 files and returns them.
    /// </summary>
    internal sealed class LocalSongFinder : ILocalSongFinder, IEnableLogger
    {
        private static readonly string[] AllowedExtensions = { ".mp3", ".wav", ".m4a", ".aac" };
        private readonly string directoryPath;
        private readonly IFileSystem fileSystem;

        public LocalSongFinder(string directoryPath, IFileSystem fileSystem = null)
        {
            if (directoryPath == null)
                Throw.ArgumentNullException(() => directoryPath);

            this.directoryPath = directoryPath;
            this.fileSystem = fileSystem ?? new FileSystem();
        }

        /// <summary>
        ///     This method scans the directory, specified in the constructor, and returns an observable
        ///     with a tuple that contains the song and the data of the artwork.
        /// </summary>
        public IObservable<Tuple<LocalSong, byte[]>> GetSongsAsync()
        {
            return ScanDirectoryForValidPaths(directoryPath)
                .Select(ProcessFile)
                .Where(t => t != null)
                .ToObservable(RxApp.TaskpoolScheduler);
        }

        private static Tuple<LocalSong, byte[]> CreateSong(Tag tag, TimeSpan duration, string filePath)
        {
            var song = new LocalSong(filePath, duration)
            {
                Album = PrepareTag(tag.Album, string.Empty),
                Artist = PrepareTag(tag.FirstAlbumArtist ?? tag.FirstPerformer,
                    "Unknown Artist"), //HACK: In the future retrieve the string for an unkown artist from the view if we want to localize it
                Genre = PrepareTag(tag.FirstGenre, string.Empty),
                Title = PrepareTag(tag.Title, Path.GetFileNameWithoutExtension(filePath)),
                TrackNumber = (int)tag.Track
            };

            var picture = tag.Pictures.FirstOrDefault();

            return Tuple.Create(song, picture == null ? null : picture.Data.Data);
        }

        private static string PrepareTag(string tag, string replacementIfNull)
        {
            return tag == null ? replacementIfNull : TagSanitizer.Sanitize(tag);
        }

        private Tuple<LocalSong, byte[]> ProcessFile(string filePath)
        {
            try
            {
                using (var fileAbstraction = new TagLibFileAbstraction(filePath, fileSystem))
                {
                    using (var file = File.Create(fileAbstraction))
                    {
                        if (file != null && file.Tag != null)
                            return CreateSong(file.Tag, file.Properties.Duration, file.Name);

                        return null;
                    }
                }
            }

            catch (Exception ex)
            {
                this.Log().ErrorException(string.Format("Couldn't read song file {0}", filePath), ex);
                return null;
            }
        }

        private IEnumerable<string> ScanDirectoryForValidPaths(string rootPath)
        {
            var files = Enumerable.Empty<string>();

            try
            {
                files = fileSystem.Directory.GetFiles(rootPath)
                    .Where(x => AllowedExtensions.Contains(Path.GetExtension(x).ToLowerInvariant()));
            }

            catch (Exception ex)
            {
                this.Log().ErrorException(string.Format("Couldn't get files from directory {0}", rootPath), ex);
            }

            var directories = Enumerable.Empty<string>();

            try
            {
                directories = fileSystem.Directory.GetDirectories(rootPath);
            }

            catch (Exception ex)
            {
                this.Log().ErrorException(string.Format("Couldn't get directories from directory {0}", rootPath), ex);
            }

            return files.Concat(directories.SelectMany(ScanDirectoryForValidPaths));
        }

        private class TagLibFileAbstraction : File.IFileAbstraction, IDisposable
        {
            public TagLibFileAbstraction(string path, IFileSystem fileSystem)
            {
                if (path == null)
                    throw new ArgumentNullException("path");

                if (fileSystem == null)
                    throw new ArgumentNullException("fileSystem");

                Name = path;

                var stream = fileSystem.File.OpenRead(path);

                ReadStream = stream;
                WriteStream = stream;
            }

            public string Name { get; }

            public Stream ReadStream { get; }

            public Stream WriteStream { get; }

            public void Dispose()
            {
                ReadStream.Dispose();
            }

            public void CloseStream(Stream stream)
            {
                stream.Close();
            }
        }
    }
}