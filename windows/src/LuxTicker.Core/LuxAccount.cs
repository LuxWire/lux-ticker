using System.Globalization;
using System.Text;
using System.Text.Json;

namespace LuxTicker;

/// <summary>
/// Shared LuxWire account. The same project can serve this ticker, the Android ticker, and Lux 531.
/// The URL and anon key come from luxwire.config.json. A secret key does not belong in the app.
/// </summary>
public static class LuxAccount
{
    public static string ProjectUrl => LuxWireConfig.SupabaseUrl;
    public static string PublishableKey => LuxWireConfig.SupabaseAnonKey;
    public const string AppKey = "lux-ticker-windows";
    public const int LoopbackPort = 47322;
    public const string RedirectUri = "http://localhost:47322/auth-callback";
    public const string PrivacyUrl = "https://luxwire.github.io/privacy.html";
    public const string TermsUrl = "https://luxwire.github.io/terms.html";

    /// <summary>Google and X use the system browser and a localhost loopback callback.</summary>
    public static readonly bool OAuthEnabled = true;

    public const int PasswordMinimum = 8;
    public const string SignedOutStatus = "Signed out. The ticker works without an account.";
}

public enum AccountTier
{
    Free,
    SignedIn,
    Pro
}

public static class AccountTierRules
{
    public static bool CanSync(AccountTier tier) => tier is AccountTier.SignedIn or AccountTier.Pro;

    public static AccountTier Parse(string? text)
    {
        return text?.Trim().ToLowerInvariant() switch
        {
            "free" => AccountTier.Free,
            "pro" => AccountTier.Pro,
            "signed_in" or "signed-in" or "signedin" => AccountTier.SignedIn,
            _ => AccountTier.SignedIn
        };
    }

    public static string Wire(AccountTier tier) => tier switch
    {
        AccountTier.Free => "free",
        AccountTier.Pro => "pro",
        _ => "signed_in"
    };

    public static string Label(AccountTier tier) => tier switch
    {
        AccountTier.Free => "Free",
        AccountTier.Pro => "Pro",
        _ => "Signed in"
    };
}

public sealed record AccountSession(
    string AccessToken,
    string RefreshToken,
    string UserId,
    string? Email,
    string? Username,
    string Tier,
    DateTimeOffset ExpiresAt)
{
    public AccountTier TierValue => AccountTierRules.Parse(Tier);
}

public sealed record AccountSnapshot(bool SignedIn, string? Email, string? Username, AccountTier Tier, string Status)
{
    public string TierLabel => AccountTierRules.Label(Tier);

    public static AccountSnapshot SignedOut(string? status = null)
        => new(false, null, null, AccountTier.Free, status ?? LuxAccount.SignedOutStatus);

    public static AccountSnapshot From(AccountSession session, string status)
        => new(true, session.Email, session.Username, session.TierValue, status);
}

public readonly record struct AccountOutcome(bool Ok, string Message)
{
    public static AccountOutcome Done(string message) => new(true, message);
    public static AccountOutcome Failed(string message) => new(false, message);
}

public sealed record AuthParse(AccountSession? Session, bool ConfirmationRequired, string? Email, string? Username, string? Error)
{
    public static AuthParse Signed(AccountSession session) => new(session, false, session.Email, session.Username, null);
    public static AuthParse Confirm(string? email, string? username) => new(null, true, email, username, null);
    public static AuthParse Fail(string error) => new(null, false, null, null, error);
}

public sealed record AccountPayload(
    string SettingsJson,
    string WatchlistJson,
    DateTimeOffset UpdatedAt,
    bool Dirty,
    bool Untouched);

public sealed record RemoteSettings(string SettingsJson, string WatchlistJson, DateTimeOffset UpdatedAt);

public enum SyncAction
{
    Push,
    Pull,
    Keep
}

public readonly record struct SyncDecision(SyncAction Action, DateTimeOffset At);

