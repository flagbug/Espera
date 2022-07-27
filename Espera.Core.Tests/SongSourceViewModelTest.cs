using System;
using System.Reactive;
using System.Reactive.Linq;
using Espera.Core.Management;
using Espera.Core.Settings;
using Espera.Core.Tests;
using Espera.View.ViewModels;
using NSubstitute;
using ReactiveUI;
using Xunit;

namespace Espera.View.Tests
{
    public class SongSourceViewModelTest
    {
        public class TheAddToPlaylistCommand
        {
            [Fact]
            public void CanExecuteWhenAdmin()
            {
                var song = Helpers.SetupSongMock();

                using (var library = new LibraryBuilder().WithPlaylist("ThePlaylist").Build())
                {
                    var accessToken = library.LocalAccessControl.RegisterLocalAccessToken();

                    var vm = new SongSourceViewModelMock(library, accessToken);
                    var songVm = Substitute.For<ISongViewModelBase>();
                    songVm.Model.Returns(song);
                    vm.SelectedSongs = new[] { songVm };

                    Assert.True(vm.AddToPlaylistCommand.CanExecute(null));
                }
            }

            [Fact]
            public void CanExecuteWhenGuestWithDisabledPlaylistLock()
            {
                var settings = new CoreSettings
                {
                    LockPlaylist = false
                };

                var song = Helpers.SetupSongMock();

                using (var library = new LibraryBuilder().WithPlaylist("ThePlaylist").Build())
                {
                    var accessToken = library.LocalAccessControl.RegisterLocalAccessToken();
                    library.LocalAccessControl.SetLocalPassword(accessToken, "Password");
                    library.LocalAccessControl.DowngradeLocalAccess(accessToken);

                    var vm = new SongSourceViewModelMock(library, accessToken, settings);
                    var songVm = Substitute.For<ISongViewModelBase>();
                    songVm.Model.Returns(song);
                    vm.SelectedSongs = new[] { songVm };

                    Assert.True(vm.AddToPlaylistCommand.CanExecute(null));
                }
            }

            [Fact]
            public void CantExecuteWhenGuest()
            {
                var song = Helpers.SetupSongMock();

                using (var library = new LibraryBuilder().WithPlaylist("ThePlaylist").Build())
                {
                    var accessToken = library.LocalAccessControl.RegisterLocalAccessToken();
                    library.LocalAccessControl.SetLocalPassword(accessToken, "Password");
                    library.LocalAccessControl.DowngradeLocalAccess(accessToken);

                    var vm = new SongSourceViewModelMock(library, accessToken);
                    var songVm = Substitute.For<ISongViewModelBase>();
                    songVm.Model.Returns(song);
                    vm.SelectedSongs = new[] { songVm };

                    Assert.False(vm.AddToPlaylistCommand.CanExecute(null));
                }
            }

            private class SongSourceViewModelMock : SongSourceViewModel<ISongViewModelBase>
            {
                public SongSourceViewModelMock(Library library, Guid accessToken, CoreSettings settings = null)
                    : base(library, settings ?? new CoreSettings(), accessToken)
                {
                    PlayNowCommand = ReactiveCommand.CreateAsyncObservable(_ => Observable.Return(Unit.Default));
                }

                public override DefaultPlaybackAction DefaultPlaybackAction => DefaultPlaybackAction.AddToPlaylist;

                public override ReactiveCommand<Unit> PlayNowCommand { get; }
            }
        }
    }
}