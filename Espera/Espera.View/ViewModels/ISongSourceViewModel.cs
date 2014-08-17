using System;
using System.Collections.Generic;
using System.Reactive;
using Espera.Core.Settings;
using ReactiveUI;

namespace Espera.View.ViewModels
{
    internal interface ISongSourceViewModel : IReactiveNotifyPropertyChanged
    {
        ReactiveUI.Legacy.ReactiveCommand AddToPlaylistCommand { get; }

        DefaultPlaybackAction DefaultPlaybackAction { get; }

        ReactiveUI.Legacy.ReactiveCommand PlayNowCommand { get; }

        string SearchText { get; set; }

        IEnumerable<ISongViewModelBase> SelectedSongs { get; set; }

        IObservable<Unit> TimeoutWarning { get; }
    }
}