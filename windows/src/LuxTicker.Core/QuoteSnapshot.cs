namespace LuxTicker;

public sealed record QuoteSnapshot
{
    public decimal? RegularPrice { get; init; }
    public decimal? PrePrice { get; init; }
    public decimal? PostPrice { get; init; }
    public decimal? PreviousClose { get; init; }
    public string? Currency { get; init; }
    public bool Stale { get; init; }
    public DateTimeOffset? AsOf { get; init; }

    public decimal? PriceFor(SessionPhase phase, SymbolKind kind)
    {
        if (kind == SymbolKind.Crypto)
            return RegularPrice;

        return phase switch
        {
            SessionPhase.PreMarket => PrePrice ?? RegularPrice ?? PostPrice,
            SessionPhase.Regular => RegularPrice ?? PostPrice ?? PrePrice,
            _ => PostPrice ?? RegularPrice ?? PrePrice
        };
    }

    public static QuoteSnapshot MarkFailed(QuoteSnapshot? previous)
    {
        if (previous == null)
            return new QuoteSnapshot { Stale = true };
        return previous with { Stale = true };
    }
}

public sealed class QuoteBook
{
    readonly object _gate = new();
    readonly Dictionary<string, QuoteSnapshot> _map = new(StringComparer.OrdinalIgnoreCase);

    public void Set(string symbol, QuoteSnapshot snapshot)
    {
        lock (_gate)
            _map[symbol] = snapshot;
    }

    public bool TryGet(string symbol, out QuoteSnapshot? snapshot)
    {
        lock (_gate)
        {
            if (_map.TryGetValue(symbol, out var found))
            {
                snapshot = found;
                return true;
            }
        }

        snapshot = null;
        return false;
    }

    public void MarkStale(string symbol)
    {
        lock (_gate)
        {
            _map.TryGetValue(symbol, out var existing);
            _map[symbol] = QuoteSnapshot.MarkFailed(existing);
        }
    }

    public void SetBtc(decimal price, decimal? previousClose)
    {
        lock (_gate)
        {
            _map.TryGetValue("BTC", out var existing);
            _map["BTC"] = new QuoteSnapshot
            {
                RegularPrice = price,
                PreviousClose = previousClose ?? existing?.PreviousClose,
                Currency = "USD",
                Stale = false,
                AsOf = DateTimeOffset.UtcNow
            };
        }
    }

    public void SetBtcPreviousClose(decimal previousClose)
    {
        lock (_gate)
        {
            if (_map.TryGetValue("BTC", out var existing))
                _map["BTC"] = existing with { PreviousClose = previousClose };
            else
                _map["BTC"] = new QuoteSnapshot { PreviousClose = previousClose, Currency = "USD", Stale = true };
        }
    }
}
