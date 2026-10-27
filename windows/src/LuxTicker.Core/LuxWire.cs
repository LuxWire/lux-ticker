using System.Globalization;
using System.Text.Json;

namespace LuxTicker;

/// <summary>Pro features are included. There is no paywall in this build.</summary>
public static class LuxWireEdition
{
    public const bool Subscribed = true;
    public const string ProductName = "LuxWire Ticker";
    public const string Version = "1.4.14";
}

public sealed record ReleaseNote(string Version, string Date, IReadOnlyList<string> Bullets);

/// <summary>What's New is one list per minor version, newest first. Patch notes roll into 1.4, 1.3, and so on.</summary>
public static class ReleaseNotes
{
    public const string Date = "October 24, 2026";

    public static IReadOnlyList<ReleaseNote> Versions { get; } =
    [
        new("1.4.14", "October 26, 2026",
        [
            "Privacy and Terms open luxwire.github.io.",
            "An update keeps the settings and watchlist already saved. A setting you never chose stays at its default.",
            "What's New is grouped by minor version, such as 1.4 and 1.3."
        ]),
        new("1.4.13", "October 26, 2026",
        [
            "Continue with Google and Continue with X are on the account sign-in screen. The browser opens, then the ticker finishes sign-in on this PC.",
            "Privacy and Terms are on the sign-in screen and in About."
        ]),
        new("1.4.12", "October 26, 2026",
        [
            "A LuxWire account is optional. Create one with an email and a password. A username is optional. The ticker still works signed out.",
            "Signed in, settings and the watchlist sync. The same tables are there for the Android ticker and Lux 531.",
            "Sign out, delete account, and forgot password are in Settings, under Account.",
            "Google and X sign-in are wired, and stay hidden until those providers are turned on."
        ]),
        new("1.4.11", "October 25, 2026",
        [
            "Background color has a custom picker. Type any hex, or set red, green, and blue. The presets stay.",
            "A new install uses the Wall Street tape: a dark board with amber and green. Appearance also has NYSE, Nasdaq, Cboe, NYSE American, NYSE Arca, IEX, MIAX, and MEMX. Colors already saved stay as they are.",
            "Live updates while scrolling is a switch. On, prices change in place and flash green on an uptick and red on a downtick.",
            "The icon is one LW, scaled to every size from 16 through 256, including the title bar, taskbar, and tray.",
            "Charts has an Open chart button.",
            "The settings menu highlights in gold."
        ]),
        new("1.4.10", Date,
        [
            "The icon is a heavy centered LW on the orange tile. The middle of the W rises above the side strokes."
        ]),
        new("1.4.9", Date,
        [
            "The ticker background has a color. It starts at paper #F6F2E8, and opacity still applies.",
            "Reset this section to defaults stays on sections you can change. About and Updates do not have it. Reset ALL settings to defaults still asks first.",
            "LuxWire post alerts is one switch, plus how often to check. There is no place to paste a key.",
            "What's new is in Updates, next to the version and Check for updates.",
            "About is the ticker and LuxWire, with Follow on X and Subscribe.",
            "Settings no longer shows where the settings file is saved.",
            "The icon is the same orange LW tile everywhere, including the tray."
        ]),
        new("1.4.8", Date,
        [
            "The bar sits directly above the taskbar, not on Start. Maximized windows stop there. Above the taskbar is that place.",
            "The tray icon is a sharp LW mark on a transparent background, at the size Windows asks for.",
            "Click-through lets clicks pass through the scrolling prices. Chart and Settings stay clickable.",
            "Updates and About are separate sections in Settings.",
            "The font list starts with recommended ticker fonts, then every other installed font.",
            "Every section has Reset this section to defaults. Reset ALL settings to defaults asks first.",
            "Symbol, price, and change each have a color. Up and down are separate. Symbol starts burnt orange (#CC5500). Price starts ink. Up is green and down is red.",
            "Settings leaves room between the controls and the scrollbar.",
            "The paper background starts fully opaque. A background you already chose stays."
        ]),
        new("1.4.7", Date,
        [
            "Settings is split into Tickers, Appearance, Motion, Position & Dock, Charts, and Updates & About.",
            "The search box filters settings by name.",
            "Each section has Reset. Reset all restores every setting.",
            "The name on screen is LuxWire Ticker.",
            "Show Desktop, Win+D, and Win+M leave the bar in place.",
            "Symbol and price start in burnt orange (#CC5500). Reset restores that color.",
            "Always on top stays on, including after Alt+Tab and when a full-screen app is in front.",
            "The button on the bar says Chart.",
            "Type a symbol in the chart to load it. Recent searches stay. An unknown symbol says so."
        ]),
        new("1.4.6", "October 24, 2026",
        [
            "The Bitcoin chart adds 1H, the last 60 minutes.",
            "Line or Candles switches the series. The crosshair on a candle shows open, high, low, close, and Eastern time."
        ]),
        new("1.4.5", "October 23, 2026",
        [
            "The ticker loops instead of stopping after one pass.",
            "The paper background starts at 50%. Speed, font, and spacing are in Settings."
        ]),
        new("1.4.4", "October 23, 2026",
        [
            "Every symbol scrolls, and the bar comes back after a screenshot.",
            "Setup upgrades the existing install in place and keeps settings."
        ]),
        new("1.4.3", "October 23, 2026",
        [
            "Vertical position in Settings moves the bar from the dock. 0 is the usual place."
        ]),
        new("1.4.2", "October 23, 2026",
        [
            "Click or drag the Bitcoin chart. A crosshair shows the price and the Eastern time."
        ]),
        new("1.4.1", "October 23, 2026",
        [
            "Paper matches Lux Wire article colors (`#F6F2E8` / accent `#F7931A`)."
        ]),
        new("1.4.0", "October 23, 2026",
        [
            "Bitcoin chart is on the bar."
        ]),
        new("1.3.0", "October 23, 2026",
        [
            "Built-in @LuxWire X feed alerts (no Wade handoff)."
        ]),
        new("1.2.1", "October 23, 2026",
        [
            "The bar stays fully opaque and sits above the Windows taskbar."
        ]),
        new("1.2.0", "October 23, 2026",
        [
            "PRE still shows before the open. Simulate Lux Wire post shows a sample toast.",
            "Dock is Above the taskbar or Top of the screen."
        ]),
        new("1.1.0", "October 23, 2026",
        [
            "Install with LuxTicker-Setup.exe. The Start Menu has LuxWire Ticker and LuxWire Ticker Settings.",
            "Questions or bugs: message @LuxWire on X → https://x.com/LuxWire"
        ])
    ];

