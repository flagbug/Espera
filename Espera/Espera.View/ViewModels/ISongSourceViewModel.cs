using ReactiveUI;
using System.Collections.Generic;

namespace Espera.View.ViewModels
{
    internal interface ISongSourceViewModel
    {
        IReactiveCommand AddToPlaylistCommand { get; }

        IReactiveCommand PlayNowCommand { get; }

        string SearchText { get; set; }

        IEnumerable<SongViewModelBase> SelectedSongs { set; }
    }
}