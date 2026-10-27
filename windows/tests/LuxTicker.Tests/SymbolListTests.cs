namespace LuxTicker.Tests;

public class SymbolListTests
{
    static SymbolEntry E(string symbol, string? label = null, SymbolKind kind = SymbolKind.Us, string currency = "USD")
        => new() { Symbol = symbol, Label = label, Kind = kind, Currency = currency };

    [Fact]
    public void Default_list_matches_the_scoreboard()
    {
        var list = DefaultSymbols.Load();
        var symbols = list.Select(s => s.Symbol).ToArray();
        Assert.Equal(46, symbols.Length);
        Assert.Equal(symbols.Distinct(StringComparer.OrdinalIgnoreCase).Count(), symbols.Length);
        Assert.Equal(new[] { "BTC", "IBIT", "MSTR" }, symbols.Take(3).ToArray());
        Assert.Equal("SPCX", symbols[^1]);

        string[] required =
        [
            "BTC", "IBIT", "MSTR", "3350.T", "MPJPY", "XXI", "ASST", "MARA", "BLSH", "COIN", "CLSK", "DJT",
            "TSLA", "RIOT", "HUT", "XYZ", "ABTC", "GDC", "GLXY", "NXTT", "GEMI", "SVIA", "SWC.L", "IREN", "NAKA", "SLE",
            "STRC", "STRF", "STRK", "STRD", "SATA",
            "^GSPC", "^DJI", "^NDX", "^RUT", "CL=F", "GLD", "SLV", "^VIX",
            "AAPL", "MSFT", "GOOGL", "AMZN", "NVDA", "META", "SPCX"
        ];
        Assert.Equal(required.OrderBy(s => s, StringComparer.Ordinal), symbols.OrderBy(s => s, StringComparer.Ordinal));
        Assert.Equal(1, symbols.Count(s => s == "TSLA"));

        Assert.Equal(SymbolKind.Crypto, list.Single(s => s.Symbol == "BTC").Kind);
        Assert.Equal(SymbolKind.International, list.Single(s => s.Symbol == "3350.T").Kind);
        Assert.Equal("JPY", list.Single(s => s.Symbol == "3350.T").Currency);
        Assert.Equal("GBp", list.Single(s => s.Symbol == "SWC.L").Currency);
        Assert.Equal(SymbolKind.Continuous, list.Single(s => s.Symbol == "CL=F").Kind);
        Assert.Equal("S&P 500", list.Single(s => s.Symbol == "^GSPC").DisplayLabel);
        Assert.Equal("DOW", list.Single(s => s.Symbol == "^DJI").DisplayLabel);
        Assert.Equal("NDX", list.Single(s => s.Symbol == "^NDX").DisplayLabel);
        Assert.Equal("RUT", list.Single(s => s.Symbol == "^RUT").DisplayLabel);
        Assert.Equal("OIL", list.Single(s => s.Symbol == "CL=F").DisplayLabel);
        Assert.Equal("VIX", list.Single(s => s.Symbol == "^VIX").DisplayLabel);
    }

    [Fact]
    public void Add_remove_and_reorder_are_pure()
    {
        var list = new List<SymbolEntry> { E("BTC", kind: SymbolKind.Crypto), E("MSTR"), E("IBIT") };

        var moved = SymbolListEditor.Move(list, 2, -1);
        Assert.Equal(new[] { "BTC", "IBIT", "MSTR" }, moved.Select(s => s.Symbol).ToArray());
        Assert.Equal(new[] { "BTC", "MSTR", "IBIT" }, list.Select(s => s.Symbol).ToArray());

        Assert.Same(list, SymbolListEditor.Move(list, 0, -1));
        Assert.Same(list, SymbolListEditor.Move(list, 2, 1));

        var dragged = SymbolListEditor.MoveTo(moved, 0, 2);
        Assert.Equal(new[] { "IBIT", "MSTR", "BTC" }, dragged.Select(s => s.Symbol).ToArray());

        var removed = SymbolListEditor.RemoveAt(dragged, 1);
        Assert.Equal(new[] { "IBIT", "BTC" }, removed.Select(s => s.Symbol).ToArray());

        var reset = SymbolListEditor.Reset(DefaultSymbols.Load());
        Assert.Equal(46, reset.Count);
        Assert.Equal("BTC", reset[0].Symbol);
    }

