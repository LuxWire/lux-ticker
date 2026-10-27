using System.Net.Http.Headers;
using System.Text;

namespace LuxTicker;

/// <summary>
/// Offline-first account calls. Local settings save immediately. Sync runs in the background
/// and keeps the signed-in session when the network is down. A rejected refresh signs out.
/// The publishable key is set per request so the shared quote client stays unchanged.
/// </summary>
sealed class LuxAccountService : ILuxAccount
{
    readonly HttpClient _http;
    readonly object _gate = new();
    readonly SemaphoreSlim _syncGate = new(1, 1);
    AccountSession? _session;
    string _status = LuxAccount.SignedOutStatus;
    Func<AccountPayload>? _capture;
    Func<string, string, bool>? _apply;
    CancellationToken _cancel;
    int _started;
    int _syncQueued;

    public LuxAccountService(HttpClient http)
    {
        _http = http;
        try
        {
            _session = AccountVault.TryLoad();
            if (_session != null)
                _status = "Signed in. Settings and the watchlist sync to this account.";
        }
        catch (Exception ex)
        {
            Log.Error("account vault " + ex.Message);
            _session = null;
        }
    }

    public event EventHandler? Changed;

    public AccountSnapshot Current
    {
        get
        {
            lock (_gate)
            {
                return _session == null
                    ? AccountSnapshot.SignedOut(_status)
                    : AccountSnapshot.From(_session, _status);
            }
        }
    }

    public void Attach(Func<AccountPayload> capture, Func<string, string, bool> apply)
    {
        _capture = capture;
        _apply = apply;
    }

    public void Start(CancellationToken cancel)
    {
        _cancel = cancel;
        if (Interlocked.Exchange(ref _started, 1) == 1)
            return;
        QueueSync();
        _ = Task.Run(async () =>
        {
            try
            {
                while (!cancel.IsCancellationRequested)
                {
                    await Task.Delay(TimeSpan.FromMinutes(2), cancel);
                    QueueSync();
                }
            }
            catch (OperationCanceledException)
            {
                // Shutting down.
            }
        }, cancel);
    }

    public void NoteLocalChange()
    {
        try
        {
            var clock = SyncClock.LoadOrSeed(AppPaths.SyncStatePath(), AccountSync.SeedStamp(AppPaths.SettingsPath(), AppPaths.SymbolsPath()));
            SyncClock.Save(AppPaths.SyncStatePath(), clock.Touch(DateTimeOffset.UtcNow));
        }
        catch (Exception ex)
        {
            Log.Error("account clock " + ex.Message);
        }

        QueueSync();
    }

    public async Task<AccountOutcome> CreateAccount(string email, string password, string? username, CancellationToken cancel = default)
    {
        var rejected = AccountInput.RejectSignup(email, password, username);
        if (rejected != null)
            return Fail(rejected);
        if (!AccountReady())
            return NeedsConfig();
        var address = email.Trim();
        var name = AccountNames.Normalize(username);
        var result = await SendAsync(HttpMethod.Post, SupabaseAuth.SignupUrl(), SupabaseAuth.SignupBody(address, password, name), null, null, cancel);
        if (result.TransportError)
            return Offline();
        var parsed = SupabaseAuth.Parse(result.Body, DateTimeOffset.UtcNow);
        if (!result.Ok)
            return Fail(SupabaseAuth.Friendly(result.Body, "Couldn't create the account."));
        if (parsed.ConfirmationRequired || parsed.Session == null)
            return Ok("Check your email to confirm the account, then sign in.");
        await AdoptAsync(parsed.Session with { Username = parsed.Session.Username ?? name }, cancel);
        return Ok("Account created. Settings and the watchlist sync to this account.");
    }

