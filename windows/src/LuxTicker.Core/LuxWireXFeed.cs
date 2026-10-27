using System.Globalization;
using System.Numerics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace LuxTicker;

/// <summary>
/// X API v2 timeline for @LuxWire. Parsing and URL building only — the app performs the HTTP calls.
/// Tokens are never stored in source.
/// </summary>
public static class LuxWireXFeed
{
    public const string Account = "LuxWire";
    public const int BacklogCap = 10;
    public const int DefaultPollMinutes = 3;
    public const int LoopbackPort = 47321;

    public static int ClampMinutes(int minutes) => Math.Clamp(minutes, 2, 5);

    public static string RedirectUri => "http://127.0.0.1:" + LoopbackPort + "/callback";

    public static bool IsApiHost(string? url)
    {
        if (string.IsNullOrWhiteSpace(url) || !Uri.TryCreate(url, UriKind.Absolute, out var uri))
            return false;
        return uri.Host.Equals("api.x.com", StringComparison.OrdinalIgnoreCase)
            || uri.Host.Equals("api.twitter.com", StringComparison.OrdinalIgnoreCase);
    }

    public static string StatusUrl(string id)
        => "https://x.com/" + Account + "/status/" + Uri.EscapeDataString(id.Trim());

    public static string UserLookupUrl()
        => "https://api.x.com/2/users/by/username/" + Account;

    public static string TimelineUrl(string userId, string? sinceId)
    {
        var query = "max_results=10&exclude=retweets,replies"
            + "&tweet.fields=created_at,text,note_tweet,attachments,entities,article"
            + "&expansions=attachments.media_keys&media.fields=url,preview_image_url,type";
        if (!string.IsNullOrWhiteSpace(sinceId))
            query += "&since_id=" + Uri.EscapeDataString(sinceId.Trim());
        return "https://api.x.com/2/users/" + Uri.EscapeDataString(userId.Trim()) + "/tweets?" + query;
    }