    [Fact]
    public void Add_requires_a_found_quote_and_rejects_duplicates()
    {
        var list = new[] { E("MSTR") };
        var missing = SymbolListEditor.Add(list, SymbolEntry.Infer("NOPE", null), ProbeStatus.NotFound);
        Assert.False(missing.Ok);
        Assert.Contains("No quote", missing.Error);
        Assert.Single(missing.Symbols);

        var offline = SymbolListEditor.Add(list, SymbolEntry.Infer("AAPL", null), ProbeStatus.Unavailable);
        Assert.False(offline.Ok);
        Assert.Contains("Couldn't reach", offline.Error);

        var dup = SymbolListEditor.Add(list, SymbolEntry.Infer("mstr", "Strategy"), ProbeStatus.Found);
        Assert.False(dup.Ok);
        Assert.Contains("already", dup.Error);

        var blank = SymbolListEditor.Add(list, SymbolEntry.Infer("   ", null), ProbeStatus.Found);
        Assert.False(blank.Ok);

        var bad = SymbolListEditor.Add(list, new SymbolEntry { Symbol = "BAD SYM", Kind = SymbolKind.Us }, ProbeStatus.Found);
        Assert.False(bad.Ok);

        var added = SymbolListEditor.Add(list, SymbolEntry.Infer("aapl", "Apple"), ProbeStatus.Found);
        Assert.True(added.Ok);
        Assert.Equal("AAPL", added.Symbols[^1].Symbol);
        Assert.Equal("Apple", added.Symbols[^1].DisplayLabel);
        Assert.Equal(2, added.Symbols.Count);
    }

    [Fact]
    public void Inference_and_currency_keep_pence_distinct_from_pounds()
    {
        var btc = SymbolEntry.Infer("btc-usd", null);
        Assert.Equal("BTC", btc.Symbol);
        Assert.Equal(SymbolKind.Crypto, btc.Kind);

        var tokyo = SymbolEntry.Infer("3350.t", null);
        Assert.Equal("3350.T", tokyo.Symbol);
        Assert.Equal("JPY", tokyo.Currency);
        Assert.Equal(SymbolKind.International, tokyo.Kind);

        var london = SymbolEntry.Infer("swc.l", null);
        Assert.Equal("GBp", london.Currency);

        var pence = SymbolEntry.Infer("AAPL", null).WithQuoteCurrency("GBp");
        var pounds = SymbolEntry.Infer("AAPL", null).WithQuoteCurrency("GBP");
        Assert.Equal(SymbolKind.International, pence.Kind);
        Assert.Equal("GBp", pence.Currency);
        Assert.Equal("GBP", pounds.Currency);
        Assert.Equal("10p", PriceFormat.Format(10, pence.Currency));
        Assert.Equal("£10.00", PriceFormat.Format(10, pounds.Currency));
        Assert.Equal("¥942", PriceFormat.Format(942, "JPY"));
        Assert.Equal("+1.84%", PriceFormat.FormatChange(1.844m));
        Assert.Equal("-0.22%", PriceFormat.FormatChange(-0.224m));
        Assert.Equal("+0.00%", PriceFormat.FormatChange(0));
    }

