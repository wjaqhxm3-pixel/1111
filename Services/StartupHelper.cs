using Microsoft.Win32;

namespace ScreenSealWindows.Services;

/// <summary>
/// Manages the "Run on Startup" feature by adding/removing this application
/// from the Windows registry auto-start key.
/// </summary>
public static class StartupHelper
{
    private const string RegistryKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
    private const string AppName = "ScreenSeal";

    /// <summary>
    /// Returns true if the app is configured to run on Windows startup.
    /// </summary>
    public static bool IsStartupEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RegistryKey, false);
        return key?.GetValue(AppName) != null;
    }

    /// <summary>
    /// Enables or disables auto-start on Windows login.
    /// </summary>
    public static void SetStartupEnabled(bool enabled)
    {
        using var key = Registry.CurrentUser.OpenSubKey(RegistryKey, true);
        if (key == null) return;

        if (enabled)
        {
            var exePath = Environment.ProcessPath ?? System.Reflection.Assembly.GetExecutingAssembly().Location;
            key.SetValue(AppName, $"\"{exePath}\"");
        }
        else
        {
            key.DeleteValue(AppName, false);
        }
    }
}
