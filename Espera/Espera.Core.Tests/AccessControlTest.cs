using System;
using System.Reactive.Linq;
using System.Threading.Tasks;
using Espera.Core.Management;
using Espera.Core.Settings;
using ReactiveUI;
using Xunit;

namespace Espera.Core.Tests
{
    public class AccessControlTests
    {
        [Fact]
        public void LockedRemoteControlGivesGuestRightsByDefault()
        {
            var settings = new CoreSettings
            {
                LockRemoteControl = true,
                RemoteControlPassword = "Password"
            };

            var accessControl = new AccessControl(settings);

            Guid token = accessControl.RegisterRemoteAccessToken(new Guid());

            Assert.Throws<AccessException>(() => accessControl.VerifyAccess(token));
        }

        [Fact]
        public void RegisteredShadowVoteUnregistersAutomaticallyWhenEntryVoteCountIsReset()
        {
            var accessControl = SetupVotableAccessControl();
            Guid token = accessControl.RegisterRemoteAccessToken(new Guid());

            PlaylistEntry entry = SetupShadowVotedEntry();

            accessControl.RegisterShadowVote(token, entry);

            entry.ResetVotes();

            Assert.False(entry.IsShadowVoted);
        }

        [Fact]
        public void RegisteredVoteUnregistersAutomaticallyWhenEntryVoteCountIsReset()
        {
            var accessControl = SetupVotableAccessControl(2);
            Guid token = accessControl.RegisterRemoteAccessToken(new Guid());

            var entry = new PlaylistEntry(0, Helpers.SetupSongMock());
            entry.Vote();

            var votes = accessControl.ObserveRemainingVotes(token).CreateCollection();
            accessControl.RegisterVote(token, entry);

            entry.ResetVotes();

            Assert.Equal(new int?[] { 2, 1, 2 }, votes);
        }

        [Fact]
        public void RegisterVoteForSameEntryThrowsInvalidOperationException()
        {
            var accessControl = SetupVotableAccessControl(2);
            Guid token = accessControl.RegisterRemoteAccessToken(new Guid());

            var entry = SetupVotedEntry();

            accessControl.RegisterVote(token, entry);
            entry.Vote();
            Assert.Throws<InvalidOperationException>(() => accessControl.RegisterVote(token, entry));
        }

        [Fact]
        public void UnknownAccessTokenThrowsArgumentException()
        {
            var settings = new CoreSettings();

            var accessControl = new AccessControl(settings);

            Assert.Throws<ArgumentException>(() => accessControl.VerifyAccess(new Guid()));
        }

        [Fact]
        public void UnlockedRemoteControlGivesAdminRightsByDefault()
        {
            var settings = new CoreSettings
            {
                LockRemoteControl = false
            };

            var accessControl = new AccessControl(settings);

            Guid token = accessControl.RegisterRemoteAccessToken(new Guid());

            accessControl.VerifyAccess(token);
        }

        [Fact]
        public void UpdatesRemoteAccessWhenLockRemoteSettingChanges()
        {
            var settings = new CoreSettings
            {
                LockRemoteControl = false
            };

            var accessControl = new AccessControl(settings);

            Guid remoteToken = accessControl.RegisterRemoteAccessToken(new Guid());
            Guid adminToken = accessControl.RegisterLocalAccessToken();

            var permissions = accessControl.ObserveAccessPermission(remoteToken).CreateCollection();

            settings.LockRemoteControl = true;
            accessControl.SetRemotePassword(adminToken, "password");

            settings.LockRemoteControl = false;
            settings.LockRemoteControl = true;

            Assert.Equal(new[] { AccessPermission.Admin, AccessPermission.Guest, AccessPermission.Admin, AccessPermission.Guest }, permissions);
        }

        private static PlaylistEntry SetupShadowVotedEntry()
        {
            var entry = new PlaylistEntry(0, Helpers.SetupSongMock());
            entry.ShadowVote();

            return entry;
        }

        private static AccessControl SetupVotableAccessControl(int maxVotes = 3)
        {
            var settings = new CoreSettings
            {
                EnableGuestSystem = true,
                LockRemoteControl = true,
                RemoteControlPassword = "Password",
                MaxVoteCount = maxVotes
            };

            return new AccessControl(settings);
        }

        private static PlaylistEntry SetupVotedEntry()
        {
            var entry = new PlaylistEntry(0, Helpers.SetupSongMock());
            entry.Vote();

            return entry;
        }

        public class TheDowngradeLocalAccessMethod
        {
            [Fact]
            public void ThrowsInvalidOperationExceptionIfLocalPasswordIsNotSet()
            {
                var settings = new CoreSettings();

                var accessControl = new AccessControl(settings);

                Guid token = accessControl.RegisterLocalAccessToken();
                Assert.Throws<InvalidOperationException>(() => accessControl.DowngradeLocalAccess(token));
            }
        }

