using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Threading.Tasks;
using Espera.Core;
using Espera.View.CacheMigration;
using ReactiveMarrow;
using ReactiveUI;

namespace Espera.View.ViewModels
{
    public class TagEditorViewModel : ReactiveObject
    {
        private readonly ObservableAsPropertyHelper<bool> isSaving;
        private readonly ObservableAsPropertyHelper<bool> saveFailed;
        private readonly IReadOnlyList<LocalSong> songs;
        private string album;
        private string artist;
        private string genre;
        private string title;

        /// <summary>
        /// The viewmodel for the song metadata editor.
        /// </summary>
        /// <param name="songs">The metadata for the songs to edit.</param>
        /// <param name="multipleSongSaveWarning">
        /// A warning that is displayed when an attempt is made to save the metadata for more than
        /// one song.
        /// </param>
        public TagEditorViewModel(IReadOnlyList<LocalSong> songs, Func<Task<bool>> multipleSongSaveWarning)
        {
            if (songs == null)
                throw new ArgumentNullException("songs");

            if (!songs.Any())
                throw new ArgumentException("Tag editor requires at least 1 song");

            this.songs = songs;

            this.Save = ReactiveCommand.CreateAsyncTask(async _ =>
            {
                bool shouldSave = true;

                if (songs.Count > 1)
                {
                    shouldSave = await multipleSongSaveWarning();
                }

                if (shouldSave)
                {
                    await this.SaveTags();
                }
            });
            this.isSaving = this.Save.IsExecuting
                .ToProperty(this, x => x.IsSaving);

            this.DismissFailure = ReactiveCommand.Create();

            this.saveFailed = this.Save.ThrownExceptions.Select(_ => true)
                .Merge(this.DismissFailure.Select(_ => false))
                .ToProperty(this, x => x.SaveFailed);

            this.Cancel = ReactiveCommand.Create();

            this.Finished = this.Save.Merge(this.Cancel.ToUnit())
                .FirstAsync()
                .PublishLast()
                .PermaRef();
        }

        public string Album
        {
            get
            {
                if (!string.IsNullOrEmpty(this.album))
                {
                    return this.album;
                }

                return songs.All(x => x.Album == songs[0].Album) ? songs[0].Album : string.Empty;
            }

            set { this.album = value; }
        }

        public string Artist
        {
            get
            {
                if (!string.IsNullOrEmpty(this.artist))
                {
                    return this.artist;
                }

                return songs.All(x => x.Artist == songs[0].Artist) ? songs[0].Artist : string.Empty;
            }

            set { this.artist = value; }
        }

        public ReactiveCommand<object> Cancel { get; private set; }

        public ReactiveCommand<object> DismissFailure { get; private set; }

        public IObservable<Unit> Finished { get; private set; }

        public string Genre
        {
            get
            {
                if (!string.IsNullOrEmpty(this.genre))
                {
                    return this.genre;
                }

                return songs.All(x => x.Genre == songs[0].Genre) ? songs[0].Genre : string.Empty;
            }

            set { this.genre = value; }
        }

        public bool IsSaving
        {
            get { return this.isSaving.Value; }
        }

        public bool IsSingleSong
        {
            get { return this.songs.Count == 1; }
        }

        public ReactiveCommand<Unit> Save { get; private set; }

        public bool SaveFailed
        {
            get { return this.saveFailed.Value; }
        }

        public string Title
        {
            get
            {
                if (!string.IsNullOrEmpty(this.title))
                {
                    return this.title;
                }

                return songs.Count == 1 ? songs[0].Title : null;
            }

            set { this.title = value; }
        }

        private async Task SaveTags()
        {
            foreach (LocalSong song in songs)
            {
                if (!string.IsNullOrEmpty(this.album))
                {
                    song.Album = this.album;
                }

                if (!string.IsNullOrEmpty(this.artist))
                {
                    song.Artist = this.artist;
                }

                if (!string.IsNullOrEmpty(this.genre))
                {
                    song.Genre = this.genre;
                }

                if (!string.IsNullOrEmpty(this.title))
                {
                    song.Title = this.title;
                }

                await song.SaveTagsToDisk();
            }
        }
    }
}