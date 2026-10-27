using System.Text.Json;

namespace LuxTicker;

public sealed record ChartPick(string Symbol, string Title, string Yahoo)
{
    public string ListLabel => Symbol.Equals(Title, StringComparison.OrdinalIgnoreCase) ? Symbol : Symbol + "   " + Title;
}

/// <summary>Yahoo symbols for the chart search. BTC and ETH become BTC-USD and ETH-USD.</summary>
public static class ChartSymbols
{
    public const string MissingPrefix = "No chart for ";

    public static readonly string[] Crypto =
    [
        "BTC", "ETH", "SOL", "DOGE", "XRP", "ADA", "AVAX", "DOT", "LINK", "LTC", "BCH",
        "UNI", "ATOM", "NEAR", "APT", "ARB", "OP", "SUI", "TON", "TRX", "SHIB", "PEPE"
    ];

    public static ChartPick? Parse(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return null;
        var text = raw.Trim().Replace(" / ", "/").Replace(" ", "");
        if (text.Contains('/'))
        {
            var parts = text.Split('/', 2);
            var left = SymbolEntry.NormalizeSymbol(parts[0]);
            var right = SymbolEntry.NormalizeSymbol(parts[1]);
            if (!SymbolEntry.IsValidSymbol(left) || right.Length is < 2 or > 8)
                return null;
            var yahoo = left + "-" + right;
            return new ChartPick(left + "/" + right, left + "/" + right, yahoo);
        }

        var normalized = SymbolEntry.NormalizeSymbol(text);
        if (normalized.Contains('-'))
        {
            if (!LooksLikeYahoo(normalized))
                return null;
            var title = normalized.Replace('-', '/');
            return new ChartPick(title, title, normalized);
        }

        if (!SymbolEntry.IsValidSymbol(normalized))
            return null;
        if (Crypto.Contains(normalized, StringComparer.OrdinalIgnoreCase))
            return new ChartPick(normalized + "/USD", normalized + "/USD", normalized + "-USD");
        return new ChartPick(normalized, normalized, normalized);
    }

    public static string Missing(string symbol) => MissingPrefix + symbol.Trim() + ".";

    public static string SearchUrl(string query)
        => "https://query2.finance.yahoo.com/v1/finance/search?q="
           + Uri.EscapeDataString(query.Trim())
           + "&quotesCount=8&newsCount=0";

    public static IReadOnlyList<ChartPick> ParseSearch(string? json)
    {
        var hits = new List<ChartPick>();
        if (string.IsNullOrWhiteSpace(json))
            return hits;
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("quotes", out var quotes) || quotes.ValueKind != JsonValueKind.Array)
                return hits;
            foreach (var quote in quotes.EnumerateArray())
            {
                if (!quote.TryGetProperty("symbol", out var symbolEl) || symbolEl.GetString() is not { } yahoo || string.IsNullOrWhiteSpace(yahoo))
                    continue;
                var name = quote.TryGetProperty("shortname", out var shortName) && shortName.ValueKind == JsonValueKind.String
                    ? shortName.GetString()
                    : null;
                if (string.IsNullOrWhiteSpace(name) && quote.TryGetProperty("longname", out var longName))
                    name = longName.GetString();
                var title = string.IsNullOrWhiteSpace(name) ? yahoo.Trim() : name.Trim();
                hits.Add(new ChartPick(yahoo.Trim(), title, yahoo.Trim()));
                if (hits.Count == 8)
                    break;
            }
        }
        catch (JsonException)
        {
            return hits;
        }

        return hits;
    }

    public static IReadOnlyList<ChartPick> Remember(IReadOnlyList<ChartPick> current, ChartPick pick)
    {
        var next = new List<ChartPick> { pick };
        foreach (var item in current)
        {
            if (item.Yahoo.Equals(pick.Yahoo, StringComparison.OrdinalIgnoreCase))
                continue;
            next.Add(item);
            if (next.Count == 8)
                break;
        }

        return next;
    }

    static bool LooksLikeYahoo(string symbol)
    {
        if (symbol.Length is < 1 or > 32)
            return false;
        foreach (var c in symbol)
        {
            if (char.IsLetterOrDigit(c) || c is '.' or '-' or '^' or '=' or '_')
                continue;
            return false;
        }

        return true;
    }
}
