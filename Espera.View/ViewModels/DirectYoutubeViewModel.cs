using System;
using System.Threading.Tasks;
using Espera.Core;
using Espera.Core.Management;
using Espera.Core.Settings;

namespace Espera.View.ViewModels
{
    /// <summary>
    ///     This ViewModel is used as proxy to add links from YouTube directly
    /// </summary>
    public class DirectYoutubeViewModel : SongSourceViewModel<YoutubeSongViewModel>
    {
        private readonly IYoutubeSongFinder youtubeSongFinder;

        public DirectYoutubeViewModel(Library library, CoreSettings coreSettings, Guid accessToken,
            IYoutubeSongFinder youtubeSongFinder = null)
            : base(library, coreSettings, accessToken)
        {
            this.youtubeSongFinder = youtubeSongFinder ??
                                     new YoutubeSongFinder(
                                         Locator.Current.GetService<IBlobCache>(BlobCacheKeys.RequestCacheContract));
        }

        public override DefaultPlaybackAction DefaultPlaybackAction => DefaultPlaybackAction.AddToPlaylist;

        public override ReactiveCommand<Unit> PlayNowCommand => throw new NotImplementedException();

        /// <summary>
        ///     Resolves the given YouTube URL and adds the song to the playlist.
        ///     This method will only execute successfully if the
        ///     <see
        ///         cref="SongSourceViewModel{T}.AddToPlaylistCommand" />
        ///     command can execute.
        /// </summary>
        public async Task AddDirectYoutubeUrlToPlaylist(Uri url, int? targetIndex)
        {
            if (url == null)
                Throw.ArgumentNullException(() => url);

            var song = await youtubeSongFinder.ResolveYoutubeSongFromUrl(url);

            if (song == null)
            {
                this.Log().Error("Could not register direct YouTube url {0}", url.OriginalString);
                return;
            }

            SelectedSongs = new[] { new YoutubeSongViewModel(song, () => { throw new NotImplementedException(); }) };

            await AddToPlaylistCommand.ExecuteAsync(targetIndex);
        }
    }
}