        public class TheIsVoteRegisteredMethod
        {
            [Fact]
            public void SmokeTest()
            {
                var settings = new CoreSettings { EnableGuestSystem = true };
                var accessControl = new AccessControl(settings);
                Guid token = accessControl.RegisterRemoteAccessToken(new Guid());

                var entry = SetupVotedEntry();
                accessControl.RegisterVote(token, entry);

                Assert.True(accessControl.IsVoteRegistered(token, entry));
                Assert.False(accessControl.IsVoteRegistered(token, new PlaylistEntry(0, Helpers.SetupSongMock())));
            }
        }

        public class TheObserveAccessPermissionMethod
        {
            [Fact]
            public void SmokeTest()
            {
                var settings = new CoreSettings
                {
                    LockRemoteControl = false
                };

                var accessControl = new AccessControl(settings);

                Guid token = accessControl.RegisterLocalAccessToken();

                var permissions = accessControl.ObserveAccessPermission(token).CreateCollection();

                accessControl.SetLocalPassword(token, "password");
                accessControl.DowngradeLocalAccess(token);
                accessControl.UpgradeLocalAccess(token, "password");

                Assert.Equal(new[] { AccessPermission.Admin, AccessPermission.Guest, AccessPermission.Admin }, permissions);
            }

            [Fact]
            public void ThrowsArgumentExceptionIfGuidIsGarbage()
            {
                var settings = new CoreSettings { LockRemoteControl = false };

                var accessControl = new AccessControl(settings);

                Assert.Throws<ArgumentException>(() => accessControl.ObserveAccessPermission(new Guid()));
            }
        }

        public class TheObserveRemainingVotesMethod
        {
            [Fact]
            public async Task ReturnsCurrentValueImmediately()
            {
                var accessControl = SetupVotableAccessControl(3);
                Guid token = accessControl.RegisterRemoteAccessToken(new Guid());

                Assert.Equal(3, await accessControl.ObserveRemainingVotes(token).FirstAsync());
            }

            [Fact]
            public void SmokeTest()
            {
                var accessControl = SetupVotableAccessControl(2);
                Guid token = accessControl.RegisterRemoteAccessToken(new Guid());

                var votes = accessControl.ObserveRemainingVotes(token).CreateCollection();

                accessControl.RegisterVote(token, SetupVotedEntry());
                accessControl.RegisterVote(token, SetupVotedEntry());

                Assert.Equal(new int?[] { 2, 1, 0 }, votes);
            }
        }

        public class TheRegisterRemoteAccessTolenMethod
        {
            [Fact]
            public void WithExistingDeviceIdIsRecognized()
            {
                var accessControl = new AccessControl(new CoreSettings());

                Guid accessToken = accessControl.RegisterRemoteAccessToken(new Guid());

                Guid existingAccessToken = accessControl.RegisterRemoteAccessToken(new Guid());

                Assert.Equal(accessToken, existingAccessToken);
            }
        }

        public class TheRegisterShadowVoteMethod
        {
            [Fact]
            public void EntryMustBeShadowVoted()
            {
                var accessControl = SetupVotableAccessControl();
                Guid token = accessControl.RegisterRemoteAccessToken(new Guid());

                var entry = new PlaylistEntry(0, Helpers.SetupSongMock());

                Assert.Throws<ArgumentException>(() => accessControl.RegisterShadowVote(token, entry));
            }

            [Fact]
            public async Task SmokeTest()
            {
                var accessControl = SetupVotableAccessControl(3);

                Guid token = accessControl.RegisterRemoteAccessToken(new Guid());

                var entry = SetupShadowVotedEntry();

                accessControl.RegisterShadowVote(token, entry);

                Assert.True(entry.IsShadowVoted);
                Assert.Equal(2, await accessControl.ObserveRemainingVotes(token).FirstAsync());
            }

            [Fact]
            public void ThrowsInvalidOperationExceptionIfGuestSystemIsDisabled()
            {
                var settings = new CoreSettings
                {
                    EnableGuestSystem = false,
                    LockRemoteControl = true
                };

                var accessControl = new AccessControl(settings);
                Guid localToken = accessControl.RegisterLocalAccessToken();
                accessControl.SetRemotePassword(localToken, "Password");

                Guid token = accessControl.RegisterRemoteAccessToken(new Guid());

                Assert.Throws<InvalidOperationException>(() => accessControl.RegisterShadowVote(token, SetupShadowVotedEntry()));
            }