public interface ILuxAccount
{
    AccountSnapshot Current { get; }
    event EventHandler? Changed;
    Task<AccountOutcome> CreateAccount(string email, string password, string? username, CancellationToken cancel = default);
    Task<AccountOutcome> SignIn(string emailOrUsername, string password, CancellationToken cancel = default);
    Task<AccountOutcome> SendReset(string emailOrUsername, CancellationToken cancel = default);
    Task<AccountOutcome> SetUsername(string? username, CancellationToken cancel = default);
    Task<AccountOutcome> SignOut(CancellationToken cancel = default);
    Task<AccountOutcome> DeleteAccount(CancellationToken cancel = default);
    Task<AccountOutcome> ContinueWith(string provider, CancellationToken cancel = default);
    void NoteLocalChange();
    void Attach(Func<AccountPayload> capture, Func<string, string, bool> apply);
    void Start(CancellationToken cancel);
}

public static class AccountNames
{
    public static string? Normalize(string? username)
        => string.IsNullOrWhiteSpace(username) ? null : username.Trim();

    public static bool IsUsername(string? username)
    {
        var text = Normalize(username);
        if (text == null || text.Length < 3 || text.Length > 24)
            return false;
        foreach (var c in text)
        {
            if (c is (>= 'A' and <= 'Z') or (>= 'a' and <= 'z') or (>= '0' and <= '9') or '_')
                continue;
            return false;
        }

        return true;
    }

    /// <summary>Blank is allowed. A new account does not have to pick a username.</summary>
    public static bool IsOptionalUsername(string? username)
        => string.IsNullOrWhiteSpace(username) || IsUsername(username);

    public static bool IsEmail(string? value)
    {
        var text = value?.Trim() ?? "";
        var at = text.IndexOf('@');
        return at > 0 && at < text.Length - 1 && !text.Contains(' ');
    }

    public static bool PasswordOk(string? password)
        => !string.IsNullOrEmpty(password) && password.Length >= LuxAccount.PasswordMinimum;
}

public static class AccountInput
{
    public static string? RejectSignup(string? email, string? password, string? username)
    {
        if (!AccountNames.IsEmail(email))
            return "Use an email address to create an account.";
        if (!AccountNames.PasswordOk(password))
            return "Use at least 8 characters.";
        if (!AccountNames.IsOptionalUsername(username))
            return "A username is 3 to 24 letters, digits, or underscores.";
        return null;
    }

    public static string? RejectSignIn(string? emailOrUsername, string? password)
    {
        if (string.IsNullOrWhiteSpace(emailOrUsername) || string.IsNullOrWhiteSpace(password))
            return "Enter an email or username, and a password.";
        if (!AccountNames.PasswordOk(password))
            return "Use at least 8 characters.";
        if (!emailOrUsername.Contains('@') && !AccountNames.IsUsername(emailOrUsername))
            return "That username is not valid.";
        return null;
    }
}

public static class SupabaseAuth
{
    public const string UpsertPrefer = "resolution=merge-duplicates,return=representation";

    public static string SignupUrl() => LuxAccount.ProjectUrl + "/auth/v1/signup";
    public static string PasswordTokenUrl() => LuxAccount.ProjectUrl + "/auth/v1/token?grant_type=password";
    public static string RefreshUrl() => LuxAccount.ProjectUrl + "/auth/v1/token?grant_type=refresh_token";
    public static string PkceTokenUrl() => LuxAccount.ProjectUrl + "/auth/v1/token?grant_type=pkce";
    public static string RecoverUrl() => LuxAccount.ProjectUrl + "/auth/v1/recover";
    public static string LogoutUrl() => LuxAccount.ProjectUrl + "/auth/v1/logout";
    public static string UserUrl() => LuxAccount.ProjectUrl + "/auth/v1/user";
    public static string EmailForUsernameUrl() => LuxAccount.ProjectUrl + "/rest/v1/rpc/email_for_username";
    public static string DeleteAccountUrl() => LuxAccount.ProjectUrl + "/rest/v1/rpc/delete_own_account";

    public static string AppSettingsSelectUrl(string userId)
        => LuxAccount.ProjectUrl + "/rest/v1/app_settings?user_id=eq." + Uri.EscapeDataString(userId)
           + "&app=eq." + Uri.EscapeDataString(LuxAccount.AppKey)
           + "&select=data,updated_at";

    public static string AppSettingsUpsertUrl()
        => LuxAccount.ProjectUrl + "/rest/v1/app_settings?on_conflict=user_id,app";

