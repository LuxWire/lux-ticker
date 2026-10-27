using System.Text.Json;

namespace LuxTicker;

/// <summary>
/// Project URL, anon key, and optional update feed. Read from luxwire.config.json.
/// The example file is checked in. The real file is not.
/// </summary>
public static class LuxWireConfig
{
    public const string FileName = "luxwire.config.json";
    public const string EnvVariable = "LUXWIRE_CONFIG";

    static LuxWireConfigData _current = LuxWireConfigData.Empty;

    public static string SupabaseUrl => _current.SupabaseUrl;
    public static string SupabaseAnonKey => _current.SupabaseAnonKey;
    public static string UpdateFeedUrl => _current.UpdateFeedUrl;

    public static bool IsAccountConfigured
        => SupabaseUrl.Length > 0 && SupabaseAnonKey.Length > 0;

    public static void Use(string? supabaseUrl, string? supabaseAnonKey, string? updateFeedUrl = "")
        => _current = Normalize(supabaseUrl, supabaseAnonKey, updateFeedUrl);

    public static void Load()
    {
        foreach (var path in CandidatePaths())
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                continue;
            try
            {
                var file = JsonSerializer.Deserialize<LuxWireConfigFile>(File.ReadAllText(path), JsonFile.Options);
                if (file == null)
                    continue;
                _current = Normalize(file.SupabaseUrl, file.SupabaseAnonKey, file.UpdateFeedUrl);
                return;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
            {
            }
        }

        _current = LuxWireConfigData.Empty;
    }

    public static IEnumerable<string> CandidatePaths()
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var env = Environment.GetEnvironmentVariable(EnvVariable);
        if (!string.IsNullOrWhiteSpace(env) && seen.Add(env.Trim()))
            yield return env.Trim();

        foreach (var start in StartDirectories())
        {
            var dir = new DirectoryInfo(start);
            for (var depth = 0; depth < 8 && dir != null; depth++, dir = dir.Parent)
            {
                var path = Path.Combine(dir.FullName, FileName);
                if (seen.Add(path))
                    yield return path;
            }
        }

        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        if (!string.IsNullOrEmpty(appData))
        {
            var path = Path.Combine(appData, AppPaths.FolderName, FileName);
            if (seen.Add(path))
                yield return path;
        }
    }

    static IEnumerable<string> StartDirectories()
    {
        if (!string.IsNullOrEmpty(AppContext.BaseDirectory))
            yield return AppContext.BaseDirectory;

        var process = Environment.ProcessPath;
        if (string.IsNullOrEmpty(process))
            yield break;
        var folder = Path.GetDirectoryName(process);
        if (!string.IsNullOrEmpty(folder))
            yield return folder;
    }

    static LuxWireConfigData Normalize(string? url, string? key, string? feed)
        => new((url ?? "").Trim().TrimEnd('/'), (key ?? "").Trim(), (feed ?? "").Trim());

    readonly record struct LuxWireConfigData(string SupabaseUrl, string SupabaseAnonKey, string UpdateFeedUrl)
    {
        public static LuxWireConfigData Empty => new("", "", "");
    }

    sealed class LuxWireConfigFile
    {
        public string? SupabaseUrl { get; set; }
        public string? SupabaseAnonKey { get; set; }
        public string? UpdateFeedUrl { get; set; }
    }
}
