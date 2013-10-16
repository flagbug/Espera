using Espera.Core;
using System;

namespace Espera.View.ViewModels
{
    public sealed class LocalSongViewModel : SongViewModelBase, IEquatable<LocalSongViewModel>
    {
        public LocalSongViewModel(Song model)
            : base(model)
        { }

        public bool Equals(LocalSongViewModel other)
        {
            return this.Model.Equals(other.Model);
        }

        public override int GetHashCode()
        {
            return this.Model.GetHashCode();
        }
    }
}