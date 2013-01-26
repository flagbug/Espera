using ReactiveUI.Xaml;
using System.Collections.Generic;

namespace Espera.View.ViewModels
{
    internal interface ISongSourceViewModel
    {
        IReactiveCommand AddToPlaylistCommand { get; }

        string SearchText { get; set; }

        IEnumerable<SongViewModelBase> SelectedSongs { set; }
    }
}