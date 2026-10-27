using System.Globalization;
using System.Text.Json;

namespace LuxTicker;

public enum BtcChartPreset
{
    Hour,
    Day,
    Week,
    Month,
    SixMonths,
    YearToDate,
    Year,
    All,
    Custom
}

public enum BtcChartStyle
{
    Line,
    Candles
}

public readonly record struct BtcPoint(DateTimeOffset Time, decimal Close)
{
    public decimal Open { get; init; }
    public decimal High { get; init; }
    public decimal Low { get; init; }
}

public readonly record struct BtcOhlc(decimal Open, decimal High, decimal Low, decimal Close);

public sealed record BtcSeries(IReadOnlyList<BtcPoint> Points, decimal Last, decimal? ChangePercent);

/// <summary>
/// BTC/USD chart ranges against Yahoo's public chart. No API key.
/// ALL starts at Yahoo's first BTC-USD print, 17 Sep 2014.
/// 1H is the last 60 minutes. Candles use 1-minute bars for that hour,
/// 5-minute bars for a day, then hourly or daily bars.
/// </summary>
public static class BtcChart
{
    public const string Pair = "BTC/USD";
    public const string Symbol = "BTC-USD";
    public const string Paper = PaperInk.Background;
    public const string SeriesColor = PaperInk.Accent;
    public const string CandleUp = "#1B7A3A";
    public const string CandleDown = "#C0392B";
    public const BtcChartPreset DefaultPreset = BtcChartPreset.Month;
    public static readonly DateTimeOffset Earliest = new(2014, 9, 17, 0, 0, 0, TimeSpan.Zero);

    public static IReadOnlyList<BtcChartPreset> Presets { get; } =
    [
        BtcChartPreset.Hour,
        BtcChartPreset.Day,
        BtcChartPreset.Week,
        BtcChartPreset.Month,
        BtcChartPreset.SixMonths,
        BtcChartPreset.YearToDate,
        BtcChartPreset.Year,
        BtcChartPreset.All,
        BtcChartPreset.Custom
    ];

    public static string PresetLabel(BtcChartPreset preset) => preset switch
    {
        BtcChartPreset.Hour => "1H",
        BtcChartPreset.Day => "1D",
        BtcChartPreset.Week => "1W",
        BtcChartPreset.Month => "1M",
        BtcChartPreset.SixMonths => "6M",
        BtcChartPreset.YearToDate => "YTD",
        BtcChartPreset.Year => "1Y",
        BtcChartPreset.All => "ALL",
        _ => "Custom"
    };

    public static bool TryRange(
        BtcChartPreset preset,
        DateTimeOffset now,
        DateOnly? from,
        DateOnly? to,
        out DateTimeOffset start,
        out DateTimeOffset end,
        out string error,
        DateTimeOffset? historyStarts = null)
    {
        end = now;
        error = "";
        var earliest = historyStarts ?? Earliest;
        if (preset == BtcChartPreset.Custom)
        {
            if (from == null || to == null)
            {
                start = default;
                error = "Choose a From date and a To date.";
                return false;
            }

            var earliestDay = DateOnly.FromDateTime(earliest.UtcDateTime);
            var today = EasternDate(now);
            if (from.Value < earliestDay || to.Value < earliestDay)
            {
                start = default;
                error = earliest == Earliest
                    ? "Bitcoin prices start on 17 Sep 2014."
                    : "That date is before this symbol's history.";
                return false;
            }

            if (from.Value > today || to.Value > today)
            {
                start = default;
                error = "Dates cannot be in the future.";
                return false;
            }

            if (to.Value < from.Value)
            {
                start = default;
                error = "To must be on or after From.";
                return false;
            }

            start = from.Value == earliestDay
                ? earliest
                : EasternMidnight(from.Value);
            end = to.Value == today ? now : EasternEnd(to.Value);
            return true;
        }

        start = preset switch
        {
            BtcChartPreset.Hour => now.AddMinutes(-60),
            BtcChartPreset.Day => now.AddDays(-1),
            BtcChartPreset.Week => now.AddDays(-7),
            BtcChartPreset.Month => now.AddDays(-30),
            BtcChartPreset.SixMonths => now.AddMonths(-6),
            BtcChartPreset.YearToDate => YearStart(now),
            BtcChartPreset.Year => now.AddDays(-365),
            BtcChartPreset.All => earliest,
            _ => now.AddDays(-30)
        };
        if (start < earliest)
            start = earliest;
        if (end < start)
            end = start;
        return true;
    }

