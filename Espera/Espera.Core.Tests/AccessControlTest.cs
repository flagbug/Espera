using Espera.Core.Management;
using Espera.Core.Settings;
using ReactiveUI;
using System;
using System.Reactive.Linq;
using System.Threading.Tasks;
using Xunit;

namespace Espera.Core.Tests
{
    public class AccessControlTests
    {
        [Fact]
        public void DowngradeLocalAccessThrowsInvalidOperationExceptionIfLocalPasswordIsNotSet()
        {
            var settings = new CoreSettings();

            var accessControl = new AccessControl(settings);

            Guid token = accessControl.RegisterLocalAccessToken();
            Assert.Throws<InvalidOperationException>(() => accessControl.DowngradeLocalAccess(token));
        }

        [Fact]
        public void IsVoteRegisteredSmokeTest()
        {
            var settings = new CoreSettings();
            var accessControl = new AccessControl(settings);
            Guid token = accessControl.RegisterRemoteAccessToken();

            var entry = SetupVotedEntry();
            accessControl.RegisterVote(token, entry);

            Assert.True(accessControl.IsVoteRegistered(token, entry));
            Assert.False(accessControl.IsVoteRegistered(token, new PlaylistEntry(0, Helpers.SetupSongMock())));
        }

        [Fact]
        public void LockedRemoteControlGivesGuestRightsByDefault()
        {
            var settings = new CoreSettings
            {
                LockRemoteControl = true,
                RemoteControlPassword = "Password"
            };

            var accessControl = new AccessControl(settings);

            Guid token = accessControl.RegisterRemoteAccessToken();

            Assert.Throws<AccessException>(() => accessControl.VerifyAccess(token));
        }

        [Fact]
        public void ObserveAccessPermissionSmokeTest()
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
        public void ObserveAccessPermissionThrowsArgumentExceptionIfGuidIsGarbage()
        {
            var settings = new CoreSettings { LockRemoteControl = false };

            var accessControl = new AccessControl(settings);

            Assert.Throws<ArgumentException>(() => accessControl.ObserveAccessPermission(Guid.NewGuid()));
        }

        [Fact]
        public void ObserveRemainingVoteSmokeTest()
        {
            var settings = new CoreSettings();
            var accessControl = new AccessControl(settings);
            Guid token = accessControl.RegisterRemoteAccessToken();

            var votes = accessControl.ObserveRemainingVotes(token).CreateCollection();

            accessControl.RegisterVote(token, SetupVotedEntry());
            accessControl.RegisterVote(token, SetupVotedEntry());

            Assert.Equal(new[] { 2, 1, 0 }, votes);
        }

        [Fact]
        public async Task ObserveRemainingVotesReturnsCurrentValueImmediately()
        {
            var settings = new CoreSettings();
            var accessControl = new AccessControl(settings);
            Guid token = accessControl.RegisterRemoteAccessToken();

            Assert.Equal(settings.MaxVoteCount, await accessControl.ObserveRemainingVotes(token).FirstAsync());
        }

        [Fact]
        public void RegisteredVoteUnregistersAutomaticallyWhenEntryvoteCountIsReset()
        {
            var settings = new CoreSettings { MaxVoteCount = 2 };
            var accessControl = new AccessControl(settings);
            Guid token = accessControl.RegisterRemoteAccessToken();

            var entry = new PlaylistEntry(0, Helpers.SetupSongMock());
            entry.Vote();

            var votes = accessControl.ObserveRemainingVotes(token).CreateCollection();
            accessControl.RegisterVote(token, entry);

            entry.ResetVotes();

            Assert.Equal(new[] { 2, 1, 2 }, votes);
        }

        [Fact]
        public void RegisterVoteForSameEntryThrowsInvalidOperationException()
        {
            var settings = new CoreSettings { MaxVoteCount = 2 };
            var accessControl = new AccessControl(settings);
            Guid token = accessControl.RegisterRemoteAccessToken();

            var entry = SetupVotedEntry();

            accessControl.RegisterVote(token, entry);
            entry.Vote();
            Assert.Throws<InvalidOperationException>(() => accessControl.RegisterVote(token, entry));
        }

