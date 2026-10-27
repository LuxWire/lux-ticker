using System.Numerics;
using System.Security.Cryptography;
using System.Text;

namespace LuxTicker.Tests;

public class LuxWireTests
{
    [Fact]
    public void Personal_build_is_subscribed_and_versioned()
    {
        Assert.True(LuxWireEdition.Subscribed);
        Assert.Equal("1.4.14", LuxWireEdition.Version);
        Assert.Equal("LuxWire Ticker", LuxWireEdition.ProductName);
        var root = Repo.Root();
        Assert.Contains("<Version>1.4.14</Version>", File.ReadAllText(Path.Combine(root, "Directory.Build.props")));
        Assert.Contains("version=\"1.4.14.0\"", File.ReadAllText(Path.Combine(root, "src", "LuxTicker.App", "app.manifest")));
        Assert.Contains("level=\"asInvoker\"", File.ReadAllText(Path.Combine(root, "src", "LuxTicker.App", "app.manifest")));
        var notes = File.ReadAllText(Path.Combine(root, "RELEASE-NOTES.md"));
        Assert.Contains("1.4.1", notes);
        Assert.Contains("1.4.0", notes);
        Assert.Contains("1.3.0", notes);
        Assert.Contains("Bitcoin", notes);
        Assert.Contains("Paper matches Lux Wire article colors", notes);
        Assert.Contains("1.2.1", notes);
        Assert.Contains("1.2.0", notes);
        Assert.Contains("1.1.0", notes);
        Assert.Contains("LuxTicker-Setup.exe", notes);
        Assert.Contains("InitPropVariantFromString", notes);
        Assert.Contains("propsys.dll", notes);
        Assert.Contains("above the Windows taskbar", notes);
        Assert.Contains("startup.log", notes);
        Assert.Contains("posts.jsonl", notes);
        Assert.Contains("Simulate", notes);
        Assert.Contains("asInvoker", notes);
        Assert.Contains("Built-in @LuxWire X feed alerts (no Wade handoff).", notes);
        Assert.Contains("1.4.14", notes);
        Assert.Equal(["1.4", "1.3", "1.2", "1.1"], ReleaseNotes.Displayed.Select(note => note.Version).ToArray());
        Assert.Contains("1.4 — October 26, 2026", ReleaseNotes.Current);
        Assert.Contains("Privacy and Terms open luxwire.github.io.", ReleaseNotes.Current);
        Assert.Contains("An update keeps the settings and watchlist already saved.", ReleaseNotes.Current);
        Assert.Contains("What's New is grouped by minor version, such as 1.4 and 1.3.", ReleaseNotes.Current);
        Assert.Contains("Continue with Google and Continue with X are on the account sign-in screen.", ReleaseNotes.Current);
        Assert.Contains("Privacy and Terms are on the sign-in screen and in About.", ReleaseNotes.Current);
        Assert.Contains("The ticker still works signed out.", ReleaseNotes.Current);
        Assert.Contains("Wall Street", ReleaseNotes.Current);
        Assert.Contains("Live updates while scrolling", ReleaseNotes.Current);
        Assert.Contains("Open chart", ReleaseNotes.Current);
        Assert.Contains("The middle of the W rises above the side strokes.", ReleaseNotes.Current);
        Assert.Contains("The ticker background has a color.", ReleaseNotes.Current);
        Assert.Contains("There is no place to paste a key.", ReleaseNotes.Current);
        Assert.Contains("What's new is in Updates", ReleaseNotes.Current);
        Assert.Contains("Follow on X and Subscribe.", ReleaseNotes.Current);
        Assert.Contains("same orange LW tile", ReleaseNotes.Current);
        Assert.Contains("1.4.7", notes);
        Assert.Contains("directly above the taskbar", ReleaseNotes.Current);
        Assert.Contains("transparent background", ReleaseNotes.Current);
        Assert.Contains("Chart and Settings stay clickable.", ReleaseNotes.Current);
        Assert.Contains("Updates and About are separate", ReleaseNotes.Current);
        Assert.Contains("recommended ticker fonts", ReleaseNotes.Current);
        Assert.Contains("Reset ALL settings to defaults asks first.", ReleaseNotes.Current);
        Assert.Contains("fully opaque", ReleaseNotes.Current);
        Assert.Contains("1.4.6", notes);
        Assert.Contains("ReleaseNotes.Displayed", File.ReadAllText(Path.Combine(root, "src", "LuxTicker.App", "SettingsWindow.xaml.cs")));
        Assert.Contains("#CC5500", ReleaseNotes.Current);
        Assert.Contains("Show Desktop", ReleaseNotes.Current);
        Assert.Contains("Always on top", ReleaseNotes.Current);
        Assert.Contains("The button on the bar says Chart.", ReleaseNotes.Current);
        Assert.Contains("An unknown symbol says so.", ReleaseNotes.Current);
        Assert.Contains("1.4.5", notes);
        Assert.Contains("1H", ReleaseNotes.Current);
        Assert.Contains("Candles", ReleaseNotes.Current);
        Assert.Contains("1.4.4", notes);
        Assert.Contains("1.4.3", notes);
        Assert.Contains("Vertical position", ReleaseNotes.Current);
        Assert.Contains("crosshair", ReleaseNotes.Current);
        Assert.Contains("Paper matches Lux Wire article colors (`#F6F2E8` / accent `#F7931A`).", ReleaseNotes.Current);
        Assert.Contains("Bitcoin chart", ReleaseNotes.Current);
        Assert.DoesNotContain("#FF6A00", ReleaseNotes.Current);
        Assert.Contains("Built-in @LuxWire X feed alerts (no Wade handoff).", ReleaseNotes.Current);
        Assert.DoesNotContain("posts.jsonl", ReleaseNotes.Current);
        Assert.Contains("https://x.com/LuxWire", ReleaseNotes.Current);
        Assert.Contains("October 24, 2026", notes);
        Assert.Contains("October 26, 2026", ReleaseNotes.Current);
        Assert.Contains("LuxTicker-Setup.exe", ReleaseNotes.Current);
        Assert.Contains("Start Menu", ReleaseNotes.Current);
        Assert.Contains("above the Windows taskbar", ReleaseNotes.Current);
        Assert.Contains("Above the taskbar", ReleaseNotes.Current);
        Assert.Contains("Simulate Lux Wire post", ReleaseNotes.Current);
        Assert.Equal(DockEdge.Bottom, AppSettings.Default.Dock);
        Assert.True(AppSettings.Default.LuxWirePosts);
        Assert.Equal(3, AppSettings.Default.LuxWirePollMinutes);
    }

