using Espera.Core.Settings;
using Rareform.Extensions;
using Rareform.Validation;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading;

namespace Espera.Core.Management
{
    /// <summary>
    /// Provides methods to manage access privileges for the local and remote (mobile) users.
    /// </summary>
    /// <remarks>
    /// The basic idea is, that each endpoint (be it the local GUI, or a mobile phone) gets an access token
    /// much like in an web API. This access token can be upgraded by providing the password that the administrator
    /// has specified.
    /// </remarks>
    internal class AccessControl : IEnableLogger, ILocalAccessControl, IRemoteAccessControl
    {
        private readonly CoreSettings coreSettings;
        private readonly ReaderWriterLockSlim endPointLock;
        private readonly HashSet<AccessEndPoint> endPoints;

        private string localPassword;

        public AccessControl(CoreSettings coreSettings)
        {
            if (coreSettings == null)
                Throw.ArgumentNullException(() => coreSettings);

            this.coreSettings = coreSettings;

            this.endPoints = new HashSet<AccessEndPoint>();
            this.endPointLock = new ReaderWriterLockSlim();
        }

        public void DowngradeLocalAccess(Guid accessToken)
        {
            AccessEndPoint endPoint = this.VerifyAccessToken(accessToken);

            if (this.localPassword == null)
                throw new InvalidOperationException("Local password is not set");

            endPoint.SetAccessPermission(AccessPermission.Guest);
        }

        public IObservable<AccessPermission> ObserveAccessPermission(Guid accessToken)
        {
            AccessEndPoint endPoint = this.VerifyAccessToken(accessToken);

            return endPoint.AccessPermission;
        }

        /// <summary>
        /// Registers a new local access token with default admin rights.
        /// </summary>
        public Guid RegisterLocalAccessToken()
        {
            this.Log().Info("Creating local access token.");

            return this.RegisterToken(AccessType.Local, AccessPermission.Admin);
        }

        /// <summary>
        /// Registers a new remote access token which's default reights depend on the <see cref="CoreSettings.LockRemoteControl"/> setting.
        /// </summary>
        public Guid RegisterRemoteAccessToken()
        {
            this.Log().Info("Creating remote access token");

            return this.RegisterToken(AccessType.Remote, this.GetDefaultRemoteAccessPermission());
        }

        public void SetLocalPassword(Guid accessToken, string password)
        {
            AccessEndPoint endPoint = this.VerifyAccessToken(accessToken);

            if (endPoint.AccessType == AccessType.Remote)
                throw new ArgumentException("Access token is a remote token!");

            this.VerifyAccess(accessToken);

            if (password == null)
                throw new ArgumentNullException("password");

            if (string.IsNullOrWhiteSpace(password))
                throw new ArgumentException("Password is invalid");

            this.localPassword = password;
        }

        public void SetRemotePassword(Guid accessToken, string password)
        {
            AccessEndPoint endPoint = this.VerifyAccessToken(accessToken);

            if (endPoint.AccessType == AccessType.Remote)
                throw new ArgumentException("Access token is a remote token!");

            this.VerifyAccess(accessToken);

            if (password == null)
                throw new ArgumentNullException("password");

            if (string.IsNullOrWhiteSpace(password))
                throw new ArgumentException("Password is invalid");

            this.coreSettings.RemoteControlPassword = password;

            this.UpdateRemoteAccessPermissions(AccessPermission.Guest);
        }

        public void UpgradeLocalAccess(Guid accessToken, string password)
        {
            AccessEndPoint endPoint = this.VerifyAccessToken(accessToken);

            if (endPoint.AccessType == AccessType.Remote)
                throw new ArgumentException("Access token is a remote token!");

            if (password != this.localPassword)
                throw new WrongPasswordException("Password is wrong");

            endPoint.SetAccessPermission(AccessPermission.Admin);
        }

        public void UpgradeRemoteAccess(Guid accessToken, string password)
        {
            AccessEndPoint endPoint = this.VerifyAccessToken(accessToken);

            if (endPoint.AccessType == AccessType.Local)
                throw new ArgumentException("Access token is a local token!");

            if (password != this.coreSettings.RemoteControlPassword)
                throw new WrongPasswordException("Remote password is wrong");

            endPoint.SetAccessPermission(AccessPermission.Admin);
        }

        /// <summary>
        /// Verifies the access rights for the given access token.
        /// </summary>
        /// <param name="accessToken">The access token, whichs access rights should be verified.</param>
        /// <param name="localRestrictionCombinator">An optional restriction constraint for local access.</param>
        /// <exception cref="AccessException">The token has local permission and is either of type remote or of type local and the restriction combinator was true.</exception>
        public void VerifyAccess(Guid accessToken, bool localRestrictionCombinator = true)
        {
            AccessEndPoint endPoint = this.VerifyAccessToken(accessToken);

            if (endPoint.AccessPermission.FirstAsync().Wait() == AccessPermission.Admin)
                return;

            if (endPoint.AccessType == AccessType.Remote || endPoint.AccessType == AccessType.Local && localRestrictionCombinator)
                throw new AccessException();
        }

        private AccessPermission GetDefaultRemoteAccessPermission()
        {
            return this.coreSettings.LockRemoteControl && !String.IsNullOrWhiteSpace(this.coreSettings.RemoteControlPassword) ?
                AccessPermission.Guest : AccessPermission.Admin;
        }

        private Guid RegisterToken(AccessType accessType, AccessPermission permission)
        {
            var token = Guid.NewGuid();

            this.endPointLock.EnterWriteLock();
            this.endPoints.Add(new AccessEndPoint(token, accessType, permission));
            this.endPointLock.ExitWriteLock();

            return token;
        }

        private void UpdateRemoteAccessPermissions(AccessPermission permission)
        {
            this.endPointLock.EnterReadLock();

            this.endPoints.Where(x => x.AccessType == AccessType.Remote)
                .ForEach(x => x.SetAccessPermission(permission));

            this.endPointLock.ExitReadLock();
        }

        private AccessEndPoint VerifyAccessToken(Guid token)
        {
            this.endPointLock.EnterReadLock();

            AccessEndPoint endPoint = this.endPoints.FirstOrDefault(x => x.AccessToken == token);

            this.endPointLock.ExitReadLock();

            if (endPoint == null)
                throw new ArgumentException("EndPoint with the gived access token was not found.");

            return endPoint;
        }

        private class AccessEndPoint : IEquatable<AccessEndPoint>
        {
            private readonly BehaviorSubject<AccessPermission> accessPermission;

            public AccessEndPoint(Guid accessToken, AccessType accessType, AccessPermission accessPermission)
            {
                this.AccessToken = accessToken;
                this.AccessType = accessType;
                this.accessPermission = new BehaviorSubject<AccessPermission>(accessPermission);
            }

            public IObservable<AccessPermission> AccessPermission
            {
                get { return this.accessPermission.AsObservable(); }
            }

            public Guid AccessToken { get; private set; }

            public AccessType AccessType { get; private set; }

            public bool Equals(AccessEndPoint other)
            {
                return this.AccessToken == other.AccessToken;
            }

            public override int GetHashCode()
            {
                return new { A = this.AccessToken, B = this.AccessType }.GetHashCode();
            }

            public void SetAccessPermission(AccessPermission permission)
            {
                this.accessPermission.OnNext(permission);
            }
        }
    }
}