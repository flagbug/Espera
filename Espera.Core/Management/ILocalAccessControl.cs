using System;
using System.Reactive.Linq;

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

    public static class LocalAccessControlMixin
    {
        /// <summary>
        /// Creates a boolean observable that returns whether the user has the permission to do an
        /// administrator action.
        /// </summary>
        /// <param name="combinator">
        /// An additional combinator that returns whether the action is restricted in guest mode.
        /// </param>
        /// <param name="accessToken">The access token to check.</param>
        /// <remarks>
        /// The user has admin access =&gt; Always returns true The user has guest access and
        /// combinator is true =&gt; Returns false The user has guest access and combinator is false
        /// = &gt; Returns true
        /// </remarks>
        public static IObservable<bool> HasAccess(this ILocalAccessControl control, IObservable<bool> combinator, Guid accessToken)
        {
            return control.ObserveAccessPermission(accessToken)
                .Select(x => x == AccessPermission.Admin)
                .CombineLatest(combinator, (admin, c) => admin || !c);
        }
    }
}