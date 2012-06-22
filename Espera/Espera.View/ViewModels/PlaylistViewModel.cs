using System.Collections.Generic;
using System.Linq;
using Espera.Core.Library;
using Rareform.Patterns.MVVM;

namespace Espera.View.ViewModels
{
    internal class PlaylistViewModel : ViewModelBase<PlaylistViewModel>
    {
        private readonly PlaylistInfo playlist;

        public PlaylistViewModel(PlaylistInfo playlist)
        {
            this.playlist = playlist;
        }

        public string Name
        {
            get { return this.playlist.Name; }
        }

        public IEnumerable<PlaylistEntryViewModel> Songs
        {
            get
            {
                var songs = this.playlist.Songs
                    .Select((song, index) => new PlaylistEntryViewModel(song, index))
                    .ToList(); // We want a list, so that ReSharper doesn't complain about multiple enumerations

                if (this.playlist.CurrentSongIndex.HasValue)
                {
                    songs[this.playlist.CurrentSongIndex.Value].IsPlaying = true;

                    // If there are more than 5 songs from the beginning of the playlist to the current played song,
                    // skip all, but 5 songs to the position of the currently played song
                    if (songs.TakeWhile(song => !song.IsPlaying).Count() > 5)
                    {
                        songs = songs.Skip(this.playlist.CurrentSongIndex.Value - 5).ToList();
                    }

                    foreach (var model in songs.TakeWhile(song => !song.IsPlaying))
                    {
                        model.IsInactive = true;
                    }
                }

                return songs;
            }
        }
    }
}