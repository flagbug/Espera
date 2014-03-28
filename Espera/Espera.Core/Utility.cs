using System;

namespace Espera.Core
{
    public static class Utility
    {
        public static void Retry(this Action block, int retries = 3)
        {
            while (true)
            {
                try
                {
                    block();
                    return;
                }

                catch (Exception)
                {
                    retries--;

                    if (retries == 0)
                    {
                        throw;
                    }
                }
            }
        }
    }
}