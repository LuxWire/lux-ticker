namespace LuxTicker;

public static class FeedEndpoints
{
    public const string UserAgent =
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/129.0.0.0 Safari/537.36";

    public const string CoinbaseWebSocket = "wss://ws-feed.exchange.coinbase.com";

    public static string YahooChart(string symbol)
        => "https://query1.finance.yahoo.com/v8/finance/chart/"
           + Uri.EscapeDataString(symbol)
           + "?interval=1m&range=1d&includePrePost=true";

    public static string CoinbaseTicker(string product)
        => "https://api.exchange.coinbase.com/products/" + Uri.EscapeDataString(product) + "/ticker";

    public static string CoinbaseCandles(string product)
        => "https://api.exchange.coinbase.com/products/" + Uri.EscapeDataString(product) + "/candles?granularity=86400";

    public static string CoinbaseSubscribe(string product)
        => "{\"type\":\"subscribe\",\"product_ids\":[\"" + product + "\"],\"channels\":[\"ticker\"]}";
}

public static class PollBackoff
{
    public static readonly TimeSpan Initial = TimeSpan.FromSeconds(15);
    public static readonly TimeSpan Max = TimeSpan.FromMinutes(5);

    public static TimeSpan Next(int failureCount)
    {
        if (failureCount <= 0)
            return TimeSpan.Zero;
        var seconds = 15 * Math.Pow(2, Math.Min(failureCount - 1, 8));
        if (seconds > Max.TotalSeconds)
            seconds = Max.TotalSeconds;
        return TimeSpan.FromSeconds(seconds);
    }
}
