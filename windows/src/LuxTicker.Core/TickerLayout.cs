namespace LuxTicker;

public enum TextRole
{
    Symbol,
    Tag,
    Price,
    Change
}

public readonly record struct TextRun(string Text, string Color, TextRole Role);

public readonly record struct DisplayItem(
    string Label,
    IReadOnlyList<TextRun> Runs,
    bool Stale,
    string Symbol = "",
    IReadOnlyList<decimal>? Spark = null,
    decimal? TapePrice = null);

/// <summary>Uptick and downtick while the tape is moving. 1 is up, -1 is down.</summary>
public static class TapeFlash
{
    public const int HoldMilliseconds = 450;

    public static int Direction(decimal? previous, decimal? next)
    {
        if (previous is not decimal from || next is not decimal to)
            return 0;
        if (to > from)
            return 1;
        if (to < from)
            return -1;
        return 0;
    }
}

/// <summary>Snaps ticker type to whole device pixels so ClearType stays sharp while the strip scrolls.</summary>
public static class TickerPixels
{
    public static double SnapDip(double dip, double dpi)
    {
        if (dpi < 1)
            dpi = 96;
        return Math.Round(dip * dpi / 96.0) * 96.0 / dpi;
    }

    public static double FontDip(double barDip, double scale, double dpi)
    {
        if (scale < 0.5)
            scale = 1;
        var px = Math.Max(11, Math.Round(Math.Max(1, barDip) * dpi / 96.0 * 0.72 * scale));
        if (dpi < 1)
            dpi = 96;
        return px * 96.0 / dpi;
    }
}

public static class QuoteLinks
{
    public static string For(string symbol, SymbolKind kind)
    {
        var name = string.IsNullOrWhiteSpace(symbol) ? "" : symbol.Trim();
        if (kind == SymbolKind.Crypto)
            return "https://www.tradingview.com/symbols/COINBASE-" + Uri.EscapeDataString(name) + "USD/";
        return "https://finance.yahoo.com/quote/" + Uri.EscapeDataString(name);
    }
}

/// <summary>Lux Wire article paper. Hex values are exact. Do not substitute a system cream or accent.</summary>
public static class PaperInk
{
    public const string Background = "#F6F2E8";
    public const string Panel = "#EFEADE";
    public const string Ink = "#282622";
    public const string Body = "#161412";
    public const string Sub = "#55555A";
    public const string Muted = "#78787D";
    public const string Rule = "#D7D7DA";
    public const string Accent = "#F7931A";
    public const string AccentDeep = "#C2410C";

    public static string For(string hex)
    {
        return hex.ToUpperInvariant() switch
        {
            "#FFB000" => Accent,
            "#FFFFFF" => Body,
            "#3DDC84" => "#1B7A3A",
            "#FF4D4D" => "#C0392B",
            "#A0A0A0" => Muted,
            "#C8C8C8" => Sub,
            "#8A8A8A" => Muted,
            _ => hex
        };
    }

    public static (byte R, byte G, byte B) Rgb(string hex)
    {
        var text = hex.Trim();
        if (text.StartsWith('#'))
            text = text[1..];
        return (
            Convert.ToByte(text[..2], 16),
            Convert.ToByte(text[2..4], 16),
            Convert.ToByte(text[4..6], 16));
    }

    /// <summary>Windows COLORREF, 0x00BBGGRR, so the caption cannot swap channels.</summary>
    public static int ColorRef(string hex)
    {
        var (r, g, b) = Rgb(hex);
        return r | (g << 8) | (b << 16);
    }
}

public static class TickerPalette
{
    public const string Amber = "#FFB000";
    public const string White = "#FFFFFF";
    public const string Green = "#3DDC84";
    public const string Red = "#FF4D4D";
    public const string Flat = "#A0A0A0";
    public const string Tag = "#C8C8C8";
    public const string Dot = "#8A8A8A";
    public const string Black = "#000000";
    public const double StaleOpacity = 0.42;
}

/// <summary>
/// Scroll step. A long gap is capped instead of dropped, so a hitch still moves the strip.
/// </summary>
public static class TickerMotion
{
    public const double MaxStepSeconds = 0.05;

    public static double Advance(double offset, double loopWidth, double elapsedSeconds, double pixelsPerSecond, double dpi, bool loop = true, bool toTheRight = false)
    {
        if (loopWidth <= 1 || elapsedSeconds <= 0 || pixelsPerSecond <= 0)
            return offset;
        if (double.IsNaN(offset) || offset < 0)
            offset = 0;
        var step = elapsedSeconds > MaxStepSeconds ? MaxStepSeconds : elapsedSeconds;
        if (dpi < 1)
            dpi = 96;
        var delta = pixelsPerSecond / (dpi / 96.0) * step;
        if (toTheRight)
            delta = -delta;
        var next = offset + delta;
        if (!loop)
        {
            if (next < 0)
                return 0;
            if (next > loopWidth)
                return loopWidth;
            return next;
        }

        if (next >= loopWidth || next < 0)
            next %= loopWidth;
        if (next < 0)
            next += loopWidth;
        return next;
    }
}