    public static string Interval(DateTimeOffset start, DateTimeOffset end)
    {
        var span = end - start;
        if (span <= TimeSpan.FromMinutes(90))
            return "1m";
        if (span <= TimeSpan.FromDays(2))
            return "5m";
        if (span <= TimeSpan.FromDays(60))
            return "1h";
        return "1d";
    }

    /// <summary>
    /// Bar size for candlesticks. 1H is 1-minute, 1D is 5-minute (inside 5–15),
    /// and longer ranges are hourly through 60 days, then daily.
    /// </summary>
    public static string CandleInterval(DateTimeOffset start, DateTimeOffset end)
    {
        var span = end - start;
        if (span <= TimeSpan.FromHours(2))
            return "1m";
        if (span <= TimeSpan.FromDays(2))
            return "5m";
        if (span <= TimeSpan.FromDays(60))
            return "1h";
        return "1d";
    }

    public static string IntervalFor(DateTimeOffset start, DateTimeOffset end, BtcChartStyle style)
        => style == BtcChartStyle.Candles ? CandleInterval(start, end) : Interval(start, end);

    public static TimeSpan RefreshInterval(DateTimeOffset start, DateTimeOffset end)
    {
        var days = (end - start).TotalDays;
        if (days <= 8)
            return TimeSpan.FromSeconds(60);
        if (days <= 200)
            return TimeSpan.FromMinutes(5);
        return TimeSpan.FromMinutes(15);
    }

    public static string ChartUrl(DateTimeOffset start, DateTimeOffset end, BtcChartStyle style = BtcChartStyle.Line)
        => ChartUrl(Symbol, start, end, style);