    public static string? ParseUserId(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return null;
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Object)
                return null;
            if (!data.TryGetProperty("id", out var id) || id.ValueKind != JsonValueKind.String)
                return null;
            var text = id.GetString()?.Trim();
            return string.IsNullOrEmpty(text) ? null : text;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public static XTimeline ParseTimeline(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return new XTimeline(Array.Empty<LuxWirePost>(), null, "Empty response.");
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            var error = ErrorTitle(root);
            if (!root.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Array)
                return new XTimeline(Array.Empty<LuxWirePost>(), null, error);

            var media = MediaMap(root);
            var posts = new List<LuxWirePost>();
            foreach (var tweet in data.EnumerateArray())
            {
                if (tweet.ValueKind != JsonValueKind.Object)
                    continue;
                var post = ToPost(tweet, media);
                if (post != null)
                    posts.Add(post);
            }

            var capped = Cap(posts);
            string? newest = null;
            foreach (var post in capped)
            {
                if (post.Id != null && (newest == null || Snowflake(post.Id) > Snowflake(newest)))
                    newest = post.Id;
            }

            if (root.TryGetProperty("meta", out var meta) && meta.TryGetProperty("newest_id", out var newestNode)
                && newestNode.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(newestNode.GetString()))
            {
                var fromMeta = newestNode.GetString()!.Trim();
                if (newest == null || Snowflake(fromMeta) > Snowflake(newest))
                    newest = fromMeta;
            }

            return new XTimeline(capped, newest, posts.Count == 0 ? error : null);
        }
        catch (JsonException)
        {
            return new XTimeline(Array.Empty<LuxWirePost>(), null, "X did not return JSON.");
        }
    }

    public static IReadOnlyList<LuxWirePost> Cap(IReadOnlyList<LuxWirePost> posts)
    {
        return posts
            .Where(post => !string.IsNullOrWhiteSpace(post.Id))
            .OrderByDescending(post => Snowflake(post.Id!))
            .Take(BacklogCap)
            .Reverse()
            .ToList();
    }

    public static XToken? ParseToken(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return null;
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (!root.TryGetProperty("access_token", out var access) || access.ValueKind != JsonValueKind.String)
                return null;
            var token = access.GetString()?.Trim();
            if (string.IsNullOrEmpty(token))
                return null;
            var refresh = root.TryGetProperty("refresh_token", out var refreshNode) && refreshNode.ValueKind == JsonValueKind.String
                ? refreshNode.GetString()
                : null;
            var expires = 7200;
            if (root.TryGetProperty("expires_in", out var expiresNode) && expiresNode.TryGetInt32(out var seconds) && seconds > 0)
                expires = seconds;
            return new XToken(token, string.IsNullOrWhiteSpace(refresh) ? null : refresh.Trim(), expires);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    static LuxWirePost? ToPost(JsonElement tweet, Dictionary<string, MediaHit> media)
    {
        if (!tweet.TryGetProperty("id", out var idNode) || idNode.ValueKind != JsonValueKind.String)
            return null;
        var id = idNode.GetString()?.Trim();
        if (string.IsNullOrEmpty(id))
            return null;

        var text = TextOf(tweet);
        var subscriber = IsSubscriber(tweet);
        DateTimeOffset? posted = null;
        if (tweet.TryGetProperty("created_at", out var created) && created.ValueKind == JsonValueKind.String
            && DateTimeOffset.TryParse(created.GetString(), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var parsed))
            posted = parsed;

        string? image = null;
        string? mediaType = null;
        if (tweet.TryGetProperty("attachments", out var attachments)
            && attachments.TryGetProperty("media_keys", out var keys)
            && keys.ValueKind == JsonValueKind.Array)
        {
            foreach (var key in keys.EnumerateArray())
            {
                if (key.ValueKind != JsonValueKind.String || key.GetString() is not { Length: > 0 } mediaKey)
                    continue;
                if (!media.TryGetValue(mediaKey, out var hit))
                    continue;
                mediaType ??= hit.Type;
                image ??= hit.Url;
            }
        }

        var slot = SlotFor(mediaType, tweet, image);
        return new LuxWirePost(StatusUrl(id), subscriber, posted, slot, text, id, image);
    }

    static string? SlotFor(string? mediaType, JsonElement tweet, string? image)
    {
        if (tweet.TryGetProperty("article", out var article) && article.ValueKind == JsonValueKind.Object)
            return "CARD";
        if (image != null || mediaType is "photo" or "video" or "animated_gif")
            return "IMAGE";
        return null;
    }

    static bool IsSubscriber(JsonElement tweet)
    {
        if (tweet.TryGetProperty("subscriber_only", out var flag) && flag.ValueKind == JsonValueKind.True)
            return true;
        if (tweet.TryGetProperty("subscription", out var subscription))
        {
            if (subscription.ValueKind == JsonValueKind.True)
                return true;
            if (subscription.ValueKind == JsonValueKind.Object
                && subscription.TryGetProperty("type", out var type)
                && type.ValueKind == JsonValueKind.String
                && type.GetString()?.Contains("sub", StringComparison.OrdinalIgnoreCase) == true)
                return true;
        }

        return false;
    }

    static string? TextOf(JsonElement tweet)
    {
        if (tweet.TryGetProperty("note_tweet", out var note) && note.ValueKind == JsonValueKind.Object
            && note.TryGetProperty("text", out var noteText) && noteText.ValueKind == JsonValueKind.String)
        {
            var longText = noteText.GetString()?.Trim();
            if (!string.IsNullOrEmpty(longText))
                return longText;
        }

        if (tweet.TryGetProperty("text", out var text) && text.ValueKind == JsonValueKind.String)
        {
            var value = text.GetString()?.Trim();
            return string.IsNullOrEmpty(value) ? null : value;
        }

        return null;
    }

    static Dictionary<string, MediaHit> MediaMap(JsonElement root)
    {
        var map = new Dictionary<string, MediaHit>(StringComparer.Ordinal);
        if (!root.TryGetProperty("includes", out var includes) || !includes.TryGetProperty("media", out var media) || media.ValueKind != JsonValueKind.Array)
            return map;
        foreach (var item in media.EnumerateArray())
        {
            if (!item.TryGetProperty("media_key", out var keyNode) || keyNode.ValueKind != JsonValueKind.String)
                continue;
            var key = keyNode.GetString();
            if (string.IsNullOrEmpty(key))
                continue;
            var type = item.TryGetProperty("type", out var typeNode) && typeNode.ValueKind == JsonValueKind.String ? typeNode.GetString() : null;
            var url = FirstHttp(item, "url") ?? FirstHttp(item, "preview_image_url");
            map[key] = new MediaHit(type, url);
        }

        return map;
    }

    static string? FirstHttp(JsonElement item, string name)
    {
        if (!item.TryGetProperty(name, out var node) || node.ValueKind != JsonValueKind.String)
            return null;
        return LuxWireLinks.Canonical(node.GetString());
    }

    static string? ErrorTitle(JsonElement root)
    {
        if (root.TryGetProperty("title", out var title) && title.ValueKind == JsonValueKind.String)
            return title.GetString();
        if (root.TryGetProperty("errors", out var errors) && errors.ValueKind == JsonValueKind.Array)
        {
            foreach (var error in errors.EnumerateArray())
            {
                if (error.TryGetProperty("message", out var message) && message.ValueKind == JsonValueKind.String)
                    return message.GetString();
            }
        }

        return null;
    }

    public static BigInteger Snowflake(string id)
    {
        return BigInteger.TryParse(id, NumberStyles.None, CultureInfo.InvariantCulture, out var value)
            ? value
            : BigInteger.Zero;
    }

    readonly record struct MediaHit(string? Type, string? Url);
}

public readonly record struct XTimeline(IReadOnlyList<LuxWirePost> Posts, string? NewestId, string? Error);

public readonly record struct XToken(string AccessToken, string? RefreshToken, int ExpiresIn);

