using System;
using System.Collections.Generic;
using System.Reactive;
using ReactiveUI;

namespace Espera.View.ViewModels
{
    internal interface ISongSourceViewModel
    {
        IReactiveCommand AddToPlaylistCommand { get; }

        IReactiveCommand PlayNowCommand { get; }

        string SearchText { get; set; }

        IEnumerable<ISongViewModelBase> SelectedSongs { get; set; }

        IObservable<Unit> TimeoutWarning { get; }
    }
}