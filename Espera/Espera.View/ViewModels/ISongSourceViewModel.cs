using System.Collections.Generic;
using System.Windows.Input;

namespace Espera.View.ViewModels
{
    internal interface ISongSourceViewModel
    {
        ICommand AddToPlaylistCommand { get; }

        string SearchText { get; set; }

        IEnumerable<SongViewModel> SelectedSongs { set; }
    }
}