            [Fact]
            public void ThrowsInvalidOperationExceptionWithoutGuestAccessToken()
            {
                var accessControl = SetupVotableAccessControl();

                Guid token = accessControl.RegisterLocalAccessToken();

                Assert.Throws<InvalidOperationException>(() => accessControl.RegisterShadowVote(token, SetupShadowVotedEntry()));
            }

            [Fact]
            public void WithoutVotesLeftThrowsInvalidOperationException()
            {
                var accessControl = SetupVotableAccessControl(0);
                Guid token = accessControl.RegisterRemoteAccessToken(new Guid());

                Assert.Throws<InvalidOperationException>(() => accessControl.RegisterShadowVote(token, SetupShadowVotedEntry()));
            }
        }

        public class TheRegisterVoteMethod
        {
            [Fact]
            public void CanVoteOnShadowVotedEntry()
            {
                var accessControl = SetupVotableAccessControl();

                Guid token = accessControl.RegisterRemoteAccessToken(new Guid());

                PlaylistEntry entry = SetupShadowVotedEntry();

                accessControl.RegisterShadowVote(token, entry);
                accessControl.RegisterVote(token, entry);
            }

            [Fact]
            public async Task SmokeTest()
            {
                AccessControl accessControl = SetupVotableAccessControl(2);

                Guid token = accessControl.RegisterRemoteAccessToken(new Guid());

                accessControl.RegisterVote(token, SetupVotedEntry());

                Assert.Equal(1, await accessControl.ObserveRemainingVotes(token).FirstAsync());
            }

            [Fact]
            public void ThrowsInvalidOperationExceptionIfGuestSystemIsDisabled()
            {
                var settings = new CoreSettings { EnableGuestSystem = false };

                var accessControl = new AccessControl(settings);
                Guid token = accessControl.RegisterRemoteAccessToken(new Guid());

                Assert.Throws<InvalidOperationException>(() => accessControl.RegisterVote(token, SetupVotedEntry()));
            }

            [Fact]
            public void WithoutVotesLeftThrowsInvalidOperationException()
            {
                var settings = new CoreSettings
                {
                    EnableGuestSystem = true,
                    MaxVoteCount = 0
                };
                var accessControl = new AccessControl(settings);
                Guid token = accessControl.RegisterRemoteAccessToken(new Guid());

                Assert.Throws<InvalidOperationException>(() => accessControl.RegisterVote(token, new PlaylistEntry(0, Helpers.SetupSongMock())));
            }
        }

        public class TheSetLocalPasswordMethod
        {
            [Fact]
            public void ThrowsAccessExceptionOnGuestToken()
            {
                var settings = new CoreSettings();

                var accessControl = new AccessControl(settings);

                Guid token = accessControl.RegisterLocalAccessToken();

                accessControl.SetLocalPassword(token, "password123");
                accessControl.DowngradeLocalAccess(token);

                Assert.Throws<AccessException>(() => accessControl.SetLocalPassword(token, "lololol"));
            }

            [Fact]
            public void ValidatesPassword()
            {
                var settings = new CoreSettings();

                var accessControl = new AccessControl(settings);

                Guid token = accessControl.RegisterLocalAccessToken();

                Assert.Throws<ArgumentException>(() => accessControl.SetLocalPassword(token, ""));
                Assert.Throws<ArgumentException>(() => accessControl.SetLocalPassword(token, " "));
                Assert.Throws<ArgumentNullException>(() => accessControl.SetLocalPassword(token, null));
            }

            [Fact]
            public void WithRemoteTokenThrowsArgumentException()
            {
                var settings = new CoreSettings();
                var accessControl = new AccessControl(settings);

                Guid token = accessControl.RegisterRemoteAccessToken(new Guid());

                Assert.Throws<ArgumentException>(() => accessControl.SetLocalPassword(token, "password123"));
            }
        }

        public class TheSetRemotePasswordMethod
        {
            [Fact]
            public void UpdatesOnlyRemoteAccessPermissions()
            {
                var settings = new CoreSettings
                {
                    LockRemoteControl = true,
                    RemoteControlPassword = null
                };

                var accessControl = new AccessControl(settings);

                Guid localToken = accessControl.RegisterLocalAccessToken();

                Guid remoteToken = accessControl.RegisterRemoteAccessToken(new Guid());
                var remotePermissions = accessControl.ObserveAccessPermission(remoteToken).CreateCollection();

                accessControl.SetRemotePassword(localToken, "password");

                Assert.Equal(AccessPermission.Admin, accessControl.ObserveAccessPermission(localToken).FirstAsync().Wait());
                Assert.Equal(new[] { AccessPermission.Admin, AccessPermission.Guest }, remotePermissions);
            }
        }