    public static string EntitlementsUrl(string userId)
        => LuxAccount.ProjectUrl + "/rest/v1/entitlements?user_id=eq." + Uri.EscapeDataString(userId) + "&select=tier,updated_at";

    public static string ProfileUrl(string userId)
        => LuxAccount.ProjectUrl + "/rest/v1/profiles?user_id=eq." + Uri.EscapeDataString(userId);

    public static string? ProviderSlug(string? provider)
    {
        return provider?.Trim().ToLowerInvariant() switch
        {
            "x" or "twitter" => "x",
            "google" => "google",
            _ => null
        };
    }

    public static string AuthorizeUrl(string provider, string redirectUri, string state, string challenge)
    {
        var slug = ProviderSlug(provider) ?? provider.Trim().ToLowerInvariant();
        return LuxAccount.ProjectUrl + "/auth/v1/authorize?provider=" + Uri.EscapeDataString(slug)
            + "&redirect_to=" + Uri.EscapeDataString(redirectUri)
            + "&code_challenge=" + Uri.EscapeDataString(challenge)
            + "&code_challenge_method=s256"
            + "&state=" + Uri.EscapeDataString(state);
    }

    public static string SignupBody(string email, string password, string? username)
    {
        var name = AccountNames.Normalize(username);
        if (name == null)
            return JsonSerializer.Serialize(new { email, password });
        return JsonSerializer.Serialize(new { email, password, data = new { username = name } });
    }

    public static string PasswordBody(string email, string password)
        => JsonSerializer.Serialize(new { email, password });

    public static string RefreshBody(string refreshToken)
        => JsonSerializer.Serialize(new { refresh_token = refreshToken });

    public static string RecoverBody(string email)
        => JsonSerializer.Serialize(new { email });

    public static string PkceBody(string code, string verifier)
        => JsonSerializer.Serialize(new { auth_code = code, code_verifier = verifier });

    public static string UsernameBody(string username)
        => JsonSerializer.Serialize(new { p_username = username });

    public static string ProfileBody(string? username)
    {
        if (username == null)
            return "{\"username\":null}";
        return JsonSerializer.Serialize(new { username });
    }

