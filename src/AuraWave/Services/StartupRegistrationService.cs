using System;
using System.Diagnostics;
using Microsoft.Win32;
using AuraWave.Utilities;

namespace AuraWave.Services
{
    public static class StartupRegistrationService
    {
        private const string RunRegistryPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string AppValueName = "AuraWave";

        public static bool IsStartupEnabled()
        {
            try
            {
                using RegistryKey? key = Registry.CurrentUser.OpenSubKey(RunRegistryPath, writable: false);
                object? val = key?.GetValue(AppValueName);
                return val is string s && !string.IsNullOrWhiteSpace(s);
            }
            catch (Exception ex)
            {
                AppLogger.Warn("Failed to query startup registry entry: " + ex.Message);
                return false;
            }
        }

        public static bool SetStartupEnabled(bool enabled)
        {
            try
            {
                using RegistryKey? key = Registry.CurrentUser.OpenSubKey(RunRegistryPath, writable: true);
                if (key == null) return false;

                if (enabled)
                {
                    string exePath = Process.GetCurrentProcess().MainModule?.FileName ?? AppContext.BaseDirectory;
                    key.SetValue(AppValueName, $"\"{exePath}\"");
                }
                else
                {
                    if (key.GetValue(AppValueName) != null)
                        key.DeleteValue(AppValueName, throwOnMissingValue: false);
                }
                return true;
            }
            catch (Exception ex)
            {
                AppLogger.Error("Failed to update Windows startup registration.", ex);
                return false;
            }
        }
    }
}