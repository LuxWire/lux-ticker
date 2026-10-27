using SixLabors.Fonts;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace LuxTicker.Tests;

public class PreviewRenderTests
{
    [Fact]
    public void Render_bar_matches_the_after_hours_layout()
    {
        var items = SampleItems();
        var fontPath = System.IO.Path.Combine(Repo.Root(), "src", "LuxTicker.App", "Fonts", "CascadiaMono-Bold.ttf");
        Assert.True(File.Exists(fontPath), fontPath);

        var dir = Environment.GetEnvironmentVariable("LUX_ARTIFACT_DIR");
        if (string.IsNullOrWhiteSpace(dir))
            dir = System.IO.Path.Combine(Repo.Root(), "artifacts");
        Directory.CreateDirectory(dir);

        using var native = DrawBar(fontPath, 1920, 23, items);
        var nativePath = System.IO.Path.Combine(dir, "lux-ticker-bar.png");
        native.Save(nativePath);

        using var scaled = native.Clone(ctx => ctx.Resize(native.Width * 3, native.Height * 3, KnownResamplers.NearestNeighbor));
        var scaledPath = System.IO.Path.Combine(dir, "lux-ticker-bar-3x.png");
        scaled.Save(scaledPath);

        using var scene = DrawBottomDock(fontPath, items);
        var scenePath = System.IO.Path.Combine(dir, "lux-ticker-bottom-dock.png");
        scene.Save(scenePath);

        Assert.Equal(1920, native.Width);
        Assert.Equal(23, native.Height);
        Assert.True(new FileInfo(nativePath).Length > 1000);
        Assert.True(Contains(native, (r, g, b) => r > 180 && g > 70 && g < 110 && b < 30), "burnt orange symbol pixels");
        Assert.True(Contains(native, (r, g, b) => g > 80 && g > r && g > b && r < 100), "green change pixels");
        Assert.True(Contains(native, (r, g, b) => r > 160 && g < 120 && b < 120), "red change pixels");
        Assert.True(File.Exists(scaledPath));
        Assert.True(File.Exists(scenePath));
    }

    static bool Contains(Image<Rgba32> image, Func<byte, byte, byte, bool> match)
    {
        for (int y = 0; y < image.Height; y++)
        {
            for (int x = 0; x < image.Width; x++)
            {
                var p = image[x, y];
                if (p.A > 200 && match(p.R, p.G, p.B))
                    return true;
            }
        }

        return false;
    }

    static Image<Rgba32> DrawBottomDock(string fontPath, IReadOnlyList<DisplayItem> items)
    {
        const int width = 1400;
        const int captionH = 36;
        const int contentH = 78;
        const int barH = 46;
        const int taskH = 90;
        var image = new Image<Rgba32>(width, captionH + contentH + barH + taskH, Color.ParseHex("2A2A2A"));
        image.Mutate(ctx =>
        {
            ctx.Fill(Color.ParseHex("1E1E1E"), new RectangleF(0, captionH, width, contentH));
            ctx.Fill(Color.ParseHex("111111"), new RectangleF(0, captionH + contentH + barH, width, taskH));
            ctx.Fill(Color.ParseHex("2E2E2E"), new RectangleF(0, captionH + contentH + barH, width, 1));
        });

        var collection = new FontCollection();
        var family = collection.Add(fontPath);
        var caption = Create(family, 15);
        image.Mutate(ctx => ctx.DrawText(
            "LuxWire Ticker   bottom dock   bar is half the taskbar (23 px on a 45 px taskbar, shown at 2×)",
            caption,
            Color.ParseHex("C8C8C8"),
            new PointF(16, 8)));

        using var bar = DrawBar(fontPath, width, barH, items);
        image.Mutate(ctx => ctx.DrawImage(bar, new Point(0, captionH + contentH), 1f));

        image.Mutate(ctx => ctx.DrawText("5:28 PM", caption, Color.White, new PointF(width - 120, captionH + contentH + barH + 28)));
        image.Mutate(ctx => ctx.DrawText("10/22/2026", Create(family, 12), Color.ParseHex("B0B0B0"), new PointF(width - 132, captionH + contentH + barH + 50)));
        return image;
    }

