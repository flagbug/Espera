using System;
using System.Collections.Generic;
using System.Reactive;
using ReactiveUI;

namespace Espera.View.ViewModels
{
    internal interface ISongSourceViewModel
    {
        ReactiveUI.Legacy.ReactiveCommand AddToPlaylistCommand { get; }

        ReactiveUI.Legacy.ReactiveCommand PlayNowCommand { get; }

        string SearchText { get; set; }

        IEnumerable<ISongViewModelBase> SelectedSongs { get; set; }

        IObservable<Unit> TimeoutWarning { get; }
    }
}