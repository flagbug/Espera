using Espera.Core.Settings;
using Rareform.Extensions;
using ReactiveUI;
using Splat;
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
    /// The basic idea is, that each endpoint (be it the local GUI, or a mobile phone) gets an
    /// access token similiar to a Web API. This access token can be upgraded by providing the
    /// password that the administrator has specified.
    /// </remarks>
    internal class AccessControl : ReactiveObject, ILocalAccessControl, IRemoteAccessControl
    {
        private readonly CoreSettings coreSettings;
        private readonly ReaderWriterLockSlim endPointLock;
        private readonly HashSet<AccessEndPoint> endPoints;
        private readonly ObservableAsPropertyHelper<bool> isGuestSystemReallyEnabled;
        private readonly ObservableAsPropertyHelper<bool> isRemoteAccessReallyLocked;

        private string localPassword;

        public AccessControl(CoreSettings coreSettings)
        {
            if (coreSettings == null)
                throw new ArgumentNullException("coreSettings");

            this.coreSettings = coreSettings;

            this.endPoints = new HashSet<AccessEndPoint>();
            this.endPointLock = new ReaderWriterLockSlim();

            this.isRemoteAccessReallyLocked = this.coreSettings.WhenAnyValue(x => x.LockRemoteControl, x => x.RemoteControlPassword,
               (lockRemoteControl, password) => lockRemoteControl && !String.IsNullOrWhiteSpace(password))
               .ToProperty(this, x => x.IsRemoteAccessReallyLocked);

            this.isGuestSystemReallyEnabled = this.WhenAnyValue(x => x.IsRemoteAccessReallyLocked, x => x.coreSettings.EnableGuestSystem,
                (isLocked, enableGuestSystem) => isLocked && enableGuestSystem)
                .ToProperty(this, x => x.IsGuestSystemReallyEnabled);

            this.WhenAnyValue(x => x.IsRemoteAccessReallyLocked)
                .Select(x => x ? AccessPermission.Guest : AccessPermission.Admin)
                .Subscribe(this.UpdateRemoteAccessPermissions);
        }

        public bool IsGuestSystemReallyEnabled
        {
            get { return this.isGuestSystemReallyEnabled.Value; }
        }

        /// <summary>
        /// Returns a value indicating whether the remote control is set to locked and there is a
        /// password set.
        /// </summary>
        public bool IsRemoteAccessReallyLocked
        {
            get { return this.isRemoteAccessReallyLocked.Value; }
        }

        public void DowngradeLocalAccess(Guid accessToken)
        {
            AccessEndPoint endPoint = this.VerifyAccessToken(accessToken);

            if (this.localPassword == null)
                throw new InvalidOperationException("Local password is not set");

            endPoint.SetAccessPermission(AccessPermission.Guest);
        }

        /// <summary>
        /// Returns whether a vote for the given access token and entry is already registered.
        /// </summary>
        public bool IsVoteRegistered(Guid accessToken, PlaylistEntry entry)
        {
            if (entry == null)
                throw new ArgumentNullException("entry");

            AccessEndPoint endPoint = this.VerifyAccessToken(accessToken);

            return endPoint.IsRegistered(entry);
        }

        public IObservable<AccessPermission> ObserveAccessPermission(Guid accessToken)
        {
            AccessEndPoint endPoint = this.VerifyAccessToken(accessToken);

            return endPoint.AccessPermission;
        }

        /// <summary>
        /// Gets the remaining votes for a given access token. Returns the current value immediately
        /// and then any changes to the remaining votes, or <c>null</c> , if voting isn't supported.
        /// </summary>
        public IObservable<int?> ObserveRemainingVotes(Guid accessToken)
        {
            AccessEndPoint endPoint = this.VerifyAccessToken(accessToken);

            return endPoint.EntryCountObservable.CombineLatest(
                    this.coreSettings.WhenAnyValue(x => x.MaxVoteCount),
                    this.WhenAnyValue(x => x.IsGuestSystemReallyEnabled),
                (entryCount, maxVoteCount, enableGuestSystem) => enableGuestSystem ? maxVoteCount - entryCount : (int?)null);
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
        /// Registers a new remote access token which's default reights depend on the <see
        /// cref="CoreSettings.LockRemoteControl" /> setting.
        /// </summary>
        public Guid RegisterRemoteAccessToken(Guid deviceId)
        {
            this.Log().Info("Creating remote access token");

            this.endPointLock.EnterReadLock();

            AccessEndPoint endPoint = this.endPoints.FirstOrDefault(x => x.DeviceId == deviceId);

            this.endPointLock.ExitReadLock();

            if (endPoint != null)
                return endPoint.AccessToken;

            return this.RegisterToken(AccessType.Remote, this.GetDefaultRemoteAccessPermission(), deviceId);
        }

        /// <summary>
        /// Registers a vote for the given access token and decrements the count of the remainig votes.
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// There are no votes left for the given access token, or a the same entry is registered twice.
        /// 
        /// -- or--
        /// 
        /// The access token isn't a guest token.
        /// </exception>
        public void RegisterShadowVote(Guid accessToken, PlaylistEntry entry)
        {
            if (entry == null)
                throw new ArgumentNullException("entry");

            if (!entry.IsShadowVoted)
                throw new ArgumentException("Entry must be shadow voted");

            this.VerifyVotingPreconditions(accessToken);

            AccessEndPoint endPoint = this.VerifyAccessToken(accessToken);

            if (endPoint.AccessPermission.FirstAsync().Wait() != AccessPermission.Guest)
            {
                throw new InvalidOperationException("Access token has to be a guest token.");
            }

            endPoint.RegisterEntry(entry);
        }

        /// <summary>
        /// Registers a vote for the given access token and decrements the count of the remainig votes.
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// There are no votes left for the given access token, or a the same entry is registered twice.
        /// </exception>
        public void RegisterVote(Guid accessToken, PlaylistEntry entry)
        {
            if (entry == null)
                throw new ArgumentNullException("entry");

            this.VerifyVotingPreconditions(accessToken);

            AccessEndPoint endPoint = this.VerifyAccessToken(accessToken);

            if (!endPoint.RegisterEntry(entry) && !entry.IsShadowVoted)
            {
                throw new InvalidOperationException("Entry already registered");
            }
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
                throw new ArgumentException("Password is invalid", "password");

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
                throw new ArgumentException("Password is invalid", "password");

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
        /// <param name="localRestrictionCombinator">
        /// An optional restriction constraint for local access.
        /// </param>
        /// <exception cref="AccessException">
        /// The token has guest permission and is either of type remote or of type local and the
        /// restriction combinator was true.
        /// </exception>
        public void VerifyAccess(Guid accessToken, bool localRestrictionCombinator = true)
        {
            AccessEndPoint endPoint = this.VerifyAccessToken(accessToken);

            if (endPoint.AccessPermission.FirstAsync().Wait() == AccessPermission.Admin)
                return;

            if (endPoint.AccessType == AccessType.Remote || endPoint.AccessType == AccessType.Local && localRestrictionCombinator)
                throw new AccessException();
        }

        /// <summary>
        /// Verifies that voting is enabled and that the vote count of the given endpoint doesn't
        /// exceed the maximum vote count.
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// The exception that is thrown if one of the preconditions isn't satisfied.
        /// </exception>
        public void VerifyVotingPreconditions(Guid accessToken)
        {
            if (!this.coreSettings.EnableGuestSystem)
                throw new InvalidOperationException("Voting isn't enabled.");

            AccessEndPoint endPoint = this.VerifyAccessToken(accessToken);

            if (endPoint.AccessType == AccessType.Local)
            {
                return;
            }

            if (endPoint.EntryCount == this.coreSettings.MaxVoteCount)
            {
                throw new InvalidOperationException("No votes left");
            }
        }

        private AccessEndPoint FindEndPoint(Guid token)
        {
            this.endPointLock.EnterReadLock();

            AccessEndPoint endPoint = this.endPoints.FirstOrDefault(x => x.AccessToken == token);

            this.endPointLock.ExitReadLock();

            return endPoint;
        }

        private AccessPermission GetDefaultRemoteAccessPermission()
        {
            return this.IsRemoteAccessReallyLocked ?
                AccessPermission.Guest : AccessPermission.Admin;
        }

        private Guid RegisterToken(AccessType accessType, AccessPermission permission, Guid? deviceId = null)
        {
            var token = Guid.NewGuid();

            this.endPointLock.EnterWriteLock();
            this.endPoints.Add(new AccessEndPoint(token, accessType, permission, deviceId));
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
            AccessEndPoint endPoint = this.FindEndPoint(token);

            if (endPoint == null)
                throw new ArgumentException("EndPoint with the gived access token was not found.");

            return endPoint;
        }

        private class AccessEndPoint : IEquatable<AccessEndPoint>
        {
            private readonly BehaviorSubject<AccessPermission> accessPermission;
            private readonly BehaviorSubject<int> entryCount;
            private readonly HashSet<PlaylistEntry> registeredEntries;

            public AccessEndPoint(Guid accessToken, AccessType accessType, AccessPermission accessPermission, Guid? deviceId = null)
            {
                this.AccessToken = accessToken;
                this.AccessType = accessType;
                this.accessPermission = new BehaviorSubject<AccessPermission>(accessPermission);
                this.DeviceId = deviceId;

                this.registeredEntries = new HashSet<PlaylistEntry>();
                this.entryCount = new BehaviorSubject<int>(0);
            }

            public IObservable<AccessPermission> AccessPermission
            {
                get { return this.accessPermission.DistinctUntilChanged(); }
            }

            public Guid AccessToken { get; private set; }

            public AccessType AccessType { get; private set; }

            public Guid? DeviceId { get; private set; }

            public int EntryCount
            {
                get { return this.registeredEntries.Count; }
            }

            public IObservable<int> EntryCountObservable
            {
                get { return this.entryCount; }
            }

            public bool Equals(AccessEndPoint other)
            {
                return this.AccessToken == other.AccessToken;
            }

            public override int GetHashCode()
            {
                return new { A = this.AccessToken, B = this.AccessType }.GetHashCode();
            }

            public bool IsRegistered(PlaylistEntry entry)
            {
                return this.registeredEntries.Contains(entry);
            }

            public bool RegisterEntry(PlaylistEntry entry)
            {
                if (this.registeredEntries.Add(entry))
                {
                    this.entryCount.OnNext(this.registeredEntries.Count);

                    // We remove the entry if it has no votes no a shadow vote
                    entry.WhenAnyValue(x => x.Votes, x => x.IsShadowVoted, (votes, isShadowVoted) => votes == 0 && !isShadowVoted)
                        .FirstAsync(x => x)
                        .Subscribe(x =>
                        {
                            this.registeredEntries.Remove(entry);
                            this.entryCount.OnNext(this.registeredEntries.Count);
                        });

                    return true;
                }

                return false;
            }

            public void SetAccessPermission(AccessPermission permission)
            {
                this.accessPermission.OnNext(permission);
            }
        }
    }
}