    [Fact]
    public void Reorder_persists_and_survives_a_restart()
    {
        var dir = Path.Combine(Path.GetTempPath(), "lux-ticker-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var defaults = new[] { E("BTC", kind: SymbolKind.Crypto), E("MSTR"), E("IBIT"), E("^GSPC", "S&P 500") };
            var store = new SymbolListStore(Path.Combine(dir, "symbols.json"), () => defaults);

            var first = store.Load();
            Assert.True(first.MissingFile);
            Assert.Equal(new[] { "BTC", "MSTR", "IBIT", "^GSPC" }, first.Symbols.Select(s => s.Symbol).ToArray());

            var reordered = SymbolListEditor.Move(first.Symbols, 1, 1);
            reordered = SymbolListEditor.RemoveAt(reordered, 0);
            var added = SymbolListEditor.Add(reordered, SymbolEntry.Infer("nvda", null), ProbeStatus.Found);
            Assert.True(added.Ok);
            store.Save(added.Symbols);

            var restarted = new SymbolListStore(store.Path, () => defaults).Load();
            Assert.False(restarted.MissingFile);
            Assert.Null(restarted.Error);
            Assert.Equal(new[] { "IBIT", "MSTR", "^GSPC", "NVDA" }, restarted.Symbols.Select(s => s.Symbol).ToArray());
            Assert.Equal("S&P 500", restarted.Symbols.Single(s => s.Symbol == "^GSPC").DisplayLabel);

            File.WriteAllText(store.Path, "{ not json");
            var broken = store.Load();
            Assert.NotNull(broken.Error);
            Assert.Equal(defaults.Select(s => s.Symbol), broken.Symbols.Select(s => s.Symbol));

            File.WriteAllText(store.Path, """{"symbols":[]}""");
            var empty = store.Load();
            Assert.Null(empty.Error);
            Assert.Empty(empty.Symbols);

            File.WriteAllText(store.Path, """
                {"symbols":[
                  {"symbol":"bad symbol","kind":"us"},
                  {"symbol":"msft","label":"Microsoft","kind":"us","currency":"USD"},
                  {"symbol":""}
                ]}
                """);
            var skipped = store.Load();
            Assert.Null(skipped.Error);
            Assert.Equal(new[] { "MSFT" }, skipped.Symbols.Select(s => s.Symbol).ToArray());
            Assert.Equal("Microsoft", skipped.Symbols[0].DisplayLabel);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void Settings_roundtrip_and_bad_values_fall_back()
    {
        var dir = Path.Combine(Path.GetTempPath(), "lux-ticker-settings-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var path = Path.Combine(dir, "settings.json");
            var store = new SettingsStore(path);
            Assert.Equal(AppSettings.Default, store.Load());

            var saved = new AppSettings(DockEdge.Top, ScrollSpeed.Fast, true);
            store.Save(saved);
            Assert.Equal(saved, new SettingsStore(path).Load());
            var json = File.ReadAllText(path);
            Assert.Contains("\"dock\": \"top\"", json);
            Assert.Contains("\"speed\": \"fast\"", json);
            Assert.DoesNotContain("Program Files", json);

            File.WriteAllText(path, """{"dock":"sideways","speed":"ludicrous","startWithWindows":false}""");
            var fallback = store.Load();
            Assert.Equal(DockEdge.Bottom, fallback.Dock);
            Assert.Equal(ScrollSpeed.Normal, fallback.Speed);
            Assert.False(fallback.StartWithWindows);
            Assert.True(fallback.LuxWirePosts);

            File.WriteAllText(path, """{"luxWirePosts":false}""");
            Assert.False(store.Load().LuxWirePosts);
            Assert.Equal(0, store.Load().VerticalOffsetDip);
            Assert.Equal(BarSurfaceMath.DefaultOpacity, store.Load().BackgroundOpacity);
            Assert.Equal(TickerSpacing.Default, store.Load().SymbolGap);
            Assert.Null(store.Load().TickerFontName);
            File.WriteAllText(path, """{"speed":"fast","opacity":0.9}""");
            var carried = store.Load();
            Assert.Equal(160, carried.ScrollPixelsPerSecond);
            Assert.Equal(1, carried.BackgroundOpacity);
            File.WriteAllText(path, """{"backgroundOpacity":0.5}""");
            Assert.Equal(1, store.Load().BackgroundOpacity);
            Assert.False(store.Load().BackgroundOpacityChosen);
            File.WriteAllText(path, """{"backgroundOpacity":0.5,"backgroundOpacityChosen":true}""");
            Assert.Equal(0.5, store.Load().BackgroundOpacity);
            File.WriteAllText(path, """{"backgroundOpacity":0.35}""");
            Assert.Equal(0.35, store.Load().BackgroundOpacity);
            Assert.Equal("#F6F2E8", store.Load().BackgroundColor);
            File.WriteAllText(path, """{"backgroundColor":"#161412","backgroundOpacity":0.8,"backgroundOpacityChosen":true,"textColor":"#ABCDEF","priceColor":"#112233"}""");
            Assert.Equal("#161412", store.Load().BackgroundColor);
            Assert.Equal("#ABCDEF", store.Load().TextColor);
            Assert.Equal("#112233", store.Load().PriceColor);
            Assert.Equal("Custom", store.Load().AppearancePreset);
            Assert.False(store.Load().LiveScrollUpdates);
            Assert.Equal(0.8, store.Load().BackgroundOpacity);
            File.WriteAllText(path, """{"backgroundColor":"nope"}""");
            Assert.Equal("#F6F2E8", store.Load().BackgroundColor);
            Assert.Equal(ScrollSpeed.Fast, carried.Speed);
            store.Save(store.Load() with { VerticalOffsetDip = 48 });
            Assert.Equal(48, new SettingsStore(path).Load().VerticalOffsetDip);
            Assert.Contains("\"verticalOffsetDip\": 48", File.ReadAllText(path));
            store.Save(store.Load() with { VerticalOffsetDip = 4000 });
            Assert.Equal(BarOffset.MaxDip, new SettingsStore(path).Load().VerticalOffsetDip);
            store.Save(store.Load() with { LuxWirePosts = false });
            Assert.False(new SettingsStore(path).Load().LuxWirePosts);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void Paths_and_startup_key_are_per_user()
    {
        Assert.Equal("LuxTicker", AppPaths.FolderName);
        Assert.Equal("symbols.json", AppPaths.SymbolsFileName);
        Assert.EndsWith(Path.Combine("LuxTicker", "symbols.json"), AppPaths.SymbolsPath());
        Assert.EndsWith(Path.Combine("LuxTicker", "settings.json"), AppPaths.SettingsPath());
        Assert.EndsWith(Path.Combine("LuxTicker", "x-auth.json"), AppPaths.XAuthPath());
        Assert.EndsWith(Path.Combine("lux-wire", "seen.json"), AppPaths.LuxWireSeenPath());
        Assert.EndsWith(Path.Combine("LuxWire Ticker.lnk"), AppPaths.StartMenuShortcut());
        Assert.Contains(Path.Combine("Start Menu", "Programs"), AppPaths.StartMenuShortcut());
        Assert.Equal(@"Software\Microsoft\Windows\CurrentVersion\Run", RunKey.SubKey);
        Assert.Equal("LuxTicker", RunKey.ValueName);
        Assert.Equal("\"C:\\Users\\me\\LuxTicker.exe\"", RunKey.QuoteCommand(@"C:\Users\me\LuxTicker.exe"));
        Assert.DoesNotContain("LocalMachine", RunKey.SubKey);
        Assert.Equal(48, ScrollPhysics.PixelsPerSecond(ScrollSpeed.Slow));
        Assert.Equal(90, ScrollPhysics.PixelsPerSecond(ScrollSpeed.Normal));
        Assert.Equal(160, ScrollPhysics.PixelsPerSecond(ScrollSpeed.Fast));
    }

    [Fact]
    public void Upgrade_keeps_saved_settings_and_defaults_only_missing_keys()
    {
        var dir = Path.Combine(Path.GetTempPath(), "lux-upgrade-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var settingsPath = Path.Combine(dir, "settings.json");
            File.WriteAllText(settingsPath, """
            {
              "dock": "top",
              "speed": "slow",
              "startWithWindows": true,
              "luxWirePosts": false,
              "opacity": 0.72,
              "alwaysOnTop": false,
              "clickThrough": true,
              "textScale": 1.4,
              "verticalOffsetDip": -12,
              "tickerFontName": "Consolas",
              "symbolGap": 2.5,
              "textColor": "#CC5500",
              "priceColor": "#111111",
              "changeUpColor": "#1B7A3A",
              "changeDownColor": "#C0392B",
              "backgroundColor": "#F6F2E8",
              "backgroundOpacity": 0.8,
              "backgroundOpacityChosen": true,
              "chartRange": "1W",
              "chartStyle": "candles",
              "chartCrosshair": false,
              "appearancePreset": "Wall Street"
            }
            """);
            var store = new SettingsStore(settingsPath);
            var loaded = store.Load();
            Assert.Equal(DockEdge.Top, loaded.Dock);
            Assert.Equal(ScrollSpeed.Slow, loaded.Speed);
            Assert.True(loaded.StartWithWindows);
            Assert.False(loaded.LuxWirePosts);
            Assert.Equal(0.72, loaded.Opacity);
            Assert.False(loaded.AlwaysOnTop);
            Assert.True(loaded.ClickThrough);
            Assert.Equal(1.4, loaded.TextScale);
            Assert.Equal(-12, loaded.VerticalOffsetDip);
            Assert.Equal("Consolas", loaded.TickerFontName);
            Assert.Equal(2.5, loaded.SymbolGap);
            Assert.Equal("#CC5500", loaded.TextColor);
            Assert.Equal("#111111", loaded.PriceColor);
            Assert.Equal("#1B7A3A", loaded.ChangeUpColor);
            Assert.Equal("#C0392B", loaded.ChangeDownColor);
            Assert.Equal("#F6F2E8", loaded.BackgroundColor);
            Assert.Equal(0.8, loaded.BackgroundOpacity);
            Assert.True(loaded.BackgroundOpacityChosen);
            Assert.Equal("1W", loaded.ChartRange);
            Assert.Equal("candles", loaded.ChartStyle);
            Assert.False(loaded.ChartCrosshair);
            Assert.Equal(AppearancePresets.WallStreet, loaded.AppearancePreset);
            Assert.NotEqual(AppearancePresets.WallSymbol, loaded.TextColor);
            Assert.NotEqual(AppearancePresets.WallBackground, loaded.BackgroundColor);
            Assert.True(loaded.ShowPre);
            Assert.False(loaded.ShowAfterHours);
            Assert.False(loaded.ShowSparklines);
            Assert.True(loaded.ScrollLoop);
            Assert.True(loaded.LiveScrollUpdates);
            Assert.False(loaded.PinBtc);

            store.Save(loaded);
            Assert.Equal(loaded, new SettingsStore(settingsPath).Load());

            var symbolsPath = Path.Combine(dir, "symbols.json");
            File.WriteAllText(symbolsPath, """{"symbols":[{"symbol":"MSTR","kind":"stock","label":"Strategy"}]}""");
            var symbols = new SymbolListStore(symbolsPath).Load();
            Assert.False(symbols.MissingFile);
            Assert.Null(symbols.Error);
            Assert.Single(symbols.Symbols);
            Assert.Equal("MSTR", symbols.Symbols[0].Symbol);
            Assert.True(DefaultSymbols.Load().Count > 1);
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }
}
