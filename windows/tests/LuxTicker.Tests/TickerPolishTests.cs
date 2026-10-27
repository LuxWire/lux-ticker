namespace LuxTicker.Tests;

public class TickerPolishTests
{
    [Fact]
    public void After_hours_drops_the_tag_and_pre_keeps_it()
    {
        var entry = new SymbolEntry { Symbol = "MSTR", Kind = SymbolKind.Us, Currency = "USD" };
        var snap = new QuoteSnapshot { RegularPrice = 10m, PostPrice = 11m, PrePrice = 9m, PreviousClose = 10m, Currency = "USD" };

        var after = QuotePresenter.Present(entry, snap, SessionPhase.AfterHours);
        Assert.Equal(new[] { "MSTR", "11.00", "+10.00%" }, after.Runs.Select(run => run.Text).ToArray());
        Assert.DoesNotContain(after.Runs, run => run.Text == "AH");
        Assert.Equal(TickerTextColor.UpDefault, after.Runs[2].Color);

        var pre = QuotePresenter.Present(entry, snap, SessionPhase.PreMarket, showPercent: false);
        Assert.Equal(new[] { "MSTR", "PRE", "9.00" }, pre.Runs.Select(run => run.Text).ToArray());
        Assert.Equal("MSTR", pre.Symbol);
    }

    [Fact]
    public void Font_size_and_scroll_land_on_whole_pixels()
    {
        const double dpi = 144;
        var dip = TickerPixels.FontDip(23, 1, dpi);
        var px = dip * dpi / 96.0;
        Assert.Equal(Math.Round(px), px);
        Assert.True(px >= 11);

        var snapped = TickerPixels.SnapDip(10.4, 120);
        var snappedPx = snapped * 120 / 96.0;
        Assert.Equal(Math.Round(snappedPx), snappedPx, 5);
    }

    [Fact]
    public void Quote_links_use_tradingview_for_crypto_and_yahoo_otherwise()
    {
        Assert.Equal("https://www.tradingview.com/symbols/COINBASE-BTCUSD/", QuoteLinks.For("BTC", SymbolKind.Crypto));
        Assert.Equal("https://finance.yahoo.com/quote/MSTR", QuoteLinks.For("MSTR", SymbolKind.Us));
        Assert.Contains("%5E", QuoteLinks.For("^GSPC", SymbolKind.Us));
    }

    [Fact]
    public void Paper_ink_keeps_palette_constants_and_remaps_for_the_bar()
    {
        Assert.Equal("#F7931A", PaperInk.For(TickerPalette.Amber));
        Assert.Equal("#161412", PaperInk.For(TickerPalette.White));
        Assert.Equal("#1B7A3A", PaperInk.For(TickerPalette.Green));
        Assert.Equal("#C0392B", PaperInk.For(TickerPalette.Red));
        Assert.Equal("#55555A", PaperInk.For(TickerPalette.Tag));
        Assert.Equal("#78787D", PaperInk.For(TickerPalette.Flat));
        Assert.Equal("#F6F2E8", PaperInk.Background);
        Assert.Equal("#EFEADE", PaperInk.Panel);
        Assert.Equal("#282622", PaperInk.Ink);
        Assert.Equal("#161412", PaperInk.Body);
        Assert.Equal("#F7931A", PaperInk.Accent);
        Assert.Equal(0x00E8F2F6, PaperInk.ColorRef(PaperInk.Background));
        Assert.Equal(0x001A93F7, PaperInk.ColorRef(PaperInk.Accent));
        Assert.Equal("#FFB000", TickerPalette.Amber);
    }

