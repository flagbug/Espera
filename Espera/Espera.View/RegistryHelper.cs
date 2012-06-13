using System.Linq;
using Microsoft.Win32;

namespace Espera.View
{
    public static class RegistryHelper
    {
        public static bool IsVlcInstalled()
        {
            return IsApplicationInstalled("VLC");
        }

        private static bool IsApplicationInKey(RegistryKey key, string applicationName)
        {
            return key.GetSubKeyNames()
                .Select(key.OpenSubKey)
                .Select(subkey => subkey.GetValue("DisplayName") as string)
                .Any(displayName => displayName != null && displayName.Contains(applicationName));
        }

        private static bool IsApplicationInstalled(string applicationName)
        {
            using (RegistryKey currentUser = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall"))
            {
                using (RegistryKey localMachine32 = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall"))
                {
                    using (RegistryKey localMachine64 = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Wow6432Node\Microsoft\Windows\CurrentVersion\Uninstall"))
                    {
                        return IsApplicationInKey(currentUser, applicationName) ||
                               IsApplicationInKey(localMachine32, applicationName) ||
                               IsApplicationInKey(localMachine64, applicationName);
                    }
                }
            }
        }
    }
}