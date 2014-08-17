using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Reactive;
using Espera.Core.Settings;

namespace Espera.View.ViewModels
{
    internal interface ISongSourceViewModel : INotifyPropertyChanged
    {
        ReactiveUI.Legacy.ReactiveCommand AddToPlaylistCommand { get; }

        DefaultPlaybackAction DefaultPlaybackAction { get; }

        ReactiveUI.Legacy.ReactiveCommand PlayNowCommand { get; }

        string SearchText { get; set; }

        IEnumerable<ISongViewModelBase> SelectedSongs { get; set; }

        IObservable<Unit> TimeoutWarning { get; }
    }
}