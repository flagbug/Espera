using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Reactive;

namespace Espera.View.ViewModels
{
    internal interface ISongSourceViewModel
    {
        IReactiveCommand AddToPlaylistCommand { get; }

        IReactiveCommand PlayNowCommand { get; }

        string SearchText { get; set; }

        IEnumerable<SongViewModelBase> SelectedSongs { set; }

        IObservable<Unit> TimeoutWarning { get; }
    }
}