    /// <summary>In-app What's New. Patch releases fold into their minor version.</summary>
    public static IReadOnlyList<ReleaseNote> Displayed { get; } = RollUp(Versions);

    public static string Current { get; } = string.Join("\n", Displayed.SelectMany(note =>
        new[] { note.Version + " — " + note.Date }.Concat(note.Bullets.Select(bullet => "• " + bullet))));

    public static string MinorVersion(string version)
    {
        var parts = version.Split('.');
        return parts.Length >= 2 ? parts[0] + "." + parts[1] : version;
    }

    static IReadOnlyList<ReleaseNote> RollUp(IReadOnlyList<ReleaseNote> versions)
    {
        var groups = new List<(string Minor, string Date, List<string> Bullets)>();
        foreach (var note in versions)
        {
            var minor = MinorVersion(note.Version);
            if (groups.Count == 0 || groups[^1].Minor != minor)
                groups.Add((minor, note.Date, []));
            groups[^1].Bullets.AddRange(note.Bullets);
        }

        return groups.Select(group => new ReleaseNote(group.Minor, group.Date, group.Bullets)).ToList();
    }
}

public static class LuxWireTitles
{
    public static string For(string? slot, bool subscriber)
    {
        var raw = slot?.Trim();
        var name = string.IsNullOrEmpty(raw)
            ? "Lux Wire"
            : raw.ToUpperInvariant() switch
            {
                "FILLER" => "Filler",
                "NEWS" => "News",
                "ALERT" => "Alert",
                "THREAD" => "Thread",
                "CARD" => "Card",
                "IMAGE" => "Image",
                "DIGEST" => "Digest",
                _ => raw
            };
        return subscriber ? name + " · Subscribers" : name;
    }
}

