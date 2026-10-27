using System.Text.Json;

namespace LuxTicker;

public enum ScrollSpeed
{
    Slow,
    Normal,
    Fast
}

public sealed record AppSettings(
    DockEdge Dock,
    ScrollSpeed Speed,
    bool StartWithWindows,
    bool LuxWirePosts = true,
    double Opacity = 0.9,
    bool AlwaysOnTop = true,
    bool ScrollEnabled = true,
    bool ShowPercent = true,
    bool CompactLayout = true,
    bool ShowSparklines = false,
    bool ClickThrough = false,
    bool PinBtc = false,
    double TextScale = 1,
    bool LuxWireDigest = false,
    int LuxWirePollMinutes = 3,
    string? MonitorDevice = null,
    int VerticalOffsetDip = BarOffset.DefaultDip,
    double BackgroundOpacity = BarSurfaceMath.DefaultOpacity,
    double ScrollPixelsPerSecond = ScrollPhysics.DefaultPixelsPerSecond,
    string? TickerFontName = null,
    double SymbolGap = TickerSpacing.Default,
    bool ShowPre = true,
    bool ShowAfterHours = false,
    bool ScrollRight = false,
    bool ScrollLoop = true,
    string TextColor = AppearancePresets.WallSymbol,
    string ChartRange = "1M",
    string ChartStyle = "line",
    bool ChartCrosshair = true,
    string PriceColor = AppearancePresets.WallPrice,
    string ChangeUpColor = AppearancePresets.WallUp,
    string ChangeDownColor = AppearancePresets.WallDown,
    bool BackgroundOpacityChosen = false,
    string BackgroundColor = AppearancePresets.WallBackground,
    bool LiveScrollUpdates = true,
    string AppearancePreset = AppearancePresets.WallStreet)
{
    public static AppSettings Default { get; } = new(DockEdge.Bottom, ScrollSpeed.Normal, false, true);

    public AppSettings Clamped() => this with
    {
        Opacity = Math.Clamp(Opacity, 0.35, 1),
        TextScale = Math.Clamp(TextScale, 0.8, 1.6),
        LuxWirePollMinutes = LuxWireXFeed.ClampMinutes(LuxWirePollMinutes),
        VerticalOffsetDip = BarOffset.Clamp(VerticalOffsetDip),
        BackgroundOpacity = BarSurfaceMath.Clamp(BackgroundOpacity),
        ScrollPixelsPerSecond = ScrollPhysics.ClampPixels(ScrollPixelsPerSecond),
        TickerFontName = CleanFont(TickerFontName),
        SymbolGap = TickerSpacing.Clamp(SymbolGap),
        TextColor = TickerTextColor.Clamp(TextColor),
        ChartRange = CleanRange(ChartRange),
        ChartStyle = CleanStyle(ChartStyle),
        PriceColor = TickerTextColor.Clamp(string.IsNullOrWhiteSpace(PriceColor) ? TickerTextColor.PriceDefault : PriceColor),
        ChangeUpColor = TickerTextColor.Clamp(string.IsNullOrWhiteSpace(ChangeUpColor) ? TickerTextColor.UpDefault : ChangeUpColor),
        ChangeDownColor = TickerTextColor.Clamp(string.IsNullOrWhiteSpace(ChangeDownColor) ? TickerTextColor.DownDefault : ChangeDownColor),
        BackgroundColor = TickerTextColor.Parse(BackgroundColor, PaperInk.Background),
        AppearancePreset = AppearancePresets.Canonical(AppearancePreset)
    };

    public static string CleanRange(string? range)
    {
        var text = range?.Trim();
        return text is "1H" or "1D" or "1W" or "1M" or "6M" or "YTD" or "1Y" or "ALL" ? text : "1M";
    }

    public static string CleanStyle(string? style)
        => string.Equals(style?.Trim(), "candles", StringComparison.OrdinalIgnoreCase) ? "candles" : "line";

    public static string? CleanFont(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return null;
        var trimmed = name.Trim();
        return trimmed.Length <= 80 ? trimmed : trimmed[..80];
    }
}

public static class ScrollPhysics
{
    /// <summary>Physical pixels per second. The strip converts this with the monitor DPI.</summary>
    public const double MinPixelsPerSecond = 24;
    public const double MaxPixelsPerSecond = 280;
    public const double DefaultPixelsPerSecond = 90;

    public static double PixelsPerSecond(ScrollSpeed speed) => speed switch
    {
        ScrollSpeed.Slow => 48,
        ScrollSpeed.Fast => 160,
        _ => DefaultPixelsPerSecond
    };

    public static double ClampPixels(double pixelsPerSecond)
    {
        if (double.IsNaN(pixelsPerSecond) || double.IsInfinity(pixelsPerSecond) || pixelsPerSecond <= 0)
            return DefaultPixelsPerSecond;
        return Math.Clamp(pixelsPerSecond, MinPixelsPerSecond, MaxPixelsPerSecond);
    }
}

