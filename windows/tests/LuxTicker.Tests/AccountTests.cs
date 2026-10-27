namespace LuxTicker.Tests;

public class AccountTests
{
    static readonly DateTimeOffset Now = new(2026, 10, 26, 16, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Sign_in_is_optional_and_oauth_uses_the_localhost_callback()
    {
        LuxWireConfig.Use("https://example.supabase.co", "example-anon-key", "");
        Assert.True(LuxAccount.OAuthEnabled);
        Assert.Equal("https://example.supabase.co", LuxAccount.ProjectUrl);
        Assert.Equal("example-anon-key", LuxAccount.PublishableKey);
        Assert.Equal("lux-ticker-windows", LuxAccount.AppKey);
        Assert.Equal(47322, LuxAccount.LoopbackPort);
        Assert.Equal("http://localhost:47322/auth-callback", LuxAccount.RedirectUri);
        Assert.Equal("https://luxwire.github.io/privacy.html", LuxAccount.PrivacyUrl);
        Assert.Equal("https://luxwire.github.io/terms.html", LuxAccount.TermsUrl);
        Assert.NotEqual(LuxWireXFeed.LoopbackPort, LuxAccount.LoopbackPort);
        Assert.False(AccountTierRules.CanSync(AccountTier.Free));
        Assert.True(AccountTierRules.CanSync(AccountTier.SignedIn));
        Assert.True(AccountTierRules.CanSync(AccountTier.Pro));
        Assert.Equal(AccountTier.SignedIn, AccountTierRules.Parse(null));
        Assert.Equal(AccountTier.SignedIn, AccountTierRules.Parse("nope"));
        Assert.Equal("free", AccountTierRules.Wire(AccountTier.Free));
    }

    [Fact]
    public void Username_rules_and_signup_rejects_a_missing_email()
    {
        Assert.True(AccountNames.IsOptionalUsername(null));
        Assert.True(AccountNames.IsOptionalUsername("  "));
        Assert.True(AccountNames.IsUsername("Ada_1"));
        Assert.False(AccountNames.IsUsername("ab"));
        Assert.False(AccountNames.IsUsername("has space"));
        Assert.False(AccountNames.IsUsername("way-too-long-name-over-24"));
        Assert.Equal("Use an email address to create an account.", AccountInput.RejectSignup("ada", "longpassword", "Ada"));
        Assert.Equal("Use at least 8 characters.", AccountInput.RejectSignup("ada@example.com", "short", null));
        Assert.Equal("A username is 3 to 24 letters, digits, or underscores.", AccountInput.RejectSignup("ada@example.com", "longpassword", "no"));
        Assert.Null(AccountInput.RejectSignup("ada@example.com", "longpassword", null));
        Assert.Equal("That username is not valid.", AccountInput.RejectSignIn("no", "longpassword"));
        Assert.Null(AccountInput.RejectSignIn("Ada_1", "longpassword"));
        Assert.Null(AccountInput.RejectSignIn("ada@example.com", "longpassword"));
    }

    [Fact]
    public void Auth_urls_and_bodies_use_the_publishable_project()
    {
        LuxWireConfig.Use("https://example.supabase.co", "example-anon-key", "");
        Assert.Equal(LuxAccount.ProjectUrl + "/auth/v1/signup", SupabaseAuth.SignupUrl());
        Assert.Contains("grant_type=password", SupabaseAuth.PasswordTokenUrl());
        Assert.Contains("grant_type=pkce", SupabaseAuth.PkceTokenUrl());
        Assert.Contains("/rest/v1/rpc/email_for_username", SupabaseAuth.EmailForUsernameUrl());
        Assert.Contains("/rest/v1/rpc/delete_own_account", SupabaseAuth.DeleteAccountUrl());
        Assert.Contains("lux-ticker-windows", SupabaseAuth.AppSettingsSelectUrl("user-1"));
        var signup = SupabaseAuth.SignupBody("ada@example.com", "longpassword", "Ada_1");
        Assert.Contains("ada@example.com", signup);
        Assert.Contains("Ada_1", signup);
        Assert.Contains("\"data\"", signup);
        Assert.DoesNotContain("service_role", signup);
        var lookup = SupabaseAuth.UsernameBody("Ada_1");
        Assert.Contains("p_username", lookup);
        var x = SupabaseAuth.AuthorizeUrl("x", LuxAccount.RedirectUri, "state", "challenge");
        Assert.Contains("provider=x", x);
        Assert.DoesNotContain("provider=twitter", x);
        Assert.Contains("47322", x);
        Assert.Contains("localhost", x);
        Assert.Contains("auth-callback", x);
        Assert.Contains("code_challenge_method=s256", x);
        Assert.Contains(LuxAccount.ProjectUrl, x);
        Assert.Contains("provider=google", SupabaseAuth.AuthorizeUrl("google", LuxAccount.RedirectUri, "state", "challenge"));
        Assert.Equal("x", SupabaseAuth.ProviderSlug("X"));
        Assert.Equal("x", SupabaseAuth.ProviderSlug("twitter"));
        Assert.Null(SupabaseAuth.ProviderSlug("apple"));
    }

    [Fact]
    public void Session_parse_accepts_a_token_and_a_confirmation_with_none()
    {
        var authed = """
            {
              "access_token": "access",
              "refresh_token": "refresh",
              "expires_in": 3600,
              "user": { "id": "user-1", "email": "ada@example.com", "user_metadata": { "username": "Ada_1" } }
            }
            """;
        var session = SupabaseAuth.Parse(authed, Now);
        Assert.NotNull(session.Session);
        Assert.False(session.ConfirmationRequired);
        Assert.Equal("user-1", session.Session!.UserId);
        Assert.Equal("ada@example.com", session.Session.Email);
        Assert.Equal("Ada_1", session.Session.Username);
        Assert.Equal(Now.AddHours(1), session.Session.ExpiresAt);

        var nested = """
            {
              "user": { "id": "user-2", "email": "bea@example.com" },
              "session": { "access_token": "access-2", "refresh_token": "refresh-2", "expires_in": 60 }
            }
            """;
        var nestedSession = SupabaseAuth.Parse(nested, Now);
        Assert.Equal("access-2", nestedSession.Session!.AccessToken);
        Assert.Equal("user-2", nestedSession.Session.UserId);

        var confirm = """
            { "id": "user-3", "email": "cy@example.com", "user_metadata": { "username": "Cy" } }
            """;
        var pending = SupabaseAuth.Parse(confirm, Now);
        Assert.Null(pending.Session);
        Assert.True(pending.ConfirmationRequired);
        Assert.Equal("cy@example.com", pending.Email);
        Assert.Equal("Cy", pending.Username);

        var bad = """{ "error": "invalid_grant", "error_description": "Invalid login credentials" }""";
        var failed = SupabaseAuth.Parse(bad, Now);
        Assert.Null(failed.Session);
        Assert.False(failed.ConfirmationRequired);
        Assert.Equal("That email and password did not match.", failed.Error);
        Assert.Equal("ada@example.com", SupabaseAuth.ParseEmail("\"ada@example.com\""));
        Assert.Null(SupabaseAuth.ParseEmail("null"));
        Assert.False(SupabaseAuth.TryParseTier("[]", out _));
        Assert.True(SupabaseAuth.TryParseTier("""[{"tier":"pro"}]""", out var tier));
        Assert.Equal(AccountTier.Pro, tier);
        Assert.Equal("That email already has an account. Sign in instead.", SupabaseAuth.Friendly("""{"msg":"User already registered"}""", "nope"));
        Assert.Equal("Confirm the email, then sign in.", SupabaseAuth.Friendly("""{"msg":"Email not confirmed"}""", "nope"));
    }

    [Fact]
    public void Sync_pulls_an_untouched_ticker_and_otherwise_uses_last_write()
    {
        var older = Now.AddMinutes(-10);
        var newer = Now;
        Assert.Equal(SyncAction.Push, AccountSync.Choose(older, null, localUntouched: false).Action);
        Assert.Equal(SyncAction.Pull, AccountSync.Choose(newer, older, localUntouched: true).Action);
        Assert.Equal(SyncAction.Push, AccountSync.Choose(newer, older, localUntouched: false).Action);
        Assert.Equal(SyncAction.Pull, AccountSync.Choose(older, newer, localUntouched: false).Action);
        Assert.Equal(SyncAction.Keep, AccountSync.Choose(older, older, localUntouched: false).Action);
        Assert.Equal(SyncAction.Pull, AccountSync.Choose(null, newer, localUntouched: false).Action);

        Assert.True(AccountSync.IsUntouched(AppSettings.Default, DefaultSymbols.Load()));
        Assert.False(AccountSync.IsUntouched(AppSettings.Default with { TextColor = "#112233" }, DefaultSymbols.Load()));

        var settings = SettingsStore.Serialize(AppSettings.Default with { TextColor = "#112233" });
        var watch = SymbolListStore.Serialize(DefaultSymbols.Load());
        var packed = AccountEnvelope.Pack(settings, watch);
        Assert.True(AccountEnvelope.TryUnpack(packed, out var settingsOut, out var watchOut));
        Assert.True(SettingsStore.TryParse(settingsOut, out var loaded));
        Assert.Equal("#112233", loaded.TextColor);
        Assert.True(SymbolListStore.TryParse(watchOut, out var symbols, out _));
        Assert.Equal(DefaultSymbols.Load().Count, symbols.Count);

        var remoteJson = """
            [{"data":{"settings":{"textColor":"#112233"},"watchlist":{"symbols":[]}},"updated_at":"2026-10-26T16:00:00+00:00"}]
            """;
        var remote = SupabaseAuth.ParseRemote(remoteJson);
        Assert.NotNull(remote);
        Assert.Equal(Now, remote!.UpdatedAt);
        Assert.Contains("#112233", remote.SettingsJson);
        var body = SupabaseAuth.UpsertBody("user-1", packed, Now);
        Assert.Contains("lux-ticker-windows", body);
        Assert.Contains("user-1", body);
        Assert.DoesNotContain("service_role", body);
        Assert.Null(SupabaseAuth.ParseRemote("[]"));
    }

    [Fact]
    public void Sync_clock_seeds_clean_and_an_edit_marks_it_dirty()
    {
        var dir = Path.Combine(Path.GetTempPath(), "lux-account-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var path = Path.Combine(dir, "sync-state.json");
            var seeded = AccountSync.SeedStamp(Path.Combine(dir, "missing-settings.json"), Path.Combine(dir, "missing-symbols.json"));
            var clock = SyncClock.LoadOrSeed(path, new DateTimeOffset(2026, 10, 20, 0, 0, 0, TimeSpan.Zero));
            Assert.False(clock.Dirty);
            Assert.Equal(new DateTimeOffset(2026, 10, 20, 0, 0, 0, TimeSpan.Zero), clock.UpdatedAt);
            Assert.True(seeded <= DateTimeOffset.UtcNow.AddMinutes(1));
            var touched = clock.Touch(Now);
            SyncClock.Save(path, touched);
            var again = SyncClock.LoadOrSeed(path, DateTimeOffset.UnixEpoch);
            Assert.True(again.Dirty);
            Assert.Equal(Now, again.UpdatedAt);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void Config_is_a_placeholder_and_source_has_no_project_secrets()
    {
        var root = Repo.Root();
        var example = File.ReadAllText(Path.Combine(root, "luxwire.config.example.json"));
        Assert.Contains("https://YOUR_PROJECT.supabase.co", example);
        Assert.Contains("YOUR_ANON_OR_PUBLISHABLE_KEY", example);
        Assert.Contains("\"updateFeedUrl\": \"\"", example);
        var ignore = File.ReadAllText(Path.Combine(root, ".gitignore"));
        Assert.Contains("luxwire.config.json", ignore);
        Assert.Contains("*.pfx", ignore);
        Assert.Contains("*.p12", ignore);
        Assert.Contains("*.pem", ignore);
        Assert.Contains("*.key", ignore);
        Assert.Contains(".env", ignore);
        Assert.False(Directory.Exists(Path.Combine(root, "supabase")));
        Assert.False(File.Exists(Path.Combine(root, "luxwire.config.json")));
        Assert.Contains("MIT License", File.ReadAllText(Path.Combine(root, "LICENSE")));

        foreach (var file in Directory.GetFiles(Path.Combine(root, "src"), "*.*", SearchOption.AllDirectories))
        {
            if (!file.EndsWith(".cs") && !file.EndsWith(".xaml") && !file.EndsWith(".json"))
                continue;
            var text = File.ReadAllText(file);
            Assert.DoesNotContain("sb_publishable", text);
            Assert.DoesNotContain(".supabase.co", text);
            Assert.DoesNotContain("service_role", text);
            Assert.DoesNotContain("drive.google.com/uc", text);
        }

        var dir = Directory.CreateTempSubdirectory("luxwire-config-");
        var path = Path.Combine(dir.FullName, "mine.json");
        File.WriteAllText(path, """
            {
              "supabaseUrl": "https://example.supabase.co/",
              "supabaseAnonKey": "example-anon-key",
              "updateFeedUrl": "https://example.com/lux-ticker-windows-latest.txt"
            }
            """);
        var previous = Environment.GetEnvironmentVariable(LuxWireConfig.EnvVariable);
        try
        {
            Environment.SetEnvironmentVariable(LuxWireConfig.EnvVariable, path);
            LuxWireConfig.Load();
            Assert.Equal("https://example.supabase.co", LuxAccount.ProjectUrl);
            Assert.Equal("example-anon-key", LuxAccount.PublishableKey);
            Assert.Equal("https://example.com/lux-ticker-windows-latest.txt", UpdateCheck.ManifestUrl);
            Assert.True(LuxWireConfig.IsAccountConfigured);
        }
        finally
        {
            Environment.SetEnvironmentVariable(LuxWireConfig.EnvVariable, previous);
            LuxWireConfig.Use("https://example.supabase.co", "example-anon-key", "");
            dir.Delete(recursive: true);
        }
    }

    [Fact]
    public void Account_page_wires_oauth_and_the_session_vault()
    {
        var root = Repo.Root();
        var xaml = File.ReadAllText(Path.Combine(root, "src", "LuxTicker.App", "SettingsWindow.xaml"));
        Assert.Contains("x:Name=\"PageAccount\"", xaml);
        Assert.Contains("Create account", xaml);
        Assert.Contains("Forgot password", xaml);
        Assert.Contains("Delete account", xaml);
        Assert.Contains("Continue with Google", xaml);
        Assert.Contains("Continue with X", xaml);
        Assert.Contains("Content=\"Privacy\"", xaml);
        Assert.Contains("Content=\"Terms\"", xaml);
        Assert.Contains("Tag=\"About|Privacy Terms\"", xaml);
        Assert.Contains("Tag=\"Account|Privacy Terms\"", xaml);
        Assert.Contains("x:Name=\"OAuthPanel\"", xaml);
        var code = File.ReadAllText(Path.Combine(root, "src", "LuxTicker.App", "SettingsWindow.xaml.cs"));
        Assert.Contains("OAuthPanel.Visibility", code);
        Assert.Contains("LuxAccount.OAuthEnabled", code);
        Assert.Contains("LuxAccount.PrivacyUrl", code);
        Assert.Contains("LuxAccount.TermsUrl", code);
        var vault = File.ReadAllText(Path.Combine(root, "src", "LuxTicker.App", "AccountVault.cs"));
        Assert.Contains("CryptProtectData", vault);
        Assert.Contains("CryptUnprotectData", vault);
        Assert.DoesNotContain("LocalMachine", vault);
        var oauth = File.ReadAllText(Path.Combine(root, "src", "LuxTicker.App", "SupabaseOAuth.cs"));
        Assert.Contains("LuxAccount.LoopbackPort", oauth);
        Assert.Contains("SupabaseAuth.AuthorizeUrl", oauth);
        Assert.Contains("LuxAccount.OAuthEnabled", oauth);
        Assert.Contains("IPAddress.IPv6Loopback", oauth);
        Assert.Contains("/auth-callback", oauth);
        Assert.Contains("UseShellExecute = true", oauth);
        var service = File.ReadAllText(Path.Combine(root, "src", "LuxTicker.App", "LuxAccountService.cs"));
        Assert.Contains("SupabaseAuth.PkceTokenUrl", service);
        Assert.Contains("EmailForUsernameUrl", service);
        Assert.Contains("DeleteAccountUrl", service);
        Assert.Contains("PublishableKey", service);
        Assert.DoesNotContain("service_role", service);
        Assert.Contains("account.bin", File.ReadAllText(Path.Combine(root, "src", "LuxTicker.Core", "AppPaths.cs")));
    }
}