    [Fact]
    public void Price_alerts_fire_on_the_cross_only()
    {
        Assert.False(PriceAlertEngine.Crossed(null, 10m, above: true, level: 9m));
        Assert.False(PriceAlertEngine.Crossed(8m, 8.5m, above: true, level: 9m));
        Assert.True(PriceAlertEngine.Crossed(8m, 9m, above: true, level: 9m));
        Assert.False(PriceAlertEngine.Crossed(10m, 11m, above: true, level: 9m));
        Assert.True(PriceAlertEngine.Crossed(10m, 9m, above: false, level: 9m));
        Assert.False(PriceAlertEngine.Crossed(8m, 7m, above: false, level: 9m));

        var dir = Path.Combine(Path.GetTempPath(), "lux-alerts-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var path = Path.Combine(dir, "alerts.json");
            var store = new PriceAlertStore(path);
            Assert.Empty(store.Load());
            store.Save([new PriceAlert("MSTR", true, 150.5m)]);
            var loaded = Assert.Single(new PriceAlertStore(path).Load());
            Assert.Equal("MSTR", loaded.Symbol);
            Assert.True(loaded.Above);
            Assert.Equal(150.5m, loaded.Price);
            Assert.EndsWith(Path.Combine("LuxTicker", "alerts.json"), AppPaths.AlertsPath());
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void Settings_defaults_cover_the_1_2_toggles()
    {
        var defaults = AppSettings.Default;
        Assert.Equal(0.9, defaults.Opacity);
        Assert.True(defaults.AlwaysOnTop);
        Assert.True(defaults.ScrollEnabled);
        Assert.True(defaults.ShowPercent);
        Assert.True(defaults.CompactLayout);
        Assert.False(defaults.ShowSparklines);
        Assert.False(defaults.ClickThrough);
        Assert.False(defaults.PinBtc);
        Assert.False(defaults.LuxWireDigest);
        Assert.True(defaults.LuxWirePosts);
        Assert.Equal(3, defaults.LuxWirePollMinutes);
        Assert.Equal(1, defaults.TextScale);
        Assert.Equal(0, defaults.VerticalOffsetDip);
        Assert.Equal(BarOffset.MinDip, (defaults with { VerticalOffsetDip = -5000 }).Clamped().VerticalOffsetDip);
        Assert.Equal(BarOffset.MaxDip, (defaults with { VerticalOffsetDip = 9000 }).Clamped().VerticalOffsetDip);

        var dir = Path.Combine(Path.GetTempPath(), "lux-settings-12-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var path = Path.Combine(dir, "settings.json");
            File.WriteAllText(path, """{"luxWirePosts":false}""");
            var loaded = new SettingsStore(path).Load();
            Assert.False(loaded.LuxWirePosts);
            Assert.Equal(0.9, loaded.Opacity);
            Assert.True(loaded.AlwaysOnTop);
            Assert.False(loaded.PinBtc);
            Assert.Equal(3, loaded.LuxWirePollMinutes);
            Assert.Equal(0, loaded.VerticalOffsetDip);
            Assert.Equal("#CC5500", loaded.TextColor);
            Assert.Equal("#F6F2E8", loaded.BackgroundColor);
            Assert.False(loaded.LiveScrollUpdates);
            Assert.NotEqual(AppearancePresets.WallStreet, loaded.AppearancePreset);
            Assert.True(loaded.ShowPre);
            Assert.False(loaded.ShowAfterHours);
            Assert.True(loaded.ScrollLoop);
            Assert.False(loaded.ScrollRight);
            Assert.Equal("1M", loaded.ChartRange);
            Assert.Equal("line", loaded.ChartStyle);
            Assert.True(loaded.ChartCrosshair);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void Burnt_orange_is_the_text_default_and_section_reset_restores_it()
    {
        Assert.Equal("#CC5500", TickerTextColor.Default);
        Assert.Equal("#CC5500", TickerTextColor.Clamp(null));
        Assert.Equal("#CC5500", TickerTextColor.Clamp("nope"));
        Assert.Equal("#AABBCC", TickerTextColor.Clamp("aabbcc"));
        Assert.Equal(AppearancePresets.WallSymbol, AppSettings.Default.TextColor);
        Assert.Equal(AppearancePresets.WallPrice, AppSettings.Default.PriceColor);
        Assert.Equal(AppearancePresets.WallBackground, AppSettings.Default.BackgroundColor);
        Assert.Equal(AppearancePresets.WallStreet, AppSettings.Default.AppearancePreset);
        Assert.True(AppSettings.Default.LiveScrollUpdates);
        Assert.True(AppSettings.Default.AlwaysOnTop);

        var tinted = AppSettings.Default with
        {
            TextColor = "#FFFFFF",
            PriceColor = "#FFFFFF",
            ChangeUpColor = "#CC5500",
            ChangeDownColor = "#161412",
            BackgroundOpacity = 0.4,
            BackgroundOpacityChosen = true,
            BackgroundColor = "#161412",
            AlwaysOnTop = false,
            ChartRange = "1H",
            ShowPre = false
        };
        var appearance = SettingsReset.Apply(tinted, SettingsReset.Appearance);
        Assert.Equal(AppearancePresets.WallSymbol, appearance.TextColor);
        Assert.Equal(AppearancePresets.WallPrice, appearance.PriceColor);
        Assert.Equal(AppearancePresets.WallUp, appearance.ChangeUpColor);
        Assert.Equal(AppearancePresets.WallDown, appearance.ChangeDownColor);
        Assert.Equal(1, appearance.BackgroundOpacity);
        Assert.True(appearance.BackgroundOpacityChosen);
        Assert.Equal(AppearancePresets.WallBackground, appearance.BackgroundColor);
        Assert.Equal(AppearancePresets.WallStreet, appearance.AppearancePreset);
        Assert.Equal(AppearancePresets.WallBackground, AppSettings.Default.BackgroundColor);
        Assert.Equal(tinted, SettingsReset.Apply(tinted, SettingsReset.About));
        Assert.Equal(tinted, SettingsReset.Apply(tinted, SettingsReset.Updates));
        Assert.False(SettingsReset.Apply(tinted, SettingsReset.Appearance).AlwaysOnTop);
        Assert.True(SettingsReset.Apply(tinted, SettingsReset.Position).AlwaysOnTop);
        Assert.Equal("#FFFFFF", SettingsReset.Apply(tinted, SettingsReset.Position).Clamped().TextColor);
        Assert.Equal("1M", SettingsReset.Apply(tinted, SettingsReset.Charts).ChartRange);
        Assert.True(SettingsReset.Apply(tinted, null).ShowPre);
        Assert.Equal(AppearancePresets.WallSymbol, SettingsReset.Apply(tinted, null).TextColor);
        Assert.True(SettingsReset.Apply(tinted with { LiveScrollUpdates = false }, SettingsReset.Motion).LiveScrollUpdates);
        Assert.Equal("Nasdaq", AppearancePresets.Find("nasdaq")!.Name);
        Assert.Equal("Custom", AppearancePresets.Match("#112233", "#445566", "#778899", "#AABBCC", "#DDEEFF"));
        Assert.Equal(1, TapeFlash.Direction(10m, 11m));
        Assert.Equal(-1, TapeFlash.Direction(10m, 9m));
        Assert.Equal(0, TapeFlash.Direction(10m, 10m));
        Assert.Equal(0, TapeFlash.Direction(null, 10m));
        Assert.Equal(450, TapeFlash.HoldMilliseconds);

        var ink = QuotePresenter.Present(
            new SymbolEntry { Symbol = "AAPL", Kind = SymbolKind.Us, Currency = "USD" },
            new QuoteSnapshot { RegularPrice = 10m, PreviousClose = 10m, Currency = "USD" },
            SessionPhase.Regular,
            textColor: "#161412");
        Assert.Equal("#161412", ink.Runs[0].Color);
        Assert.Equal("#161412", ink.Runs[1].Color);

        var split = QuotePresenter.Present(
            new SymbolEntry { Symbol = "AAPL", Kind = SymbolKind.Us, Currency = "USD" },
            new QuoteSnapshot { RegularPrice = 12m, PreviousClose = 10m, Currency = "USD" },
            SessionPhase.Regular,
            textColor: "#CC5500",
            priceColor: "#FFFFFF",
            changeUp: "#112233",
            changeDown: "#445566");
        Assert.Equal("#CC5500", split.Runs[0].Color);
        Assert.Equal("#FFFFFF", split.Runs[1].Color);
        Assert.Equal("#112233", split.Runs[2].Color);

        var fonts = TickerFontList.Build(["Consolas", "Arial", "Cascadia Mono"]);
        Assert.Equal("Recommended ticker fonts", fonts[0].Label);
        Assert.True(fonts[0].Header);
        Assert.Equal(new[] { "Cascadia Mono", "Consolas" }, fonts.Where(row => !row.Header).Select(row => row.Name!).Take(2).ToArray());
        Assert.Contains(fonts, row => row.Header && row.Label == "────────");
        Assert.Contains(fonts, row => row.Name == "Arial");
        Assert.Equal(1, fonts.Count(row => row.Name == "Consolas"));
        Assert.Contains(TickerFontList.Build(["Arial"]), row => row.Name == "Cascadia Mono");

        var matches = SettingsSearch.Filter("font");
        Assert.Contains(matches, entry => entry.Name == "Font");
        Assert.Contains(SettingsSearch.Filter("chart"), entry => entry.Section == "Charts");
        Assert.Contains(SettingsSearch.Filter("always"), entry => entry.Name == "Always on top");
    }

    [Fact]
    public void Toast_titles_subscribers_images_and_digests()
    {
        var filler = new LuxWirePost("https://x.com/LuxWire/status/1", false, null, "FILLER", "hash", "1");
        Assert.Equal("Filler", filler.Title);

        var news = new LuxWirePost(
            "https://x.com/LuxWire/status/2", true, null, "NEWS", "desk", "2", "https://cdn.example/card.png");
        Assert.Equal("News · Subscribers", news.Title);
        Assert.Equal("https://cdn.example/card.png", news.ImageUrl);
        var xml = LuxWireToast.Xml(news, null);
        Assert.Contains("News · Subscribers", xml);
        Assert.Contains("placement=\"hero\"", xml);
        Assert.Contains("https://cdn.example/card.png", xml);
        Assert.Contains("https://x.com/LuxWire/status/2", xml);

        var blocked = new LuxWirePost("https://x.com/LuxWire/status/3", false, null, null, null, "3", "file:///tmp/x.png");
        Assert.Equal("Lux Wire", blocked.Title);
        Assert.DoesNotContain("file:", LuxWireToast.Xml(blocked, null));

        var paper = LuxWireToast.Xml(filler, null, "/tmp/toast-paper.png");
        Assert.Contains("placement=\"hero\"", paper);
        Assert.Contains("Filler", paper);

        var second = new LuxWirePost("https://x.com/LuxWire/status/4", false, null, "ALERT", "move", "b");
        var digest = LuxWireDigest.Combine([filler, second]);
        Assert.Equal("DIGEST", digest.SlotType);
        Assert.Contains("2 new @LuxWire posts", digest.Body);
        Assert.Equal("Digest", digest.Title);
    }

    [Fact]
    public void The_bar_stays_opaque_and_on_a_monitor()
    {
        Assert.Equal(1, BarVisibility.WindowOpacity(0.9));
        Assert.Equal(1, BarVisibility.WindowOpacity(0));
        Assert.Equal(0.9, BarVisibility.SanitizeOpacity(0));
        Assert.Equal(0.9, BarVisibility.SanitizeOpacity(0.05));
        Assert.Equal(0.9, BarVisibility.SanitizeOpacity(double.NaN));
        Assert.Equal(0.9, BarVisibility.SanitizeOpacity(0.9));
        Assert.False(AppSettings.Default.ClickThrough);
        Assert.Equal(DockEdge.Bottom, AppSettings.Default.Dock);

        var primary = new PxRect(0, 0, 1920, 1080);
        var left = new PxRect(-1920, 0, 1920, 1080);
        var aboveTaskbar = new PxRect(0, 1080 - 48 - 24, 1920, 24);
        Assert.True(BarVisibility.IsOnScreen(aboveTaskbar, new[] { primary }));
        Assert.True(BarVisibility.IsOnScreen(new PxRect(-1920, 1056, 1920, 24), new[] { left, primary }));
        Assert.False(BarVisibility.IsOnScreen(new PxRect(-32000, 0, 640, 23), new[] { primary }));
        Assert.False(BarVisibility.IsOnScreen(new PxRect(0, 1000, 1920, 2), new[] { primary }));
        Assert.False(BarVisibility.IsOnScreen(new PxRect(4000, 0, 200, 24), new[] { primary }));

        var root = Repo.Root();
        var main = File.ReadAllText(Path.Combine(root, "src", "LuxTicker.App", "MainWindow.xaml.cs"));
        Assert.DoesNotContain("Opacity = _settings.Opacity", main);
        Assert.Contains("BarVisibility.WindowOpacity", main);
        var controller = File.ReadAllText(Path.Combine(root, "src", "LuxTicker.App", "AppBarController.cs"));
        Assert.Contains("if (Topmost || _overTaskbar)", controller);
        Assert.Contains("insert = NativeMethods.HwndTopmost", controller);
        Assert.Contains("IntPtr.Zero", controller);
        Assert.Contains("WS_EX_LAYERED", controller);
        Assert.Contains("WithVerticalOffset", controller);
        Assert.Contains("VerticalOffsetDip", controller);
        Assert.Contains("TaskbarCreated", controller);
        Assert.Contains("StuckFullscreen", File.ReadAllText(Path.Combine(root, "src", "LuxTicker.Core", "TickerLayout.cs")));
        var strip = File.ReadAllText(Path.Combine(root, "src", "LuxTicker.App", "TickerStrip.cs"));
        Assert.Contains("TickerMotion.Advance", strip);
        Assert.Contains("Canvas.SetLeft", strip);
        Assert.Contains("HorizontalAlignment.Stretch", strip);
        Assert.DoesNotContain("FromMilliseconds(80)", strip);
        var settingsXaml = File.ReadAllText(Path.Combine(root, "src", "LuxTicker.App", "SettingsWindow.xaml"));
        Assert.Contains("Check for updates", settingsXaml);
        Assert.Contains("Vertical position", settingsXaml);
        Assert.Contains("x:Name=\"OffsetSlider\"", settingsXaml);
        Assert.Contains("Minimum=\"-240\"", settingsXaml);
        Assert.Contains("Maximum=\"640\"", settingsXaml);
        Assert.Contains("#F6F2E8", settingsXaml);
        Assert.DoesNotContain("#FF6A00", settingsXaml);
        Assert.Contains("Reset this section to defaults", settingsXaml);
        Assert.Contains("Reset ALL settings to defaults", settingsXaml);
        Assert.Contains("Content=\"Updates\"", settingsXaml);
        Assert.Contains("Content=\"About\"", settingsXaml);
        Assert.Contains("Padding=\"0,0,24,0\"", settingsXaml);
        Assert.Contains("x:Name=\"PageUpdates\"", settingsXaml);
        Assert.Contains("x:Name=\"PriceColorBox\"", settingsXaml);
        Assert.Contains("Background color", settingsXaml);
        Assert.Contains("x:Name=\"BackgroundColorBox\"", settingsXaml);
        Assert.Contains("Custom…", settingsXaml);
        Assert.Contains("Wall Street", settingsXaml);
        Assert.Contains("x:Name=\"LookBox\"", settingsXaml);
        Assert.Contains("Live updates while scrolling", settingsXaml);
        Assert.Contains("Open chart", settingsXaml);
        Assert.Contains("#D4A017", settingsXaml);
        Assert.Contains("#F0D48A", settingsXaml);
        Assert.Contains("Follow on X", settingsXaml);
        Assert.Contains("Subscribe", settingsXaml);
        Assert.Contains("Follow @LuxWire on X for Bitcoin news, treasury company buys, and market moves.", settingsXaml);
        Assert.Contains("x:Name=\"WhatsNewHost\"", settingsXaml);
        Assert.Contains("x:Name=\"PageLux\"", settingsXaml);
        Assert.Contains("LuxWire post alerts", settingsXaml);
        Assert.DoesNotContain("App bearer", settingsXaml);
        Assert.DoesNotContain("X client id", settingsXaml);
        Assert.DoesNotContain("Save X login", settingsXaml);
        Assert.DoesNotContain("Sign in to X", settingsXaml);
        Assert.DoesNotContain("No X token yet", settingsXaml);
        Assert.DoesNotContain("Simulate Lux Wire post", settingsXaml);
        Assert.DoesNotContain("settings.json", settingsXaml);
        Assert.DoesNotContain("PathText", settingsXaml);
        var updates = settingsXaml[settingsXaml.IndexOf("x:Name=\"PageUpdates\"", StringComparison.Ordinal)..settingsXaml.IndexOf("x:Name=\"PageAbout\"", StringComparison.Ordinal)];
        var about = settingsXaml[settingsXaml.IndexOf("x:Name=\"PageAbout\"", StringComparison.Ordinal)..];
        Assert.Contains("What's new", updates);
        Assert.Contains("Check for updates", updates);
        Assert.DoesNotContain("Reset this section to defaults", updates);
        Assert.DoesNotContain("Reset this section to defaults", about);
        Assert.Contains("creator-subscriptions/subscribe", File.ReadAllText(Path.Combine(root, "src", "LuxTicker.App", "SettingsWindow.xaml.cs")));
        var chartXaml = File.ReadAllText(Path.Combine(root, "src", "LuxTicker.App", "BtcChartWindow.xaml"));
        Assert.DoesNotContain("What's new", chartXaml);
        Assert.DoesNotContain("IsHitTestVisible = !_settings.ClickThrough", main);
        Assert.Contains("ButtonScreenHit", main);

        var dir = Path.Combine(Path.GetTempPath(), "lux-visible-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var path = Path.Combine(dir, "settings.json");
            File.WriteAllText(path, """{"opacity":0,"dock":"top"}""");
            var loaded = new SettingsStore(path).Load();
            Assert.Equal(0.9, loaded.Opacity);
            Assert.Equal(DockEdge.Top, loaded.Dock);

            StartupReport.Note("ticker opacity=1.00 rect=0,1032 1920x24 monitor=\\\\.\\DISPLAY1", dir);
            var log = File.ReadAllText(Path.Combine(dir, "startup.log"));
            Assert.Contains("opacity=1.00", log);
            Assert.Contains("1920x24", log);
            Assert.Contains("DISPLAY1", log);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }
}
