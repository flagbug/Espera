using Rareform.IO;
using Rareform.Validation;
using System;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using TagLib;

namespace Espera.Core
{
    /// <summary>
    /// Encapsulates a recursive call through the local filesystem that reads the tags of all WAV and MP3 files and returns them.
    /// </summary>
    internal sealed class LocalSongFinder
    {
        private static readonly string[] AllowedExtensions = { ".mp3", ".wav", ".m4a", ".aac" };
        private readonly string directoryPath;
        private readonly DriveType driveType;

        public LocalSongFinder(string directoryPath)
        {
            if (directoryPath == null)
                Throw.ArgumentNullException(() => directoryPath);

            this.directoryPath = directoryPath;

            this.driveType = new DriveInfo(Path.GetPathRoot(directoryPath)).DriveType;
        }

        /// <summary>
        /// This method scans the directory, specified in the constructor, and returns an observable with a tuple that contains the song and the data of the artwork.
        /// </summary>
        public IObservable<Tuple<LocalSong, byte[]>> GetSongs()
        {
            return this.ScanDirectoryForValidPaths()
                .Select(this.ProcessFile)
                .Where(t => t != null);
        }

        private static Tuple<LocalSong, byte[]> CreateSong(Tag tag, TimeSpan duration, string filePath, DriveType driveType)
        {
            var song = new LocalSong(filePath, duration, driveType)
            {
                Album = PrepareTag(tag.Album, String.Empty),
                Artist = PrepareTag(tag.FirstPerformer, "Unknown Artist"), //HACK: In the future retrieve the string for an unkown artist from the view if we want to localize it
                Genre = PrepareTag(tag.FirstGenre, String.Empty),
                Title = PrepareTag(tag.Title, Path.GetFileNameWithoutExtension(filePath)),
                TrackNumber = (int)tag.Track
            };

            IPicture picture = tag.Pictures.FirstOrDefault();

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
                using (var file = TagLib.File.Create(filePath))
                {
                    if (file != null && file.Tag != null)
                    {
                        return CreateSong(file.Tag, file.Properties.Duration, file.Name, this.driveType);
                    }

                    return null;
                }
            }

            catch (CorruptFileException)
            {
                return null;
            }

            catch (IOException)
            {
                return null;
            }
        }

        private IObservable<string> ScanDirectoryForValidPaths()
        {
            return Observable.Create<string>(o =>
            {
                var scanner = new DirectoryScanner(this.directoryPath);

                IDisposable sub = Observable.FromEventPattern<FileEventArgs>(
                    handler => scanner.FileFound += handler,
                    handler => scanner.FileFound -= handler)
                .Select(x => x.EventArgs.File)
                .Where(file => AllowedExtensions.Contains(file.Extension.ToLowerInvariant()))
                .Subscribe(file => o.OnNext(file.FullName));

                scanner.Finished += (sender, args) =>
                {
                    sub.Dispose();
                    o.OnCompleted();
                };

                scanner.Start();

                return scanner.Stop;
            });
        }
    }
}