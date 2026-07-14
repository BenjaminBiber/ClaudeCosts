using System.Diagnostics;
using System.IO;
using Microsoft.Win32;

namespace ClaudeCosts.App.Services;

/// <summary>
/// Manages the "start with Windows" entry under
/// <c>HKCU\Software\Microsoft\Windows\CurrentVersion\Run</c>.
/// </summary>
public sealed class AutostartService
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "ClaudeCosts";

    /// <summary>Path to the running executable (used as the Run command).</summary>
    public static string ExecutablePath
    {
        get
        {
            // Prefer the real .exe (Environment.ProcessPath) over the managed dll.
            var path = Environment.ProcessPath;
            if (!string.IsNullOrEmpty(path)) return path;
            return Process.GetCurrentProcess().MainModule?.FileName
                   ?? Path.ChangeExtension(typeof(AutostartService).Assembly.Location, ".exe");
        }
    }

    public bool IsEnabled()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: false);
            var value = key?.GetValue(ValueName) as string;
            return !string.IsNullOrEmpty(value);
        }
        catch
        {
            return false;
        }
    }

    public void SetEnabled(bool enabled)
    {
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(RunKeyPath, writable: true);
            if (key is null) return;

            if (enabled)
                key.SetValue(ValueName, $"\"{ExecutablePath}\"");
            else
                key.DeleteValue(ValueName, throwOnMissingValue: false);
        }
        catch
        {
            // non-fatal: registry access denied → toggle is a no-op
        }
    }
}