public sealed class SettingsStore
{
    readonly string _path;

    public SettingsStore(string path) => _path = path;

    public string Path => _path;

    public AppSettings Load()
    {
        try
        {
            if (!File.Exists(_path))
                return AppSettings.Default;
            return TryParse(File.ReadAllText(_path), out var settings) ? settings : AppSettings.Default;
        }
        catch
        {
            return AppSettings.Default;
        }
    }

    public static bool TryParse(string json, out AppSettings settings)
    {
        settings = AppSettings.Default;
        try
        {
            if (string.IsNullOrWhiteSpace(json))
                return false;
            var file = JsonSerializer.Deserialize<SettingsFile>(json, JsonFile.Options);
            if (file == null)
                return false;
            settings = FromFile(file);
            return true;
        }
        catch (Exception ex) when (ex is JsonException or NotSupportedException)
        {
            return false;
        }
    }

    static AppSettings FromFile(SettingsFile file)
    {
            var legacy = AppearancePresets.IsLegacyFile(file.AppearancePreset);
            var background = AppearancePresets.BackgroundFor(file.BackgroundColor, legacy);
            var symbol = AppearancePresets.SymbolFor(file.TextColor, legacy);
            var price = AppearancePresets.PriceFor(file.PriceColor, legacy);
            var up = AppearancePresets.UpFor(file.ChangeUpColor, legacy);
            var down = AppearancePresets.DownFor(file.ChangeDownColor, legacy);
            var preset = legacy
                ? AppearancePresets.Match(background, symbol, price, up, down)
                : AppearancePresets.Canonical(file.AppearancePreset);
            return new AppSettings(
                ParseDock(file.Dock),
                ParseSpeed(file.Speed),
                file.StartWithWindows,
                file.LuxWirePosts ?? true,
                BarVisibility.SanitizeOpacity(file.Opacity ?? 0.9),
                file.AlwaysOnTop ?? true,
                file.ScrollEnabled ?? true,
                file.ShowPercent ?? true,
                file.CompactLayout ?? true,
                file.ShowSparklines ?? false,
                file.ClickThrough ?? false,
                file.PinBtc ?? false,
                Math.Clamp(file.TextScale ?? 1, 0.8, 1.6),
                file.LuxWireDigest ?? false,
                LuxWireXFeed.ClampMinutes(file.LuxWirePollMinutes ?? 3),
                string.IsNullOrWhiteSpace(file.MonitorDevice) ? null : file.MonitorDevice.Trim(),
                BarOffset.Clamp(file.VerticalOffsetDip ?? BarOffset.DefaultDip),
                BarSurfaceMath.Migrate(file.BackgroundOpacity, file.BackgroundOpacityChosen == true),
                ScrollPhysics.ClampPixels(file.ScrollPixelsPerSecond ?? ScrollPhysics.PixelsPerSecond(ParseSpeed(file.Speed))),
                AppSettings.CleanFont(file.TickerFontName),
                TickerSpacing.Clamp(file.SymbolGap ?? TickerSpacing.Default),
                file.ShowPre ?? true,
                file.ShowAfterHours ?? false,
                file.ScrollRight ?? false,
                file.ScrollLoop ?? true,
                symbol,
                AppSettings.CleanRange(file.ChartRange),
                AppSettings.CleanStyle(file.ChartStyle),
                file.ChartCrosshair ?? true,
                price,
                up,
                down,
                file.BackgroundOpacityChosen == true,
                background,
                file.LiveScrollUpdates ?? !legacy,
                preset);
    }

    public void Save(AppSettings settings)
        => JsonFile.AtomicWrite(_path, Serialize(settings));

