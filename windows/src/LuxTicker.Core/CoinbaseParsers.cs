using System.Globalization;
using System.Text.Json;

namespace LuxTicker;

public readonly record struct CoinbaseCandle(long Time, decimal Low, decimal High, decimal Open, decimal Close, decimal Volume);

public static class CoinbaseTickerParser
{
    public static bool TryParsePrice(string json, out decimal price)
    {
        price = 0;
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (!root.TryGetProperty("type", out var type) || type.GetString() != "ticker")
                return false;
            if (root.TryGetProperty("product_id", out var product) && product.GetString() is { } id && !id.Equals("BTC-USD", StringComparison.OrdinalIgnoreCase))
                return false;
            if (!root.TryGetProperty("price", out var priceEl))
                return false;
            var text = priceEl.ValueKind == JsonValueKind.String ? priceEl.GetString() : priceEl.GetRawText();
            return decimal.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out price);
        }
        catch (JsonException)
        {
            return false;
        }
    }
}

public static class CoinbaseCandles
{
    public static bool TryParse(string json, out IReadOnlyList<CoinbaseCandle> candles, out string error)
    {
        candles = Array.Empty<CoinbaseCandle>();
        error = "";
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Array)
            {
                error = "expected an array";
                return false;
            }

            var list = new List<CoinbaseCandle>();
            foreach (var row in doc.RootElement.EnumerateArray())
            {
                if (row.ValueKind != JsonValueKind.Array || row.GetArrayLength() < 6)
                    continue;
                if (!row[0].TryGetInt64(out var time))
                    continue;
                if (!TryDec(row[1], out var low) || !TryDec(row[2], out var high) || !TryDec(row[3], out var open) || !TryDec(row[4], out var close))
                    continue;
                TryDec(row[5], out var volume);
                list.Add(new CoinbaseCandle(time, low, high, open, close, volume));
            }

            candles = list;
            return list.Count > 0;
        }
        catch (JsonException ex)
        {
            error = ex.Message;
            return false;
        }
    }

    /// <summary>Close of the last completed UTC day. The in-progress candle is not the previous close.</summary>
    public static bool TryPreviousDailyClose(string json, DateTimeOffset now, out decimal previousClose)
    {
        previousClose = 0;
        if (!TryParse(json, out var candles, out _))
            return false;

        long bucket = now.ToUnixTimeSeconds() / 86400 * 86400;
        long bestTime = long.MinValue;
        decimal best = 0;
        var found = false;
        foreach (var candle in candles)
        {
            if (candle.Time < bucket && candle.Time >= bestTime)
            {
                bestTime = candle.Time;
                best = candle.Close;
                found = true;
            }
        }

        if (!found)
            return false;
        previousClose = best;
        return true;
    }

    static bool TryDec(JsonElement el, out decimal value)
    {
        value = 0;
        if (el.ValueKind == JsonValueKind.Number)
        {
            if (el.TryGetDecimal(out value))
                return true;
            return decimal.TryParse(el.GetRawText(), NumberStyles.Float, CultureInfo.InvariantCulture, out value);
        }

        if (el.ValueKind == JsonValueKind.String)
            return decimal.TryParse(el.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out value);
        return false;
    }
}
