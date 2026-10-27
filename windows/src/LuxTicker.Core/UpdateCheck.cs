using System.Globalization;
using System.Text.RegularExpressions;

namespace LuxTicker;

/// <summary>
/// A two-line update feed: a version, then the Setup URL. An HTML page is not a feed.
/// </summary>
public static class UpdateCheck
{
    public static string ManifestUrl => LuxWireConfig.UpdateFeedUrl;

    public static bool TryParse(string? body, out string version, out string? setupUrl)
    {
        version = "";
        setupUrl = null;
        if (string.IsNullOrWhiteSpace(body))
            return false;

        var text = body.Trim().TrimStart('\uFEFF');
        if (IsHtml(text))
            return false;

        var lines = text.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (lines.Length == 0 || !VersionParts(lines[0], out _))
            return false;

        version = lines[0];
        if (lines.Length > 1 && IsHttp(lines[1]))
            setupUrl = lines[1];
        return true;
    }

    public static bool IsNewer(string? current, string? candidate)
    {
        if (!VersionParts(current, out var left) || !VersionParts(candidate, out var right))
            return false;

        var count = Math.Max(left.Length, right.Length);
        for (var i = 0; i < count; i++)
        {
            var a = i < left.Length ? left[i] : 0;
            var b = i < right.Length ? right[i] : 0;
            if (b != a)
                return b > a;
        }

        return false;
    }

    /// <summary>Drive's confirm page is HTML. Pull the real download URL out of the form.</summary>
    public static string? NextDownloadUrl(string pageUrl, string? html)
    {
        if (string.IsNullOrWhiteSpace(html) || string.IsNullOrWhiteSpace(pageUrl))
            return null;

        var action = Regex.Match(html, "action=\"(https://drive\\.usercontent\\.google\\.com/download[^\"]*)\"", RegexOptions.IgnoreCase);
        var id = Regex.Match(html, "name=\"id\"\\s+value=\"([^\"]+)\"", RegexOptions.IgnoreCase);
        var confirm = Regex.Match(html, "name=\"confirm\"\\s+value=\"([^\"]+)\"", RegexOptions.IgnoreCase);
        if (action.Success && id.Success && confirm.Success)
        {
            var url = action.Groups[1].Value
                + "?id=" + Uri.EscapeDataString(id.Groups[1].Value)
                + "&export=download&confirm=" + Uri.EscapeDataString(confirm.Groups[1].Value);
            var uuid = Regex.Match(html, "name=\"uuid\"\\s+value=\"([^\"]+)\"", RegexOptions.IgnoreCase);
            if (uuid.Success)
                url += "&uuid=" + Uri.EscapeDataString(uuid.Groups[1].Value);
            return url;
        }

        var token = Regex.Match(html, "confirm=([0-9A-Za-z_\\-]{4,})", RegexOptions.IgnoreCase);
        if (!token.Success || pageUrl.Contains("confirm=", StringComparison.OrdinalIgnoreCase))
            return null;
        var sep = pageUrl.Contains('?') ? "&" : "?";
        return pageUrl + sep + "confirm=" + token.Groups[1].Value;
    }

    public static bool IsHtml(string text)
        => text.Contains("<html", StringComparison.OrdinalIgnoreCase)
           || text.Contains("<!DOCTYPE", StringComparison.OrdinalIgnoreCase)
           || text.Contains("<head", StringComparison.OrdinalIgnoreCase);

    static bool IsHttp(string text)
        => text.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
           || text.StartsWith("http://", StringComparison.OrdinalIgnoreCase);

    static bool VersionParts(string? text, out int[] parts)
    {
        parts = [];
        if (string.IsNullOrWhiteSpace(text))
            return false;
        var tokens = text.Trim().Split('.');
        if (tokens.Length == 0 || tokens.Length > 4)
            return false;
        var parsed = new int[tokens.Length];
        for (var i = 0; i < tokens.Length; i++)
        {
            if (!int.TryParse(tokens[i], NumberStyles.None, CultureInfo.InvariantCulture, out var value) || value < 0)
                return false;
            parsed[i] = value;
        }

        parts = parsed;
        return true;
    }
}
