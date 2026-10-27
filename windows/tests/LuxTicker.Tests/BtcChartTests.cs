namespace LuxTicker.Tests;

public class BtcChartTests
{
    static readonly DateTimeOffset Now = new(2026, 10, 23, 16, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Presets_cover_the_spec_and_default_to_one_month()
    {
        Assert.Equal(BtcChartPreset.Month, BtcChart.DefaultPreset);
        Assert.Equal(["1H", "1D", "1W", "1M", "6M", "YTD", "1Y", "ALL", "Custom"], BtcChart.Presets.Select(BtcChart.PresetLabel).ToArray());
        Assert.Equal("#1B7A3A", BtcChart.CandleUp);
        Assert.Equal("#C0392B", BtcChart.CandleDown);

        Assert.True(BtcChart.TryRange(BtcChartPreset.Hour, Now, null, null, out var hourStart, out var hourEnd, out _));
        Assert.Equal(Now.AddMinutes(-60), hourStart);
        Assert.Equal(Now, hourEnd);
        Assert.Equal("1m", BtcChart.Interval(hourStart, hourEnd));
        Assert.Equal("1m", BtcChart.CandleInterval(hourStart, hourEnd));
        Assert.Equal(TimeSpan.FromSeconds(60), BtcChart.RefreshInterval(hourStart, hourEnd));
        Assert.Contains("interval=1m", BtcChart.ChartUrl(hourStart, hourEnd, BtcChartStyle.Candles));
        Assert.Equal("#F6F2E8", BtcChart.Paper);
        Assert.Equal("#F7931A", BtcChart.SeriesColor);
        Assert.Equal(new DateTimeOffset(2014, 9, 17, 0, 0, 0, TimeSpan.Zero), BtcChart.Earliest);

        Assert.True(BtcChart.TryRange(BtcChartPreset.Day, Now, null, null, out var dayStart, out var dayEnd, out _));
        Assert.Equal(Now.AddDays(-1), dayStart);
        Assert.Equal(Now, dayEnd);
        Assert.Equal("5m", BtcChart.Interval(dayStart, dayEnd));
        Assert.Equal("5m", BtcChart.CandleInterval(dayStart, dayEnd));
        Assert.Equal(TimeSpan.FromSeconds(60), BtcChart.RefreshInterval(dayStart, dayEnd));

        Assert.True(BtcChart.TryRange(BtcChartPreset.Week, Now, null, null, out var weekStart, out _, out _));
        Assert.Equal(Now.AddDays(-7), weekStart);
        Assert.Equal("1h", BtcChart.Interval(weekStart, Now));
        Assert.Equal("1h", BtcChart.CandleInterval(weekStart, Now));

        Assert.True(BtcChart.TryRange(BtcChartPreset.Month, Now, null, null, out var monthStart, out _, out _));
        Assert.Equal(Now.AddDays(-30), monthStart);
        Assert.Equal(TimeSpan.FromMinutes(5), BtcChart.RefreshInterval(monthStart, Now));

        Assert.True(BtcChart.TryRange(BtcChartPreset.SixMonths, Now, null, null, out var sixStart, out _, out _));
        Assert.Equal(Now.AddMonths(-6), sixStart);
        Assert.Equal("1d", BtcChart.Interval(sixStart, Now));
        Assert.Equal("1d", BtcChart.CandleInterval(sixStart, Now));

        Assert.True(BtcChart.TryRange(BtcChartPreset.YearToDate, Now, null, null, out var ytd, out _, out _));
        Assert.Equal(new DateTimeOffset(2026, 1, 1, 5, 0, 0, TimeSpan.Zero), ytd);
        Assert.Contains("1 Jan 2026", BtcChart.Caption(BtcChartPreset.YearToDate, ytd, Now, null, null));
        Assert.EndsWith(" ET", BtcChart.Caption(BtcChartPreset.YearToDate, ytd, Now, null, null));

        Assert.True(BtcChart.TryRange(BtcChartPreset.Year, Now, null, null, out var yearStart, out _, out _));
        Assert.Equal(Now.AddDays(-365), yearStart);

        Assert.True(BtcChart.TryRange(BtcChartPreset.All, Now, null, null, out var allStart, out _, out _));
        Assert.Equal(BtcChart.Earliest, allStart);
        Assert.Equal(TimeSpan.FromMinutes(15), BtcChart.RefreshInterval(allStart, Now));
        Assert.Equal("1d", BtcChart.Interval(allStart, Now));
    }

    [Fact]
    public void Custom_range_rejects_an_end_before_the_start_and_dates_before_the_first_print()
    {
        var from = new DateOnly(2024, 1, 2);
        var to = new DateOnly(2024, 1, 1);
        Assert.False(BtcChart.TryRange(BtcChartPreset.Custom, Now, from, to, out _, out _, out var order));
        Assert.Equal("To must be on or after From.", order);
        Assert.False(BtcChart.TryRange(BtcChartPreset.Custom, Now, null, to, out _, out _, out var missing));
        Assert.Equal("Choose a From date and a To date.", missing);
        Assert.False(BtcChart.TryRange(BtcChartPreset.Custom, Now, new DateOnly(2010, 7, 17), new DateOnly(2010, 8, 1), out _, out _, out var early));
        Assert.Equal("Bitcoin prices start on 17 Sep 2014.", early);
        Assert.False(BtcChart.TryRange(BtcChartPreset.Custom, Now, new DateOnly(2026, 10, 24), new DateOnly(2026, 10, 24), out _, out _, out var future));
        Assert.Equal("Dates cannot be in the future.", future);

        Assert.True(BtcChart.TryRange(BtcChartPreset.Custom, Now, new DateOnly(2014, 9, 17), new DateOnly(2014, 9, 20), out var start, out var end, out var ok));
        Assert.Equal("", ok);
        Assert.Equal(BtcChart.Earliest, start);
        Assert.True(end > start);
        Assert.Equal("17 Sep 2014 – 20 Sep 2014 ET", BtcChart.Caption(BtcChartPreset.Custom, start, end, new DateOnly(2014, 9, 17), new DateOnly(2014, 9, 20)));
    }

    [Fact]
    public void Chart_url_is_public_and_the_series_uses_the_first_and_last_close()
    {
        var start = Now.AddDays(-1);
        var url = BtcChart.ChartUrl(start, Now);
        Assert.Contains("https://query1.finance.yahoo.com/v8/finance/chart/BTC-USD?", url);
        Assert.Contains("interval=5m", url);
        Assert.DoesNotContain("api_key", url, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("apikey", url, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("interval=1d", BtcChart.ChartUrl(BtcChart.Earliest, Now));

        const string json = """
            {"chart":{"result":[{"timestamp":[100,200,300],"indicators":{"quote":[{"close":[100,null,110]}]}}],"error":null}}
            """;
        Assert.True(BtcChart.TryParse(json, out var series, out var failure));
        Assert.Null(failure);
        Assert.NotNull(series);
        Assert.Equal(2, series!.Points.Count);
        Assert.Equal(110m, series.Last);
        Assert.Equal(10m, series.ChangePercent);
        Assert.Equal(100m, series.Points[0].Open);
        Assert.Equal(110m, series.Points[1].Close);

        const string ohlc = """
            {"chart":{"result":[{"timestamp":[100,200],"indicators":{"quote":[{"open":[10,20],"high":[15,28],"low":[8,18],"close":[12,22]}]}}],"error":null}}
            """;
        Assert.True(BtcChart.TryParse(ohlc, out var bars, out var ohlcError));
        Assert.Null(ohlcError);
        Assert.Equal(10m, bars!.Points[0].Open);
        Assert.Equal(15m, bars.Points[0].High);
        Assert.Equal(8m, bars.Points[0].Low);
        Assert.Equal(12m, bars.Points[0].Close);
        Assert.Equal(22m, bars.Points[1].Close);
        Assert.Equal(28m, bars.Points[1].High);

        Assert.True(BtcChart.TryParse("""{"chart":{"result":[{"timestamp":[],"indicators":{"quote":[{"close":[]}]}}]}}""", out var empty, out var emptyError));
        Assert.Null(empty);
        Assert.Null(emptyError);
        Assert.False(BtcChart.TryParse("""{"chart":{"error":{"description":"No data found"}}}""", out _, out var failed));
        Assert.Equal("No data found", failed);
    }

    [Fact]
    public void The_bar_opens_the_chart_and_the_feed_file_is_not_involved()
    {
        var root = Repo.Root();
        var xaml = File.ReadAllText(Path.Combine(root, "src", "LuxTicker.App", "MainWindow.xaml"));
        Assert.Contains("x:Name=\"BtcButton\"", xaml);
        Assert.Contains("Content=\"Chart\"", xaml);
        Assert.Contains("AutomationProperties.Name=\"Chart\"", xaml);
        Assert.Contains("#F7931A", xaml);
        Assert.DoesNotContain("#FF6A00", xaml);
        var main = File.ReadAllText(Path.Combine(root, "src", "LuxTicker.App", "MainWindow.xaml.cs"));
        Assert.Contains("Item(\"Chart\"", main);
        Assert.Contains("OpenChart", main);
        var window = File.ReadAllText(Path.Combine(root, "src", "LuxTicker.App", "BtcChartWindow.xaml"));
        Assert.Contains("#F6F2E8", window);
        Assert.Contains("#F7931A", window);
        Assert.Contains("#EFEADE", window);
        Assert.DoesNotContain("#FF6A00", window);
        Assert.Contains("BTC/USD", window);
        Assert.Contains("x:Name=\"SymbolSearch\"", window);
        Assert.Contains("Foreground=\"#161412\"", window);
        Assert.Contains("CaretBrush=\"#161412\"", window);
        Assert.Contains("x:Name=\"SymbolText\"", window);
        var chartWindow = File.ReadAllText(Path.Combine(root, "src", "LuxTicker.App", "BtcChartWindow.xaml.cs"));
        Assert.Contains("Couldn't load Bitcoin prices. Try again.", chartWindow);
        Assert.Contains("ChartSymbols.Missing", chartWindow);
        Assert.Contains("No chart for ", File.ReadAllText(Path.Combine(root, "src", "LuxTicker.Core", "ChartSymbols.cs")));
        var chart = File.ReadAllText(Path.Combine(root, "src", "LuxTicker.Core", "BtcChart.cs"));
        Assert.DoesNotContain("posts.jsonl", chart);
        Assert.DoesNotContain("api_key", chart);
        foreach (var file in Directory.GetFiles(Path.Combine(root, "src"), "*.*", SearchOption.AllDirectories))
        {
            if (!file.EndsWith(".cs", StringComparison.OrdinalIgnoreCase) && !file.EndsWith(".xaml", StringComparison.OrdinalIgnoreCase))
                continue;
            var text = File.ReadAllText(file);
            Assert.DoesNotContain("#FF6A00", text);
            Assert.DoesNotContain("#EFE8D8", text);
            Assert.DoesNotContain("#1C1A16", text);
        }

        var chrome = File.ReadAllText(Path.Combine(root, "src", "LuxTicker.App", "PaperChrome.cs"));
        Assert.Contains("DWMSBT_NONE", chrome);
        Assert.Contains("DWMWA_SYSTEMBACKDROP_TYPE", chrome);

        var plot = File.ReadAllText(Path.Combine(root, "src", "LuxTicker.App", "BtcChartPlot.cs"));
        Assert.Contains("MouseLeftButtonDown", plot);
        Assert.Contains("NearestIndex", plot);
        Assert.Contains("HitTestCore", plot);
        Assert.Contains("Brushes.Transparent", plot);
        Assert.Contains("_scrub = -1", plot);
        Assert.Contains("PaperInk.Body", plot);
        Assert.Contains("PaperInk.Panel", plot);
        Assert.Contains("BtcChart.SeriesColor", plot);
        Assert.Contains("ScrubPrice", plot);
        Assert.Contains("ScrubStamp", plot);
        Assert.Contains("BtcChartStyle.Candles", plot);
        Assert.Contains("DrawRectangle", plot);
        Assert.Contains("BtcChart.CandleUp", plot);
        Assert.Contains("BtcChart.CandleDown", plot);
        Assert.Contains("ScrubOhlc", plot);
        Assert.Contains("x:Name=\"LineButton\"", window);
        Assert.Contains("x:Name=\"CandlesButton\"", window);
        Assert.Contains("Content=\"Candles\"", window);
    }

    [Fact]
    public void Scrubber_snaps_to_the_nearest_print_on_every_series()
    {
        Assert.Equal(-1, BtcChart.NearestIndex(Array.Empty<BtcPoint>(), 0.4));
        var only = new BtcPoint(Now, 50_000m);
        Assert.Equal(0, BtcChart.NearestIndex([only], 0.9));
        Assert.Equal(0, BtcChart.NearestIndex([only], double.NaN));

        var t0 = new DateTimeOffset(2026, 1, 15, 15, 0, 0, TimeSpan.Zero);
        var points = new[]
        {
            new BtcPoint(t0, 100m),
            new BtcPoint(t0.AddMinutes(100), 110m),
            new BtcPoint(t0.AddMinutes(200), 120m),
            new BtcPoint(t0.AddMinutes(300), 130m)
        };
        Assert.Equal(0, BtcChart.NearestIndex(points, 0));
        Assert.Equal(0, BtcChart.NearestIndex(points, -0.2));
        Assert.Equal(3, BtcChart.NearestIndex(points, 1));
        Assert.Equal(3, BtcChart.NearestIndex(points, 4));
        Assert.Equal(1, BtcChart.NearestIndex(points, 110d / 300d));
        Assert.Equal(2, BtcChart.NearestIndex(points, 190d / 300d));
        Assert.Equal(1, BtcChart.NearestIndex(points, 0.5));

        var stamp = BtcChart.ScrubStamp(points[0]);
        Assert.Equal("15 Jan 2026, 10:00 ET", stamp);
        Assert.EndsWith(" ET", stamp);
        Assert.Equal("$110.00", BtcChart.ScrubPrice(points[1].Close));

        var candle = new BtcPoint(t0, 110m) { Open = 100m, High = 120m, Low = 90m };
        var ohlc = BtcChart.ScrubOhlc(candle);
        Assert.Contains("O $100.00", ohlc);
        Assert.Contains("H $120.00", ohlc);
        Assert.Contains("L $90.00", ohlc);
        Assert.Contains("C $110.00", ohlc);
        Assert.Equal("15 Jan 2026, 10:00 ET", BtcChart.ScrubStamp(candle));
    }

    [Fact]
    public void Any_symbol_uses_the_same_ranges_and_an_unknown_one_is_named()
    {
        var btc = ChartSymbols.Parse("BTC");
        Assert.Equal("BTC-USD", btc!.Yahoo);
        Assert.Equal("BTC/USD", btc.Title);
        Assert.Equal("ETH-USD", ChartSymbols.Parse("eth")!.Yahoo);
        Assert.Equal("BTC-USD", ChartSymbols.Parse("BTC / USD")!.Yahoo);
        Assert.Equal("AAPL", ChartSymbols.Parse("aapl")!.Yahoo);
        Assert.Equal("SPY", ChartSymbols.Parse("SPY")!.Yahoo);
        Assert.Equal("^GSPC", ChartSymbols.Parse("^GSPC")!.Yahoo);
        Assert.Null(ChartSymbols.Parse(" "));
        Assert.Equal("No chart for ZZZZ.", ChartSymbols.Missing("ZZZZ"));

        var hits = ChartSymbols.ParseSearch("""{"quotes":[{"symbol":"AAPL","shortname":"Apple Inc."},{"symbol":"ETH-USD","longname":"Ethereum USD"}]}""");
        Assert.Equal("AAPL", hits[0].Yahoo);
        Assert.Equal("Apple Inc.", hits[0].Title);
        Assert.Contains("Apple", hits[0].ListLabel);
        Assert.Equal("ETH-USD", hits[1].Symbol);
        Assert.Contains("q=AAPL", ChartSymbols.SearchUrl("AAPL"));

        var recent = ChartSymbols.Remember(Array.Empty<ChartPick>(), hits[0]);
        recent = ChartSymbols.Remember(recent, hits[1]);
        recent = ChartSymbols.Remember(recent, hits[0]);
        Assert.Equal("AAPL", recent[0].Yahoo);
        Assert.Equal(2, recent.Count);
        var many = recent;
        for (var i = 0; i < 10; i++)
            many = ChartSymbols.Remember(many, new ChartPick("S" + i, "S" + i, "S" + i));
        Assert.Equal(8, many.Count);
        Assert.Equal("S9", many[0].Yahoo);

        Assert.Equal(BtcChart.Earliest, BtcChart.HistoryStart("BTC-USD"));
        Assert.Equal(new DateTimeOffset(1970, 1, 2, 0, 0, 0, TimeSpan.Zero), BtcChart.HistoryStart("AAPL"));
        Assert.Contains("/AAPL?", BtcChart.ChartUrl("AAPL", Now.AddHours(-1), Now, BtcChartStyle.Candles));
        Assert.Contains("interval=1m", BtcChart.ChartUrl("ETH-USD", Now.AddMinutes(-60), Now, BtcChartStyle.Candles));
        Assert.Contains("/BTC-USD?", BtcChart.ChartUrl(Now.AddDays(-30), Now));
        Assert.Equal(BtcChartPreset.Month, BtcChart.ParsePreset("nope"));
        Assert.Equal(BtcChartPreset.Hour, BtcChart.ParsePreset("1H"));

        Assert.True(BtcChart.TryRange(BtcChartPreset.All, Now, null, null, out var start, out _, out var error, BtcChart.HistoryStart("AAPL")));
        Assert.Equal("", error);
        Assert.Equal(1970, start.Year);
        Assert.False(BtcChart.TryRange(
            BtcChartPreset.Custom,
            Now,
            new DateOnly(2010, 1, 1),
            new DateOnly(2010, 2, 1),
            out _,
            out _,
            out var early,
            BtcChart.HistoryStart("BTC-USD")));
        Assert.Equal("Bitcoin prices start on 17 Sep 2014.", early);
        Assert.False(BtcChart.TryRange(
            BtcChartPreset.Custom,
            Now,
            new DateOnly(1969, 1, 1),
            new DateOnly(1971, 1, 1),
            out _,
            out _,
            out var stockEarly,
            BtcChart.HistoryStart("AAPL")));
        Assert.Equal("That date is before this symbol's history.", stockEarly);
    }
}
