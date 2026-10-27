using System.Text.Json;
using System.Text.Json.Serialization;

namespace LuxTicker;

public sealed class SymbolKindJsonConverter : JsonConverter<SymbolKind>
{
    public override SymbolKind Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
            return SymbolKind.Us;
        var s = reader.GetString()?.Trim().ToLowerInvariant();
        return s switch
        {
            "crypto" => SymbolKind.Crypto,
            "intl" or "international" or "non-us" or "nonus" => SymbolKind.International,
            "continuous" or "futures" => SymbolKind.Continuous,
            _ => SymbolKind.Us
        };
    }

    public override void Write(Utf8JsonWriter writer, SymbolKind value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value switch
        {
            SymbolKind.Crypto => "crypto",
            SymbolKind.International => "intl",
            SymbolKind.Continuous => "continuous",
            _ => "us"
        });
    }
}

public static class JsonFile
{
    public static JsonSerializerOptions Options { get; } = Create();

    static JsonSerializerOptions Create()
    {
        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };
        options.Converters.Add(new SymbolKindJsonConverter());
        return options;
    }

    public static void AtomicWrite(string path, string contents)
    {
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        var tmp = path + ".tmp";
        File.WriteAllText(tmp, contents);
        File.Move(tmp, path, overwrite: true);
    }
}
