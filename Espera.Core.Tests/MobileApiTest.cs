using System;
using Espera.Core.Mobile;

namespace Espera.Core.Tests
{
    public class MobileApiTest
    {
        public class TheConstructor
        {
            [Fact]
            public void AssertsMaximumPort()
            {
                var port = NetworkConstants.MaxPort + 1;

                using (var library = Helpers.CreateLibrary())
                {
                    Assert.Throws<ArgumentOutOfRangeException>(() => new MobileApi(port, library));
                }
            }

            [Fact]
            public void AssertsMinimumPort()
            {
                var port = NetworkConstants.MinPort - 1;

                using (var library = Helpers.CreateLibrary())
                {
                    Assert.Throws<ArgumentOutOfRangeException>(() => new MobileApi(port, library));
                }
            }
        }
    }
}