    public static string Serialize(AppSettings settings)
    {
        var file = new SettingsFile
        {
            Dock = settings.Dock == DockEdge.Top ? "top" : "bottom",
            Speed = settings.Speed switch
            {
                ScrollSpeed.Slow => "slow",
                ScrollSpeed.Fast => "fast",
                _ => "normal"
            },
            StartWithWindows = settings.StartWithWindows,
            LuxWirePosts = settings.LuxWirePosts,
            Opacity = settings.Opacity,
            AlwaysOnTop = settings.AlwaysOnTop,
            ScrollEnabled = settings.ScrollEnabled,
            ShowPercent = settings.ShowPercent,
            CompactLayout = settings.CompactLayout,
            ShowSparklines = settings.ShowSparklines,
            ClickThrough = settings.ClickThrough,
            PinBtc = settings.PinBtc,
            TextScale = settings.TextScale,
            LuxWireDigest = settings.LuxWireDigest,
            LuxWirePollMinutes = settings.LuxWirePollMinutes,
            MonitorDevice = settings.MonitorDevice,
            VerticalOffsetDip = BarOffset.Clamp(settings.VerticalOffsetDip),
            BackgroundOpacity = BarSurfaceMath.Clamp(settings.BackgroundOpacity),
            ScrollPixelsPerSecond = ScrollPhysics.ClampPixels(settings.ScrollPixelsPerSecond),
            TickerFontName = AppSettings.CleanFont(settings.TickerFontName),
            SymbolGap = TickerSpacing.Clamp(settings.SymbolGap),
            ShowPre = settings.ShowPre,
            ShowAfterHours = settings.ShowAfterHours,
            ScrollRight = settings.ScrollRight,
            ScrollLoop = settings.ScrollLoop,
            TextColor = TickerTextColor.Clamp(settings.TextColor),
            ChartRange = AppSettings.CleanRange(settings.ChartRange),
            ChartStyle = AppSettings.CleanStyle(settings.ChartStyle),
            ChartCrosshair = settings.ChartCrosshair,
            PriceColor = TickerTextColor.Clamp(settings.PriceColor),
            ChangeUpColor = TickerTextColor.Clamp(settings.ChangeUpColor),
            ChangeDownColor = TickerTextColor.Clamp(settings.ChangeDownColor),
            BackgroundOpacityChosen = settings.BackgroundOpacityChosen,
            BackgroundColor = TickerTextColor.Parse(settings.BackgroundColor, PaperInk.Background),
            LiveScrollUpdates = settings.LiveScrollUpdates,
            AppearancePreset = AppearancePresets.Canonical(settings.AppearancePreset)
        };
        return JsonSerializer.Serialize(file, JsonFile.Options) + "\n";
    }

    public static DockEdge ParseDock(string? value)
        => string.Equals(value?.Trim(), "top", StringComparison.OrdinalIgnoreCase) ? DockEdge.Top : DockEdge.Bottom;

    public static ScrollSpeed ParseSpeed(string? value)
    {
        return value?.Trim().ToLowerInvariant() switch
        {
            "slow" => ScrollSpeed.Slow,
            "fast" => ScrollSpeed.Fast,
            _ => ScrollSpeed.Normal
        };
    }

    sealed class SettingsFile
    {
        public string? Dock { get; set; }
        public string? Speed { get; set; }
        public bool StartWithWindows { get; set; }
        public bool? LuxWirePosts { get; set; }
        public double? Opacity { get; set; }
        public bool? AlwaysOnTop { get; set; }
        public bool? ScrollEnabled { get; set; }
        public bool? ShowPercent { get; set; }
        public bool? CompactLayout { get; set; }
        public bool? ShowSparklines { get; set; }
        public bool? ClickThrough { get; set; }
        public bool? PinBtc { get; set; }
        public double? TextScale { get; set; }
        public bool? LuxWireDigest { get; set; }
        public int? LuxWirePollMinutes { get; set; }
        public string? MonitorDevice { get; set; }
        public int? VerticalOffsetDip { get; set; }
        public double? BackgroundOpacity { get; set; }
        public double? ScrollPixelsPerSecond { get; set; }
        public string? TickerFontName { get; set; }
        public double? SymbolGap { get; set; }
        public bool? ShowPre { get; set; }
        public bool? ShowAfterHours { get; set; }
        public bool? ScrollRight { get; set; }
        public bool? ScrollLoop { get; set; }
        public string? TextColor { get; set; }
        public string? ChartRange { get; set; }
        public string? ChartStyle { get; set; }
        public bool? ChartCrosshair { get; set; }
        public string? PriceColor { get; set; }
        public string? ChangeUpColor { get; set; }
        public string? ChangeDownColor { get; set; }
        public bool? BackgroundOpacityChosen { get; set; }
        public string? BackgroundColor { get; set; }
        public bool? LiveScrollUpdates { get; set; }
        public string? AppearancePreset { get; set; }
    }
}

public static class SettingsReset
{
    public const string Tickers = "Tickers";
    public const string Appearance = "Appearance";
    public const string Motion = "Motion";
    public const string Position = "Position";
    public const string Charts = "Charts";
    public const string About = "About";
    public const string Updates = "Updates";
    public const string LuxWire = "LuxWire";
    public const string Account = "Account";

