using Espera.Core.Management;
using Espera.Core.Settings;
using ReactiveUI;
using System;
using System.Reactive.Linq;
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
        public void LockedRemoteControlGivesGuestRightsByDefault()
        {
            var settings = new CoreSettings
            {
                LockRemoteControl = true
            };

            var accessControl = new AccessControl(settings);

            Guid token = accessControl.RegisterRemoteAccessToken();

            Assert.Throws<AccessException>(() => accessControl.VerifyAccess(token));
        }

        [Fact]
        public void LockRemoteControlOnlyUpdatesRemoteAccessPermissions()
        {
            var settings = new CoreSettings
            {
                LockRemoteControl = false
            };

            var accessControl = new AccessControl(settings);

            Guid localToken = accessControl.RegisterLocalAccessToken();

            Guid remoteToken = accessControl.RegisterRemoteAccessToken();
            var remotePermissions = accessControl.ObserveAccessPermission(remoteToken).CreateCollection();

            settings.LockRemoteControl = true;

            Assert.Equal(AccessPermission.Admin, accessControl.ObserveAccessPermission(localToken).FirstAsync().Wait());
            Assert.Equal(new[] { AccessPermission.Admin, AccessPermission.Guest }, remotePermissions);
        }

        [Fact]
        public void ObserveAccessPermissionSmokeTest()
        {
            var settings = new CoreSettings
            {
                LockRemoteControl = false
            };

            var accessControl = new AccessControl(settings);

            Guid token = accessControl.RegisterRemoteAccessToken();

            var conn = accessControl.ObserveAccessPermission(token).Replay();
            conn.Connect();

            settings.LockRemoteControl = true;
            settings.LockRemoteControl = false;

            var permissions = conn.CreateCollection();

            Assert.Equal(AccessPermission.Admin, permissions[0]);
            Assert.Equal(AccessPermission.Guest, permissions[1]);
            Assert.Equal(AccessPermission.Admin, permissions[2]);
        }

        [Fact]
        public void ObserveAccessPermissionThrowsArgumentExceptionIfGuidIsGarbage()
        {
            var settings = new CoreSettings { LockRemoteControl = false };

            var accessControl = new AccessControl(settings);

            Assert.Throws<ArgumentException>(() => accessControl.ObserveAccessPermission(Guid.NewGuid()));
        }

        [Fact]
        public void RemoteControlLockUpdatesAccessPermission()
        {
            var settings = new CoreSettings
            {
                LockRemoteControl = false
            };

            var accessControl = new AccessControl(settings);

            Guid token1 = accessControl.RegisterRemoteAccessToken();
            Guid token2 = accessControl.RegisterRemoteAccessToken();

            accessControl.VerifyAccess(token1);
            accessControl.VerifyAccess(token2);

            settings.LockRemoteControl = true;
            Assert.Throws<AccessException>(() => accessControl.VerifyAccess(token1));
            Assert.Throws<AccessException>(() => accessControl.VerifyAccess(token2));
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
    }
}