    public static string ChartUrl(string yahooSymbol, DateTimeOffset start, DateTimeOffset end, BtcChartStyle style = BtcChartStyle.Line)
    {
        if (end <= start)
            end = start.AddMinutes(5);
        var from = start.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture);
        var to = end.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture);
        var symbol = string.IsNullOrWhiteSpace(yahooSymbol) ? Symbol : yahooSymbol.Trim();
        return "https://query1.finance.yahoo.com/v8/finance/chart/"
            + Uri.EscapeDataString(symbol)
            + "?period1=" + from
            + "&period2=" + to
            + "&interval=" + IntervalFor(start, end, style)
            + "&includePrePost=false";
    }

    public static BtcChartPreset ParsePreset(string? label)
    {
        foreach (var preset in Presets)
        {
            if (preset == BtcChartPreset.Custom)
                continue;
            if (PresetLabel(preset).Equals(label?.Trim(), StringComparison.OrdinalIgnoreCase))
                return preset;
        }

        return DefaultPreset;
    }

    public static DateTimeOffset HistoryStart(string? yahooSymbol)
        => string.Equals(yahooSymbol?.Trim(), Symbol, StringComparison.OrdinalIgnoreCase) ? Earliest : new DateTimeOffset(1970, 1, 2, 0, 0, 0, TimeSpan.Zero);

    public static bool TryParse(string? json, out BtcSeries? series, out string? failure)
    {
        series = null;
        failure = null;
        if (string.IsNullOrWhiteSpace(json))
        {
            failure = "empty";
            return false;
        }

        try
        {
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("chart", out var chart))
            {
                failure = "missing chart";
                return false;
            }

            if (chart.TryGetProperty("error", out var err) && err.ValueKind == JsonValueKind.Object)
            {
                failure = err.TryGetProperty("description", out var description) && description.ValueKind == JsonValueKind.String
                    ? description.GetString() ?? "chart error"
                    : "chart error";
                return false;
            }

            if (!chart.TryGetProperty("result", out var result) || result.ValueKind != JsonValueKind.Array || result.GetArrayLength() == 0)
            {
                failure = "no result";
                return false;
            }

            var first = result[0];
            if (!first.TryGetProperty("timestamp", out var stamps) || stamps.ValueKind != JsonValueKind.Array
                || !first.TryGetProperty("indicators", out var indicators)
                || !indicators.TryGetProperty("quote", out var quoteArr)
                || quoteArr.ValueKind != JsonValueKind.Array
                || quoteArr.GetArrayLength() == 0
                || !quoteArr[0].TryGetProperty("close", out var closes)
                || closes.ValueKind != JsonValueKind.Array)
            {
                failure = "missing prices";
                return false;
            }

            var quote = quoteArr[0];
            var hasOpen = quote.TryGetProperty("open", out var opens) && opens.ValueKind == JsonValueKind.Array;
            var hasHigh = quote.TryGetProperty("high", out var highs) && highs.ValueKind == JsonValueKind.Array;
            var hasLow = quote.TryGetProperty("low", out var lows) && lows.ValueKind == JsonValueKind.Array;
            var points = new Dictionary<long, BtcPoint>();
            var n = Math.Min(stamps.GetArrayLength(), closes.GetArrayLength());
            for (int i = 0; i < n; i++)
            {
                if (!stamps[i].TryGetInt64(out var unix))
                    continue;
                var close = ReadDecimal(closes[i]);
                if (close is not > 0)
                    continue;
                var open = (hasOpen ? ReadAt(opens, i) : null) ?? close.Value;
                if (open <= 0)
                    open = close.Value;
                var high = (hasHigh ? ReadAt(highs, i) : null) ?? Math.Max(open, close.Value);
                var low = (hasLow ? ReadAt(lows, i) : null) ?? Math.Min(open, close.Value);
                if (high < low)
                    (high, low) = (low, high);
                high = Math.Max(high, Math.Max(open, close.Value));
                low = Math.Min(low, Math.Min(open, close.Value));
                if (low <= 0)
                    low = Math.Min(open, close.Value);
                points[unix] = new BtcPoint(DateTimeOffset.FromUnixTimeSeconds(unix), close.Value)
                {
                    Open = open,
                    High = high,
                    Low = low
                };
            }

            if (points.Count == 0)
                return true;

            var ordered = points
                .OrderBy(pair => pair.Key)
                .Select(pair => pair.Value)
                .ToList();
            var firstClose = ordered[0].Close;
            var last = ordered[^1].Close;
            decimal? change = firstClose == 0 ? null : (last - firstClose) / firstClose * 100m;
            series = new BtcSeries(ordered, last, change);
            return true;
        }
        catch (JsonException)
        {
            failure = "not json";
            return false;
        }
    }

    /// <summary>
    /// Maps a 0..1 fraction of the plot (first point time through last point time) to the nearest sample.
    /// An empty series returns -1. One point returns 0. The fraction is clamped.
    /// </summary>
    public static int NearestIndex(IReadOnlyList<BtcPoint> points, double fraction)
    {
        if (points.Count == 0)
            return -1;
        if (points.Count == 1)
            return 0;
        if (double.IsNaN(fraction))
            fraction = 0;
        if (fraction < 0)
            fraction = 0;
        if (fraction > 1)
            fraction = 1;

        var start = points[0].Time;
        var end = points[^1].Time;
        var span = end - start;
        if (span <= TimeSpan.Zero)
            return 0;

        var target = start + TimeSpan.FromTicks((long)Math.Round(span.Ticks * fraction));
        int lo = 0;
        int hi = points.Count - 1;
        while (lo <= hi)
        {
            int mid = lo + ((hi - lo) / 2);
            var cmp = points[mid].Time.CompareTo(target);
            if (cmp == 0)
                return mid;
            if (cmp < 0)
                lo = mid + 1;
            else
                hi = mid - 1;
        }

        if (hi < 0)
            return 0;
        if (lo >= points.Count)
            return points.Count - 1;
        var before = target - points[hi].Time;
        var after = points[lo].Time - target;
        return before <= after ? hi : lo;
    }

    public static string ScrubPrice(decimal close) => "$" + PriceFormat.Format(close, "USD");

    public static BtcOhlc Ohlc(BtcPoint point)
    {
        var close = point.Close;
        var open = point.Open > 0 ? point.Open : close;
        var high = point.High > 0 ? point.High : Math.Max(open, close);
        var low = point.Low > 0 ? point.Low : Math.Min(open, close);
        high = Math.Max(high, Math.Max(open, close));
        if (low <= 0)
            low = Math.Min(open, close);
        low = Math.Min(low, Math.Min(open, close));
        return new BtcOhlc(open, high, low, close);
    }

    public static string ScrubOhlc(BtcPoint point)
    {
        var bar = Ohlc(point);
        return "O " + ScrubPrice(bar.Open) + "   H " + ScrubPrice(bar.High)
            + "\nL " + ScrubPrice(bar.Low) + "   C " + ScrubPrice(bar.Close);
    }

    public static string ScrubStamp(BtcPoint point)
    {
        var eastern = TimeZoneInfo.ConvertTime(point.Time, EasternTime.Zone);
        return eastern.ToString("d MMM yyyy, HH:mm", CultureInfo.InvariantCulture) + " ET";
    }

    public static string Caption(BtcChartPreset preset, DateTimeOffset start, DateTimeOffset end, DateOnly? from, DateOnly? to)
    {
        if (preset == BtcChartPreset.Custom && from != null && to != null)
        {
            return from.Value.ToString("d MMM yyyy", CultureInfo.InvariantCulture)
                + " – " + to.Value.ToString("d MMM yyyy", CultureInfo.InvariantCulture)
                + " ET";
        }

        var zone = EasternTime.Zone;
        var a = TimeZoneInfo.ConvertTime(start, zone);
        var b = TimeZoneInfo.ConvertTime(end, zone);
        var pattern = (end - start).TotalDays <= 2 ? "d MMM yyyy HH:mm" : "d MMM yyyy";
        return a.ToString(pattern, CultureInfo.InvariantCulture)
            + " – " + b.ToString(pattern, CultureInfo.InvariantCulture)
            + " ET";
    }

    public static DateOnly EasternDate(DateTimeOffset instant)
        => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(instant, EasternTime.Zone).Date);

    static DateTimeOffset YearStart(DateTimeOffset now)
    {
        var eastern = TimeZoneInfo.ConvertTime(now, EasternTime.Zone);
        return EasternMidnight(new DateOnly(eastern.Year, 1, 1));
    }

    static DateTimeOffset EasternMidnight(DateOnly date)
    {
        var local = date.ToDateTime(TimeOnly.MinValue);
        return new DateTimeOffset(local, EasternTime.Zone.GetUtcOffset(local));
    }

    static DateTimeOffset EasternEnd(DateOnly date)
    {
        var local = date.ToDateTime(new TimeOnly(23, 59, 59));
        return new DateTimeOffset(local, EasternTime.Zone.GetUtcOffset(local));
    }

    static decimal? ReadDecimal(JsonElement el)
    {
        if (el.ValueKind == JsonValueKind.Number)
        {
            if (el.TryGetDecimal(out var value))
                return value;
            return decimal.TryParse(el.GetRawText(), NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
                ? parsed
                : null;
        }

        if (el.ValueKind == JsonValueKind.String
            && decimal.TryParse(el.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var text))
            return text;
        return null;
    }

    static decimal? ReadAt(JsonElement array, int index)
        => index < array.GetArrayLength() ? ReadDecimal(array[index]) : null;
}
