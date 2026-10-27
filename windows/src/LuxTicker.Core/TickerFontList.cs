namespace LuxTicker;

/// <summary>Recommended ticker faces first, then every other installed face A–Z.</summary>
public static class TickerFontList
{
    public static readonly string[] Recommended =
    [
        "Cascadia Mono",
        "Consolas",
        "Lucida Console",
        "Courier New",
        "Bahnschrift",
        "Bahnschrift Condensed",
        "Segoe UI Mono",
        "Arial Narrow"
    ];

    public sealed record Row(string Label, string? Name, bool Header);

    public static IReadOnlyList<Row> Build(IEnumerable<string>? installed)
    {
        var have = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Cascadia Mono" };
        if (installed != null)
        {
            foreach (var name in installed)
            {
                if (!string.IsNullOrWhiteSpace(name))
                    have.Add(name.Trim());
            }
        }

        var rows = new List<Row> { new("Recommended ticker fonts", null, true) };
        foreach (var name in Recommended)
        {
            if (have.Contains(name))
                rows.Add(new Row(name, name, false));
        }

        rows.Add(new Row("────────", null, true));
        rows.Add(new Row("All fonts", null, true));
        foreach (var name in have.OrderBy(item => item, StringComparer.OrdinalIgnoreCase))
        {
            if (Recommended.Contains(name, StringComparer.OrdinalIgnoreCase))
                continue;
            rows.Add(new Row(name, name, false));
        }

        return rows;
    }
}
