using System.Net.Http.Headers;

namespace LuxTicker;

/// <summary>
/// Polls the @LuxWire timeline on X while the tray app is running.
/// The first successful poll with no since id marks those posts seen and does not toast.
/// Later polls toast new posts, capped at the newest ten. Alerts off skips the poll.
/// Tokens are read from x-auth.json or the environment and are never written to the log.
/// </summary>
sealed class LuxWireFeed : IDisposable
{
    readonly string _seenPath;
    readonly string _authPath;
    readonly LuxWireNotifier _notifier;
    readonly HttpClient _http;
    readonly object _gate = new();
    readonly List<LuxWirePost> _digest = new();
    readonly CancellationTokenSource _stop = new();
    Timer? _poll;
    Timer? _digestTimer;
    int _minutes;
    int _busy;
    bool _started;
    bool _disposed;

    public LuxWireFeed(string seenPath, string authPath, LuxWireNotifier notifier, bool alertsEnabled, int pollMinutes)
    {
        _seenPath = seenPath;
        _authPath = authPath;
        _notifier = notifier;
        AlertsEnabled = alertsEnabled;
        _minutes = LuxWireXFeed.ClampMinutes(pollMinutes);
        _http = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
        _http.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", "LuxTicker/" + LuxWireEdition.Version);
        _http.DefaultRequestHeaders.Accept.ParseAdd("application/json");
    }

    public bool AlertsEnabled { get; set; }

    public bool DigestEnabled { get; private set; }

    public void SetDigest(bool enabled)
    {
        List<LuxWirePost>? pending = null;
        lock (_gate)
        {
            DigestEnabled = enabled;
            if (!enabled && _digest.Count > 0)
            {
                pending = _digest.ToList();
                _digest.Clear();
                _digestTimer?.Dispose();
                _digestTimer = null;
            }
        }

        if (pending == null)
            return;
        foreach (var post in pending)
            _notifier.Show(post);
    }

    public void SetPollMinutes(int minutes)
    {
        var next = LuxWireXFeed.ClampMinutes(minutes);
        var restart = false;
        lock (_gate)
        {
            if (_minutes == next)
                return;
            _minutes = next;
            restart = _started && !_disposed;
        }

        if (restart)
            ArmTimer();
    }

