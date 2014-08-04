using Espera.Core;

namespace Espera.View.ViewModels
{
    internal class SoundCloudSongViewModel : SongViewModelBase
    {
        public SoundCloudSongViewModel(SoundCloudSong model)
            : base(model)
        { }

        public string Uploader
        {
            get { return ((SoundCloudSong)this.Model).User.Username; }
        }
    }
}