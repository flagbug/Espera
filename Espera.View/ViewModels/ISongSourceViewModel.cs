using System.Collections.Generic;
using System.ComponentModel;
using System.Reactive;
using Espera.Core.Settings;
using ReactiveUI;

namespace Espera.View.ViewModels
{
    internal interface ISongSourceViewModel : INotifyPropertyChanged
    {
        /// <summary>
        /// Adds the selected songs to the playlist.
        ///
        /// The parameter is an optional integer that specifies the target index of the song in the playlist.
        /// </summary>
        ReactiveCommand<object> AddToPlaylistCommand { get; }

        DefaultPlaybackAction DefaultPlaybackAction { get; }

        ReactiveCommand<Unit> PlayNowCommand { get; }

        string SearchText { get; set; }

        IEnumerable<ISongViewModelBase> SelectedSongs { get; set; }
    }
}