    public static AppSettings Apply(AppSettings current, string? section)
    {
        var defaults = AppSettings.Default;
        if (string.IsNullOrEmpty(section))
            return defaults;
        return section switch
        {
            Tickers => current with { ShowPre = defaults.ShowPre, ShowAfterHours = defaults.ShowAfterHours, PinBtc = defaults.PinBtc },
            Appearance => current with
            {
                TickerFontName = defaults.TickerFontName,
                TextScale = defaults.TextScale,
                SymbolGap = defaults.SymbolGap,
                BackgroundOpacity = defaults.BackgroundOpacity,
                BackgroundOpacityChosen = true,
                TextColor = defaults.TextColor,
                PriceColor = defaults.PriceColor,
                ChangeUpColor = defaults.ChangeUpColor,
                ChangeDownColor = defaults.ChangeDownColor,
                BackgroundColor = defaults.BackgroundColor,
                AppearancePreset = defaults.AppearancePreset,
                ShowPercent = defaults.ShowPercent,
                ShowSparklines = defaults.ShowSparklines,
                CompactLayout = defaults.CompactLayout
            },
            Motion => current with
            {
                ScrollEnabled = defaults.ScrollEnabled,
                Speed = defaults.Speed,
                ScrollPixelsPerSecond = defaults.ScrollPixelsPerSecond,
                ScrollRight = defaults.ScrollRight,
                ScrollLoop = defaults.ScrollLoop,
                LiveScrollUpdates = defaults.LiveScrollUpdates
            },
            Position => current with
            {
                Dock = defaults.Dock,
                VerticalOffsetDip = defaults.VerticalOffsetDip,
                AlwaysOnTop = defaults.AlwaysOnTop,
                ClickThrough = defaults.ClickThrough,
                MonitorDevice = defaults.MonitorDevice,
                StartWithWindows = defaults.StartWithWindows
            },
            LuxWire => current with
            {
                LuxWirePosts = defaults.LuxWirePosts,
                LuxWireDigest = defaults.LuxWireDigest,
                LuxWirePollMinutes = defaults.LuxWirePollMinutes
            },
            Charts => current with
            {
                ChartRange = defaults.ChartRange,
                ChartStyle = defaults.ChartStyle,
                ChartCrosshair = defaults.ChartCrosshair
            },
            About => current,
            Updates => current,
            Account => current,
            _ => current
        };
    }
}

public static class SettingsSearch
{
    public static readonly string[] Sections = [SettingsReset.Tickers, SettingsReset.Appearance, SettingsReset.Motion, "Position & Dock", SettingsReset.Charts, SettingsReset.LuxWire, SettingsReset.Account, SettingsReset.Updates, SettingsReset.About];

    public static readonly (string Section, string Name)[] Entries =
    [
        ("Tickers", "Add ticker"),
        ("Tickers", "Symbol order"),
        ("Tickers", "PRE"),
        ("Tickers", "After hours"),
        ("Tickers", "Keep BTC fixed"),
        ("Tickers", "Price alerts"),
        ("Tickers", "Refresh"),
        ("Appearance", "Font"),
        ("Appearance", "Font size"),
        ("Appearance", "Symbol color"),
        ("Appearance", "Price color"),
        ("Appearance", "Change up"),
        ("Appearance", "Change down"),
        ("Appearance", "Spacing"),
        ("Appearance", "Ticker look"),
        ("Appearance", "Background color"),
        ("Appearance", "Opacity"),
        ("Appearance", "Percent"),
        ("Appearance", "Sparklines"),
        ("Appearance", "Compact"),
        ("Motion", "Scroll"),
        ("Motion", "Live updates"),
        ("Motion", "Speed"),
        ("Motion", "Direction"),
        ("Motion", "Loop"),
        ("Position & Dock", "Always on top"),
        ("Position & Dock", "Above the taskbar"),
        ("Position & Dock", "Top of the screen"),
        ("Position & Dock", "Vertical position"),
        ("Position & Dock", "Monitor"),
        ("Position & Dock", "Click-through"),
        ("Position & Dock", "Start with Windows"),
        ("Charts", "Open chart"),
        ("Charts", "Default range"),
        ("Charts", "Line"),
        ("Charts", "Candles"),
        ("Charts", "Crosshair"),
        ("LuxWire", "LuxWire post alerts"),
        ("LuxWire", "Poll"),
        ("Account", "Create account"),
        ("Account", "Sign in"),
        ("Account", "Forgot password"),
        ("Account", "Sign out"),
        ("Account", "Delete account"),
        ("Account", "Username"),
        ("Account", "Continue with Google"),
        ("Account", "Continue with X"),
        ("Account", "Privacy"),
        ("Account", "Terms"),
        ("Updates", "What's new"),
        ("Updates", "Check for updates"),
        ("About", "Follow on X"),
        ("About", "Subscribe"),
        ("About", "Privacy"),
        ("About", "Terms")
    ];

    public static IReadOnlyList<(string Section, string Name)> Filter(string? query)
    {
        if (string.IsNullOrWhiteSpace(query))
            return Entries;
        var text = query.Trim();
        return Entries.Where(entry =>
            entry.Name.Contains(text, StringComparison.OrdinalIgnoreCase)
            || entry.Section.Contains(text, StringComparison.OrdinalIgnoreCase)).ToArray();
    }
}
