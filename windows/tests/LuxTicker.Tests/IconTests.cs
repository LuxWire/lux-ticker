namespace LuxTicker.Tests;

public class IconTests
{
    static readonly int[] Sizes = [16, 20, 24, 32, 40, 48, 64, 96, 128, 256];

    [Fact]
    public void Ico_has_every_size_in_the_lux_wire_orange()
    {
        var file = File.ReadAllBytes(Path.Combine(Repo.Root(), "src", "LuxTicker.App", "Assets", "LuxTicker.ico"));
        var images = IcoCatalog.Read(file);
        Assert.Equal(Sizes, images.Select(image => image.Width).ToArray());
        Assert.All(images, image => Assert.Equal(image.Width, image.Height));

        foreach (var image in images)
        {
            var pixels = Decode(image);
            Assert.Equal(image.Width, pixels.GetLength(0));
            var corners = new[]
            {
                pixels[0, 0],
                pixels[0, image.Width - 1],
                pixels[image.Height - 1, 0],
                pixels[image.Height - 1, image.Width - 1]
            };
            Assert.All(corners, pixel => Assert.Equal((247, 147, 26, 255), pixel));
            Assert.Contains(pixels.Cast<(byte R, byte G, byte B, byte A)>(), pixel => pixel == (246, 242, 232, 255));
            Assert.DoesNotContain(pixels.Cast<(byte R, byte G, byte B, byte A)>(), pixel =>
                pixel.A != 255 || (pixel != (246, 242, 232, 255) && pixel != (247, 147, 26, 255)));
            var margin = PaperMargin(pixels);
            Assert.True(margin.Left * 10 >= image.Width, "letters leave a margin on the left");
            Assert.True(margin.Right * 10 >= image.Width, "letters leave a margin on the right");
            Assert.True(margin.Top * 10 >= image.Height, "letters leave a margin on the top");
            Assert.True(margin.Bottom * 10 >= image.Height, "letters leave a margin on the bottom");
            var peakX = 0;
            var peakCount = 0;
            for (var x = 0; x < image.Width; x++)
            {
                if (pixels[margin.Top, x] == (246, 242, 232, 255))
                {
                    peakX += x;
                    peakCount++;
                }
            }

            Assert.True(peakCount > 0 && peakX * 2 > peakCount * image.Width, "the W peak is the top of the mark");
        }
    }

    [Fact]
    public void Every_size_is_the_same_mark_scaled_from_256()
    {
        var images = IcoCatalog.Read(File.ReadAllBytes(Path.Combine(Repo.Root(), "src", "LuxTicker.App", "Assets", "LuxTicker.ico")));
        var master = Decode(images.Single(image => image.Width == 256));
        foreach (var size in Sizes)
        {
            if (size == 256)
                continue;
            var scaled = Scale(master, size);
            var frame = Decode(images.Single(image => image.Width == size));
            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                    Assert.Equal(scaled[y, x], frame[y, x]);
            }
        }
    }

    static (int Left, int Right, int Top, int Bottom) PaperMargin((byte R, byte G, byte B, byte A)[,] pixels)
    {
        var height = pixels.GetLength(0);
        var width = pixels.GetLength(1);
        var minX = width;
        var minY = height;
        var maxX = 0;
        var maxY = 0;
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                if (pixels[y, x] != (246, 242, 232, 255))
                    continue;
                minX = Math.Min(minX, x);
                minY = Math.Min(minY, y);
                maxX = Math.Max(maxX, x);
                maxY = Math.Max(maxY, y);
            }
        }

        return (minX, width - 1 - maxX, minY, height - 1 - maxY);
    }

    [Fact]
    public void Tray_uses_the_same_orange_tile_as_the_exe()
    {
        var root = Repo.Root();
        Assert.False(File.Exists(Path.Combine(root, "src", "LuxTicker.App", "Assets", "LuxTickerTray.ico")));
        var icon = File.ReadAllText(Path.Combine(root, "src", "LuxTicker.App", "AppIcon.cs"));
        Assert.Contains("IcoCatalog.Read(Bytes())", icon);
        Assert.DoesNotContain("LuxTickerTray", icon);
        var tray = File.ReadAllText(Path.Combine(root, "src", "LuxTicker.App", "TrayIcon.cs"));
        Assert.Contains("LoadTrayIcon", tray);
        Assert.Contains("ForTrayDpi", tray);
        var project = File.ReadAllText(Path.Combine(root, "src", "LuxTicker.App", "LuxTicker.App.csproj"));
        Assert.DoesNotContain("LuxTickerTray.ico", project);
        Assert.Contains("lux-wire-toast.png", project);
    }

    [Fact]
    public void Manifest_stays_asInvoker()
    {
        var manifest = File.ReadAllText(Path.Combine(Repo.Root(), "src", "LuxTicker.App", "app.manifest"));
        Assert.Contains("level=\"asInvoker\"", manifest);
        Assert.DoesNotContain("requireAdministrator", manifest, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("highestAvailable", manifest, StringComparison.OrdinalIgnoreCase);
    }

    static (byte R, byte G, byte B, byte A)[,] Scale((byte R, byte G, byte B, byte A)[,] src, int size)
    {
        var n = src.GetLength(0);
        var paper = ((byte)246, (byte)242, (byte)232, (byte)255);
        var orange = ((byte)247, (byte)147, (byte)26, (byte)255);
        var dst = new (byte R, byte G, byte B, byte A)[size, size];
        if (size == n)
        {
            for (var y = 0; y < n; y++)
            {
                for (var x = 0; x < n; x++)
                    dst[y, x] = src[y, x];
            }

            return dst;
        }

        for (var y = 0; y < size; y++)
        {
            var yStart = y * n;
            var yEnd = (y + 1) * n;
            for (var x = 0; x < size; x++)
            {
                var xStart = x * n;
                var xEnd = (x + 1) * n;
                long covered = 0;
                var iy0 = yStart / size;
                var iy1 = (yEnd + size - 1) / size;
                var ix0 = xStart / size;
                var ix1 = (xEnd + size - 1) / size;
                for (var iy = iy0; iy < Math.Min(n, iy1); iy++)
                {
                    var top = Math.Max(yStart, iy * size);
                    var bot = Math.Min(yEnd, (iy + 1) * size);
                    var ovY = bot - top;
                    if (ovY <= 0)
                        continue;
                    for (var ix = ix0; ix < Math.Min(n, ix1); ix++)
                    {
                        var left = Math.Max(xStart, ix * size);
                        var right = Math.Min(xEnd, (ix + 1) * size);
                        var ovX = right - left;
                        if (ovX > 0 && src[iy, ix] == paper)
                            covered += (long)ovX * ovY;
                    }
                }

                dst[y, x] = covered * 2 >= (long)n * n ? paper : orange;
            }
        }

        return dst;
    }

    static (byte R, byte G, byte B, byte A)[,] Decode(IcoImage image)
    {
        var dib = image.ImageBytes;
        var width = BitConverter.ToInt32(dib, 4);
        var height = BitConverter.ToInt32(dib, 8) / 2;
        var pixels = new (byte R, byte G, byte B, byte A)[height, width];
        var offset = 40;
        for (var y = height - 1; y >= 0; y--)
        {
            for (var x = 0; x < width; x++)
            {
                var b = dib[offset];
                var g = dib[offset + 1];
                var r = dib[offset + 2];
                var a = dib[offset + 3];
                pixels[y, x] = (r, g, b, a);
                offset += 4;
            }
        }

        return pixels;
    }
}