    public async Task<AccountOutcome> SignIn(string emailOrUsername, string password, CancellationToken cancel = default)
    {
        var rejected = AccountInput.RejectSignIn(emailOrUsername, password);
        if (rejected != null)
            return Fail(rejected);
        if (!AccountReady())
            return NeedsConfig();
        var id = emailOrUsername.Trim();
        var email = id;
        string? lookedUp = null;
        if (!id.Contains('@'))
        {
            lookedUp = id;
            var lookup = await SendAsync(HttpMethod.Post, SupabaseAuth.EmailForUsernameUrl(), SupabaseAuth.UsernameBody(id), null, null, cancel);
            if (lookup.TransportError)
                return Offline();
            if (!lookup.Ok)
                return Fail("Couldn't look up that username.");
            email = SupabaseAuth.ParseEmail(lookup.Body) ?? "";
            if (email.Length == 0)
                return Fail("No account uses that username.");
        }

        var result = await SendAsync(HttpMethod.Post, SupabaseAuth.PasswordTokenUrl(), SupabaseAuth.PasswordBody(email, password), null, null, cancel);
        if (result.TransportError)
            return Offline();
        var parsed = SupabaseAuth.Parse(result.Body, DateTimeOffset.UtcNow);
        if (!result.Ok || parsed.Session == null)
            return Fail(SupabaseAuth.Friendly(result.Body, parsed.Error ?? "Sign-in did not finish."));
        await AdoptAsync(parsed.Session with { Username = parsed.Session.Username ?? lookedUp, Email = parsed.Session.Email ?? email }, cancel);
        return Ok("Signed in. Settings and the watchlist sync to this account.");
    }

    public async Task<AccountOutcome> SendReset(string emailOrUsername, CancellationToken cancel = default)
    {
        var id = emailOrUsername?.Trim() ?? "";
        if (id.Length == 0)
            return Fail("Enter the email or username on the account.");
        if (!AccountReady())
            return NeedsConfig();
        var email = id;
        if (!id.Contains('@'))
        {
            if (!AccountNames.IsUsername(id))
                return Fail("That username is not valid.");
            var lookup = await SendAsync(HttpMethod.Post, SupabaseAuth.EmailForUsernameUrl(), SupabaseAuth.UsernameBody(id), null, null, cancel);
            if (lookup.TransportError)
                return Offline();
            if (!lookup.Ok)
                return Fail("Couldn't look up that username.");
            email = SupabaseAuth.ParseEmail(lookup.Body) ?? "";
            if (email.Length == 0)
                return Fail("No account uses that username.");
        }
        else if (!AccountNames.IsEmail(email))
        {
            return Fail("Use an email address to create an account.");
        }

        var result = await SendAsync(HttpMethod.Post, SupabaseAuth.RecoverUrl(), SupabaseAuth.RecoverBody(email), null, null, cancel);
        if (result.TransportError)
            return Offline();
        if (!result.Ok)
            return Fail(SupabaseAuth.Friendly(result.Body, "Couldn't send the reset email."));
        return Ok("Password reset email sent. Check your inbox.");
    }

    public async Task<AccountOutcome> SetUsername(string? username, CancellationToken cancel = default)
    {
        var name = AccountNames.Normalize(username);
        if (!AccountNames.IsOptionalUsername(name))
            return Fail("A username is 3 to 24 letters, digits, or underscores.");
        var session = await EnsureFreshAsync(CurrentSession(), cancel);
        if (session == null)
            return Fail("Sign in first.");
        if (!AccountReady())
            return NeedsConfig();
        var result = await SendAsync(HttpMethod.Patch, SupabaseAuth.ProfileUrl(session.UserId), SupabaseAuth.ProfileBody(name), session.AccessToken, "return=representation", cancel);
        if (result.TransportError)
            return Offline();
        if (!result.Ok)
            return Fail(SupabaseAuth.Friendly(result.Body, "Couldn't save that username."));
        var saved = SupabaseAuth.ParseProfileUsername(result.Body) ?? name;
        Store(session with { Username = saved });
        return Ok(saved == null ? "Username cleared." : "Username saved.");
    }

    public async Task<AccountOutcome> SignOut(CancellationToken cancel = default)
    {
        var session = CurrentSession();
        if (session != null)
        {
            try
            {
                await SendAsync(HttpMethod.Post, SupabaseAuth.LogoutUrl(), "{}", session.AccessToken, null, cancel);
            }
            catch (Exception ex)
            {
                Log.Error("account sign out " + ex.Message);
            }
        }

        ClearSession("Signed out on this PC. The ticker keeps working.");
        return Ok(_status);
    }

