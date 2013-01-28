using Caliburn.Micro;
using Espera.Core.Management;
using Rareform.Extensions;
using Rareform.Reflection;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;

namespace Espera.View.ViewModels
{
    internal sealed class PlaylistViewModel : PropertyChangedBase, IDataErrorInfo
    {
        private readonly Playlist playlist;
        private readonly Func<string, bool> renameRequest;
        private bool editName;
        private string saveName;

        private int? songCount;

        /// <summary>
        /// Initializes a new instance of the <see cref="PlaylistViewModel"/> class.
        /// </summary>
        /// <param name="playlist">The playlist info.</param>
        /// <param name="renameRequest">A function that requests the rename of the playlist. Return true, if the rename is granted, otherwise false.</param>
        public PlaylistViewModel(Playlist playlist, Func<string, bool> renameRequest)
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

                    if (this.EditName)
                    {
                        this.saveName = this.Name;
                    }

                    else if (this.HasErrors())
                    {
                        this.Name = this.saveName;
                        this.saveName = null;
                    }

                    this.NotifyOfPropertyChange(() => this.EditName);
                }
            }
        }

        public string Error
        {
            get { return null; }
        }

        public string Name
        {
            get { return this.playlist.Name; }
            set
            {
                if (this.Name != value)
                {
                    this.playlist.Name = value;
                    this.NotifyOfPropertyChange(() => this.Name);
                }
            }
        }

        public int SongCount
        {
            get
            {
                // We use this to get a value, even if the Songs property hasn't been called
                if (songCount == null)
                {
                    return this.Songs.Count();
                }

                return songCount.Value;
            }

            set
            {
                if (this.songCount != value)
                {
                    this.songCount = value;
                    this.NotifyOfPropertyChange(() => this.SongCount);
                }
            }
        }

        public IEnumerable<PlaylistEntryViewModel> Songs
        {
            get
            {
                var songs = this.playlist
                    .Select(entry => new PlaylistEntryViewModel(entry))
                    .ToList(); // We want a list, so that ReSharper doesn't complain about multiple enumerations

                this.SongCount = songs.Count;

                if (this.playlist.CurrentSongIndex.HasValue)
                {
                    PlaylistEntryViewModel entry = songs[this.playlist.CurrentSongIndex.Value];

                    if (!entry.IsCorrupted)
                    {
                        entry.IsPlaying = true;
                    }

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

                if (columnName == Reflector.GetMemberName(() => this.Name))
                {
                    if (!this.renameRequest(this.Name))
                    {
                        error = "Name already exists.";
                    }

                    else if (String.IsNullOrWhiteSpace(this.Name))
                    {
                        error = "Name cannot be empty or whitespace.";
                    }
                }

                return error;
            }
        }
    }
}