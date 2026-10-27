namespace LuxTicker.Tests;

public class YahooParserTests
{
    [Fact]
    public void Meta_pre_and_post_prices_win_over_the_series()
    {
        const string json = """
            {"chart":{"result":[{"meta":{
              "symbol":"MSTR","currency":"USD",
              "regularMarketPrice":10,
              "previousClose":8,
              "preMarketPrice":11.5,
              "postMarketPrice":12.25,
              "hasPrePostMarketData":true
            }}],"error":null}}
            """;

        Assert.True(YahooChartParser.TryParse(json, out var quote, out var error), error);
        var snap = YahooChartParser.ToSnapshot(quote);
        Assert.Equal(11.5m, snap.PriceFor(SessionPhase.PreMarket, SymbolKind.Us));
        Assert.Equal(10m, snap.PriceFor(SessionPhase.Regular, SymbolKind.Us));
        Assert.Equal(12.25m, snap.PriceFor(SessionPhase.AfterHours, SymbolKind.Us));
        Assert.Equal(12.25m, snap.PriceFor(SessionPhase.Closed, SymbolKind.Us));
    }

    [Fact]
    public void Not_found_fixture_is_a_clean_miss()
    {
        const string body = """{"chart":{"result":null,"error":{"code":"Not Found","description":"No data found, symbol may be delisted"}}}""";
        Assert.False(YahooChartParser.TryParse(body, out _, out var error));
        Assert.Contains("No data found", error);
        Assert.Equal(ProbeStatus.NotFound, YahooProbe.Classify(404, body, out _));
        Assert.Equal(ProbeStatus.NotFound, YahooProbe.Classify(200, body, out _));
        Assert.Equal(ProbeStatus.Unavailable, YahooProbe.Classify(429, body, out _));
        Assert.Equal(ProbeStatus.Unavailable, YahooProbe.Classify(503, "", out _));
    }

    [Fact]
    public void Malformed_json_does_not_throw()
    {
        Assert.False(YahooChartParser.TryParse("{", out _, out var error));
        Assert.Contains("invalid JSON", error);
        Assert.False(YahooChartParser.TryParse("", out _, out _));
    }

    [Fact]
    public void Presenter_formats_yen_pence_signs_and_hides_non_us_tags()
    {
        var tokyo = QuotePresenter.Present(
            new SymbolEntry { Symbol = "3350.T", Kind = SymbolKind.International, Currency = "JPY" },
            new QuoteSnapshot { RegularPrice = 942m, PostPrice = 942m, PreviousClose = 920m, Currency = "JPY" },
            SessionPhase.AfterHours);
        Assert.Equal(new[] { "3350.T", "¥942", "+2.39%" }, tokyo.Runs.Select(r => r.Text).ToArray());
        Assert.Equal(TickerTextColor.UpDefault, tokyo.Runs[^1].Color);

        var london = QuotePresenter.Present(
            new SymbolEntry { Symbol = "SWC.L", Kind = SymbolKind.International, Currency = "GBp" },
            new QuoteSnapshot { RegularPrice = 50.65m, PreviousClose = 54.99m, Currency = "GBp" },
            SessionPhase.Regular);
        Assert.Contains(london.Runs, r => r.Text == "50.65p");
        Assert.DoesNotContain(london.Runs, r => r.Text is "PRE" or "AH");

        var btc = QuotePresenter.Present(
            new SymbolEntry { Symbol = "BTC", Label = "BTC", Kind = SymbolKind.Crypto, Currency = "USD" },
            new QuoteSnapshot { RegularPrice = 108412.50m, PreviousClose = 106453.16m, Currency = "USD" },
            SessionPhase.Closed);
        Assert.Equal("BTC", btc.Runs[0].Text);
        Assert.Equal("108,412.50", btc.Runs[1].Text);
        Assert.DoesNotContain(btc.Runs, r => r.Text is "PRE" or "AH");

        var missing = QuotePresenter.Present(
            new SymbolEntry { Symbol = "ZZZ", Kind = SymbolKind.Us, Currency = "USD" },
            null,
            SessionPhase.Regular);
        Assert.Contains(missing.Runs, r => r.Text == "—");
        Assert.True(missing.Stale);
        Assert.DoesNotContain(missing.Runs, r => r.Text == "");
    }

    [Fact]
    public void Failed_fetch_keeps_the_last_price_and_marks_it_stale()
    {
        var kept = QuoteSnapshot.MarkFailed(new QuoteSnapshot { RegularPrice = 10m, PreviousClose = 9m, Currency = "USD" });
        Assert.Equal(10m, kept.RegularPrice);
        Assert.True(kept.Stale);

        var none = QuoteSnapshot.MarkFailed(null);
        Assert.Null(none.RegularPrice);
        Assert.True(none.Stale);

        var shown = QuotePresenter.Present(
            new SymbolEntry { Symbol = "MSTR", Kind = SymbolKind.Us, Currency = "USD" },
            kept,
            SessionPhase.Regular);
        Assert.True(shown.Stale);
        Assert.Contains(shown.Runs, r => r.Text == "10.00");
    }
}
