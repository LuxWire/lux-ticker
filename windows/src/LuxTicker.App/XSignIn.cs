using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;

namespace LuxTicker;

sealed record XSignInResult(XToken? Token, string? Error);

/// <summary>
/// OAuth 2.0 PKCE on a loopback socket. A URL reservation would need an admin ACL, so this uses TcpListener.
/// </summary>
static class XSignIn
{
    public static async Task<XSignInResult> RunAsync(XCredentials credentials, CancellationToken cancel = default)
    {
        if (string.IsNullOrWhiteSpace(credentials.ClientId))
            return new XSignInResult(null, "Add an X client id first.");

        var (verifier, challenge) = LuxWirePkce.Create();
        var state = LuxWirePkce.Base64Url(RandomNumberGenerator.GetBytes(16));
        TcpListener listener;
        try
        {
            listener = new TcpListener(IPAddress.Loopback, LuxWireXFeed.LoopbackPort);
            listener.Start();
        }
        catch (Exception ex)
        {
            Log.Error("lux wire sign-in listen " + ex.Message);
            return new XSignInResult(null, "Port " + LuxWireXFeed.LoopbackPort + " is in use. Close the other app and try again.");
        }

        var authorize = LuxWirePkce.AuthorizeUrl(credentials.ClientId, LuxWireXFeed.RedirectUri, state, challenge);
        try
        {
            Process.Start(new ProcessStartInfo(authorize) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            listener.Stop();
            Log.Error("lux wire browser " + ex.Message);
            return new XSignInResult(null, "Couldn't open the browser.");
        }

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancel);
        timeout.CancelAfter(TimeSpan.FromMinutes(5));
        string? code;
        try
        {
            using var client = await listener.AcceptTcpClientAsync(timeout.Token);
            code = await ReadCallback(client, state);
        }
        catch (OperationCanceledException)
        {
            return new XSignInResult(null, "Sign-in timed out.");
        }
        catch (Exception ex)
        {
            Log.Error("lux wire callback " + ex.Message);
            return new XSignInResult(null, "Sign-in did not finish.");
        }
        finally
        {
            listener.Stop();
        }

        if (string.IsNullOrEmpty(code))
            return new XSignInResult(null, "X did not return a code.");

        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
            var token = await XTokenClient.ExchangeCode(http, credentials, code, verifier, timeout.Token);
            if (token == null)
                return new XSignInResult(null, "X did not return a token.");
            return new XSignInResult(token, null);
        }
        catch (OperationCanceledException)
        {
            return new XSignInResult(null, "Sign-in timed out.");
        }
        catch (Exception ex)
        {
            Log.Error("lux wire token " + ex.Message);
            return new XSignInResult(null, "Couldn't finish signing in to X.");
        }
    }

    static async Task<string?> ReadCallback(TcpClient client, string expectedState)
    {
        using var stream = client.GetStream();
        var buffer = new byte[8192];
        var read = 0;
        while (read < buffer.Length)
        {
            var n = await stream.ReadAsync(buffer.AsMemory(read, buffer.Length - read));
            if (n == 0)
                break;
            read += n;
            if (Encoding.ASCII.GetString(buffer, 0, read).Contains("\r\n\r\n", StringComparison.Ordinal))
                break;
        }

        var head = Encoding.ASCII.GetString(buffer, 0, read);
        var requestLine = head.Split('\n')[0].Trim();
        var path = requestLine.Split(' ').ElementAtOrDefault(1) ?? "";
        string? code = null;
        string? state = null;
        var denied = false;
        var queryAt = path.IndexOf('?');
        if (queryAt >= 0)
        {
            foreach (var pair in path[(queryAt + 1)..].Split('&', StringSplitOptions.RemoveEmptyEntries))
            {
                var eq = pair.IndexOf('=');
                var key = Uri.UnescapeDataString(eq < 0 ? pair : pair[..eq]);
                var value = eq < 0 ? "" : Uri.UnescapeDataString(pair[(eq + 1)..]);
                if (key == "code")
                    code = value;
                else if (key == "state")
                    state = value;
                else if (key == "error")
                    denied = true;
            }
        }

        var stateOk = state != null
            && state.Length == expectedState.Length
            && CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(state), Encoding.UTF8.GetBytes(expectedState));
        var ok = !denied && stateOk && !string.IsNullOrEmpty(code);
        var page = "font-family:Segoe UI;background:" + PaperInk.Background + ";color:" + PaperInk.Body + ";padding:24px";
        var body = ok
            ? "<!doctype html><html><body style=\"" + page + "\"><p>Signed in. You can close this window.</p></body></html>"
            : "<!doctype html><html><body style=\"" + page + "\"><p>Sign-in did not finish. Close this window and try again.</p></body></html>";
        var bytes = Encoding.UTF8.GetBytes(body);
        var header = "HTTP/1.1 " + (ok ? "200 OK" : "400 Bad Request")
            + "\r\nContent-Type: text/html; charset=utf-8\r\nContent-Length: " + bytes.Length
            + "\r\nConnection: close\r\n\r\n";
        await stream.WriteAsync(Encoding.ASCII.GetBytes(header));
        await stream.WriteAsync(bytes);
        await stream.FlushAsync();
        return ok ? code : null;
    }
}

static class XTokenClient
{
    public const string TokenUrl = "https://api.x.com/2/oauth2/token";

    public static Task<XToken?> ExchangeCode(HttpClient http, XCredentials credentials, string code, string verifier, CancellationToken cancel)
    {
        var form = new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code",
            ["code"] = code,
            ["redirect_uri"] = LuxWireXFeed.RedirectUri,
            ["code_verifier"] = verifier,
            ["client_id"] = credentials.ClientId ?? ""
        };
        return Post(http, form, credentials.ClientId, credentials.ClientSecret, cancel);
    }

    public static Task<XToken?> Refresh(HttpClient http, XCredentials credentials, CancellationToken cancel)
    {
        if (string.IsNullOrWhiteSpace(credentials.RefreshToken) || string.IsNullOrWhiteSpace(credentials.ClientId))
            return Task.FromResult<XToken?>(null);
        var form = new Dictionary<string, string>
        {
            ["grant_type"] = "refresh_token",
            ["refresh_token"] = credentials.RefreshToken,
            ["client_id"] = credentials.ClientId
        };
        return Post(http, form, credentials.ClientId, credentials.ClientSecret, cancel);
    }

    static async Task<XToken?> Post(
        HttpClient http,
        Dictionary<string, string> form,
        string? clientId,
        string? clientSecret,
        CancellationToken cancel)
    {
        if (!LuxWireXFeed.IsApiHost(TokenUrl))
            return null;

        using var request = new HttpRequestMessage(HttpMethod.Post, TokenUrl);
        request.Content = new FormUrlEncodedContent(form);
        request.Headers.Accept.ParseAdd("application/json");
        if (!string.IsNullOrWhiteSpace(clientSecret) && !string.IsNullOrWhiteSpace(clientId))
        {
            var raw = Convert.ToBase64String(Encoding.UTF8.GetBytes(clientId + ":" + clientSecret));
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic", raw);
        }

        using var response = await http.SendAsync(request, cancel);
        if (!response.IsSuccessStatusCode)
        {
            Log.Error("lux wire token http " + (int)response.StatusCode);
            return null;
        }

        return LuxWireXFeed.ParseToken(await response.Content.ReadAsStringAsync(cancel));
    }
}