    public async Task<AccountOutcome> DeleteAccount(CancellationToken cancel = default)
    {
        var session = await EnsureFreshAsync(CurrentSession(), cancel);
        if (session == null)
            return Fail("Sign in first.");
        if (!AccountReady())
            return NeedsConfig();
        var result = await SendAsync(HttpMethod.Post, SupabaseAuth.DeleteAccountUrl(), "{}", session.AccessToken, null, cancel);
        if (result.TransportError)
            return Fail("Couldn't delete the account. Try again when you are online. The ticker on this PC is unchanged.");
        if (!result.Ok)
            return Fail(SupabaseAuth.Friendly(result.Body, "Couldn't delete the account. The ticker on this PC is unchanged."));
        ClearSession("Account deleted. The ticker on this PC is unchanged.");
        return Ok(_status);
    }

    public async Task<AccountOutcome> ContinueWith(string provider, CancellationToken cancel = default)
    {
        if (!LuxAccount.OAuthEnabled)
            return Fail("That sign-in is not turned on yet.");
        if (!AccountReady())
            return NeedsConfig();
        var oauth = await SupabaseOAuth.RunAsync(provider, cancel);
        if (oauth.Error != null || oauth.Code == null || oauth.Verifier == null)
            return Fail(oauth.Error ?? "Sign-in did not finish.");
        var result = await SendAsync(HttpMethod.Post, SupabaseAuth.PkceTokenUrl(), SupabaseAuth.PkceBody(oauth.Code, oauth.Verifier), null, null, cancel);
        if (result.TransportError)
            return Offline();
        var parsed = SupabaseAuth.Parse(result.Body, DateTimeOffset.UtcNow);
        if (!result.Ok || parsed.Session == null)
            return Fail(SupabaseAuth.Friendly(result.Body, parsed.Error ?? "Sign-in did not finish."));
        await AdoptAsync(parsed.Session, cancel);
        return Ok("Signed in. Settings and the watchlist sync to this account.");
    }

    async Task AdoptAsync(AccountSession session, CancellationToken cancel)
    {
        Store(session);
        session = await RefreshProfileAsync(session, cancel);
        QueueSync();
    }

    async Task<AccountSession> RefreshProfileAsync(AccountSession session, CancellationToken cancel)
    {
        var profile = await SendAsync(HttpMethod.Get, SupabaseAuth.ProfileUrl(session.UserId) + "&select=username", null, session.AccessToken, null, cancel);
        if (profile.Ok)
        {
            var name = SupabaseAuth.ParseProfileUsername(profile.Body);
            if (name != null)
                session = session with { Username = name };
        }

        var tier = await SendAsync(HttpMethod.Get, SupabaseAuth.EntitlementsUrl(session.UserId), null, session.AccessToken, null, cancel);
        if (tier.Ok && SupabaseAuth.TryParseTier(tier.Body, out var parsed))
            session = session with { Tier = AccountTierRules.Wire(parsed) };
        else if (tier.Ok)
            session = session with { Tier = AccountTierRules.Wire(AccountTier.SignedIn) };
        Store(session);
        return session;
    }

    void QueueSync()
    {
        if (!AccountReady())
            return;
        if (Interlocked.Exchange(ref _syncQueued, 1) == 1)
            return;
        _ = Task.Run(RunSyncAsync);
    }

    async Task RunSyncAsync()
    {
        var held = false;
        try
        {
            await _syncGate.WaitAsync(_cancel);
            held = true;
            do
            {
                Interlocked.Exchange(ref _syncQueued, 0);
                var ok = await SyncBodyAsync();
                if (!ok)
                    break;
            }
            while (Volatile.Read(ref _syncQueued) == 1 || ClockDirty());
        }
        catch (OperationCanceledException)
        {
            // Shutting down.
        }
        catch (Exception ex)
        {
            Log.Error("account sync " + ex.Message);
        }
        finally
        {
            if (held)
                _syncGate.Release();
        }
    }