    [Fact]
    public void Timeline_parses_public_subscriber_and_media_posts()
    {
        var timeline = LuxWireXFeed.ParseTimeline("""
            {
              "data": [
                {
                  "id": "300",
                  "text": "public note",
                  "created_at": "2026-10-23T16:00:00.000Z",
                  "attachments": { "media_keys": ["m1"] }
                },
                {
                  "id": "301",
                  "text": "short",
                  "note_tweet": { "text": "subscriber desk note" },
                  "subscriber_only": true,
                  "article": { "title": "Desk" }
                },
                {
                  "id": "302",
                  "text": "members",
                  "subscription": { "type": "subscriber" }
                }
              ],
              "includes": {
                "media": [
                  { "media_key": "m1", "type": "photo", "url": "https://cdn.example/a.png" }
                ]
              },
              "meta": { "newest_id": "302", "result_count": 3 }
            }
            """);

        Assert.Equal(3, timeline.Posts.Count);
        Assert.Equal("302", timeline.NewestId);
        Assert.Null(timeline.Error);
        var photo = timeline.Posts[0];
        Assert.Equal("300", photo.Id);
        Assert.Equal("https://x.com/LuxWire/status/300", photo.Url);
        Assert.False(photo.SubscriberOnly);
        Assert.Equal("IMAGE", photo.SlotType);
        Assert.Equal("https://cdn.example/a.png", photo.ImageUrl);
        Assert.Equal("public note", photo.TextPreview);
        Assert.Equal(new DateTimeOffset(2026, 10, 23, 16, 0, 0, TimeSpan.Zero), photo.PostedAt);

        var card = timeline.Posts[1];
        Assert.True(card.SubscriberOnly);
        Assert.Equal("CARD", card.SlotType);
        Assert.Equal("subscriber desk note", card.TextPreview);
        Assert.Equal("https://x.com/LuxWire/status/301", card.Url);
        Assert.True(timeline.Posts[2].SubscriberOnly);
        Assert.Equal("Lux Wire · Subscribers", timeline.Posts[2].Title);
        Assert.Equal("Card · Subscribers", card.Title);

        var denied = LuxWireXFeed.ParseTimeline("""{"title":"Unauthorized"}""");
        Assert.Empty(denied.Posts);
        Assert.Equal("Unauthorized", denied.Error);
        Assert.Null(LuxWireXFeed.ParseUserId(null));
        Assert.Equal("99", LuxWireXFeed.ParseUserId("""{"data":{"id":"99"}}"""));
        Assert.False(LuxWireLinks.IsHttp("file:///tmp/x"));
    }

