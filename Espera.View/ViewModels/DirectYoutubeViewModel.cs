using Akavache;
using Espera.Core;
using Espera.Core.Management;
using Espera.Core.Settings;
using Rareform.Validation;
using ReactiveUI;
using Splat;
using System;
using System.Reactive;
using System.Reactive.Linq;
using System.Threading.Tasks;

namespace Espera.View.ViewModels
{
    /// <summary>
    /// This ViewModel is used as proxy to add links from YouTube directly
    /// </summary>
    public class DirectYoutubeViewModel : SongSourceViewModel<YoutubeSongViewModel>
    {
        private readonly IYoutubeSongFinder youtubeSongFinder;

        public DirectYoutubeViewModel(Library library, CoreSettings coreSettings, Guid accessToken, IYoutubeSongFinder youtubeSongFinder = null)
            : base(library, coreSettings, accessToken)
        {
            this.youtubeSongFinder = youtubeSongFinder ?? new YoutubeSongFinder(Locator.Current.GetService<IBlobCache>(BlobCacheKeys.RequestCacheContract));
        }

        public override DefaultPlaybackAction DefaultPlaybackAction
        {
            get { return DefaultPlaybackAction.AddToPlaylist; }
        }

        public override ReactiveCommand<Unit> PlayNowCommand
        {
            get { throw new NotImplementedException(); }
        }

        /// <summary>
        /// Resolves the given YouTube URL and adds the song to the playlist.
        /// 
        /// This method will only execute successfully if the <see
        /// cref="SongSourceViewModel{T}.AddToPlaylistCommand" /> command can execute.
        /// </summary>
        public async Task AddDirectYoutubeUrlToPlaylist(Uri url, int? targetIndex)
        {
            if (url == null)
                Throw.ArgumentNullException(() => url);

            YoutubeSong song = await this.youtubeSongFinder.ResolveYoutubeSongFromUrl(url);

            if (song == null)
            {
                this.Log().Error("Could not register direct YouTube url {0}", url.OriginalString);
                return;
            }

            this.SelectedSongs = new[] { new YoutubeSongViewModel(song, () => { throw new NotImplementedException(); }) };

            await this.AddToPlaylistCommand.ExecuteAsync(targetIndex);
        }
    }
}