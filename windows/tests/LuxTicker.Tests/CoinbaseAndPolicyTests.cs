namespace LuxTicker.Tests;

public class CoinbaseAndPolicyTests
{
    [Fact]
    public void Previous_utc_daily_close_skips_the_candle_still_in_progress()
    {
        // Synthetic candles (made-up numbers, real Coinbase shape: [time, low, high, open, close, volume]).
        const string json = "[[1792627200, 90, 110, 100, 105, 1], [1792540800, 95, 105, 98, 100, 1], [1792454400, 90, 100, 92, 98, 1]]";
        var during = new DateTimeOffset(2026, 10, 22, 21, 40, 0, TimeSpan.Zero);
        Assert.True(CoinbaseCandles.TryPreviousDailyClose(json, during, out var previous));
        Assert.Equal(100m, previous);

        var nextDay = new DateTimeOffset(2026, 10, 23, 0, 0, 1, TimeSpan.Zero);
        Assert.True(CoinbaseCandles.TryPreviousDailyClose(json, nextDay, out var rolled));
        Assert.Equal(105m, rolled);
    }

    [Fact]
    public void Ticker_messages_parse_and_other_frames_are_ignored()
    {
        Assert.True(CoinbaseTickerParser.TryParsePrice(
            """{"type":"ticker","product_id":"BTC-USD","price":"108412.50"}""", out var price));
        Assert.Equal(108412.50m, price);
        Assert.False(CoinbaseTickerParser.TryParsePrice("""{"type":"subscriptions"}""", out _));
        Assert.False(CoinbaseTickerParser.TryParsePrice("""{"type":"ticker","product_id":"ETH-USD","price":"1"}""", out _));
        Assert.False(CoinbaseTickerParser.TryParsePrice("nope", out _));
    }

    [Fact]
    public void Public_endpoints_need_no_key()
    {
        Assert.Contains("%5EGSPC", FeedEndpoints.YahooChart("^GSPC"));
        Assert.Contains("CL%3DF", FeedEndpoints.YahooChart("CL=F"));
        Assert.Contains("includePrePost=true", FeedEndpoints.YahooChart("MSTR"));
        Assert.StartsWith("wss://ws-feed.exchange.coinbase.com", FeedEndpoints.CoinbaseWebSocket);
        Assert.Contains("BTC-USD", FeedEndpoints.CoinbaseSubscribe("BTC-USD"));
        Assert.Contains("granularity=86400", FeedEndpoints.CoinbaseCandles("BTC-USD"));
        Assert.Contains("Mozilla/5.0", FeedEndpoints.UserAgent);
    }

    [Fact]
    public void Backoff_grows_and_caps_at_five_minutes()
    {
        Assert.Equal(TimeSpan.Zero, PollBackoff.Next(0));
        Assert.Equal(TimeSpan.FromSeconds(15), PollBackoff.Next(1));
        Assert.Equal(TimeSpan.FromSeconds(30), PollBackoff.Next(2));
        Assert.Equal(TimeSpan.FromSeconds(60), PollBackoff.Next(3));
        Assert.Equal(TimeSpan.FromMinutes(5), PollBackoff.Next(20));
    }

    [Fact]
    public void Manifest_is_asInvoker_and_sources_do_not_touch_machine_state()
    {
        var root = Repo.Root();
        var manifest = File.ReadAllText(Path.Combine(root, "src", "LuxTicker.App", "app.manifest"));
        Assert.Contains("level=\"asInvoker\"", manifest);
        Assert.Contains("uiAccess=\"false\"", manifest);
        Assert.Contains("PerMonitorV2", manifest);
        Assert.DoesNotContain("requireAdministrator", manifest, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("highestAvailable", manifest, StringComparison.OrdinalIgnoreCase);

        var startup = File.ReadAllText(Path.Combine(root, "src", "LuxTicker.App", "StartupRegistrar.cs"));
        Assert.Contains("Registry.CurrentUser", startup);
        Assert.Contains("RunKey.SubKey", startup);

        foreach (var file in Directory.GetFiles(Path.Combine(root, "src"), "*.cs", SearchOption.AllDirectories))
        {
            var text = File.ReadAllText(file);
            Assert.DoesNotContain("LocalMachine", text);
            Assert.DoesNotContain("requireAdministrator", text);
            Assert.DoesNotContain("Program Files", text);
        }
    }
}
