using System;
using System.ComponentModel;

namespace Espera.Core.Management
{
    public interface IRemoteAccessControl : INotifyPropertyChanged
    {
        bool IsGuestSystemReallyEnabled { get; }

        bool IsRemoteAccessReallyLocked { get; }

        bool IsVoteRegistered(Guid accessToken, PlaylistEntry entry);

        IObservable<AccessPermission> ObserveAccessPermission(Guid accessToken);

        IObservable<int?> ObserveRemainingVotes(Guid accessToken);

        Guid RegisterRemoteAccessToken(Guid deviceId);

        void RegisterVote(Guid accessToken, PlaylistEntry entry);

        void SetRemotePassword(Guid accessToken, string password);

        void UpgradeRemoteAccess(Guid accessToken, string password);
    }
}