        public class TheUpgradeLocalAccessMethod
        {
            [Fact]
            public void ThrowsArgumentExceptionOnBogusAccessToken()
            {
                var settings = new CoreSettings();
                var accessControl = new AccessControl(settings);

                Guid token = accessControl.RegisterLocalAccessToken();
                accessControl.SetLocalPassword(token, "password123");

                Assert.Throws<ArgumentException>(() => accessControl.UpgradeLocalAccess(new Guid(), "password123"));
            }

            [Fact]
            public void ThrowsWrongPasswordExceptionOnWrongPassword()
            {
                var settings = new CoreSettings();
                var accessControl = new AccessControl(settings);

                Guid token = accessControl.RegisterLocalAccessToken();
                accessControl.SetLocalPassword(token, "password123");

                Assert.Throws<WrongPasswordException>(() => accessControl.UpgradeLocalAccess(token, "lolol"));
            }

            [Fact]
            public void UpgradesToAdmin()
            {
                var settings = new CoreSettings();
                var accessControl = new AccessControl(settings);

                Guid token = accessControl.RegisterLocalAccessToken();

                accessControl.SetLocalPassword(token, "password123");
                accessControl.UpgradeLocalAccess(token, "password123");

                accessControl.VerifyAccess(token);
            }

            [Fact]
            public void WithRemoteAccessTokenThrowsArgumentException()
            {
                var settings = new CoreSettings();
                var accessControl = new AccessControl(settings);

                Guid token = accessControl.RegisterRemoteAccessToken(new Guid());

                Assert.Throws<ArgumentException>(() => accessControl.UpgradeLocalAccess(token, "password123"));
            }
        }

        public class TheUpgradeRemoteAccessMethod
        {
            [Fact]
            public void ThrowsWrongPasswordExceptionOnWrongPassword()
            {
                var settings = new CoreSettings
                {
                    RemoteControlPassword = "password123"
                };

                var accessControl = new AccessControl(settings);

                Guid token = accessControl.RegisterRemoteAccessToken(new Guid());

                Assert.Throws<WrongPasswordException>(() => accessControl.UpgradeRemoteAccess(token, "lolol"));
            }

            [Fact]
            public void UpgradesToAdmin()
            {
                var settings = new CoreSettings
                {
                    LockRemoteControl = true,
                    RemoteControlPassword = "password123"
                };

                var accessControl = new AccessControl(settings);

                Guid token = accessControl.RegisterRemoteAccessToken(new Guid());

                accessControl.UpgradeRemoteAccess(token, "password123");

                accessControl.VerifyAccess(token);
            }

            [Fact]
            public void WithBogusAccessTokenThrowsArgumentException()
            {
                var settings = new CoreSettings
                {
                    RemoteControlPassword = "password123"
                };

                var accessControl = new AccessControl(settings);

                Assert.Throws<ArgumentException>(() => accessControl.UpgradeRemoteAccess(new Guid(), "password123"));
            }

            [Fact]
            public void WithLocalAccessTokenThrowsArgumentException()
            {
                var settings = new CoreSettings
                {
                    RemoteControlPassword = "password123"
                };

                var accessControl = new AccessControl(settings);

                Guid token = accessControl.RegisterLocalAccessToken();

                Assert.Throws<ArgumentException>(() => accessControl.UpgradeRemoteAccess(token, "password123"));
            }
        }

        public class TheVerifyAccessMethod
        {
            [Fact]
            public void LocalSmokeTest()
            {
                var settings = new CoreSettings();

                var accessControl = new AccessControl(settings);

                Guid token = accessControl.RegisterLocalAccessToken();

                accessControl.VerifyAccess(token, false);

                accessControl.SetLocalPassword(token, "password123");
                accessControl.DowngradeLocalAccess(token);

                Assert.Throws<AccessException>(() => accessControl.VerifyAccess(token));
            }
        }

        public class TheVerifyVotingPreconditionsMethod
        {
            [Fact]
            public void LocalAccessTokenIgnoresVoteCount()
            {
                var settings = new CoreSettings { MaxVoteCount = 0 };

                var accessControl = new AccessControl(settings);

                Guid accessToken = accessControl.RegisterLocalAccessToken();

                accessControl.VerifyVotingPreconditions(accessToken);
            }

            [Fact]
            public void ThrowsInvalidOperationExceptionIfGuestSystemIsDisabled()
            {
                var settings = new CoreSettings { EnableGuestSystem = false };

                var accessControl = new AccessControl(settings);

                Guid accessToken = accessControl.RegisterLocalAccessToken();

                Assert.Throws<InvalidOperationException>(() => accessControl.VerifyVotingPreconditions(accessToken));
            }
        }
    }
}