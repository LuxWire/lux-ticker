namespace LuxTicker;

public static class Log
{
    static readonly object Gate = new();

    public static void Info(string message) => Write("INFO", message);

    public static void Error(string message) => Write("ERROR", message);

    static void Write(string level, string message)
    {
        try
        {
            var path = AppPaths.LogPath();
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);

            var line = DateTimeOffset.Now.ToString("yyyy-MM-dd HH:mm:ss.fff zzz") + " " + level + " " + message + Environment.NewLine;
            lock (Gate)
            {
                if (File.Exists(path) && new FileInfo(path).Length > 1_000_000)
                {
                    var keep = File.ReadAllText(path);
                    if (keep.Length > 200_000)
                        keep = keep[^200_000..];
                    File.WriteAllText(path, keep);
                }

                File.AppendAllText(path, line);
            }
        }
        catch
        {
            // Logging must not take the ticker down.
        }
    }
}