/// <summary>Show Desktop and Win+M try to hide the bar. The saved rect stays.</summary>
public static class DeskBand
{
    public const long ToolWindow = 0x80;
    public const long Transparent = 0x20;
    public const long Layered = 0x80000;
    public const long AppWindow = 0x00040000;
    public const long NoActivate = 0x08000000;
    public const uint SwpHideWindow = 0x0080;
    public const uint SwpShowWindow = 0x0040;

    public static long Compose(long style, bool clickThrough)
    {
        // Click-through is decided per pixel in WM_NCHITTEST. WS_EX_TRANSPARENT
        // would make Chart and Settings pass clicks through as well.
        _ = clickThrough;
        style |= ToolWindow | NoActivate;
        style &= ~Layered;
        style &= ~AppWindow;
        style &= ~Transparent;
        return style;
    }

    public readonly record struct Placement(uint Flags, int X, int Y, int Cx, int Cy, bool RefusedHide);

    public static Placement Lock(uint flags, int x, int y, int cx, int cy, int left, int top, int width, int height)
    {
        var refused = (flags & SwpHideWindow) != 0;
        flags &= ~SwpHideWindow;
        flags |= SwpShowWindow;
        return new Placement(flags, left, top, Math.Max(1, width), Math.Max(1, height), refused);
    }
}

/// <summary>Always-on-top uses HWND_TOPMOST. The taskbar overlap still does, even if the toggle is off.</summary>
public static class ZOrder
{
    public static int InsertAfter(bool alwaysOnTop, bool overTaskbar)
        => alwaysOnTop || overTaskbar ? -1 : 0;
}

/// <summary>Screen point vs the Chart/Settings strip. HTCLIENT stays clickable; the tape returns HTTRANSPARENT.</summary>
public static class ClickThroughHit
{
    public const int HtClient = 1;
    public const int HtTransparent = -1;

    public static int Code(bool clickThrough, bool overButtons)
    {
        if (!clickThrough)
            return HtClient;
        return overButtons ? HtClient : HtTransparent;
    }

    public static bool Contains(int x, int y, int left, int top, int width, int height)
        => width > 0 && height > 0 && x >= left && y >= top && x < left + width && y < top + height;
}

public static class TickerTextColor
{
    public const string Default = "#CC5500";
    public const string PriceDefault = "#161412";
    public const string UpDefault = "#1B7A3A";
    public const string DownDefault = "#C0392B";

    public static string Clamp(string? hex)
    {
        if (string.IsNullOrWhiteSpace(hex))
            return Default;
        var text = hex.Trim();
        if (text.Length == 6)
            text = "#" + text;
        if (text.Length != 7 || text[0] != '#')
            return Default;
        for (int i = 1; i < 7; i++)
        {
            var c = text[i];
            var hexDigit = c is >= '0' and <= '9' or >= 'a' and <= 'f' or >= 'A' and <= 'F';
            if (!hexDigit)
                return Default;
        }

        return text.ToUpperInvariant();
    }

    /// <summary>A real #RRGGBB stays. Anything else uses the fallback, not the symbol default.</summary>
    public static string Parse(string? hex, string fallback)
    {
        if (string.IsNullOrWhiteSpace(hex))
            return fallback;
        var text = hex.Trim();
        if (text.Length == 6)
            text = "#" + text;
        var clamped = Clamp(text);
        return text.Length == 7 && string.Equals(clamped, text, StringComparison.OrdinalIgnoreCase) ? clamped : fallback;
    }
}

/// <summary>Gap between ticker symbols. Scale 1 is the 1.4.4 spacing.</summary>
public static class TickerSpacing
{
    public const double Default = 1;
    public const double Min = 0;
    public const double Max = 3;

    public static double Clamp(double scale)
    {
        if (double.IsNaN(scale) || double.IsInfinity(scale))
            return Default;
        return Math.Clamp(scale, Min, Max);
    }

    public static double GapDip(double fontDip, double scale, double dpi)
        => TickerPixels.SnapDip(Math.Max(8, fontDip * 0.55) * Clamp(scale), dpi);
}

public static class BarSurfaceMath
{
    public const double LegacyDefaultOpacity = 0.5;
    public const double DefaultOpacity = 1;

    /// <summary>
    /// 1.4.5–1.4.7 saved 0.5 for everyone who never moved the slider.
    /// That value becomes fully opaque. Any other saved value stays.
    /// A slider the user moved in 1.4.8 is marked chosen, including an explicit 50%.
    /// </summary>
    public static double Migrate(double? stored, bool chosen)
    {
        if (chosen)
            return Clamp(stored ?? DefaultOpacity);
        if (stored == null || Math.Abs(stored.Value - LegacyDefaultOpacity) < 0.0000001)
            return DefaultOpacity;
        return Clamp(stored.Value);
    }

    public static double Clamp(double opacity)
    {
        if (double.IsNaN(opacity) || double.IsInfinity(opacity))
            return DefaultOpacity;
        return Math.Clamp(opacity, 0, 1);
    }

    public static byte Alpha(double opacity)
        => (byte)Math.Round(Clamp(opacity) * 255);
}

