using System;
using Espera.Core;
using ReactiveUI;

namespace Espera.View.ViewModels
{
    /// <summary>
    /// This interface is used to avoid the expensive inheritance of <see cref="ReactiveObject" />
    /// in <see cref="LocalSongViewModel" />.
    /// </summary>
    public interface ISongViewModelBase
    {
        string Album { get; }

        string Artist { get; }

        TimeSpan Duration { get; }

        string FormattedDuration { get; }

        string Genre { get; }

        Song Model { get; }

        string Path { get; }

        string Title { get; }

        int TrackNumber { get; }
    }
}