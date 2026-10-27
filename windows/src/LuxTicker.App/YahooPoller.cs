namespace LuxTicker;

public sealed class YahooPoller
{
    readonly HttpClient _http;
    readonly QuoteBook _book;
    readonly object _state = new();
    volatile IReadOnlyList<SymbolEntry> _symbols = Array.Empty<SymbolEntry>();
    int _failures;
    DateTimeOffset _pauseUntil;
    CancellationTokenSource? _hold;

    public YahooPoller(HttpClient http, QuoteBook book)
    {
        _http = http;
        _book = book;
    }

    public event Action? Updated;

    public void SetSymbols(IReadOnlyList<SymbolEntry> symbols)
        => _symbols = symbols;

    /// <summary>Interrupts the rest between polls so Refresh does not wait out the 12 second cycle.</summary>
    public void RefreshNow()
    {
        lock (_state)
            _pauseUntil = DateTimeOffset.MinValue;
        try
        {
            _hold?.Cancel();
        }
        catch (ObjectDisposedException)
        {
        }
    }

    public void Start(CancellationToken cancellationToken)
        => Task.Run(() => Loop(cancellationToken), cancellationToken);

    async Task Loop(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                var pause = _pauseUntil - DateTimeOffset.UtcNow;
                if (pause > TimeSpan.Zero)
                    await Hold(pause, ct);

                var symbols = _symbols.Where(s => s.Kind != SymbolKind.Crypto).ToList();
                if (symbols.Count == 0)
                {
                    await Hold(TimeSpan.FromSeconds(2), ct);
                    continue;
                }

                var started = DateTimeOffset.UtcNow;
                using var cycle = CancellationTokenSource.CreateLinkedTokenSource(ct);
                var gate = new SemaphoreSlim(4);
                var tasks = new List<Task>(symbols.Count);
                foreach (var symbol in symbols)
                {
                    if (cycle.Token.IsCancellationRequested)
                        break;
                    await Task.Delay(40, ct);
                    await gate.WaitAsync(ct);
                    tasks.Add(FetchOne(symbol, gate, cycle));
                }

                try
                {
                    await Task.WhenAll(tasks);
                }
                catch (OperationCanceledException) when (!ct.IsCancellationRequested)
                {
                }

                var rest = TimeSpan.FromSeconds(12) - (DateTimeOffset.UtcNow - started);
                if (rest < TimeSpan.FromMilliseconds(250))
                    rest = TimeSpan.FromMilliseconds(250);
                await Hold(rest, ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                Log.Error("yahoo loop " + ex.Message);
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(5), ct);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }
    }

    async Task Hold(TimeSpan delay, CancellationToken ct)
    {
        if (delay <= TimeSpan.Zero)
            return;

        CancellationTokenSource hold;
        lock (_state)
        {
            _hold?.Dispose();
            _hold = CancellationTokenSource.CreateLinkedTokenSource(ct);
            hold = _hold;
        }

        try
        {
            await Task.Delay(delay, hold.Token);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
        }
        finally
        {
            lock (_state)
            {
                if (ReferenceEquals(_hold, hold))
                    _hold = null;
            }

            hold.Dispose();
        }
    }

    async Task FetchOne(SymbolEntry symbol, SemaphoreSlim gate, CancellationTokenSource cycle)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, FeedEndpoints.YahooChart(symbol.Symbol));
            using var response = await _http.SendAsync(request, cycle.Token);
            if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests || (int)response.StatusCode >= 500)
            {
                TimeSpan wait;
                lock (_state)
                {
                    _failures++;
                    wait = PollBackoff.Next(_failures);
                    if (response.Headers.RetryAfter?.Delta is { } retry && retry > wait)
                        wait = retry;
                    _pauseUntil = DateTimeOffset.UtcNow + wait;
                }

                Log.Info($"yahoo {(int)response.StatusCode} on {symbol.Symbol}; pausing {wait.TotalSeconds:0}s");
                _book.MarkStale(symbol.Symbol);
                Updated?.Invoke();
                cycle.Cancel();
                return;
            }

            var body = await response.Content.ReadAsStringAsync(cycle.Token);
            YahooQuote quote;
            var error = "";
            var parsed = false;
            if (response.IsSuccessStatusCode)
                parsed = YahooChartParser.TryParse(body, out quote, out error);
            else
                quote = new YahooQuote();

            if (!parsed)
            {
                _book.MarkStale(symbol.Symbol);
                Updated?.Invoke();
                Log.Info($"yahoo {(int)response.StatusCode} {symbol.Symbol} {error}");
                return;
            }

            lock (_state)
                _failures = 0;
            _book.Set(symbol.Symbol, YahooChartParser.ToSnapshot(quote));
            Updated?.Invoke();
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            _book.MarkStale(symbol.Symbol);
            Updated?.Invoke();
            Log.Info($"yahoo {symbol.Symbol} {ex.Message}");
        }
        finally
        {
            gate.Release();
        }
    }
}
