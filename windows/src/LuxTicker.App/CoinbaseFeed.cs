using System.Net.WebSockets;
using System.Text;

namespace LuxTicker;

public sealed class CoinbaseFeed
{
    readonly HttpClient _http;
    readonly QuoteBook _book;
    readonly object _gate = new();
    CancellationTokenSource? _cts;
    int _generation;

    public CoinbaseFeed(HttpClient http, QuoteBook book)
    {
        _http = http;
        _book = book;
    }

    public event Action? Updated;

    public void SetEnabled(bool enabled, CancellationToken appStop)
    {
        lock (_gate)
        {
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = null;
            if (!enabled)
                return;

            var linked = CancellationTokenSource.CreateLinkedTokenSource(appStop);
            _cts = linked;
            var generation = ++_generation;
            _ = Task.Run(() => RunAsync(linked.Token, generation), linked.Token);
        }
    }

    async Task RunAsync(CancellationToken ct, int generation)
    {
        var delay = TimeSpan.FromSeconds(1);
        while (!ct.IsCancellationRequested && generation == _generation)
        {
            try
            {
                await RefreshPreviousClose(ct);
                using var socket = new ClientWebSocket();
                socket.Options.KeepAliveInterval = TimeSpan.FromSeconds(20);
                await socket.ConnectAsync(new Uri(FeedEndpoints.CoinbaseWebSocket), ct);
                var subscribe = Encoding.UTF8.GetBytes(FeedEndpoints.CoinbaseSubscribe("BTC-USD"));
                await socket.SendAsync(subscribe, WebSocketMessageType.Text, true, ct);
                Log.Info("btc websocket connected");
                delay = TimeSpan.FromSeconds(1);
                await Receive(socket, ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _book.MarkStale("BTC");
                Updated?.Invoke();
                Log.Error("btc " + ex.Message);
                try
                {
                    await Task.Delay(delay, ct);
                }
                catch (OperationCanceledException)
                {
                    break;
                }

                var next = delay.TotalSeconds * 2;
                if (next > 30)
                    next = 30;
                delay = TimeSpan.FromSeconds(next);
            }
        }
    }

    async Task Receive(ClientWebSocket socket, CancellationToken ct)
    {
        var buffer = new byte[16 * 1024];
        using var message = new MemoryStream();
        var closeRefresh = DateTimeOffset.UtcNow;
        while (socket.State == WebSocketState.Open && !ct.IsCancellationRequested)
        {
            message.SetLength(0);
            WebSocketReceiveResult result;
            do
            {
                result = await socket.ReceiveAsync(buffer, ct);
                if (result.MessageType == WebSocketMessageType.Close)
                    return;
                message.Write(buffer, 0, result.Count);
            }
            while (!result.EndOfMessage);

            var json = Encoding.UTF8.GetString(message.GetBuffer(), 0, (int)message.Length);
            if (CoinbaseTickerParser.TryParsePrice(json, out var price))
            {
                _book.SetBtc(price, null);
                Updated?.Invoke();
            }

            if (DateTimeOffset.UtcNow - closeRefresh > TimeSpan.FromMinutes(30))
            {
                closeRefresh = DateTimeOffset.UtcNow;
                await RefreshPreviousClose(ct);
            }
        }
    }

    async Task RefreshPreviousClose(CancellationToken ct)
    {
        try
        {
            var json = await _http.GetStringAsync(FeedEndpoints.CoinbaseCandles("BTC-USD"), ct);
            if (CoinbaseCandles.TryPreviousDailyClose(json, DateTimeOffset.UtcNow, out var close))
            {
                _book.SetBtcPreviousClose(close);
                Updated?.Invoke();
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Log.Error("btc previous close " + ex.Message);
        }
    }
}