public static class LuxWireDigest
{
    public static LuxWirePost Combine(IReadOnlyList<LuxWirePost> posts)
    {
        if (posts.Count == 0)
            throw new ArgumentException("A digest needs at least one post.", nameof(posts));
        if (posts.Count == 1)
            return posts[0];

        var latest = posts[posts.Count - 1];
        var preview = posts.Count + " new @LuxWire posts. " + (latest.TextPreview ?? "New post on @LuxWire");
        return latest with
        {
            TextPreview = preview,
            SlotType = "DIGEST",
            SubscriberOnly = posts.Any(post => post.SubscriberOnly)
        };
    }
}

public sealed record LuxWirePost(
    string Url,
    bool SubscriberOnly,
    DateTimeOffset? PostedAt,
    string? SlotType,
    string? TextPreview,
    string? Id,
    string? ImageUrl = null)
{
    public string? TitleOverride { get; init; }

    public string? Key => KeyFor(Id, Url);

    public string Title => TitleOverride ?? LuxWireTitles.For(SlotType, SubscriberOnly);

    public string Body
    {
        get
        {
            var text = TextPreview?.Trim();
            if (string.IsNullOrEmpty(text))
                return "New post on @LuxWire";
            return text.Length <= 180 ? text : text[..180].TrimEnd() + "…";
        }
    }

    public static LuxWirePost Simulate() => new(
        "https://x.com/LuxWire",
        SubscriberOnly: false,
        PostedAt: null,
        SlotType: null,
        TextPreview: "Sample post. Tap to open @LuxWire.",
        Id: null);

    public static string? KeyFor(string? id, string? url)
    {
        if (!string.IsNullOrWhiteSpace(id))
            return "id:" + id.Trim();
        return LuxWireLinks.Canonical(url);
    }
}

public static class LuxWireLinks
{
    public static bool IsHttp(string? value) => Canonical(value) != null;

    public static string? Canonical(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;
        var text = value.Trim();
        if (!Uri.TryCreate(text, UriKind.Absolute, out var uri))
            return null;
        if (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp)
            return null;
        if (string.IsNullOrEmpty(uri.Host))
            return null;

        var builder = new UriBuilder(uri)
        {
            Scheme = uri.Scheme.ToLowerInvariant(),
            Host = uri.Host.ToLowerInvariant(),
            Port = uri.IsDefaultPort ? -1 : uri.Port
        };
        if (builder.Path.Length > 1 && builder.Path.EndsWith('/'))
            builder.Path = builder.Path.TrimEnd('/');
        return builder.Uri.AbsoluteUri;
    }
}

public static class LuxWirePolicy
{
    public static bool ShouldNotify(LuxWirePost post, bool alertsEnabled, bool subscribed)
    {
        if (!alertsEnabled)
            return false;
        if (post.SubscriberOnly && !subscribed)
            return false;
        return true;
    }
}

/// <summary>
/// Remembers urls already handled. The first poll baselines history so an existing file does not fire a burst.
/// Later lines toast. Alerts that are switched off stay unseen until the toggle is on again.
/// </summary>
public sealed class LuxWireInbox
{
    readonly HashSet<string> _seen;

    public LuxWireInbox(IEnumerable<string>? seen = null)
    {
        _seen = new HashSet<string>(seen ?? Array.Empty<string>(), StringComparer.Ordinal);
    }

    public IReadOnlyCollection<string> Seen => _seen;