/// <summary>Which icon frame matches a DPI, and when a stuck fullscreen hide should end.</summary>
public static class IconMetrics
{
    public static readonly int[] Frames = [16, 20, 24, 32, 40, 48, 64, 256];

    public static readonly int[] TrayFrames = [16, 20, 24, 32, 40, 48];

    public static int ForDpi(int dpi)
    {
        if (dpi < 96)
            dpi = 96;
        return Closest((int)Math.Round(16.0 * dpi / 96.0));
    }

    /// <summary>Tray frames stop at 48. The exe icon still has 64 and 256.</summary>
    public static int ForTrayDpi(int dpi)
    {
        if (dpi < 96)
            dpi = 96;
        var px = (int)Math.Round(16.0 * dpi / 96.0);
        var best = TrayFrames[0];
        var delta = Math.Abs(best - px);
        foreach (var frame in TrayFrames)
        {
            var next = Math.Abs(frame - px);
            if (next < delta || (next == delta && frame > best))
            {
                best = frame;
                delta = next;
            }
        }

        return best;
    }

    public static int Closest(int px)
    {
        var best = Frames[0];
        var delta = Math.Abs(best - px);
        foreach (var frame in Frames)
        {
            var next = Math.Abs(frame - px);
            if (next < delta || (next == delta && frame > best))
            {
                best = frame;
                delta = next;
            }
        }

        return best;
    }
}

public static class BarRecovery
{
    public const double FullscreenCover = 0.92;

    public static bool Covers(PxRect monitor, PxRect window)
    {
        if (monitor.Width <= 0 || monitor.Height <= 0 || window.Width <= 0 || window.Height <= 0)
            return false;
        long left = Math.Max(monitor.X, window.X);
        long top = Math.Max(monitor.Y, window.Y);
        long right = Math.Min(monitor.Right, window.Right);
        long bottom = Math.Min(monitor.Bottom, window.Bottom);
        var overlap = Math.Max(0, right - left) * Math.Max(0, bottom - top);
        var area = (long)monitor.Width * monitor.Height;
        return overlap >= area * FullscreenCover;
    }

    public static bool StuckFullscreen(bool fullscreenFlag, bool foregroundCovers)
        => fullscreenFlag && !foregroundCovers;
}

public static class TickerMetrics
{
    public static double FontSizeForBar(double barPx) => Math.Max(8, Math.Floor(barPx * 0.70));
    public static double DotSize(double barPx) => Math.Max(2, Math.Round(barPx * 0.13));
}

public static class QuotePresenter
{
    public static DisplayItem Present(
        SymbolEntry entry,
        QuoteSnapshot? snap,
        SessionPhase phase,
        bool showPercent = true,
        IReadOnlyList<decimal>? spark = null,
        bool showPre = true,
        bool showAfterHours = false,
        string? textColor = null,
        string? priceColor = null,
        string? changeUp = null,
        string? changeDown = null)
    {
        var tag = MarketSession.TagFor(phase, entry.Kind);
        var price = snap?.PriceFor(phase, entry.Kind);
        var currency = string.IsNullOrWhiteSpace(snap?.Currency) ? entry.Currency : snap!.Currency!;
        var priceText = price == null ? "—" : PriceFormat.Format(price.Value, currency);
        var symbolInk = TickerTextColor.Clamp(textColor);
        var priceInk = string.IsNullOrWhiteSpace(priceColor) ? TickerTextColor.PriceDefault : TickerTextColor.Clamp(priceColor);
        var upInk = string.IsNullOrWhiteSpace(changeUp) ? TickerTextColor.UpDefault : TickerTextColor.Clamp(changeUp);
        var downInk = string.IsNullOrWhiteSpace(changeDown) ? TickerTextColor.DownDefault : TickerTextColor.Clamp(changeDown);

        string? change = null;
        var changeColor = PaperInk.For(TickerPalette.Flat);
        if (price != null && snap?.PreviousClose is decimal prev && prev != 0)
        {
            var pct = (price.Value - prev) / prev * 100m;
            change = PriceFormat.FormatChange(pct);
            if (pct > 0)
                changeColor = upInk;
            else if (pct < 0)
                changeColor = downInk;
        }

        var runs = new List<TextRun> { new(entry.DisplayLabel, symbolInk, TextRole.Symbol) };
        if (showPre && tag == SessionTag.Pre)
            runs.Add(new TextRun("PRE", PaperInk.For(TickerPalette.Tag), TextRole.Tag));
        if (showAfterHours && tag == SessionTag.Ah)
            runs.Add(new TextRun("AH", PaperInk.For(TickerPalette.Tag), TextRole.Tag));
        runs.Add(new TextRun(priceText, price == null ? PaperInk.For(TickerPalette.Flat) : priceInk, TextRole.Price));
        if (showPercent && change != null)
            runs.Add(new TextRun(change, changeColor, TextRole.Change));

        var stale = snap == null || snap.Stale || price == null;
        return new DisplayItem(entry.DisplayLabel, runs, stale, entry.Symbol, spark, price);
    }
}