    [Fact]
    public void Backlog_keeps_the_newest_ten_oldest_first_and_since_id_is_in_the_url()
    {
        var posts = Enumerable.Range(1, 12)
            .Select(i => new LuxWirePost(LuxWireXFeed.StatusUrl(i.ToString()), false, null, null, "t", i.ToString()))
            .ToList();
        var capped = LuxWireXFeed.Cap(posts);
        Assert.Equal(10, capped.Count);
        Assert.Equal("3", capped[0].Id);
        Assert.Equal("12", capped[^1].Id);
        Assert.Equal("https://x.com/LuxWire/status/12", LuxWireXFeed.StatusUrl("12"));

        var url = LuxWireXFeed.TimelineUrl("4242", "50");
        Assert.Contains("/users/4242/tweets?", url);
        Assert.Contains("since_id=50", url);
        Assert.Contains("max_results=10", url);
        Assert.DoesNotContain("since_id", LuxWireXFeed.TimelineUrl("4242", null));
        Assert.True(LuxWireXFeed.IsApiHost(url));
        Assert.False(LuxWireXFeed.IsApiHost("https://example.com/tweets"));
        Assert.Equal(2, LuxWireXFeed.ClampMinutes(1));
        Assert.Equal(3, LuxWireXFeed.ClampMinutes(3));
        Assert.Equal(5, LuxWireXFeed.ClampMinutes(9));
        Assert.True(LuxWireXFeed.Snowflake("12") > LuxWireXFeed.Snowflake("3"));
        Assert.Equal(BigInteger.Zero, LuxWireXFeed.Snowflake("nope"));
    }