    public IReadOnlyList<LuxWirePost> Accept(IEnumerable<LuxWirePost> posts, bool alertsEnabled, bool subscribed, bool baseline)
    {
        var notify = new List<LuxWirePost>();
        foreach (var post in posts)
        {
            var key = post.Key;
            if (string.IsNullOrEmpty(key))
                continue;
            if (!_seen.Add(key))
                continue;
            if (baseline)
                continue;
            if (LuxWirePolicy.ShouldNotify(post, alertsEnabled, subscribed))
                notify.Add(post);
            else if (!alertsEnabled)
                _seen.Remove(key);
        }

        return notify;
    }
}

public sealed class LuxWireCursor
{
    public HashSet<string> Seen { get; }
    public string? SinceId { get; set; }

    public LuxWireCursor(IEnumerable<string>? seen = null, string? sinceId = null)
    {
        Seen = new HashSet<string>(seen ?? Array.Empty<string>(), StringComparer.Ordinal);
        SinceId = string.IsNullOrWhiteSpace(sinceId) ? null : sinceId.Trim();
    }
}

public static class LuxWireSeenFile
{
    public static HashSet<string> Load(string path) => LoadCursor(path).Seen;

    public static LuxWireCursor LoadCursor(string path)
    {
        var cursor = new LuxWireCursor();
        if (!File.Exists(path))
            return cursor;
        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(path));
            if (doc.RootElement.TryGetProperty("keys", out var keys) && keys.ValueKind == JsonValueKind.Array)
            {
                foreach (var key in keys.EnumerateArray())
                {
                    if (key.ValueKind == JsonValueKind.String && key.GetString() is { Length: > 0 } text)
                        cursor.Seen.Add(text);
                }
            }

            if (doc.RootElement.TryGetProperty("sinceId", out var since) && since.ValueKind == JsonValueKind.String)
                cursor.SinceId = string.IsNullOrWhiteSpace(since.GetString()) ? null : since.GetString()!.Trim();
        }
        catch (JsonException)
        {
            // A damaged seen file is treated as empty so new posts can still alert.
        }

        return cursor;
    }

    public static void Save(string path, IEnumerable<string> keys, string? sinceId = null)
    {
        var payload = JsonSerializer.Serialize(new SeenFile
        {
            Keys = keys.OrderBy(key => key, StringComparer.Ordinal).ToArray(),
            SinceId = string.IsNullOrWhiteSpace(sinceId) ? null : sinceId.Trim()
        }, JsonFile.Options);
        JsonFile.AtomicWrite(path, payload + "\n");
    }

    sealed class SeenFile
    {
        public string[] Keys { get; set; } = Array.Empty<string>();
        public string? SinceId { get; set; }
    }
}

public static class LuxWireToast
{
    public static string Xml(LuxWirePost post, string? logoPath, string? paperPath = null)
    {
        var url = LuxWireLinks.Canonical(post.Url) ?? post.Url;
        var image = "";
        if (!string.IsNullOrWhiteSpace(logoPath))
        {
            var src = new Uri(logoPath).AbsoluteUri;
            image = $"""<image placement="appLogoOverride" hint-crop="none" src="{Esc(src)}"/>""";
        }

        var hero = LuxWireLinks.Canonical(post.ImageUrl);
        if (hero == null && !string.IsNullOrWhiteSpace(paperPath))
            hero = new Uri(paperPath).AbsoluteUri;
        if (hero != null)
            image += $"""<image placement="hero" src="{Esc(hero)}"/>""";

        return $"""
            <toast activationType="protocol" launch="{Esc(url)}">
              <visual>
                <binding template="ToastGeneric">
                  {image}
                  <text hint-style="title">{Esc(post.Title)}</text>
                  <text>{Esc(post.Body)}</text>
                </binding>
              </visual>
            </toast>
            """;
    }

    public static string Esc(string value)
    {
        var chars = value.Where(ch => !char.IsControl(ch)).ToArray();
        return new string(chars)
            .Replace("&", "&amp;", StringComparison.Ordinal)
            .Replace("<", "&lt;", StringComparison.Ordinal)
            .Replace(">", "&gt;", StringComparison.Ordinal)
            .Replace("\"", "&quot;", StringComparison.Ordinal);
    }
}
