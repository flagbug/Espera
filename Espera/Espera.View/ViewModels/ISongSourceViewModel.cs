using System;
using System.Collections.Generic;
using System.Reactive;
using Espera.Core.Settings;
using ReactiveUI;

namespace Espera.View.ViewModels
{
    internal interface ISongSourceViewModel : IReactiveNotifyPropertyChanged
    {
        IReactiveCommand AddToPlaylistCommand { get; }

        DefaultPlaybackAction DefaultPlaybackAction { get; }

        IReactiveCommand PlayNowCommand { get; }

        string SearchText { get; set; }

        IEnumerable<ISongViewModelBase> SelectedSongs { get; set; }

        IObservable<Unit> TimeoutWarning { get; }
    }
}