    [Fact]
    public void Pkce_challenge_is_s256_and_tokens_stay_out_of_the_description()
    {
        const string verifier = "abc";
        var challenge = LuxWirePkce.Challenge(verifier);
        Assert.Equal(LuxWirePkce.Base64Url(SHA256.HashData(Encoding.ASCII.GetBytes(verifier))), challenge);
        var authorize = LuxWirePkce.AuthorizeUrl("cid", LuxWireXFeed.RedirectUri, "st", challenge);
        Assert.Contains("code_challenge_method=S256", authorize);
        Assert.Contains("127.0.0.1:47321/callback", LuxWireXFeed.RedirectUri);
        Assert.Contains(Uri.EscapeDataString(LuxWireXFeed.RedirectUri), authorize);
        Assert.Contains("tweet.read", authorize);
        Assert.Contains("offline.access", authorize);
        Assert.StartsWith("https://twitter.com/i/oauth2/authorize?", authorize);

        var parsed = LuxWireXFeed.ParseToken("""{"access_token":"ya","refresh_token":"rb","expires_in":60}""");
        Assert.True(parsed.HasValue);
        Assert.Equal("ya", parsed.Value.AccessToken);
        Assert.Equal("rb", parsed.Value.RefreshToken);
        Assert.Equal(60, parsed.Value.ExpiresIn);
        Assert.Null(LuxWireXFeed.ParseToken("""{"error":"nope"}"""));

        var now = DateTimeOffset.UtcNow;
        var fresh = new XCredentials("app-bearer", "cid", null, "user-token", "refresh", now.AddHours(1), "99");
        Assert.Equal("user-token", fresh.PollToken(now));
        var stale = fresh with { AccessExpires = now.AddSeconds(30) };
        Assert.Equal("app-bearer", stale.PollToken(now));
        Assert.Contains("Signed in to X", fresh.Describe());
        Assert.DoesNotContain("user-token", fresh.Describe());
        Assert.DoesNotContain("app-bearer", fresh.Describe());
        Assert.Contains("App bearer saved", new XCredentials("app-bearer", null, null, null, null, null, null).Describe());
        Assert.Contains("No X token yet", XCredentials.Empty.Describe());
        Assert.False(XCredentials.Empty.CanAlert(now));
        Assert.True(new XCredentials("app-bearer", null, null, null, null, null, null).CanAlert(now));
        Assert.True(new XCredentials(null, null, null, null, "refresh", null, null).CanAlert(now));
        Assert.True(fresh.CanAlert(now));
    }

