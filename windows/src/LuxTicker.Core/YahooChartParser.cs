using System.Globalization;
using System.Text.Json;

namespace LuxTicker;

public sealed record YahooQuote
{
    public string Symbol { get; init; } = "";
    public string? Currency { get; init; }
    public decimal? RegularMarketPrice { get; init; }
    public decimal? PreviousClose { get; init; }
    public decimal? ChartPreviousClose { get; init; }
    public decimal? PreMarketPrice { get; init; }
    public decimal? PostMarketPrice { get; init; }
    public decimal? PreFromSeries { get; init; }
    public decimal? PostFromSeries { get; init; }
    public bool HasPrePostMarketData { get; init; }
    public int? PriceHint { get; init; }
}

public static class YahooChartParser
{
    public static bool TryParse(string json, out YahooQuote quote, out string error)
    {
        quote = new YahooQuote();
        error = "";
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("chart", out var chart))
            {
                error = "missing chart";
                return false;
            }

            if (chart.TryGetProperty("error", out var err) && err.ValueKind == JsonValueKind.Object)
            {
                error = err.TryGetProperty("description", out var description)
                    ? description.GetString() ?? "chart error"
                    : "chart error";
                return false;
            }

            if (!chart.TryGetProperty("result", out var result) || result.ValueKind != JsonValueKind.Array || result.GetArrayLength() == 0)
            {
                error = "no result";
                return false;
            }

            var first = result[0];
            if (!first.TryGetProperty("meta", out var meta))
            {
                error = "missing meta";
                return false;
            }

            decimal? preSeries = null;
            decimal? postSeries = null;
            if (meta.TryGetProperty("currentTradingPeriod", out var period)
                && first.TryGetProperty("timestamp", out var stamps)
                && stamps.ValueKind == JsonValueKind.Array
                && first.TryGetProperty("indicators", out var indicators)
                && indicators.TryGetProperty("quote", out var quoteArr)
                && quoteArr.ValueKind == JsonValueKind.Array
                && quoteArr.GetArrayLength() > 0
                && quoteArr[0].TryGetProperty("close", out var closes)
                && closes.ValueKind == JsonValueKind.Array)
            {
                var pre = Window(period, "pre");
                var post = Window(period, "post");
                var n = Math.Min(stamps.GetArrayLength(), closes.GetArrayLength());
                for (int i = 0; i < n; i++)
                {
                    if (!stamps[i].TryGetInt64(out var ts))
                        continue;
                    var close = ReadDecimal(closes[i]);
                    if (close == null)
                        continue;
                    if (pre is { } pw && ts >= pw.Start && ts < pw.End)
                        preSeries = close;
                    if (post is { } ow && ts >= ow.Start && ts < ow.End)
                        postSeries = close;
                }
            }

            quote = new YahooQuote
            {
                Symbol = meta.TryGetProperty("symbol", out var symbol) ? symbol.GetString() ?? "" : "",
                Currency = meta.TryGetProperty("currency", out var currency) ? currency.GetString() : null,
                RegularMarketPrice = ReadOptional(meta, "regularMarketPrice"),
                PreviousClose = ReadOptional(meta, "previousClose"),
                ChartPreviousClose = ReadOptional(meta, "chartPreviousClose"),
                PreMarketPrice = ReadOptional(meta, "preMarketPrice"),
                PostMarketPrice = ReadOptional(meta, "postMarketPrice"),
                PreFromSeries = preSeries,
                PostFromSeries = postSeries,
                HasPrePostMarketData = meta.TryGetProperty("hasPrePostMarketData", out var flag) && flag.ValueKind == JsonValueKind.True,
                PriceHint = meta.TryGetProperty("priceHint", out var hint) && hint.TryGetInt32(out var hintValue) ? hintValue : null
            };
            return true;
        }
        catch (JsonException ex)
        {
            error = "invalid JSON: " + ex.Message;
            return false;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }

    public static QuoteSnapshot ToSnapshot(YahooQuote quote)
    {
        return new QuoteSnapshot
        {
            RegularPrice = quote.RegularMarketPrice,
            PrePrice = quote.PreMarketPrice ?? quote.PreFromSeries,
            PostPrice = quote.PostMarketPrice ?? quote.PostFromSeries,
            PreviousClose = quote.PreviousClose ?? quote.ChartPreviousClose,
            Currency = quote.Currency,
            Stale = false,
            AsOf = DateTimeOffset.UtcNow
        };
    }

    static decimal? ReadOptional(JsonElement meta, string name)
        => meta.TryGetProperty(name, out var el) ? ReadDecimal(el) : null;

    static decimal? ReadDecimal(JsonElement el)
    {
        if (el.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
            return null;
        if (el.ValueKind == JsonValueKind.String)
        {
            var text = el.GetString();
            if (decimal.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed))
                return parsed;
            return null;
        }

        if (el.ValueKind != JsonValueKind.Number)
            return null;
        if (el.TryGetDecimal(out var dec))
            return dec;
        var raw = el.GetRawText();
        if (decimal.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var fromRaw))
            return fromRaw;
        if (el.TryGetDouble(out var dbl) && !double.IsNaN(dbl) && !double.IsInfinity(dbl))
            return (decimal)dbl;
        return null;
    }

    readonly record struct WindowSpan(long Start, long End);

    static WindowSpan? Window(JsonElement period, string name)
    {
        if (!period.TryGetProperty(name, out var window))
            return null;
        if (!window.TryGetProperty("start", out var startEl) || !startEl.TryGetInt64(out var start))
            return null;
        if (!window.TryGetProperty("end", out var endEl) || !endEl.TryGetInt64(out var end))
            return null;
        return new WindowSpan(start, end);
    }
}

public static class YahooProbe
{
    public static ProbeStatus Classify(int statusCode, string? body, out YahooQuote? quote)
    {
        quote = null;
        if (statusCode == 404)
            return ProbeStatus.NotFound;
        if (statusCode == 429 || statusCode >= 500 || statusCode <= 0)
            return ProbeStatus.Unavailable;
        if (statusCode != 200)
            return ProbeStatus.Unavailable;
        if (string.IsNullOrWhiteSpace(body) || !YahooChartParser.TryParse(body, out var parsed, out _))
            return ProbeStatus.NotFound;
        quote = parsed;
        var hasPrice = parsed.RegularMarketPrice != null
                       || parsed.PreMarketPrice != null
                       || parsed.PostMarketPrice != null
                       || parsed.PreFromSeries != null
                       || parsed.PostFromSeries != null;
        return hasPrice ? ProbeStatus.Found : ProbeStatus.NotFound;
    }
}
