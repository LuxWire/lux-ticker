using System.Reflection;
using System.Windows.Media;

namespace LuxTicker;

public static class TickerFont
{
    public static string EnsureExtracted()
    {
        var path = AppPaths.FontPath();
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        if (!File.Exists(path) || new FileInfo(path).Length < 1000)
        {
            using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("LuxTicker.Fonts.CascadiaMono-Bold.ttf")
                ?? throw new InvalidOperationException("Embedded ticker font is missing.");
            using var file = File.Create(path);
            stream.CopyTo(file);
        }

        return path;
    }

    public const string DefaultName = "Cascadia Mono";

    public static bool IsDefault(string? name)
        => string.IsNullOrWhiteSpace(name) || name.Trim().Equals(DefaultName, StringComparison.OrdinalIgnoreCase);

    public static FontFamily Resolve(string? name)
    {
        if (IsDefault(name))
            return Load();
        return new FontFamily(name!.Trim());
    }

    public static FontFamily Load()
    {
        try
        {
            var path = EnsureExtracted();
            var dir = Path.GetDirectoryName(path)!;
            var baseUri = new Uri(dir.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar);
            return new FontFamily(baseUri, "./#Cascadia Mono");
        }
        catch (Exception ex)
        {
            Log.Error("font " + ex.Message);
            return new FontFamily("Consolas, Cascadia Mono, Courier New");
        }
    }
}