    public void Start()
    {
        lock (_gate)
        {
            if (_started || _disposed)
                return;
            _started = true;
        }

        try
        {
            var dir = Path.GetDirectoryName(_seenPath);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);
            ArmTimer();
            Log.Info("lux wire x feed every " + _minutes + " min");
        }
        catch (Exception ex)
        {
            Log.Error("lux wire start " + ex.Message);
        }
    }

    public void CatchUp() => _ = PollAsync();

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        try
        {
            _stop.Cancel();
        }
        catch (ObjectDisposedException)
        {
            // Already stopped.
        }

        lock (_gate)
        {
            _poll?.Dispose();
            _poll = null;
            _digestTimer?.Dispose();
            _digestTimer = null;
        }

        _http.Dispose();
        _stop.Dispose();
    }

    void ArmTimer()
    {
        lock (_gate)
        {
            _poll?.Dispose();
            if (_disposed || !_started)
                return;
            var period = TimeSpan.FromMinutes(_minutes);
            _poll = new Timer(_ => { _ = PollAsync(); }, null, TimeSpan.FromSeconds(3), period);
        }
    }

    async Task PollAsync()
    {
        if (_disposed || !AlertsEnabled)
            return;
        if (Interlocked.Exchange(ref _busy, 1) == 1)
            return;

        try
        {
            var cancel = _stop.Token;
            var cursor = LuxWireSeenFile.LoadCursor(_seenPath);
            var stored = XAuthStore.Load(_authPath, includeEnvironment: false);
            var live = await RefreshIfStale(stored, XAuthStore.Load(_authPath), cancel);
            var token = live.PollToken(DateTimeOffset.UtcNow);
            if (string.IsNullOrEmpty(token))
            {
                Log.Info("lux wire waiting for an X token");
                return;
            }

            if (string.IsNullOrWhiteSpace(live.LuxWireUserId))
            {
                var lookup = await Get(LuxWireXFeed.UserLookupUrl(), token, cancel);
                var id = LuxWireXFeed.ParseUserId(lookup);
                if (string.IsNullOrEmpty(id))
                {
                    Log.Error("lux wire user id missing");
                    return;
                }

                stored = XAuthStore.Load(_authPath, includeEnvironment: false) with { LuxWireUserId = id };
                XAuthStore.Save(_authPath, stored);
                live = live with { LuxWireUserId = id };
            }

            if (!AlertsEnabled || _disposed)
                return;

            var baseline = string.IsNullOrEmpty(cursor.SinceId);
            var json = await Get(LuxWireXFeed.TimelineUrl(live.LuxWireUserId!, baseline ? null : cursor.SinceId), token, cancel);
            if (json == null || !AlertsEnabled || _disposed)
                return;

            var timeline = LuxWireXFeed.ParseTimeline(json);
            if (timeline.Posts.Count == 0)
            {
                if (!string.IsNullOrEmpty(timeline.Error))
                    Log.Error("lux wire timeline " + SafeError(timeline.Error));
                return;
            }

            var inbox = new LuxWireInbox(cursor.Seen);
            var notify = inbox.Accept(timeline.Posts, alertsEnabled: true, LuxWireEdition.Subscribed, baseline).ToList();
            if (!AlertsEnabled || _disposed)
                return;

            LuxWireSeenFile.Save(_seenPath, inbox.Seen, timeline.NewestId ?? cursor.SinceId);
            Queue(notify);
        }
        catch (OperationCanceledException)
        {
            // Shutting down.
        }
        catch (Exception ex)
        {
            Log.Error("lux wire poll " + ex.Message);
        }
        finally
        {
            Interlocked.Exchange(ref _busy, 0);
        }
    }

    async Task<XCredentials> RefreshIfStale(XCredentials stored, XCredentials live, CancellationToken cancel)
    {
        if (!live.UserTokenStale(DateTimeOffset.UtcNow) || string.IsNullOrWhiteSpace(live.RefreshToken))
            return live;

        var token = await XTokenClient.Refresh(_http, live, cancel);
        if (token == null)
            return live;

        var issued = token.Value;
        var expires = DateTimeOffset.UtcNow.AddSeconds(issued.ExpiresIn);
        var saved = stored with
        {
            AccessToken = issued.AccessToken,
            RefreshToken = string.IsNullOrWhiteSpace(issued.RefreshToken) ? stored.RefreshToken : issued.RefreshToken,
            AccessExpires = expires
        };
        XAuthStore.Save(_authPath, saved);
        return live with
        {
            AccessToken = saved.AccessToken,
            RefreshToken = saved.RefreshToken,
            AccessExpires = expires
        };
    }

    async Task<string?> Get(string url, string token, CancellationToken cancel)
    {
        if (!LuxWireXFeed.IsApiHost(url))
        {
            Log.Error("lux wire refused host");
            return null;
        }

        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using var response = await _http.SendAsync(request, cancel);
        if (!response.IsSuccessStatusCode)
        {
            Log.Error("lux wire http " + (int)response.StatusCode);
            return null;
        }

        return await response.Content.ReadAsStringAsync(cancel);
    }

    static string SafeError(string error)
    {
        var text = error.Replace('\r', ' ').Replace('\n', ' ').Trim();
        if (text.Length == 0 || text.Length > 160 || text.Contains("Bearer", StringComparison.OrdinalIgnoreCase))
            return "request failed";
        return text;
    }

    void Queue(List<LuxWirePost> notify)
    {
        if (notify.Count == 0)
            return;
        if (!DigestEnabled)
        {
            foreach (var post in notify)
                _notifier.Show(post);
            return;
        }

        lock (_gate)
        {
            _digest.AddRange(notify);
            _digestTimer?.Dispose();
            _digestTimer = new Timer(_ =>
            {
                try
                {
                    FlushDigest();
                }
                catch (Exception ex)
                {
                    Log.Error("lux wire digest " + ex.Message);
                }
            }, null, TimeSpan.FromSeconds(20), Timeout.InfiniteTimeSpan);
        }
    }

    void FlushDigest()
    {
        LuxWirePost? combined;
        lock (_gate)
        {
            if (_digest.Count == 0)
                return;
            combined = LuxWireDigest.Combine(_digest.ToList());
            _digest.Clear();
        }

        _notifier.Show(combined);
    }
}
