namespace LuxTicker;

public sealed class QuoteProbeClient : IQuoteProbe
{
    readonly HttpClient _http;

    public QuoteProbeClient(HttpClient http) => _http = http;

    public async Task<QuoteProbeResult> ProbeAsync(string symbol, CancellationToken cancellationToken)
    {
        var normalized = SymbolEntry.NormalizeSymbol(symbol);
        if (normalized.Equals("BTC", StringComparison.OrdinalIgnoreCase))
            return await ProbeCoinbase(cancellationToken);

        try
        {
            using var response = await _http.GetAsync(FeedEndpoints.YahooChart(normalized), cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            var status = YahooProbe.Classify((int)response.StatusCode, body, out var quote);
            if (status != ProbeStatus.Found || quote == null)
                return new QuoteProbeResult(status, null, null, null);

            var price = quote.RegularMarketPrice ?? quote.PostMarketPrice ?? quote.PreMarketPrice ?? quote.PostFromSeries ?? quote.PreFromSeries;
            return new QuoteProbeResult(ProbeStatus.Found, quote.Currency, price, null);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return new QuoteProbeResult(ProbeStatus.Unavailable, null, null, ex.Message);
        }
    }

    async Task<QuoteProbeResult> ProbeCoinbase(CancellationToken cancellationToken)
    {
        try
        {
            using var response = await _http.GetAsync(FeedEndpoints.CoinbaseTicker("BTC-USD"), cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                return new QuoteProbeResult(ProbeStatus.NotFound, null, null, null);
            if (!response.IsSuccessStatusCode)
                return new QuoteProbeResult(ProbeStatus.Unavailable, null, null, ((int)response.StatusCode).ToString());
            if (!TryReadTickerPrice(body, out var price))
                return new QuoteProbeResult(ProbeStatus.NotFound, null, null, null);
            return new QuoteProbeResult(ProbeStatus.Found, "USD", price, null);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return new QuoteProbeResult(ProbeStatus.Unavailable, null, null, ex.Message);
        }
    }

    static bool TryReadTickerPrice(string body, out decimal price)
    {
        price = 0;
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(body);
            if (!doc.RootElement.TryGetProperty("price", out var el))
                return false;
            var text = el.ValueKind == System.Text.Json.JsonValueKind.String ? el.GetString() : el.GetRawText();
            return decimal.TryParse(text, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out price);
        }
        catch (System.Text.Json.JsonException)
        {
            return false;
        }
    }

}

public interface IQuoteProbe
{
    Task<QuoteProbeResult> ProbeAsync(string symbol, CancellationToken cancellationToken);
}
