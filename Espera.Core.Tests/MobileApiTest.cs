using System;
using Espera.Core.Mobile;
using Espera.Network;
using Xunit;

namespace Espera.Core.Tests
{
    public class MobileApiTest
    {
        public class TheConstructor
        {
            [Fact]
            public void AssertsMaximumPort()
            {
                int port = NetworkConstants.MaxPort + 1;

                using (var library = Helpers.CreateLibrary())
                {
                    Assert.Throws<ArgumentOutOfRangeException>(() => new MobileApi(port, library));
                }
            }

            [Fact]
            public void AssertsMinimumPort()
            {
                int port = NetworkConstants.MinPort - 1;

                using (var library = Helpers.CreateLibrary())
                {
                    Assert.Throws<ArgumentOutOfRangeException>(() => new MobileApi(port, library));
                }
            }
        }
    }
}