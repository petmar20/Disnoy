using System.Windows.Forms;
using Microsoft.Win32;

namespace Disnoy
{
    internal static class Settings
    {
        private const string SettingsKey = @"Software\Disnoy";
        private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string RunValue = "Disnoy";

        public static bool RefreshTaskbarButtons
        {
            get => ReadFlag("RefreshTaskbarButtons", true);
            set => WriteFlag("RefreshTaskbarButtons", value);
        }

        public static bool IsFirstRun => !ReadFlag("Initialized", false);

        public static void MarkInitialized()
        {
            WriteFlag("Initialized", true);
        }

        public static bool StartWithWindows
        {
            get
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RunKey))
                {
                    return key?.GetValue(RunValue) != null;
                }
            }
            set
            {
                using (RegistryKey key = Registry.CurrentUser.CreateSubKey(RunKey))
                {
                    if (value)
                    {
                        key.SetValue(RunValue, "\"" + Application.ExecutablePath + "\"");
                    }
                    else
                    {
                        key.DeleteValue(RunValue, false);
                    }
                }
            }
        }

        private static bool ReadFlag(string name, bool defaultValue)
        {
            using (RegistryKey key = Registry.CurrentUser.OpenSubKey(SettingsKey))
            {
                object value = key?.GetValue(name);
                return value is int number ? number != 0 : defaultValue;
            }
        }

        private static void WriteFlag(string name, bool value)
        {
            using (RegistryKey key = Registry.CurrentUser.CreateSubKey(SettingsKey))
            {
                key.SetValue(name, value ? 1 : 0, RegistryValueKind.DWord);
            }
        }
    }
}
