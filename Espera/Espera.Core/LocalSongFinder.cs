using Espera.Core.Audio;
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
    internal sealed class LocalSongFinder : ISongFinder<LocalSong>
    {
        private static readonly string[] AllowedExtensions = new[] { ".mp3", ".wav" };
        private readonly string directoryPath;
        private readonly DriveType driveType;

        public LocalSongFinder(string directoryPath)
        {
            if (directoryPath == null)
                Throw.ArgumentNullException(() => directoryPath);

            this.directoryPath = directoryPath;

            this.driveType = new DriveInfo(Path.GetPathRoot(directoryPath)).DriveType;
        }

        public IObservable<LocalSong> GetSongs()
        {
            return this.StartFileScan()
                .Select(this.ProcessFile)
                .Where(song => song != null);
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

        private LocalSong ProcessFile(string filePath)
        {
            TagLib.File file = null;

            try
            {
                AudioType? audioType = null; // Use a nullable value so that we don't have to assign a enum value

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

                if (file != null && file.Tag != null)
                {
                    return CreateSong(file.Tag, file.Properties.Duration, audioType.Value, file.Name, this.driveType);
                }

                return null;
            }

            catch (CorruptFileException)
            {
                return null;
            }

            catch (IOException)
            {
                return null;
            }

            finally
            {
                if (file != null)
                {
                    file.Dispose();
                }
            }
        }

        private IObservable<string> StartFileScan()
        {
            return Observable.Create<string>(o =>
            {
                var scanner = new DirectoryScanner(this.directoryPath);

                IDisposable sub = Observable.FromEventPattern<FileEventArgs>(
                    handler => scanner.FileFound += handler,
                    handler => scanner.FileFound -= handler)
                .Select(x => x.EventArgs.File)
                .Where(file => AllowedExtensions.Contains(file.Extension))
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