using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Espera.Core;

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
        ///     The viewmodel for the song metadata editor.
        /// </summary>
        /// <param name="songs">The metadata for the songs to edit.</param>
        /// <param name="multipleSongSaveWarning">
        ///     A warning that is displayed when an attempt is made to save the metadata for more than
        ///     one song.
        /// </param>
        public TagEditorViewModel(IReadOnlyList<LocalSong> songs, Func<Task<bool>> multipleSongSaveWarning)
        {
            if (songs == null)
                throw new ArgumentNullException("songs");

            if (!songs.Any())
                throw new ArgumentException("Tag editor requires at least 1 song");

            this.songs = songs;

            Save = ReactiveCommand.CreateAsyncTask(async _ =>
            {
                var shouldSave = true;

                if (songs.Count > 1) shouldSave = await multipleSongSaveWarning();

                if (shouldSave) await SaveTags();
            });
            isSaving = Save.IsExecuting
                .ToProperty(this, x => x.IsSaving);

            DismissFailure = ReactiveCommand.Create();

            saveFailed = Save.ThrownExceptions.Select(_ => true)
                .Merge(DismissFailure.Select(_ => false))
                .ToProperty(this, x => x.SaveFailed);

            Cancel = ReactiveCommand.Create();

            Finished = Save.Merge(Cancel.ToUnit())
                .FirstAsync()
                .PublishLast()
                .PermaRef();
        }

        public string Album
        {
            get
            {
                if (!string.IsNullOrEmpty(album)) return album;

                return songs.All(x => x.Album == songs[0].Album) ? songs[0].Album : string.Empty;
            }

            set => album = value;
        }

        public string Artist
        {
            get
            {
                if (!string.IsNullOrEmpty(artist)) return artist;

                return songs.All(x => x.Artist == songs[0].Artist) ? songs[0].Artist : string.Empty;
            }

            set => artist = value;
        }

        public ReactiveCommand<object> Cancel { get; }

        public ReactiveCommand<object> DismissFailure { get; }

        public IObservable<Unit> Finished { get; }

        public string Genre
        {
            get
            {
                if (!string.IsNullOrEmpty(genre)) return genre;

                return songs.All(x => x.Genre == songs[0].Genre) ? songs[0].Genre : string.Empty;
            }

            set => genre = value;
        }

        public bool IsSaving => isSaving.Value;

        public bool IsSingleSong => songs.Count == 1;

        public ReactiveCommand<Unit> Save { get; }

        public bool SaveFailed => saveFailed.Value;

        public string Title
        {
            get
            {
                if (!string.IsNullOrEmpty(title)) return title;

                return songs.Count == 1 ? songs[0].Title : null;
            }

            set => title = value;
        }

        private async Task SaveTags()
        {
            foreach (var song in songs)
            {
                if (!string.IsNullOrEmpty(album)) song.Album = album;

                if (!string.IsNullOrEmpty(artist)) song.Artist = artist;

                if (!string.IsNullOrEmpty(genre)) song.Genre = genre;

                if (!string.IsNullOrEmpty(title)) song.Title = title;

                await song.SaveTagsToDisk();
            }
        }
    }
}