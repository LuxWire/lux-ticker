namespace LuxTicker;

public sealed record EditResult(bool Ok, string? Error, IReadOnlyList<SymbolEntry> Symbols)
{
    public static EditResult Success(IReadOnlyList<SymbolEntry> symbols) => new(true, null, symbols);
    public static EditResult Fail(string error, IReadOnlyList<SymbolEntry> symbols) => new(false, error, symbols);
}

public static class SymbolListEditor
{
    public static EditResult Add(IReadOnlyList<SymbolEntry> current, SymbolEntry entry, ProbeStatus probe)
    {
        var symbol = SymbolEntry.NormalizeSymbol(entry.Symbol);
        if (string.IsNullOrEmpty(symbol))
            return EditResult.Fail("Enter a symbol.", current);
        if (!SymbolEntry.IsValidSymbol(symbol))
            return EditResult.Fail("Symbols can use letters, numbers, and . - ^ = _.", current);
        if (current.Any(s => s.Symbol.Equals(symbol, StringComparison.OrdinalIgnoreCase)))
            return EditResult.Fail($"{symbol} is already on the ticker.", current);

        if (probe == ProbeStatus.NotFound)
            return EditResult.Fail($"No quote for \"{symbol}\". Check the symbol and try again.", current);
        if (probe != ProbeStatus.Found)
            return EditResult.Fail("Couldn't reach the quote feed. Check the connection and try again.", current);

        var list = current.ToList();
        list.Add(entry with { Symbol = symbol });
        return EditResult.Success(list);
    }

    public static IReadOnlyList<SymbolEntry> RemoveAt(IReadOnlyList<SymbolEntry> current, int index)
    {
        if (index < 0 || index >= current.Count)
            return current;
        var list = current.ToList();
        list.RemoveAt(index);
        return list;
    }

    public static IReadOnlyList<SymbolEntry> Move(IReadOnlyList<SymbolEntry> current, int index, int delta)
    {
        if (delta == 0 || index < 0 || index >= current.Count)
            return current;
        return MoveTo(current, index, index + delta);
    }

    public static IReadOnlyList<SymbolEntry> MoveTo(IReadOnlyList<SymbolEntry> current, int from, int to)
    {
        if (from < 0 || to < 0 || from >= current.Count || to >= current.Count || from == to)
            return current;
        var list = current.ToList();
        var item = list[from];
        list.RemoveAt(from);
        list.Insert(to, item);
        return list;
    }

    public static IReadOnlyList<SymbolEntry> Reset(IReadOnlyList<SymbolEntry> defaults)
        => defaults.ToList();
}
