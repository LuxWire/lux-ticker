namespace LuxTicker;

/// <summary>One named ticker look: board plus symbol, price, and change colors.</summary>
public sealed record TapeLook(string Name, string Background, string Symbol, string Price, string Up, string Down);

/// <summary>
/// Exchange-style tapes. Wall Street is the default on a new install.
/// A settings file that never named a look keeps the colors it already stored.
/// </summary>
public static class AppearancePresets
{
    public const string WallStreet = "Wall Street";
    public const string PaperName = "Lux Wire paper";
    public const string Custom = "Custom";

    public const string WallBackground = "#07110C";
    public const string WallSymbol = "#F0C14A";
    public const string WallPrice = "#B6FF4A";
    public const string WallUp = "#39F26A";
    public const string WallDown = "#FF4B4B";

    public static TapeLook DefaultLook { get; } = new(WallStreet, WallBackground, WallSymbol, WallPrice, WallUp, WallDown);

    public static IReadOnlyList<TapeLook> All { get; } =
    [
        DefaultLook,
        new("NYSE", "#002855", "#FFFFFF", "#F5C518", "#00A651", "#E10600"),
        new("Nasdaq", "#000000", "#00A9E0", "#7CFF4A", "#39FF6A", "#FF3355"),
        new("Cboe", "#1A1A1A", "#FFFFFF", "#F4F4F4", "#00A36C", "#D71920"),
        new("NYSE American", "#123A63", "#EAF3FF", "#9DCCFF", "#3DDC97", "#FF5A5A"),
        new("NYSE Arca", "#06281A", "#E7FFF3", "#5CFFB1", "#2EE59D", "#FF6B6B"),
        new("IEX", "#000000", "#FFD100", "#FFFFFF", "#3DDC67", "#FF4545"),
        new("MIAX", "#041E42", "#00B5E2", "#E8F7FF", "#2ECC71", "#FF5A6A"),
        new("MEMX", "#140818", "#FFFFFF", "#E58CFF", "#3DDC67", "#FF5C8A"),
        new(PaperName, PaperInk.Background, TickerTextColor.Default, TickerTextColor.PriceDefault, TickerTextColor.UpDefault, TickerTextColor.DownDefault)
    ];

    public static bool IsLegacyFile(string? appearancePreset) => string.IsNullOrWhiteSpace(appearancePreset);

    public static TapeLook? Find(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return null;
        foreach (var look in All)
        {
            if (string.Equals(look.Name, name.Trim(), StringComparison.OrdinalIgnoreCase))
                return look;
        }

        return null;
    }

    public static string Canonical(string? name) => Find(name)?.Name ?? Custom;

    public static string Match(string background, string symbol, string price, string up, string down)
    {
        foreach (var look in All)
        {
            if (Same(look.Background, background)
                && Same(look.Symbol, symbol)
                && Same(look.Price, price)
                && Same(look.Up, up)
                && Same(look.Down, down))
                return look.Name;
        }

        return Custom;
    }

    public static string BackgroundFor(string? stored, bool legacy)
        => TickerTextColor.Parse(stored, legacy ? PaperInk.Background : WallBackground);

    public static string SymbolFor(string? stored, bool legacy)
    {
        if (string.IsNullOrWhiteSpace(stored))
            return legacy ? TickerTextColor.Default : WallSymbol;
        return TickerTextColor.Clamp(stored);
    }

    public static string PriceFor(string? stored, bool legacy)
    {
        if (string.IsNullOrWhiteSpace(stored))
            return legacy ? TickerTextColor.PriceDefault : WallPrice;
        return TickerTextColor.Clamp(stored);
    }

    public static string UpFor(string? stored, bool legacy)
    {
        if (string.IsNullOrWhiteSpace(stored))
            return legacy ? TickerTextColor.UpDefault : WallUp;
        return TickerTextColor.Clamp(stored);
    }

    public static string DownFor(string? stored, bool legacy)
    {
        if (string.IsNullOrWhiteSpace(stored))
            return legacy ? TickerTextColor.DownDefault : WallDown;
        return TickerTextColor.Clamp(stored);
    }

    public static AppSettings Apply(AppSettings current, TapeLook look) => current with
    {
        BackgroundColor = look.Background,
        TextColor = look.Symbol,
        PriceColor = look.Price,
        ChangeUpColor = look.Up,
        ChangeDownColor = look.Down,
        AppearancePreset = look.Name
    };

    static bool Same(string left, string right)
        => string.Equals(TickerTextColor.Parse(left, left), TickerTextColor.Parse(right, right), StringComparison.OrdinalIgnoreCase);
}
