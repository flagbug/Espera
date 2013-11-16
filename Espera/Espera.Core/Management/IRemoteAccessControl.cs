using System;

namespace Espera.Core.Management
{
    public interface IRemoteAccessControl
    {
        IObservable<AccessPermission> ObserveAccessPermission(Guid accessToken);

        Guid RegisterRemoteAccessToken();

        void SetRemotePassword(Guid accessToken, string password);

        void UpgradeRemoteAccess(Guid accessToken, string password);
    }
}