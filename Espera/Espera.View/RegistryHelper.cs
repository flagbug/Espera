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

        private static bool IsApplicationInstalled(string applicationName)
        {
            string displayName;

            // Search in: CurrentUser
            RegistryKey key = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall");

            foreach (RegistryKey subkey in key.GetSubKeyNames().Select(keyName => key.OpenSubKey(keyName)))
            {
                displayName = subkey.GetValue("DisplayName") as string;

                if (displayName != null && displayName.Contains(applicationName))
                {
                    return true;
                }
            }

            // Search in: LocalMachine_32
            key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall");

            foreach (RegistryKey subkey in key.GetSubKeyNames().Select(keyName => key.OpenSubKey(keyName)))
            {
                displayName = subkey.GetValue("DisplayName") as string;

                if (displayName != null && displayName.Contains(applicationName))
                {
                    return true;
                }
            }

            // Search in: LocalMachine_64
            key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Wow6432Node\Microsoft\Windows\CurrentVersion\Uninstall");

            foreach (RegistryKey subkey in key.GetSubKeyNames().Select(keyName => key.OpenSubKey(keyName)))
            {
                displayName = subkey.GetValue("DisplayName") as string;

                if (displayName != null && displayName.Contains(applicationName))
                {
                    return true;
                }
            }

            // Not found
            return false;
        }
    }
}