        [Fact]
        public async Task RegisterVoteSmokeTest()
        {
            var settings = new CoreSettings { MaxVoteCount = 2 };
            var accessControl = new AccessControl(settings);
            Guid token = accessControl.RegisterRemoteAccessToken();

            accessControl.RegisterVote(token, SetupVotedEntry());

            Assert.Equal(settings.MaxVoteCount - 1, await accessControl.ObserveRemainingVotes(token).FirstAsync());
        }

        [Fact]
        public void RegisterVoteWithoutVotesLeftThrowsInvalidOperationException()
        {
            var settings = new CoreSettings { MaxVoteCount = 0 };
            var accessControl = new AccessControl(settings);
            Guid token = accessControl.RegisterRemoteAccessToken();

            Assert.Throws<InvalidOperationException>(() => accessControl.RegisterVote(token, new PlaylistEntry(0, Helpers.SetupSongMock())));
        }

        [Fact]
        public void SetLocalPasswordThrowsAccessExceptionOnGuestToken()
        {
            var settings = new CoreSettings();

            var accessControl = new AccessControl(settings);

            Guid token = accessControl.RegisterLocalAccessToken();

            accessControl.SetLocalPassword(token, "password123");
            accessControl.DowngradeLocalAccess(token);

            Assert.Throws<AccessException>(() => accessControl.SetLocalPassword(token, "lololol"));
        }

        [Fact]
        public void SetLocalPasswordValidatesPassword()
        {
            var settings = new CoreSettings();

            var accessControl = new AccessControl(settings);

            Guid token = accessControl.RegisterLocalAccessToken();

            Assert.Throws<ArgumentException>(() => accessControl.SetLocalPassword(token, ""));
            Assert.Throws<ArgumentException>(() => accessControl.SetLocalPassword(token, " "));
            Assert.Throws<ArgumentNullException>(() => accessControl.SetLocalPassword(token, null));
        }

        [Fact]
        public void SetLocalPasswordWithRemoteTokenThrowsArgumentException()
        {
            var settings = new CoreSettings();
            var accessControl = new AccessControl(settings);

            Guid token = accessControl.RegisterRemoteAccessToken();

            Assert.Throws<ArgumentException>(() => accessControl.SetLocalPassword(token, "password123"));
        }

        [Fact]
        public void SetRemoteControlPasswordOnlyUpdatesRemoteAccessPermissions()
        {
            var settings = new CoreSettings
            {
                LockRemoteControl = true,
                RemoteControlPassword = null
            };

            var accessControl = new AccessControl(settings);

            Guid localToken = accessControl.RegisterLocalAccessToken();

            Guid remoteToken = accessControl.RegisterRemoteAccessToken();
            var remotePermissions = accessControl.ObserveAccessPermission(remoteToken).CreateCollection();

            accessControl.SetRemotePassword(localToken, "password");

            Assert.Equal(AccessPermission.Admin, accessControl.ObserveAccessPermission(localToken).FirstAsync().Wait());
            Assert.Equal(new[] { AccessPermission.Admin, AccessPermission.Guest }, remotePermissions);
        }

        [Fact]
        public void UnknownAccessTokenThrowsArgumentException()
        {
            var settings = new CoreSettings();

            var accessControl = new AccessControl(settings);

            Assert.Throws<ArgumentException>(() => accessControl.VerifyAccess(Guid.NewGuid()));
        }

        [Fact]
        public void UnlockedRemoteControlGivesAdminRightsByDefault()
        {
            var settings = new CoreSettings
            {
                LockRemoteControl = false
            };

            var accessControl = new AccessControl(settings);

            Guid token = accessControl.RegisterRemoteAccessToken();

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

            Guid remoteToken = accessControl.RegisterRemoteAccessToken();
            Guid adminToken = accessControl.RegisterLocalAccessToken();

            var permissions = accessControl.ObserveAccessPermission(remoteToken).CreateCollection();

            settings.LockRemoteControl = true;
            accessControl.SetRemotePassword(adminToken, "password");

            settings.LockRemoteControl = false;
            settings.LockRemoteControl = true;

            Assert.Equal(new[] { AccessPermission.Admin, AccessPermission.Guest, AccessPermission.Admin, AccessPermission.Guest }, permissions);
        }