    [Fact]
    public void Auth_file_does_not_pick_up_the_environment_unless_asked()
    {
        var dir = Path.Combine(Path.GetTempPath(), "lux-xauth-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var previousBearer = Environment.GetEnvironmentVariable(XAuthStore.BearerVariable);
        var previousClient = Environment.GetEnvironmentVariable(XAuthStore.ClientIdVariable);
        try
        {
            var path = Path.Combine(dir, "x-auth.json");
            XAuthStore.Save(path, new XCredentials("file-bearer", "file-client", null, null, null, null, null));
            Environment.SetEnvironmentVariable(XAuthStore.BearerVariable, "env-bearer");
            Environment.SetEnvironmentVariable(XAuthStore.ClientIdVariable, "env-client");

            var fromFile = XAuthStore.Load(path, includeEnvironment: false);
            Assert.Equal("file-bearer", fromFile.AppBearer);
            Assert.Equal("file-client", fromFile.ClientId);
            Assert.DoesNotContain("file-bearer", fromFile.Describe());

            File.Delete(path);
            Assert.Null(XAuthStore.Load(path, includeEnvironment: false).AppBearer);
            var fromEnv = XAuthStore.Load(path);
            Assert.Equal("env-bearer", fromEnv.AppBearer);
            Assert.Equal("env-client", fromEnv.ClientId);
            Assert.False(File.Exists(path));
        }
        finally
        {
            Environment.SetEnvironmentVariable(XAuthStore.BearerVariable, previousBearer);
            Environment.SetEnvironmentVariable(XAuthStore.ClientIdVariable, previousClient);
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void Inbox_baselines_history_then_dedupes_by_id()
    {
        var seen = new LuxWirePost(
            "https://x.com/LuxWire/status/2113430136347332737",
            false,
            new DateTimeOffset(2026, 10, 22, 20, 39, 44, TimeSpan.FromHours(-4)),
            "FILLER",
            "Bitcoin's hash rate...",
            "2113430136347332737");
        var inbox = new LuxWireInbox();
        Assert.Empty(inbox.Accept([seen, seen], alertsEnabled: true, subscribed: true, baseline: true));
        Assert.Single(inbox.Seen);

        var again = new LuxWirePost("https://x.com/LuxWire/status/9", false, null, null, "a<b>&c", "post-9");
        var duplicate = again with { Url = "https://x.com/LuxWire/status/10", SubscriberOnly = true, TextPreview = "second" };
        var notified = inbox.Accept([again, duplicate], alertsEnabled: true, subscribed: LuxWireEdition.Subscribed, baseline: false);
        var only = Assert.Single(notified);
        Assert.Equal("id:post-9", only.Key);
        Assert.Equal("https://x.com/LuxWire/status/9", only.Url);
        Assert.Contains("a&lt;b&gt;&amp;c", LuxWireToast.Xml(only, "/tmp/lux wire.png"));

        var held = new LuxWirePost("https://x.com/LuxWire/status/11", false, null, null, "later", "11");
        Assert.Empty(inbox.Accept([held], alertsEnabled: false, subscribed: true, baseline: false));
        var released = inbox.Accept([held], alertsEnabled: true, subscribed: true, baseline: false);
        Assert.Equal("https://x.com/LuxWire/status/11", Assert.Single(released).Url);
    }

    [Fact]
    public void Subscriber_posts_alert_when_subscribed_and_are_labeled_in_the_toast()
    {
        var post = new LuxWirePost("https://x.com/LuxWire/status/5", true, null, null, "desk note", "5");
        Assert.True(post.SubscriberOnly);
        Assert.True(LuxWirePolicy.ShouldNotify(post, alertsEnabled: true, subscribed: true));
        Assert.False(LuxWirePolicy.ShouldNotify(post, alertsEnabled: true, subscribed: false));
        Assert.False(LuxWirePolicy.ShouldNotify(post, alertsEnabled: false, subscribed: true));

        var xml = LuxWireToast.Xml(post, null);
        Assert.Contains("activationType=\"protocol\"", xml);
        Assert.Contains("https://x.com/LuxWire/status/5", xml);
        Assert.Contains("desk note", xml);
        Assert.Contains("Lux Wire", xml);
        Assert.DoesNotContain("<image", xml);
    }

    [Fact]
    public void Seen_file_roundtrip_keeps_since_id_and_simulate_opens_the_profile()
    {
        var dir = Path.Combine(Path.GetTempPath(), "lux-wire-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var path = Path.Combine(dir, "seen.json");
            LuxWireSeenFile.Save(path, ["https://x.com/LuxWire/status/1", "id:abc"], "99");
            var loaded = LuxWireSeenFile.LoadCursor(path);
            Assert.Contains("id:abc", loaded.Seen);
            Assert.Contains("https://x.com/LuxWire/status/1", loaded.Seen);
            Assert.Equal("99", loaded.SinceId);
            File.WriteAllText(path, "{not json");
            Assert.Empty(LuxWireSeenFile.Load(path));
            Assert.Null(LuxWireSeenFile.LoadCursor(path).SinceId);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }

        var sample = LuxWirePost.Simulate();
        Assert.Equal("https://x.com/LuxWire", sample.Url);
        var xml = LuxWireToast.Xml(sample, null);
        Assert.Contains("launch=\"https://x.com/LuxWire\"", xml);
        Assert.Contains("Sample post. Tap to open @LuxWire.", xml);
    }

    [Fact]
    public void The_app_no_longer_watches_a_wade_posts_file()
    {
        var root = Repo.Root();
        var feed = File.ReadAllText(Path.Combine(root, "src", "LuxTicker.App", "LuxWireFeed.cs"));
        Assert.DoesNotContain("posts.jsonl", feed);
        Assert.DoesNotContain("FileSystemWatcher", feed);
        Assert.DoesNotContain("LuxWireIngest", feed);
        Assert.Contains("TimelineUrl", feed);
        var signIn = File.ReadAllText(Path.Combine(root, "src", "LuxTicker.App", "XSignIn.cs"));
        Assert.Contains("TcpListener", signIn);
        Assert.DoesNotContain("HttpListener", signIn);
        foreach (var file in Directory.GetFiles(Path.Combine(root, "src"), "*.cs", SearchOption.AllDirectories))
        {
            var text = File.ReadAllText(file);
            Assert.DoesNotContain("posts.jsonl", text);
            Assert.DoesNotContain("LuxWireIngest", text);
            Assert.DoesNotContain("HttpListener", text);
        }
    }
}
