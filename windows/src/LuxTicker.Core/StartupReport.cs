using System.Reflection;
using System.Runtime.InteropServices;

namespace LuxTicker;

/// <summary>Names the DLL and export behind an entry-point failure. EntryPointNotFoundException's own message does not.</summary>
public static class NativeExport
{
    public static string? Describe(Exception exception)
    {
        for (var current = exception; current != null; current = current.InnerException)
        {
            if (current is not (EntryPointNotFoundException or DllNotFoundException))
                continue;

            var trace = new System.Diagnostics.StackTrace(current, false);
            for (int i = 0; i < trace.FrameCount; i++)
            {
                var method = trace.GetFrame(i)?.GetMethod();
                var import = method?.GetCustomAttribute<DllImportAttribute>();
                if (import == null || string.IsNullOrWhiteSpace(import.Value))
                    continue;
                var entry = string.IsNullOrWhiteSpace(import.EntryPoint) ? method!.Name : import.EntryPoint;
                return import.Value + "!" + entry;
            }
        }

        return null;
    }
}

/// <summary>Appends the full exception to %APPDATA%\LuxTicker\startup.log. Never throws.</summary>
public static class StartupReport
{
    public static string Write(Exception exception, string? directory = null)
    {
        var dir = string.IsNullOrWhiteSpace(directory) ? AppPaths.Root() : directory;
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, AppPaths.StartupLogFileName);
        var native = NativeExport.Describe(exception);
        var detail = exception.ToString();
        if (!string.IsNullOrEmpty(native))
            detail = "Native entry point: " + native + Environment.NewLine + detail;
        var entry = DateTimeOffset.Now.ToString("yyyy-MM-dd HH:mm:ss.fff zzz")
            + Environment.NewLine
            + detail
            + Environment.NewLine
            + "----"
            + Environment.NewLine;
        File.AppendAllText(path, entry);
        return path;
    }

    /// <summary>One diagnostic line in startup.log. Does not show a dialog and never throws.</summary>
    public static void Note(string message, string? directory = null)
    {
        try
        {
            var dir = string.IsNullOrWhiteSpace(directory) ? AppPaths.Root() : directory;
            Directory.CreateDirectory(dir);
            var path = Path.Combine(dir, AppPaths.StartupLogFileName);
            var entry = DateTimeOffset.Now.ToString("yyyy-MM-dd HH:mm:ss.fff zzz")
                + " "
                + message.Replace("\r", " ").Replace("\n", " ")
                + Environment.NewLine;
            File.AppendAllText(path, entry);
        }
        catch
        {
            // Diagnosis must not stop the ticker.
        }
    }
}
