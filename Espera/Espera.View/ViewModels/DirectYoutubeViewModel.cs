using System;
using System.Threading.Tasks;
using Espera.Core;
using Espera.Core.Management;
using ReactiveUI;

namespace Espera.View.ViewModels
{
    /// <summary>
    /// This ViewModel is used as proxy to add links from YouTube directly
    /// </summary>
    public class DirectYoutubeViewModel : SongSourceViewModel<YoutubeSongViewModel>
    {
        private readonly IYoutubeSongFinder youtubeSongFinder;

        public DirectYoutubeViewModel(Library library, Guid accessToken, IYoutubeSongFinder youtubeSongFinder = null)
            : base(library, accessToken)
        {
            this.youtubeSongFinder = youtubeSongFinder ?? new YoutubeSongFinder();
        }

        public override IReactiveCommand PlayNowCommand
        {
            get { throw new NotImplementedException(); }
        }

        /// <summary>
        /// Resolves the given YouTube URL and adds the song to the playlist.
        /// </summary>
        public async Task AddDirectYoutubeUrlToPlaylist(Uri url)
        {
            YoutubeSong song = await this.youtubeSongFinder.ResolveYoutubeSongFromUrl(url);

            if (song == null)
            {
                this.Log().Error("Could not register direct YouTube url {0}", url.OriginalString);
                return;
            }

            this.SelectedSongs = new[] { new YoutubeSongViewModel(song, () => { throw new NotImplementedException(); }) };

            this.AddToPlaylistCommand.Execute(null);
        }
    }
}