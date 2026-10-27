using System.Text.Json;

namespace LuxTicker;

public readonly record struct SymbolLoadResult(IReadOnlyList<SymbolEntry> Symbols, bool MissingFile, string? Error);

public sealed class SymbolListStore
{
    readonly string _path;
    readonly Func<IReadOnlyList<SymbolEntry>> _defaults;

    public SymbolListStore(string path, Func<IReadOnlyList<SymbolEntry>>? defaults = null)
    {
        _path = path;
        _defaults = defaults ?? DefaultSymbols.Load;
    }

    public string Path => _path;

    public SymbolLoadResult Load()
    {
        try
        {
            if (!File.Exists(_path))
                return new SymbolLoadResult(_defaults(), MissingFile: true, Error: null);

            var text = File.ReadAllText(_path);
            if (!TryParse(text, out var symbols, out var error))
                return new SymbolLoadResult(_defaults(), MissingFile: false, Error: error);

            return new SymbolLoadResult(symbols, MissingFile: false, Error: null);
        }
        catch (Exception ex)
        {
            return new SymbolLoadResult(_defaults(), MissingFile: false, Error: ex.Message);
        }
    }

    public void Save(IReadOnlyList<SymbolEntry> symbols)
        => JsonFile.AtomicWrite(_path, Serialize(symbols));

    public static string Serialize(IReadOnlyList<SymbolEntry> symbols)
    {
        var payload = new SymbolFile { Symbols = symbols.ToList() };
        return JsonSerializer.Serialize(payload, JsonFile.Options) + "\n";
    }

    public static bool TryParse(string json, out IReadOnlyList<SymbolEntry> symbols, out string? error)
    {
        symbols = Array.Empty<SymbolEntry>();
        error = null;
        try
        {
            using var doc = JsonDocument.Parse(json, new JsonDocumentOptions
            {
                CommentHandling = JsonCommentHandling.Skip,
                AllowTrailingCommas = true
            });
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
            {
                error = "expected an object";
                return false;
            }

            JsonElement arr = default;
            var found = false;
            foreach (var prop in doc.RootElement.EnumerateObject())
            {
                if (!prop.Name.Equals("symbols", StringComparison.OrdinalIgnoreCase))
                    continue;
                arr = prop.Value;
                found = true;
                break;
            }

            if (!found || arr.ValueKind != JsonValueKind.Array)
            {
                error = "symbols array is missing";
                return false;
            }

            var list = new List<SymbolEntry>();
            foreach (var el in arr.EnumerateArray())
            {
                if (el.ValueKind != JsonValueKind.Object)
                    continue;
                var symbol = SymbolEntry.NormalizeSymbol(StringProp(el, "symbol") ?? "");
                if (!SymbolEntry.IsValidSymbol(symbol))
                    continue;
                var label = StringProp(el, "label");
                list.Add(new SymbolEntry
                {
                    Symbol = symbol,
                    Label = string.IsNullOrWhiteSpace(label) ? null : label.Trim(),
                    Kind = ParseKind(StringProp(el, "kind")),
                    Currency = string.IsNullOrWhiteSpace(StringProp(el, "currency")) ? "USD" : StringProp(el, "currency")!.Trim()
                });
            }

            symbols = list;
            return true;
        }
        catch (JsonException ex)
        {
            error = ex.Message;
            return false;
        }
    }

    static string? StringProp(JsonElement obj, string name)
    {
        foreach (var prop in obj.EnumerateObject())
        {
            if (!prop.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
                continue;
            return prop.Value.ValueKind == JsonValueKind.String ? prop.Value.GetString() : prop.Value.ToString();
        }

        return null;
    }

    static SymbolKind ParseKind(string? value)
    {
        return value?.Trim().ToLowerInvariant() switch
        {
            "crypto" => SymbolKind.Crypto,
            "intl" or "international" or "non-us" or "nonus" => SymbolKind.International,
            "continuous" or "futures" => SymbolKind.Continuous,
            _ => SymbolKind.Us
        };
    }

    sealed class SymbolFile
    {
        public List<SymbolEntry>? Symbols { get; set; }
    }
}