    public static string UpsertBody(string userId, string dataJson, DateTimeOffset updatedAt)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            writer.WriteString("user_id", userId);
            writer.WriteString("app", LuxAccount.AppKey);
            writer.WritePropertyName("data");
            using (var data = JsonDocument.Parse(dataJson))
                data.RootElement.WriteTo(writer);
            writer.WriteString("updated_at", updatedAt.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture));
            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }

    public static AuthParse Parse(string json, DateTimeOffset now)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
                return AuthParse.Fail("The account service returned something unexpected.");

            var tokenRoot = root;
            if (string.IsNullOrEmpty(StringOf(root, "access_token"))
                && root.TryGetProperty("session", out var sessionEl)
                && sessionEl.ValueKind == JsonValueKind.Object)
                tokenRoot = sessionEl;

            var access = StringOf(tokenRoot, "access_token");
            var user = root.TryGetProperty("user", out var userEl) && userEl.ValueKind == JsonValueKind.Object
                ? userEl
                : root;
            if (string.IsNullOrEmpty(access))
            {
                if (HasError(root))
                    return AuthParse.Fail(Friendly(json, "Sign-in did not finish."));
                var id = StringOf(user, "id");
                if (!string.IsNullOrEmpty(id))
                    return AuthParse.Confirm(StringOf(user, "email"), UsernameOf(user));
                return AuthParse.Fail(Friendly(json, "Sign-in did not finish."));
            }

            var userId = StringOf(user, "id") ?? "";
            if (userId.Length == 0)
                return AuthParse.Fail("Sign-in did not return a user.");
            var seconds = 3600;
            if (tokenRoot.TryGetProperty("expires_in", out var exp) && exp.ValueKind == JsonValueKind.Number && exp.TryGetInt32(out var parsed))
                seconds = Math.Clamp(parsed, 30, 60 * 60 * 24);
            var session = new AccountSession(
                access,
                StringOf(tokenRoot, "refresh_token") ?? "",
                userId,
                StringOf(user, "email"),
                UsernameOf(user),
                AccountTierRules.Wire(AccountTier.SignedIn),
                now.AddSeconds(seconds));
            return AuthParse.Signed(session);
        }
        catch (JsonException)
        {
            return AuthParse.Fail("The account service returned something unexpected.");
        }
    }

    public static string? ParseEmail(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            return doc.RootElement.ValueKind == JsonValueKind.String ? doc.RootElement.GetString() : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public static bool TryParseTier(string json, out AccountTier tier)
    {
        tier = AccountTier.SignedIn;
        try
        {
            using var doc = JsonDocument.Parse(json);
            var row = FirstObject(doc.RootElement);
            if (row == null)
                return false;
            tier = AccountTierRules.Parse(StringOf(row.Value, "tier"));
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    public static string? ParseProfileUsername(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var row = FirstObject(doc.RootElement);
            return row == null ? null : AccountNames.Normalize(StringOf(row.Value, "username"));
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public static RemoteSettings? ParseRemote(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var row = FirstObject(doc.RootElement);
            if (row == null || !row.Value.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Object)
                return null;
            if (!data.TryGetProperty("settings", out var settings) || settings.ValueKind != JsonValueKind.Object)
                return null;
            if (!data.TryGetProperty("watchlist", out var watchlist) || watchlist.ValueKind != JsonValueKind.Object)
                return null;
            if (!TryStamp(row.Value, "updated_at", out var at))
                at = DateTimeOffset.UnixEpoch;
            return new RemoteSettings(settings.GetRawText(), watchlist.GetRawText(), at);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public static string Friendly(string? body, string fallback)
    {
        var text = ExtractMessage(body);
        if (string.IsNullOrWhiteSpace(text))
            return fallback;
        var lower = text.ToLowerInvariant();
        if (lower.Contains("invalid login") || lower.Contains("invalid_credentials") || lower.Contains("invalid grant"))
            return "That email and password did not match.";
        if (lower.Contains("not confirmed"))
            return "Confirm the email, then sign in.";
        if (lower.Contains("already registered") || lower.Contains("already been registered") || lower.Contains("user already"))
            return "That email already has an account. Sign in instead.";
        if (lower.Contains("password"))
            return "Use at least 8 characters.";
        if (lower.Contains("duplicate") || lower.Contains("unique") || lower.Contains("23505"))
            return "That username is taken.";
        return fallback;
    }

    static bool HasError(JsonElement root)
    {
        if (root.TryGetProperty("error_code", out var code) && code.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(code.GetString()))
            return true;
        if (root.TryGetProperty("error", out var error) && error.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(error.GetString()))
            return true;
        if (root.TryGetProperty("msg", out var msg) && msg.ValueKind == JsonValueKind.String && root.TryGetProperty("error_code", out _))
            return true;
        return false;
    }

    static string? ExtractMessage(string? body)
    {
        if (string.IsNullOrWhiteSpace(body))
            return null;
        try
        {
            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
                return null;
            return StringOf(doc.RootElement, "msg")
                ?? StringOf(doc.RootElement, "error_description")
                ?? StringOf(doc.RootElement, "message")
                ?? StringOf(doc.RootElement, "error");
        }
        catch (JsonException)
        {
            return null;
        }
    }

    static string? UsernameOf(JsonElement user)
    {
        foreach (var key in new[] { "user_metadata", "raw_user_meta_data" })
        {
            if (!user.TryGetProperty(key, out var meta) || meta.ValueKind != JsonValueKind.Object)
                continue;
            var name = AccountNames.Normalize(StringOf(meta, "username"));
            if (name != null)
                return name;
        }

        return null;
    }

    static JsonElement? FirstObject(JsonElement root)
    {
        if (root.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in root.EnumerateArray())
            {
                if (item.ValueKind == JsonValueKind.Object)
                    return item;
            }

            return null;
        }

        return root.ValueKind == JsonValueKind.Object ? root : null;
    }

    static bool TryStamp(JsonElement obj, string name, out DateTimeOffset at)
    {
        at = default;
        var text = StringOf(obj, name);
        return text != null && DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out at);
    }

    static string? StringOf(JsonElement obj, string name)
    {
        if (obj.ValueKind != JsonValueKind.Object || !obj.TryGetProperty(name, out var value))
            return null;
        return value.ValueKind == JsonValueKind.String ? value.GetString() : null;
    }
}

public static class AccountEnvelope
{
    public static string Pack(string settingsJson, string watchlistJson)
    {
        using var settings = JsonDocument.Parse(settingsJson);
        using var watchlist = JsonDocument.Parse(watchlistJson);
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            writer.WritePropertyName("settings");
            settings.RootElement.WriteTo(writer);
            writer.WritePropertyName("watchlist");
            watchlist.RootElement.WriteTo(writer);
            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }

    public static bool TryUnpack(string dataJson, out string settingsJson, out string watchlistJson)
    {
        settingsJson = "";
        watchlistJson = "";
        try
        {
            using var doc = JsonDocument.Parse(dataJson);
            if (!doc.RootElement.TryGetProperty("settings", out var settings) || settings.ValueKind != JsonValueKind.Object)
                return false;
            if (!doc.RootElement.TryGetProperty("watchlist", out var watchlist) || watchlist.ValueKind != JsonValueKind.Object)
                return false;
            settingsJson = settings.GetRawText();
            watchlistJson = watchlist.GetRawText();
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }
}

public static class AccountSync
{
    public static SyncDecision Choose(DateTimeOffset? localUpdatedAt, DateTimeOffset? remoteUpdatedAt, bool localUntouched)
    {
        if (remoteUpdatedAt == null)
            return new SyncDecision(SyncAction.Push, localUpdatedAt ?? DateTimeOffset.UtcNow);
        if (localUntouched)
            return new SyncDecision(SyncAction.Pull, remoteUpdatedAt.Value);
        if (localUpdatedAt == null || remoteUpdatedAt > localUpdatedAt)
            return new SyncDecision(SyncAction.Pull, remoteUpdatedAt.Value);
        if (localUpdatedAt > remoteUpdatedAt)
            return new SyncDecision(SyncAction.Push, localUpdatedAt.Value);
        return new SyncDecision(SyncAction.Keep, localUpdatedAt.Value);
    }

    public static bool IsUntouched(AppSettings settings, IReadOnlyList<SymbolEntry> symbols)
    {
        if (settings.Clamped() != AppSettings.Default.Clamped())
            return false;
        var defaults = DefaultSymbols.Load();
        if (symbols.Count != defaults.Count)
            return false;
        for (var i = 0; i < symbols.Count; i++)
        {
            var left = symbols[i];
            var right = defaults[i];
            if (!string.Equals(left.Symbol, right.Symbol, StringComparison.OrdinalIgnoreCase)
                || !string.Equals(left.Label ?? "", right.Label ?? "", StringComparison.Ordinal)
                || left.Kind != right.Kind
                || !string.Equals(left.Currency, right.Currency, StringComparison.OrdinalIgnoreCase))
                return false;
        }

        return true;
    }

    public static DateTimeOffset SeedStamp(string settingsPath, string symbolsPath)
    {
        DateTimeOffset? best = null;
        foreach (var path in new[] { settingsPath, symbolsPath })
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                continue;
            var stamp = new DateTimeOffset(File.GetLastWriteTimeUtc(path), TimeSpan.Zero);
            if (best == null || stamp > best)
                best = stamp;
        }

        return best ?? DateTimeOffset.UtcNow;
    }
}

public sealed record SyncClock(DateTimeOffset UpdatedAt, bool Dirty)
{
    public SyncClock Touch(DateTimeOffset at) => new(at, true);

    public static SyncClock LoadOrSeed(string path, DateTimeOffset seeded)
    {
        if (File.Exists(path))
        {
            try
            {
                var clock = JsonSerializer.Deserialize<SyncClock>(File.ReadAllText(path), JsonFile.Options);
                if (clock != null && clock.UpdatedAt > DateTimeOffset.UnixEpoch)
                    return clock;
            }
            catch (Exception ex) when (ex is JsonException or IOException)
            {
                // A broken clock is seeded again below.
            }
        }

        var fresh = new SyncClock(seeded, false);
        Save(path, fresh);
        return fresh;
    }

    public static void Save(string path, SyncClock clock)
        => JsonFile.AtomicWrite(path, JsonSerializer.Serialize(clock, JsonFile.Options) + "\n");
}
