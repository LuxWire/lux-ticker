using System.Globalization;
using System.Text.Json;

namespace LuxTicker;

public sealed record PriceAlert(string Symbol, bool Above, decimal Price, bool Enabled = true);

public static class PriceAlertEngine
{
    /// <summary>
    /// Fires once when the price crosses the level. The first observation (no previous price) does not fire.
    /// </summary>
    public static bool Crossed(decimal? previous, decimal current, bool above, decimal level)
    {
        if (previous == null)
            return false;
        if (above)
            return previous.Value < level && current >= level;
        return previous.Value > level && current <= level;
    }
}

public sealed class PriceAlertStore
{
    readonly string _path;

    public PriceAlertStore(string path) => _path = path;

    public IReadOnlyList<PriceAlert> Load()
    {
        try
        {
            if (!File.Exists(_path))
                return Array.Empty<PriceAlert>();
            var file = JsonSerializer.Deserialize<AlertFile>(File.ReadAllText(_path), JsonFile.Options);
            if (file?.Alerts == null)
                return Array.Empty<PriceAlert>();
            var list = new List<PriceAlert>();
            foreach (var row in file.Alerts)
            {
                if (string.IsNullOrWhiteSpace(row.Symbol) || row.Price <= 0)
                    continue;
                var symbol = SymbolEntry.NormalizeSymbol(row.Symbol);
                if (symbol.Length == 0)
                    continue;
                list.Add(new PriceAlert(symbol, row.Above, row.Price, row.Enabled ?? true));
            }

            return list;
        }
        catch
        {
            return Array.Empty<PriceAlert>();
        }
    }

    public void Save(IReadOnlyList<PriceAlert> alerts)
    {
        var file = new AlertFile
        {
            Alerts = alerts.Select(alert => new AlertRow
            {
                Symbol = alert.Symbol,
                Above = alert.Above,
                Price = alert.Price,
                Enabled = alert.Enabled
            }).ToList()
        };
        JsonFile.AtomicWrite(_path, JsonSerializer.Serialize(file, JsonFile.Options) + "\n");
    }

    sealed class AlertFile
    {
        public List<AlertRow>? Alerts { get; set; }
    }

    sealed class AlertRow
    {
        public string? Symbol { get; set; }
        public bool Above { get; set; }
        public decimal Price { get; set; }
        public bool? Enabled { get; set; }
    }
}

public static class AlertKeys
{
    public static string For(PriceAlert alert)
        => alert.Symbol.ToUpperInvariant()
           + "|" + (alert.Above ? "A" : "B")
           + "|" + alert.Price.ToString(CultureInfo.InvariantCulture);
}