        [Fact]
        public void UpgradeLocalAccessThrowsArgumentExceptionOnBogusAccessToken()
        {
            var settings = new CoreSettings();
            var accessControl = new AccessControl(settings);

            Guid token = accessControl.RegisterLocalAccessToken();
            accessControl.SetLocalPassword(token, "password123");

            Assert.Throws<ArgumentException>(() => accessControl.UpgradeLocalAccess(Guid.NewGuid(), "password123"));
        }

        [Fact]
        public void UpgradeLocalAccessThrowsWrongPasswordExceptionOnWrongPassword()
        {
            var settings = new CoreSettings();
            var accessControl = new AccessControl(settings);

            Guid token = accessControl.RegisterLocalAccessToken();
            accessControl.SetLocalPassword(token, "password123");

            Assert.Throws<WrongPasswordException>(() => accessControl.UpgradeLocalAccess(token, "lolol"));
        }

        [Fact]
        public void UpgradeLocalAccessUpgradesToAdmin()
        {
            var settings = new CoreSettings();
            var accessControl = new AccessControl(settings);

            Guid token = accessControl.RegisterLocalAccessToken();

            accessControl.SetLocalPassword(token, "password123");
            accessControl.UpgradeLocalAccess(token, "password123");

            accessControl.VerifyAccess(token);
        }

        [Fact]
        public void UpgradeLocalAccessWithRemoteAccessTokenThrowsArgumentException()
        {
            var settings = new CoreSettings();
            var accessControl = new AccessControl(settings);

            Guid token = accessControl.RegisterRemoteAccessToken();

            Assert.Throws<ArgumentException>(() => accessControl.UpgradeLocalAccess(token, "password123"));
        }

        [Fact]
        public void UpgradeRemoteAccessThrowsWrongPasswordExceptionOnWrongPassword()
        {
            var settings = new CoreSettings
            {
                RemoteControlPassword = "password123"
            };

            var accessControl = new AccessControl(settings);

            Guid token = accessControl.RegisterRemoteAccessToken();

            Assert.Throws<WrongPasswordException>(() => accessControl.UpgradeRemoteAccess(token, "lolol"));
        }

        [Fact]
        public void UpgradeRemoteAccessUpgradesToAdmin()
        {
            var settings = new CoreSettings
            {
                LockRemoteControl = true,
                RemoteControlPassword = "password123"
            };

            var accessControl = new AccessControl(settings);

            Guid token = accessControl.RegisterRemoteAccessToken();

            accessControl.UpgradeRemoteAccess(token, "password123");

            accessControl.VerifyAccess(token);
        }

        [Fact]
        public void UpgradeRemoteAccessWithBogusAccessTokenThrowsArgumentException()
        {
            var settings = new CoreSettings
            {
                RemoteControlPassword = "password123"
            };

            var accessControl = new AccessControl(settings);

            Assert.Throws<ArgumentException>(() => accessControl.UpgradeRemoteAccess(Guid.NewGuid(), "password123"));
        }

        [Fact]
        public void UpgradeRemoteAccessWithLocalAccessTokenThrowsArgumentException()
        {
            var settings = new CoreSettings
            {
                RemoteControlPassword = "password123"
            };

            var accessControl = new AccessControl(settings);

            Guid token = accessControl.RegisterLocalAccessToken();

            Assert.Throws<ArgumentException>(() => accessControl.UpgradeRemoteAccess(token, "password123"));
        }

        [Fact]
        public void VerifyLocalAccessSmokeTest()
        {
            var settings = new CoreSettings();

            var accessControl = new AccessControl(settings);

            Guid token = accessControl.RegisterLocalAccessToken();

            accessControl.VerifyAccess(token, false);

            accessControl.SetLocalPassword(token, "password123");
            accessControl.DowngradeLocalAccess(token);

            Assert.Throws<AccessException>(() => accessControl.VerifyAccess(token));
        }

        private static PlaylistEntry SetupVotedEntry()
        {
            var entry = new PlaylistEntry(0, Helpers.SetupSongMock());
            entry.Vote();

            return entry;
        }
    }
}