namespace LuxTicker;

/// <summary>
/// Per-user files live under %APPDATA%\LuxTicker.
/// Windows toasts also need one shortcut in the user Start Menu (still the roaming profile).
/// </summary>
public static class AppPaths
{
    public const string FolderName = "LuxTicker";
    public const string SymbolsFileName = "symbols.json";
    public const string SettingsFileName = "settings.json";
    public const string LogFileName = "lux-ticker.log";
    public const string StartupLogFileName = "startup.log";
    public const string FontFileName = "CascadiaMono-Bold.ttf";
    public const string LogoFileName = "lux-wire-logo.png";
    public const string LuxWireFolderName = "lux-wire";
    public const string LuxWireSeenFileName = "seen.json";
    public const string XAuthFileName = "x-auth.json";
    public const string AlertsFileName = "alerts.json";
    public const string ChartRecentFileName = "chart-recent.json";
    public const string AccountFileName = "account.bin";
    public const string SyncStateFileName = "sync-state.json";

    public static string Root()
        => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), FolderName);

    public static string SymbolsPath() => Path.Combine(Root(), SymbolsFileName);
    public static string SettingsPath() => Path.Combine(Root(), SettingsFileName);
    public static string LogPath() => Path.Combine(Root(), LogFileName);
    public static string StartupLogPath() => Path.Combine(Root(), StartupLogFileName);
    public static string FontPath() => Path.Combine(Root(), FontFileName);
    public static string LogoPath() => Path.Combine(Root(), LogoFileName);
    public static string LuxWireFolder() => Path.Combine(Root(), LuxWireFolderName);
    public static string LuxWireSeenPath() => Path.Combine(LuxWireFolder(), LuxWireSeenFileName);
    public static string XAuthPath() => Path.Combine(Root(), XAuthFileName);
    public static string AlertsPath() => Path.Combine(Root(), AlertsFileName);
    public static string ChartRecentPath() => Path.Combine(Root(), ChartRecentFileName);
    public static string AccountPath() => Path.Combine(Root(), AccountFileName);
    public static string SyncStatePath() => Path.Combine(Root(), SyncStateFileName);

    public static string StartMenuShortcut()
    {
        var root = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        return Path.Combine(root, "Microsoft", "Windows", "Start Menu", "Programs", "LuxWire Ticker.lnk");
    }
}

/// <summary>HKCU Run key only. The app never opens HKLM.</summary>
public static class RunKey
{
    public const string SubKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    public const string ValueName = "LuxTicker";

    public static string QuoteCommand(string exePath) => "\"" + exePath + "\"";
}
