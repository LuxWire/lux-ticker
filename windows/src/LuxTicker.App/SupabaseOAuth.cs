using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;

namespace LuxTicker;

sealed record SupabaseOAuthResult(string? Code, string? Verifier, string? Error);

/// <summary>
/// signInWithOAuth with PKCE. The system browser opens the Supabase authorize URL.
/// Google is provider google. X is provider x (the enabled OAuth 2.0 provider). The project allows
/// http://localhost:*/auth-callback, so the callback is http://localhost:47322/auth-callback.
/// Port 47322 stays off the X feed callback.
/// </summary>
static class SupabaseOAuth
{
    public static async Task<SupabaseOAuthResult> RunAsync(string provider, CancellationToken cancel = default)
    {
        if (!LuxAccount.OAuthEnabled)
            return new SupabaseOAuthResult(null, null, "That sign-in is not turned on yet.");
        var slug = SupabaseAuth.ProviderSlug(provider);
        if (slug == null)
            return new SupabaseOAuthResult(null, null, "That sign-in is not turned on yet.");

        var (verifier, challenge) = LuxWirePkce.Create();
        var state = LuxWirePkce.Base64Url(RandomNumberGenerator.GetBytes(16));
        var listeners = StartLoopback();
        if (listeners.Count == 0)
            return new SupabaseOAuthResult(null, null, "Port " + LuxAccount.LoopbackPort + " is in use. Close the other window and try again.");

        var authorize = SupabaseAuth.AuthorizeUrl(provider, LuxAccount.RedirectUri, state, challenge);
        try
        {
            Process.Start(new ProcessStartInfo(authorize) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            Stop(listeners);
            Log.Error("account browser " + ex.Message);
            return new SupabaseOAuthResult(null, null, "Couldn't open the browser.");
        }

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancel);
        timeout.CancelAfter(TimeSpan.FromMinutes(5));
        using var accept = CancellationTokenSource.CreateLinkedTokenSource(timeout.Token);
        var waits = listeners.Select(listener => WaitForCode(listener, state, accept.Token)).ToArray();
        string? code;
        try
        {
            var finished = await Task.WhenAny(waits);
            code = await finished;
            accept.Cancel();
        }
        catch (OperationCanceledException)
        {
            return new SupabaseOAuthResult(null, null, "Sign-in timed out.");
        }
        catch (Exception ex)
        {
            Log.Error("account callback " + ex.Message);
            return new SupabaseOAuthResult(null, null, "Sign-in did not finish.");
        }
        finally
        {
            Stop(listeners);
            try
            {
                await Task.WhenAll(waits);
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                Log.Error("account callback " + ex.Message);
            }
        }

        if (string.IsNullOrEmpty(code))
            return new SupabaseOAuthResult(null, null, "Sign-in did not finish.");
        return new SupabaseOAuthResult(code, verifier, null);
    }

    static List<TcpListener> StartLoopback()
    {
        var listeners = new List<TcpListener>();
        foreach (var address in new[] { IPAddress.Loopback, IPAddress.IPv6Loopback })
        {
            try
            {
                var listener = new TcpListener(address, LuxAccount.LoopbackPort);
                listener.Start();
                listeners.Add(listener);
            }
            catch (Exception ex)
            {
                Log.Error("account listen " + ex.Message);
            }
        }

        return listeners;
    }

    static void Stop(List<TcpListener> listeners)
    {
        foreach (var listener in listeners)
        {
            try
            {
                listener.Stop();
            }
            catch (Exception ex)
            {
                Log.Error("account listen " + ex.Message);
            }
        }
    }

    static async Task<string?> WaitForCode(TcpListener listener, string expectedState, CancellationToken cancel)
    {
        while (!cancel.IsCancellationRequested)
        {
            using var client = await listener.AcceptTcpClientAsync(cancel);
            var read = await ReadCallback(client, expectedState);
            if (read == "")
                continue;
            return read;
        }

        throw new OperationCanceledException(cancel);
    }

    /// <summary>Empty string means this request was not the auth callback. Null means sign-in failed.</summary>
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
        var queryAt = path.IndexOf('?');
        var route = queryAt >= 0 ? path[..queryAt] : path;
        if (!route.Equals("/auth-callback", StringComparison.OrdinalIgnoreCase))
        {
            await WritePage(stream, false, "Not found.");
            return "";
        }

        string? code = null;
        string? state = null;
        var denied = false;
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

        var stateOk = state == null
            || (state.Length == expectedState.Length
                && CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(state), Encoding.UTF8.GetBytes(expectedState)));
        var ok = !denied && stateOk && !string.IsNullOrEmpty(code);
        var page = "font-family:Segoe UI;background:" + PaperInk.Background + ";color:" + PaperInk.Body + ";padding:24px";
        var body = ok
            ? "<!doctype html><html><body style=\"" + page + "\"><p>Signed in. You can close this window.</p></body></html>"
            : "<!doctype html><html><body style=\"" + page + "\"><p>Sign-in did not finish. Close this window and try again.</p></body></html>";
        await WritePage(stream, ok, body);
        return ok ? code : null;
    }

    static async Task WritePage(NetworkStream stream, bool ok, string body)
    {
        var bytes = Encoding.UTF8.GetBytes(body.StartsWith("<!doctype", StringComparison.OrdinalIgnoreCase)
            ? body
            : "<!doctype html><html><body><p>" + body + "</p></body></html>");
        var header = "HTTP/1.1 " + (ok ? "200 OK" : "400 Bad Request")
            + "\r\nContent-Type: text/html; charset=utf-8\r\nContent-Length: " + bytes.Length
            + "\r\nConnection: close\r\n\r\n";
        await stream.WriteAsync(Encoding.ASCII.GetBytes(header));
        await stream.WriteAsync(bytes);
        await stream.FlushAsync();
    }
}
