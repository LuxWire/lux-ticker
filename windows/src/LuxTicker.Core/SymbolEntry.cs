using System.Text.Json.Serialization;

namespace LuxTicker;

public enum SymbolKind
{
    Us = 0,
    Crypto = 1,
    International = 2,
    Continuous = 3
}

public enum ProbeStatus
{
    Found,
    NotFound,
    Unavailable
}

public readonly record struct QuoteProbeResult(ProbeStatus Status, string? Currency, decimal? Price, string? Detail);

public sealed record SymbolEntry
{
    public required string Symbol { get; init; }
    public string? Label { get; init; }
    public SymbolKind Kind { get; init; }
    public string Currency { get; init; } = "USD";

    [JsonIgnore]
    public string DisplayLabel => string.IsNullOrWhiteSpace(Label) ? Symbol : Label.Trim();

    [JsonIgnore]
    public string EditorDetail
    {
        get
        {
            var parts = new List<string>();
            if (!string.Equals(DisplayLabel, Symbol, StringComparison.Ordinal))
                parts.Add(Symbol);
            if (!string.Equals(Currency, "USD", StringComparison.OrdinalIgnoreCase))
                parts.Add(Currency);
            return string.Join("   ", parts);
        }
    }

    public static string NormalizeSymbol(string raw)
    {
        var s = raw.Trim();
        if (s.Equals("BTC-USD", StringComparison.OrdinalIgnoreCase))
            return "BTC";
        if (s.StartsWith('^'))
            return "^" + s[1..].ToUpperInvariant();
        return s.ToUpperInvariant();
    }

    public static bool IsValidSymbol(string symbol)
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

    public static SymbolEntry Infer(string rawSymbol, string? label)
    {
        var symbol = NormalizeSymbol(rawSymbol);
        var display = string.IsNullOrWhiteSpace(label) ? null : label.Trim();

        if (symbol.Equals("BTC", StringComparison.OrdinalIgnoreCase))
        {
            return new SymbolEntry
            {
                Symbol = "BTC",
                Label = display ?? "BTC",
                Kind = SymbolKind.Crypto,
                Currency = "USD"
            };
        }

        var currency = "USD";
        var kind = SymbolKind.Us;

        if (EndsWithExchange(symbol, ".T"))
        {
            currency = "JPY";
            kind = SymbolKind.International;
        }
        else if (EndsWithExchange(symbol, ".L") || EndsWithExchange(symbol, ".IL"))
        {
            currency = "GBp";
            kind = SymbolKind.International;
        }
        else if (EndsWithExchange(symbol, ".HK"))
        {
            currency = "HKD";
            kind = SymbolKind.International;
        }
        else if (EndsWithExchange(symbol, ".TO") || EndsWithExchange(symbol, ".AX"))
        {
            currency = EndsWithExchange(symbol, ".AX") ? "AUD" : "CAD";
            kind = SymbolKind.International;
        }
        else if (EndsWithExchange(symbol, ".PA") || EndsWithExchange(symbol, ".DE")
                 || EndsWithExchange(symbol, ".AS") || EndsWithExchange(symbol, ".SW"))
        {
            currency = EndsWithExchange(symbol, ".SW") ? "CHF" : "EUR";
            kind = SymbolKind.International;
        }
        else if (symbol.Contains('=') || symbol.EndsWith("-USD", StringComparison.Ordinal))
        {
            kind = SymbolKind.Continuous;
        }

        return new SymbolEntry
        {
            Symbol = symbol,
            Label = display,
            Kind = kind,
            Currency = currency
        };
    }

    /// <summary>
    /// Yahoo's currency is authoritative. GBp (pence) is case-sensitive versus GBP (pounds).
    /// A non-USD quote on a symbol we assumed was a US listing is treated as non-US, so it does not get PRE/AH.
    /// </summary>
    public SymbolEntry WithQuoteCurrency(string? currency)
    {
        if (Kind == SymbolKind.Crypto)
            return this with { Currency = "USD" };
        if (string.IsNullOrWhiteSpace(currency))
            return this;

        var cur = currency.Trim();
        if (cur is "GBp" or "GBX" or "GBx")
            cur = "GBp";

        var kind = Kind;
        if (kind == SymbolKind.Us && !cur.Equals("USD", StringComparison.OrdinalIgnoreCase))
            kind = SymbolKind.International;

        return this with { Kind = kind, Currency = cur };
    }

    static bool EndsWithExchange(string symbol, string suffix)
        => symbol.EndsWith(suffix, StringComparison.OrdinalIgnoreCase);
}
