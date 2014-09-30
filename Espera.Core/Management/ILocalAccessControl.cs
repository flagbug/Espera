using System;

namespace Espera.Core.Management
{
    public interface ILocalAccessControl
    {
        void DowngradeLocalAccess(Guid accessToken);

        IObservable<AccessPermission> ObserveAccessPermission(Guid accessToken);

        Guid RegisterLocalAccessToken();

        void SetLocalPassword(Guid accessToken, string password);

        void UpgradeLocalAccess(Guid accessToken, string password);
    }
}