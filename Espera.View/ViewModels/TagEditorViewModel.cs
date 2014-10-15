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
        private readonly IReadOnlyList<LocalSong> songs;
        private string album;
        private string artist;
        private string genre;
        private string title;

        public TagEditorViewModel(IReadOnlyList<LocalSong> songs)
        {
            if (songs == null)
                throw new ArgumentNullException("songs");

            if (!songs.Any())
                throw new ArgumentException("Tag editor requires at least 1 song");

            this.songs = songs;

            this.Save = ReactiveCommand.CreateAsyncTask(_ => this.SaveTags());
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

                if (songs.Count > 1 && songs.Select(x => x.Album).Distinct().Count() > 1)
                {
                    return string.Empty;
                }

                return songs[0].Album;
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

                if (songs.Count > 1 && songs.Select(x => x.Artist).Distinct().Count() > 1)
                {
                    return string.Empty;
                }

                return songs[0].Artist;
            }

            set { this.artist = value; }
        }

        public ReactiveCommand<object> Cancel { get; private set; }

        public IObservable<Unit> Finished { get; private set; }

        public string Genre
        {
            get
            {
                if (!string.IsNullOrEmpty(this.genre))
                {
                    return this.genre;
                }

                if (songs.Count > 1 && songs.Select(x => x.Genre).Distinct().Count() > 1)
                {
                    return string.Empty;
                }

                return songs[0].Genre;
            }
            set { this.genre = value; }
        }

        public bool IsSingleSong
        {
            get { return this.songs.Count == 1; }
        }

        public ReactiveCommand<Unit> Save { get; private set; }

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