    static Image<Rgba32> DrawBar(string fontPath, int width, int height, IReadOnlyList<DisplayItem> items)
    {
        var collection = new FontCollection();
        var family = collection.Add(fontPath);
        float fontSize = (float)TickerMetrics.FontSizeForBar(height);
        var font = Create(family, fontSize);
        float dot = (float)TickerMetrics.DotSize(height);
        float gap = Math.Max(4, fontSize * 0.38f);
        float dotGap = Math.Max(8, fontSize * 0.55f);

        var image = new Image<Rgba32>(width, height, new Rgba32(0, 0, 0, 255));
        float x = 8;
        image.Mutate(ctx =>
        {
            foreach (var item in items)
            {
                if (x > width + 40)
                    break;
                for (int i = 0; i < item.Runs.Count; i++)
                {
                    var run = item.Runs[i];
                    var color = Paint(run.Color, item.Stale);
                    var advance = TextMeasurer.MeasureAdvance(run.Text, new TextOptions(font)).Width;
                    ctx.DrawText(new RichTextOptions(font)
                    {
                        Origin = new PointF(x, height / 2f),
                        HorizontalAlignment = HorizontalAlignment.Left,
                        VerticalAlignment = VerticalAlignment.Center
                    }, run.Text, color);
                    x += advance;
                    if (i != item.Runs.Count - 1)
                        x += gap;
                }

                var dotColor = Paint(TickerPalette.Dot, item.Stale);
                ctx.Fill(dotColor, new SixLabors.ImageSharp.Drawing.EllipsePolygon(x + dotGap + dot / 2f, height / 2f, dot / 2f));
                x += dotGap + dot + dotGap;
            }
        });
        return image;
    }

    static Font Create(FontFamily family, float size)
    {
        try
        {
            return family.CreateFont(size, FontStyle.Bold);
        }
        catch (Exception)
        {
            return family.CreateFont(size);
        }
    }

    static Color Paint(string hex, bool stale)
    {
        var pixel = Color.ParseHex(hex.TrimStart('#')).ToPixel<Rgba32>();
        if (!stale)
            return Color.FromRgba(pixel.R, pixel.G, pixel.B, 255);
        return Color.FromRgba(pixel.R, pixel.G, pixel.B, (byte)Math.Round(255 * TickerPalette.StaleOpacity));
    }

    static IReadOnlyList<DisplayItem> SampleItems()
    {
        DisplayItem At(string symbol, string? label, SymbolKind kind, string currency, decimal price, decimal percent)
        {
            var prev = price / (1m + percent / 100m);
            return QuotePresenter.Present(
                new SymbolEntry { Symbol = symbol, Label = label ?? symbol, Kind = kind, Currency = currency },
                new QuoteSnapshot
                {
                    RegularPrice = price,
                    PrePrice = price,
                    PostPrice = price,
                    PreviousClose = prev,
                    Currency = currency
                },
                SessionPhase.AfterHours);
        }

        return
        [
            At("BTC", "BTC", SymbolKind.Crypto, "USD", 108412.50m, 1.84m),
            At("IBIT", null, SymbolKind.Us, "USD", 52.10m, 0.80m),
            At("MSTR", null, SymbolKind.Us, "USD", 352.18m, 3.12m),
            At("STRC", null, SymbolKind.Us, "USD", 99.85m, 0.05m),
            At("STRF", null, SymbolKind.Us, "USD", 112.40m, -0.22m),
            At("MARA", null, SymbolKind.Us, "USD", 18.06m, -1.47m),
            At("3350.T", null, SymbolKind.International, "JPY", 942m, 2.30m),
            At("SPCX", null, SymbolKind.Us, "USD", 214.90m, 6.08m),
            At("NVDA", null, SymbolKind.Us, "USD", 186.33m, 0.42m),
            At("^GSPC", "S&P 500", SymbolKind.Us, "USD", 7744.06m, 0.33m),
            At("CL=F", "OIL", SymbolKind.Continuous, "USD", 96.29m, 1.26m),
            At("SWC.L", null, SymbolKind.International, "GBp", 53m, 3.11m),
            At("^VIX", "VIX", SymbolKind.Us, "USD", 16.38m, -2.90m),
            At("AAPL", null, SymbolKind.Us, "USD", 228.40m, 0.55m)
        ];
    }
}
