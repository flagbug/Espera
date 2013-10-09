using System;

namespace Espera.Core.Management
{
    public interface IRemoteAccessControl
    {
        Guid RegisterRemoteAccessToken();

        void UpgradeRemoteAccess(Guid accessToken, string password);
    }
}