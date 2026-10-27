using Microsoft.Win32;

namespace LuxTicker;

public static class StartupRegistrar
{
    public static void Apply(bool enabled, string? exePath)
    {
        if (!OperatingSystem.IsWindows())
            return;

        using var key = Registry.CurrentUser.CreateSubKey(RunKey.SubKey, writable: true);
        if (key == null)
            return;

        if (!enabled)
        {
            key.DeleteValue(RunKey.ValueName, throwOnMissingValue: false);
            return;
        }

        if (string.IsNullOrWhiteSpace(exePath))
            throw new InvalidOperationException("Cannot register startup without the exe path.");

        key.SetValue(RunKey.ValueName, RunKey.QuoteCommand(exePath));
    }
}
