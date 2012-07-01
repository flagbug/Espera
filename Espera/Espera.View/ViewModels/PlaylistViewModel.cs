using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using Espera.Core.Library;
using Rareform.Patterns.MVVM;
using Rareform.Reflection;

namespace Espera.View.ViewModels
{
    internal class PlaylistViewModel : ViewModelBase<PlaylistViewModel>, IDataErrorInfo
    {
        private readonly PlaylistInfo playlist;
        private bool editName;
        private readonly Func<string, bool> renameRequest;

        public PlaylistViewModel(PlaylistInfo playlist, Func<string, bool> renameRequest)
        {
            this.playlist = playlist;
            this.renameRequest = renameRequest;
        }

        public bool EditName
        {
            get { return this.editName; }
            set
            {
                if (this.EditName != value)
                {
                    this.editName = value;
                    this.OnPropertyChanged(vm => vm.EditName);
                }
            }
        }

        public string Name
        {
            get { return this.playlist.Name; }
            set { this.playlist.Name = value; }
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

        public string this[string columnName]
        {
            get
            {
                string error = null;

                if (columnName == Reflector.GetMemberName(() => this.Name) && !this.renameRequest(this.Name))
                {
                    error = "Name already exists.";
                }

                return error;
            }
        }

        public string Error
        {
            get { return null; }
        }
    }
}