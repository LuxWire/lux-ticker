using System.Reflection;

namespace LuxTicker;

public static class DefaultSymbols
{
    public const string ResourceName = "LuxTicker.symbols.default.json";

    public static IReadOnlyList<SymbolEntry> Load()
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException($"Missing embedded resource {ResourceName}.");
        using var reader = new StreamReader(stream);
        var json = reader.ReadToEnd();
        if (!SymbolListStore.TryParse(json, out var symbols, out var error))
            throw new InvalidOperationException("Default symbol list is invalid: " + error);
        return symbols;
    }
}