public sealed record XCredentials(
    string? AppBearer,
    string? ClientId,
    string? ClientSecret,
    string? AccessToken,
    string? RefreshToken,
    DateTimeOffset? AccessExpires,
    string? LuxWireUserId)
{
    public static XCredentials Empty { get; } = new(null, null, null, null, null, null, null);

    public bool HasUserToken => !string.IsNullOrWhiteSpace(AccessToken);

    public bool UserTokenStale(DateTimeOffset now)
        => HasUserToken && AccessExpires != null && AccessExpires <= now.AddMinutes(1);

    public string? PollToken(DateTimeOffset now)
    {
        if (HasUserToken && !UserTokenStale(now))
            return AccessToken;
        return string.IsNullOrWhiteSpace(AppBearer) ? null : AppBearer;
    }

    /// <summary>True when a saved or environment token can poll. The Settings UI hides alerts otherwise.</summary>
    public bool CanAlert(DateTimeOffset now)
        => PollToken(now) != null || !string.IsNullOrWhiteSpace(RefreshToken);

    public string Describe()
    {
        if (HasUserToken)
            return "Signed in to X. Public posts, and subscriber posts this account can see.";
        if (!string.IsNullOrWhiteSpace(AppBearer))
            return "App bearer saved. Public posts only until you sign in to X.";
        return "No X token yet. Paste an app bearer, or sign in.";
    }
}

public static class XAuthStore
{
    public const string BearerVariable = "LUXWIRE_X_BEARER";
    public const string ClientIdVariable = "LUXWIRE_X_CLIENT_ID";

    public static XCredentials Load(string path, bool includeEnvironment = true)
    {
        var credentials = ReadFile(path);
        if (!includeEnvironment)
            return credentials;
        var bearer = credentials.AppBearer;
        if (string.IsNullOrWhiteSpace(bearer))
            bearer = Clean(Environment.GetEnvironmentVariable(BearerVariable));
        var clientId = credentials.ClientId;
        if (string.IsNullOrWhiteSpace(clientId))
            clientId = Clean(Environment.GetEnvironmentVariable(ClientIdVariable));
        return credentials with { AppBearer = bearer, ClientId = clientId };
    }

    public static void Save(string path, XCredentials credentials)
    {
        var file = new AuthFile
        {
            AppBearer = Clean(credentials.AppBearer),
            ClientId = Clean(credentials.ClientId),
            ClientSecret = Clean(credentials.ClientSecret),
            AccessToken = Clean(credentials.AccessToken),
            RefreshToken = Clean(credentials.RefreshToken),
            AccessExpires = credentials.AccessExpires,
            LuxWireUserId = Clean(credentials.LuxWireUserId)
        };
        JsonFile.AtomicWrite(path, JsonSerializer.Serialize(file, JsonFile.Options) + "\n");
    }

    static XCredentials ReadFile(string path)
    {
        if (!File.Exists(path))
            return XCredentials.Empty;
        try
        {
            var file = JsonSerializer.Deserialize<AuthFile>(File.ReadAllText(path), JsonFile.Options);
            if (file == null)
                return XCredentials.Empty;
            return new XCredentials(
                Clean(file.AppBearer),
                Clean(file.ClientId),
                Clean(file.ClientSecret),
                Clean(file.AccessToken),
                Clean(file.RefreshToken),
                file.AccessExpires,
                Clean(file.LuxWireUserId));
        }
        catch
        {
            return XCredentials.Empty;
        }
    }

    static string? Clean(string? value)
    {
        var text = value?.Trim();
        return string.IsNullOrEmpty(text) ? null : text;
    }

    sealed class AuthFile
    {
        public string? AppBearer { get; set; }
        public string? ClientId { get; set; }
        public string? ClientSecret { get; set; }
        public string? AccessToken { get; set; }
        public string? RefreshToken { get; set; }
        public DateTimeOffset? AccessExpires { get; set; }
        public string? LuxWireUserId { get; set; }
    }
}

public static class LuxWirePkce
{
    public static (string Verifier, string Challenge) Create()
    {
        var verifier = Base64Url(RandomNumberGenerator.GetBytes(32));
        return (verifier, Challenge(verifier));
    }

    public static string Challenge(string verifier)
        => Base64Url(SHA256.HashData(Encoding.ASCII.GetBytes(verifier)));

    public static string AuthorizeUrl(string clientId, string redirectUri, string state, string challenge)
    {
        return "https://twitter.com/i/oauth2/authorize?response_type=code"
            + "&client_id=" + Uri.EscapeDataString(clientId)
            + "&redirect_uri=" + Uri.EscapeDataString(redirectUri)
            + "&scope=" + Uri.EscapeDataString("tweet.read users.read offline.access")
            + "&state=" + Uri.EscapeDataString(state)
            + "&code_challenge=" + Uri.EscapeDataString(challenge)
            + "&code_challenge_method=S256";
    }

    public static string Base64Url(byte[] bytes)
        => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