    bool ClockDirty()
    {
        try
        {
            var path = AppPaths.SyncStatePath();
            if (!File.Exists(path))
                return false;
            return SyncClock.LoadOrSeed(path, DateTimeOffset.UtcNow).Dirty;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    async Task<bool> SyncBodyAsync()
    {
        var session = CurrentSession();
        if (session == null)
            return true;
        session = await EnsureFreshAsync(session, _cancel);
        if (session == null)
            return true;

        var tierCall = await SendAsync(HttpMethod.Get, SupabaseAuth.EntitlementsUrl(session.UserId), null, session.AccessToken, null, _cancel);
        if (tierCall.TransportError)
        {
            SetStatus("Saved on this PC. Sync will retry when the account service is reachable.");
            return false;
        }

        if (tierCall.Status == 401)
        {
            ClearSession("Sign in again.");
            return true;
        }

        if (tierCall.Ok)
        {
            var tier = SupabaseAuth.TryParseTier(tierCall.Body, out var parsed) ? parsed : AccountTier.SignedIn;
            session = session with { Tier = AccountTierRules.Wire(tier) };
            Store(session);
        }

        if (!AccountTierRules.CanSync(session.TierValue))
        {
            SetStatus("Sync is off on the free tier.");
            return true;
        }

        if (_capture == null || _apply == null)
            return true;

        AccountPayload payload;
        try
        {
            payload = _capture();
        }
        catch (Exception ex)
        {
            Log.Error("account capture " + ex.Message);
            return false;
        }

        var remoteCall = await SendAsync(HttpMethod.Get, SupabaseAuth.AppSettingsSelectUrl(session.UserId), null, session.AccessToken, null, _cancel);
        if (remoteCall.TransportError)
        {
            SetStatus("Saved on this PC. Sync will retry when the account service is reachable.");
            return false;
        }

        if (remoteCall.Status == 401)
        {
            ClearSession("Sign in again.");
            return true;
        }

        if (!remoteCall.Ok)
        {
            SetStatus("Saved on this PC. Sync will retry when the account service is reachable.");
            return false;
        }

        var remote = SupabaseAuth.ParseRemote(remoteCall.Body);
        var decision = AccountSync.Choose(payload.UpdatedAt, remote?.UpdatedAt, payload.Untouched);
        if (decision.Action == SyncAction.Pull)
        {
            if (remote == null)
                return true;
            bool applied;
            try
            {
                applied = _apply(remote.SettingsJson, remote.WatchlistJson);
            }
            catch (Exception ex)
            {
                Log.Error("account apply " + ex.Message);
                applied = false;
            }

            if (!applied)
            {
                SetStatus("Saved on this PC. The account copy could not be read.");
                return true;
            }

            SyncClock.Save(AppPaths.SyncStatePath(), new SyncClock(remote.UpdatedAt, false));
            SetStatus("Settings and the watchlist are synced.");
            return true;
        }

        if (decision.Action == SyncAction.Keep)
        {
            SyncClock.Save(AppPaths.SyncStatePath(), new SyncClock(decision.At, false));
            SetStatus("Settings and the watchlist are synced.");
            return true;
        }

        string data;
        try
        {
            data = AccountEnvelope.Pack(payload.SettingsJson, payload.WatchlistJson);
        }
        catch (Exception ex)
        {
            Log.Error("account pack " + ex.Message);
            SetStatus("Saved on this PC. The account copy could not be read.");
            return true;
        }

        var pushed = await SendAsync(
            HttpMethod.Post,
            SupabaseAuth.AppSettingsUpsertUrl(),
            SupabaseAuth.UpsertBody(session.UserId, data, decision.At),
            session.AccessToken,
            SupabaseAuth.UpsertPrefer,
            _cancel);
        if (pushed.TransportError || !pushed.Ok)
        {
            SetStatus("Saved on this PC. Sync will retry when the account service is reachable.");
            return false;
        }

        var echoed = SupabaseAuth.ParseRemote(pushed.Body);
        SyncClock.Save(AppPaths.SyncStatePath(), new SyncClock(echoed?.UpdatedAt ?? decision.At, false));
        SetStatus("Settings and the watchlist are synced.");
        return true;
    }

    async Task<AccountSession?> EnsureFreshAsync(AccountSession? session, CancellationToken cancel)
    {
        if (session == null)
            return null;
        if (session.ExpiresAt > DateTimeOffset.UtcNow.AddSeconds(60) || string.IsNullOrEmpty(session.RefreshToken))
            return session;
        var result = await SendAsync(HttpMethod.Post, SupabaseAuth.RefreshUrl(), SupabaseAuth.RefreshBody(session.RefreshToken), session.AccessToken, null, cancel);
        if (result.TransportError)
            return session;
        if (result.Status is 400 or 401)
        {
            ClearSession("Sign in again.");
            return null;
        }

        if (!result.Ok)
            return session;
        var parsed = SupabaseAuth.Parse(result.Body, DateTimeOffset.UtcNow);
        if (parsed.Session == null)
            return session;
        var next = parsed.Session with
        {
            Email = parsed.Session.Email ?? session.Email,
            Username = parsed.Session.Username ?? session.Username,
            Tier = session.Tier
        };
        Store(next);
        return next;
    }

    AccountSession? CurrentSession()
    {
        lock (_gate)
            return _session;
    }

    void Store(AccountSession session)
    {
        try
        {
            AccountVault.Save(session);
        }
        catch (Exception ex)
        {
            Log.Error("account vault save " + ex.Message);
        }

        lock (_gate)
        {
            _session = session;
            if (!KeepStatus(_status))
                _status = "Signed in. Settings and the watchlist sync to this account.";
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }

    static bool KeepStatus(string status)
        => status.StartsWith("Saved on this PC", StringComparison.Ordinal)
           || status.StartsWith("Settings and the watchlist are synced", StringComparison.Ordinal)
           || status.StartsWith("Sync is off", StringComparison.Ordinal)
           || status.StartsWith("Username", StringComparison.Ordinal)
           || status.StartsWith("Check your email", StringComparison.Ordinal)
           || status.StartsWith("Password reset", StringComparison.Ordinal)
           || status.StartsWith("Account created", StringComparison.Ordinal)
           || status.StartsWith("Account deleted", StringComparison.Ordinal)
           || status.StartsWith("Signed out", StringComparison.Ordinal);

    void ClearSession(string status)
    {
        AccountVault.Clear();
        lock (_gate)
        {
            _session = null;
            _status = status;
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }

    void SetStatus(string status)
    {
        lock (_gate)
            _status = status;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    AccountOutcome Ok(string message)
    {
        lock (_gate)
            _status = message;
        Changed?.Invoke(this, EventArgs.Empty);
        return AccountOutcome.Done(message);
    }

    AccountOutcome Fail(string message)
    {
        lock (_gate)
            _status = message;
        Changed?.Invoke(this, EventArgs.Empty);
        return AccountOutcome.Failed(message);
    }

    AccountOutcome Offline()
        => Fail("Couldn't reach the account service. The ticker still works on this PC.");

    static bool AccountReady()
        => LuxWireConfig.IsAccountConfigured;

    AccountOutcome NeedsConfig()
        => Fail("Copy luxwire.config.example.json to luxwire.config.json and set supabaseUrl and supabaseAnonKey.");

    async Task<ApiResult> SendAsync(HttpMethod method, string url, string? json, string? bearer, string? prefer, CancellationToken cancel)
    {
        if (!AccountReady())
            return new ApiResult(0, "", true);
        try
        {
            using var request = new HttpRequestMessage(method, url);
            request.Headers.TryAddWithoutValidation("apikey", LuxAccount.PublishableKey);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", string.IsNullOrEmpty(bearer) ? LuxAccount.PublishableKey : bearer);
            request.Headers.Accept.ParseAdd("application/json");
            if (!string.IsNullOrEmpty(prefer))
                request.Headers.TryAddWithoutValidation("Prefer", prefer);
            if (json != null)
                request.Content = new StringContent(json, Encoding.UTF8, "application/json");
            using var response = await _http.SendAsync(request, cancel);
            var body = await response.Content.ReadAsStringAsync(cancel);
            if (!response.IsSuccessStatusCode)
                Log.Error("account http " + (int)response.StatusCode);
            return new ApiResult((int)response.StatusCode, body, false);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            if (cancel.IsCancellationRequested)
                throw;
            Log.Error("account http " + ex.Message);
            return new ApiResult(0, "", true);
        }
    }

    readonly record struct ApiResult(int Status, string Body, bool TransportError)
    {
        public bool Ok => Status is >= 200 and < 300;
    }
}
