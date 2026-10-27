using System.Diagnostics;
using System.Windows;

namespace LuxTicker;

/// <summary>
/// Reads the update feed from luxwire.config.json and, when asked, downloads the Setup and starts it.
/// An HTML page is a failed read, not a newer version. An empty feed means updates are not configured.
/// </summary>
static class UpdateClient
{
    public static async Task<string> CheckAsync(bool install)
    {
        if (string.IsNullOrWhiteSpace(UpdateCheck.ManifestUrl))
            return "No update feed is configured. Set updateFeedUrl in luxwire.config.json.";

        string body;
        try
        {
            using var http = NewClient(TimeSpan.FromSeconds(20));
            body = await http.GetStringAsync(UpdateCheck.ManifestUrl);
        }
        catch (Exception ex)
        {
            Log.Error("update check " + ex.Message);
            return Manual();
        }

        if (!UpdateCheck.TryParse(body, out var version, out var setupUrl))
            return Manual();
        if (!UpdateCheck.IsNewer(LuxWireEdition.Version, version))
            return LuxWireEdition.ProductName + " " + LuxWireEdition.Version + " is up to date.";
        if (!install)
            return "newer:" + version + "|" + (setupUrl ?? "");
        if (string.IsNullOrWhiteSpace(setupUrl))
            return LuxWireEdition.ProductName + " " + version + " is available. " + Manual();

        try
        {
            var path = await DownloadAsync(setupUrl, version);
            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
            Application.Current?.Shutdown();
            return "Installing " + LuxWireEdition.ProductName + " " + version + ".";
        }
        catch (Exception ex)
        {
            Log.Error("update download " + ex.Message);
            return "Couldn't download the installer. " + Manual();
        }
    }

    public static string Manual()
        => "Couldn't read the update feed. Download the latest LuxTicker-Setup from Drive and run it. That upgrades this install and keeps your settings.";

    static async Task<string> DownloadAsync(string url, string version)
    {
        using var http = NewClient(TimeSpan.FromMinutes(5));
        var current = url;
        for (var attempt = 0; attempt < 3; attempt++)
        {
            using var response = await http.GetAsync(current, HttpCompletionOption.ResponseHeadersRead);
            response.EnsureSuccessStatusCode();
            var type = response.Content.Headers.ContentType?.MediaType ?? "";
            await using var stream = await response.Content.ReadAsStreamAsync();
            var head = new byte[8];
            var read = await stream.ReadAsync(head);
            var exe = read >= 2 && head[0] == (byte)'M' && head[1] == (byte)'Z';
            if (!exe && (type.Contains("html", StringComparison.OrdinalIgnoreCase) || read == 0 || head[0] == (byte)'<' || head[0] == (byte)'{'))
            {
                using var rest = new MemoryStream();
                rest.Write(head, 0, read);
                await stream.CopyToAsync(rest);
                var html = System.Text.Encoding.UTF8.GetString(rest.ToArray());
                var next = UpdateCheck.NextDownloadUrl(current, html);
                if (string.IsNullOrWhiteSpace(next) || next == current)
                    throw new InvalidDataException("Drive returned a page instead of the installer.");
                current = next;
                continue;
            }

            var safe = new string(version.Where(ch => char.IsDigit(ch) || ch == '.').ToArray());
            if (safe.Length == 0)
                safe = "update";
            var path = Path.Combine(Path.GetTempPath(), "LuxTicker-Setup-" + safe + ".exe");
            await using (var file = File.Create(path))
            {
                if (read > 0)
                    await file.WriteAsync(head.AsMemory(0, read));
                await stream.CopyToAsync(file);
            }

            return path;
        }

        throw new InvalidDataException("Drive returned a page instead of the installer.");
    }

    static HttpClient NewClient(TimeSpan timeout)
    {
        var http = new HttpClient { Timeout = timeout };
        http.DefaultRequestHeaders.UserAgent.ParseAdd(FeedEndpoints.UserAgent);
        return http;
    }
}
