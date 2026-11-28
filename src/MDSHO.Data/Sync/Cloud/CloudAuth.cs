using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using MDSHO.Common;

namespace MDSHO.Data {

  /// <summary>
  /// The OAuth clients of MDSHO's Google and Microsoft app registrations, in one place. With an empty ID (or no Google
  /// secret) the choice shows in Settings, but Connect says it is not set up yet.
  /// </summary>
  public static class SyncClients {
    /// <summary>The Google OAuth client ID (Desktop app type).</summary>
    public const string GoogleClientId = "296376837622-nkm0garhkcf4r0ggk1e1dl5otu6r7c94.apps.googleusercontent.com";
    /// <summary>
    /// The Google desktop client secret. Kept out of the repository: the build adds it from MDSHO.secrets.props
    /// (git-ignored) or the GOOGLE_CLIENT_SECRET variable. Empty in builds without it.
    /// </summary>
    public static string GoogleClientSecret { get; } = typeof(SyncClients).Assembly
      .GetCustomAttributes(typeof(System.Reflection.AssemblyMetadataAttribute), false)
      .Cast<System.Reflection.AssemblyMetadataAttribute>()
      .FirstOrDefault(a => a.Key == "GoogleClientSecret")?.Value ?? "";
    /// <summary>The Microsoft app (client) ID: a public client with the redirect URI http://localhost.</summary>
    public const string MicrosoftClientId = "";
  }

  /// <summary>One OAuth provider: its endpoints, scopes and how its loopback redirect is named.</summary>
  public sealed class OAuthProvider {
    /// <summary>The kind it signs in to (see <see cref="SyncKinds"/>).</summary>
    public string Kind { get; init; }
    /// <summary>The sign-in page.</summary>
    public string AuthorizeUrl { get; init; }
    /// <summary>The token endpoint.</summary>
    public string TokenUrl { get; init; }
    /// <summary>The revoke endpoint, or null when the provider has none for these tokens.</summary>
    public string RevokeUrl { get; init; }
    /// <summary>The client ID.</summary>
    public string ClientId { get; init; }
    /// <summary>The (non-confidential) client secret, or null.</summary>
    public string ClientSecret { get; init; }
    /// <summary>The scopes, split by spaces.</summary>
    public string Scope { get; init; }
    /// <summary>The loopback host name of the redirect URI: 127.0.0.1 (Google) or localhost (Microsoft).</summary>
    public string RedirectHost { get; init; }
    /// <summary>More parameters for the sign-in page.</summary>
    public IReadOnlyDictionary<string, string> ExtraAuthorize { get; init; } = new Dictionary<string, string>();
    /// <summary>True when the provider needs a client secret (Google's desktop clients do).</summary>
    public bool NeedsSecret { get; init; }
    /// <summary>True when a client ID is filled in, and the secret when it needs one.</summary>
    public bool IsSetUp => !string.IsNullOrWhiteSpace(ClientId) && (!NeedsSecret || !string.IsNullOrEmpty(ClientSecret));
  }

  /// <summary>The two providers.</summary>
  public static class OAuthProviders {
    /// <summary>Google: Drive API scope drive.file only (the files MDSHO makes), offline access for a refresh token.</summary>
    /// <param name="clientSecret">Null uses the built-in secret (see SyncClients).</param>
    public static OAuthProvider Google(string clientId = SyncClients.GoogleClientId, string clientSecret = null) {
      clientSecret ??= SyncClients.GoogleClientSecret;
      return new OAuthProvider {
        Kind = SyncKinds.GoogleDrive,
        AuthorizeUrl = "https://accounts.google.com/o/oauth2/v2/auth",
        TokenUrl = "https://oauth2.googleapis.com/token",
        RevokeUrl = "https://oauth2.googleapis.com/revoke",
        ClientId = clientId ?? "",
        ClientSecret = string.IsNullOrEmpty(clientSecret) ? null : clientSecret,
        NeedsSecret = true,
        Scope = "https://www.googleapis.com/auth/drive.file",
        RedirectHost = "127.0.0.1",
        ExtraAuthorize = new Dictionary<string, string> { ["access_type"] = "offline", ["prompt"] = "consent" }
      };
    }

    /// <summary>Microsoft: personal accounts (consumers), the app folder of OneDrive, offline access for a refresh token.</summary>
    public static OAuthProvider Microsoft(string clientId = SyncClients.MicrosoftClientId) {
      return new OAuthProvider {
        Kind = SyncKinds.OneDrive,
        AuthorizeUrl = "https://login.microsoftonline.com/consumers/oauth2/v2.0/authorize",
        TokenUrl = "https://login.microsoftonline.com/consumers/oauth2/v2.0/token",
        RevokeUrl = null,
        ClientId = clientId ?? "",
        Scope = "Files.ReadWrite.AppFolder offline_access",
        RedirectHost = "localhost"
      };
    }

    /// <summary>The provider of a cloud kind, or null for Folder.</summary>
    public static OAuthProvider For(string kind) {
      return kind == SyncKinds.GoogleDrive ? Google() : kind == SyncKinds.OneDrive ? Microsoft() : null;
    }
  }

  /// <summary>PKCE (RFC 7636): a random verifier and its S256 challenge.</summary>
  public static class Pkce {
    /// <summary>A new verifier: 43 characters of base64url from 32 random bytes.</summary>
    public static string NewVerifier() => Base64Url(RandomNumberGenerator.GetBytes(32));

    /// <summary>The S256 challenge of a verifier.</summary>
    public static string Challenge(string verifier) => Base64Url(SHA256.HashData(Encoding.ASCII.GetBytes(verifier)));

    internal static string Base64Url(byte[] bytes) => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
  }

  /// <summary>Tokens from a provider.</summary>
  public sealed class OAuthTokens {
    /// <summary>The access token for API calls.</summary>
    public string AccessToken { get; init; }
    /// <summary>The refresh token, kept on this PC.</summary>
    public string RefreshToken { get; init; }
    /// <summary>When the access token runs out (UTC).</summary>
    public DateTime ExpiresUtc { get; init; }
  }

  /// <summary>Signing in did not work (turned down, wrong redirect, not set up); the message says why.</summary>
  public class CloudSignInException : Exception {
    /// <summary>Creates the exception.</summary>
    public CloudSignInException(string message, Exception inner = null) : base(message, inner) { }
  }

  /// <summary>The provider refused the refresh token (revoked, expired): this PC must connect again.</summary>
  public class CloudSignedOutException : SyncStoreException {
    /// <summary>Creates the exception.</summary>
    public CloudSignedOutException(string message, Exception inner = null) : base(message, inner) { }
  }

  /// <summary>
  /// OAuth 2.0 for desktop apps: the sign-in page in the default browser, PKCE, a loopback redirect, and the token calls.
  /// </summary>
  public sealed class OAuthClient {
    private readonly HttpClient http;

    /// <summary>A client of a provider; <paramref name="handler"/> sends the requests (tests replace it).</summary>
    public OAuthClient(OAuthProvider provider, HttpMessageHandler handler = null) {
      Provider = provider ?? throw new ArgumentNullException(nameof(provider));
      this.http = handler == null ? new HttpClient() : new HttpClient(handler, disposeHandler: false);
      this.http.Timeout = TimeSpan.FromSeconds(30);
    }

    /// <summary>The provider.</summary>
    public OAuthProvider Provider { get; }
    /// <summary>Now (tests replace it).</summary>
    public Func<DateTime> UtcNow { get; set; } = () => DateTime.UtcNow;
    /// <summary>How long sign-in waits for the browser.</summary>
    public TimeSpan SignInTimeout { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>The sign-in page URL.</summary>
    public string AuthorizeUrl(string redirectUri, string state, string challenge) {
      Dictionary<string, string> q = new Dictionary<string, string> {
        ["client_id"] = Provider.ClientId,
        ["response_type"] = "code",
        ["redirect_uri"] = redirectUri,
        ["scope"] = Provider.Scope,
        ["code_challenge"] = challenge,
        ["code_challenge_method"] = "S256",
        ["state"] = state
      };
      foreach (KeyValuePair<string, string> e in Provider.ExtraAuthorize) {
        q[e.Key] = e.Value;
      }
      return Provider.AuthorizeUrl + "?" + string.Join("&", q.Select(e => Uri.EscapeDataString(e.Key) + "=" + Uri.EscapeDataString(e.Value ?? "")));
    }

    /// <summary>
    /// Signs in: opens the sign-in page with <paramref name="openBrowser"/>, waits for the redirect to the loopback page,
    /// checks the state and exchanges the code.
    /// </summary>
    public async Task<OAuthTokens> SignInAsync(Action<string> openBrowser, CancellationToken cancel) {
      if (!Provider.IsSetUp) {
        throw new CloudSignInException(SyncKinds.DisplayName(Provider.Kind) + " is not set up in this version of MDSHO yet.");
      }
      string verifier = Pkce.NewVerifier();
      string state = Pkce.Base64Url(RandomNumberGenerator.GetBytes(16));
      using (LoopbackListener listener = LoopbackListener.Start(Provider.RedirectHost))
      using (CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(cancel)) {
        timeout.CancelAfter(SignInTimeout);
        Task<IReadOnlyDictionary<string, string>> wait = listener.WaitForQueryAsync(timeout.Token);
        openBrowser(AuthorizeUrl(listener.RedirectUri, state, Pkce.Challenge(verifier)));
        IReadOnlyDictionary<string, string> q;
        try {
          q = await wait.ConfigureAwait(false);
        } catch (OperationCanceledException ex) {
          throw new CloudSignInException("Signing in took too long or was stopped.", ex);
        }
        if (!q.TryGetValue("state", out string back) || back != state) {
          throw new CloudSignInException("The sign-in page answered for another request; nothing was changed.");
        }
        if (q.TryGetValue("error", out string error)) {
          throw new CloudSignInException("Signing in was not finished (" + error + ").");
        }
        if (!q.TryGetValue("code", out string code) || string.IsNullOrEmpty(code)) {
          throw new CloudSignInException("The sign-in page sent no code.");
        }
        return await ExchangeCode(code, verifier, listener.RedirectUri).ConfigureAwait(false);
      }
    }

    /// <summary>Exchanges the code for tokens.</summary>
    public Task<OAuthTokens> ExchangeCode(string code, string verifier, string redirectUri) {
      return Token(new Dictionary<string, string> {
        ["grant_type"] = "authorization_code",
        ["code"] = code,
        ["code_verifier"] = verifier,
        ["redirect_uri"] = redirectUri
      }, null);
    }

    /// <summary>A new access token from the refresh token; throws <see cref="CloudSignedOutException"/> when refused.</summary>
    public Task<OAuthTokens> Refresh(string refreshToken) {
      return Token(new Dictionary<string, string> { ["grant_type"] = "refresh_token", ["refresh_token"] = refreshToken }, refreshToken);
    }

    /// <summary>Revokes the token when the provider supports it. Never throws.</summary>
    public async Task Revoke(string token) {
      if (Provider.RevokeUrl == null || string.IsNullOrEmpty(token)) {
        return;
      }
      try {
        using (HttpResponseMessage r = await this.http.PostAsync(Provider.RevokeUrl, new FormUrlEncodedContent(new Dictionary<string, string> { ["token"] = token })).ConfigureAwait(false)) {
          if (!r.IsSuccessStatusCode) {
            AppLog.Warning(null, "{Kind} did not revoke the token: {Status}", SyncKinds.DisplayName(Provider.Kind), (int)r.StatusCode);
          }
        }
      } catch (Exception ex) when (ex is HttpRequestException || ex is TaskCanceledException) {
        AppLog.Warning(ex, "The token could not be revoked; it is forgotten on this PC");
      }
    }

    private async Task<OAuthTokens> Token(Dictionary<string, string> form, string oldRefresh) {
      form["client_id"] = Provider.ClientId;
      if (Provider.ClientSecret != null) {
        form["client_secret"] = Provider.ClientSecret;
      }
      if (Provider.Kind == SyncKinds.OneDrive) {
        form["scope"] = Provider.Scope;
      }
      HttpResponseMessage r;
      try {
        r = await this.http.PostAsync(Provider.TokenUrl, new FormUrlEncodedContent(form)).ConfigureAwait(false);
      } catch (Exception ex) when (ex is HttpRequestException || ex is TaskCanceledException) {
        throw new SyncStoreException("Can't reach " + SyncKinds.DisplayName(Provider.Kind), ex);
      }
      using (r) {
        string body = await r.Content.ReadAsStringAsync().ConfigureAwait(false);
        if (r.StatusCode == HttpStatusCode.BadRequest || r.StatusCode == HttpStatusCode.Unauthorized) {
          string error = ErrorOf(body);
          if (oldRefresh != null) {
            throw new CloudSignedOutException(SyncKinds.DisplayName(Provider.Kind) + " signed this PC out (" + error + ")");
          }
          throw new CloudSignInException("Signing in did not work (" + error + ").");
        }
        if (!r.IsSuccessStatusCode) {
          throw new SyncStoreException(SyncKinds.DisplayName(Provider.Kind) + " answered " + (int)r.StatusCode);
        }
        using (JsonDocument doc = JsonDocument.Parse(body)) {
          JsonElement root = doc.RootElement;
          string access = root.GetProperty("access_token").GetString();
          string refresh = root.TryGetProperty("refresh_token", out JsonElement rt) ? rt.GetString() : null;
          long seconds = root.TryGetProperty("expires_in", out JsonElement e) && e.TryGetInt64(out long s) ? s : 3600;
          return new OAuthTokens { AccessToken = access, RefreshToken = string.IsNullOrEmpty(refresh) ? oldRefresh : refresh, ExpiresUtc = UtcNow().AddSeconds(seconds) };
        }
      }
    }

    private static string ErrorOf(string body) {
      try {
        using (JsonDocument doc = JsonDocument.Parse(body)) {
          return doc.RootElement.TryGetProperty("error", out JsonElement e) ? e.GetString() : "refused";
        }
      } catch (JsonException) {
        return "refused";
      }
    }
  }

  /// <summary>
  /// Access tokens from the refresh token kept on this PC: refreshed a minute before they run out; a new refresh token
  /// from the provider is saved.
  /// </summary>
  public sealed class CloudTokenSource {
    private readonly OAuthClient client;
    private readonly Func<string> readRefresh;
    private readonly Action<string> saveRefresh;
    private readonly SemaphoreSlim gate = new SemaphoreSlim(1, 1);
    private OAuthTokens current;

    /// <summary>A source over a client and the saved refresh token.</summary>
    public CloudTokenSource(OAuthClient client, Func<string> readRefresh, Action<string> saveRefresh) {
      this.client = client;
      this.readRefresh = readRefresh;
      this.saveRefresh = saveRefresh;
    }

    /// <summary>Now (tests replace it).</summary>
    public Func<DateTime> UtcNow { get; set; } = () => DateTime.UtcNow;

    /// <summary>A valid access token; throws <see cref="CloudSignedOutException"/> when this PC is not connected.</summary>
    public async Task<string> AccessTokenAsync(CancellationToken cancel) {
      await this.gate.WaitAsync(cancel).ConfigureAwait(false);
      try {
        if (this.current != null && this.current.ExpiresUtc - UtcNow() > TimeSpan.FromMinutes(1)) {
          return this.current.AccessToken;
        }
        string refresh = this.readRefresh();
        if (string.IsNullOrEmpty(refresh)) {
          throw new CloudSignedOutException(SyncKinds.DisplayName(this.client.Provider.Kind) + " is not connected on this PC");
        }
        this.current = await this.client.Refresh(refresh).ConfigureAwait(false);
        if (this.current.RefreshToken != refresh) {
          this.saveRefresh(this.current.RefreshToken);
        }
        return this.current.AccessToken;
      } finally {
        this.gate.Release();
      }
    }

    /// <summary>Forgets the access token (after a 401), so the next call refreshes.</summary>
    public void Invalidate() {
      this.current = null;
    }
  }

  /// <summary>
  /// The loopback redirect page: listens on a free port of 127.0.0.1 (and of ::1 for localhost), takes the one redirect
  /// from the browser and answers with a short page that says to go back to MDSHO.
  /// </summary>
  public sealed class LoopbackListener : IDisposable {
    private const string Page = "<!DOCTYPE html><html><head><meta charset=\"utf-8\"><title>MDSHO</title></head>"
      + "<body style=\"font-family:Segoe UI,sans-serif;margin:3em\"><h2>MDSHO</h2><p>You can close this tab and go back to MDSHO.</p></body></html>";
    private readonly List<TcpListener> listeners = new List<TcpListener>();

    private LoopbackListener(string host) {
      Host = host;
    }

    /// <summary>The host name in the redirect URI.</summary>
    public string Host { get; }
    /// <summary>The port.</summary>
    public int Port { get; private set; }
    /// <summary>The redirect URI to give the provider: http://host:port/.</summary>
    public string RedirectUri => "http://" + Host + ":" + Port + "/";

    /// <summary>Starts listening on a free port.</summary>
    public static LoopbackListener Start(string host) {
      LoopbackListener l = new LoopbackListener(host);
      TcpListener v4 = new TcpListener(IPAddress.Loopback, 0);
      v4.Start();
      l.listeners.Add(v4);
      l.Port = ((IPEndPoint)v4.LocalEndpoint).Port;
      if (string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase) && Socket.OSSupportsIPv6) {
        try {
          // A browser may resolve localhost to ::1: listen there too, on the same port.
          TcpListener v6 = new TcpListener(IPAddress.IPv6Loopback, l.Port);
          v6.Start();
          l.listeners.Add(v6);
        } catch (SocketException) {
        }
      }
      return l;
    }

    /// <summary>Waits for the browser's redirect and returns its query parameters.</summary>
    public async Task<IReadOnlyDictionary<string, string>> WaitForQueryAsync(CancellationToken cancel) {
      using (CancellationTokenSource stop = CancellationTokenSource.CreateLinkedTokenSource(cancel)) {
        while (true) {
          List<Task<TcpClient>> accepts = this.listeners.Select(t => t.AcceptTcpClientAsync(stop.Token).AsTask()).ToList();
          Task<TcpClient> done;
          try {
            done = await Task.WhenAny(accepts).ConfigureAwait(false);
          } finally {
            stop.Token.ThrowIfCancellationRequested();
          }
          using (TcpClient client = await done.ConfigureAwait(false)) {
            IReadOnlyDictionary<string, string> query = await Answer(client, stop.Token).ConfigureAwait(false);
            if (query != null) {
              stop.Cancel();
              foreach (Task<TcpClient> other in accepts.Where(a => a != done)) {
                _ = other.ContinueWith(t => { if (t.Status == TaskStatus.RanToCompletion) { t.Result.Dispose(); } }, TaskScheduler.Default);
              }
              return query;
            }
          }
        }
      }
    }

    // Reads one request; answers the page for the redirect (null for anything else, such as /favicon.ico).
    private static async Task<IReadOnlyDictionary<string, string>> Answer(TcpClient client, CancellationToken cancel) {
      NetworkStream stream = client.GetStream();
      byte[] buffer = new byte[8192];
      StringBuilder head = new StringBuilder();
      while (!head.ToString().Contains("\r\n\r\n")) {
        int n = await stream.ReadAsync(buffer, 0, buffer.Length, cancel).ConfigureAwait(false);
        if (n == 0) {
          break;
        }
        head.Append(Encoding.ASCII.GetString(buffer, 0, n));
        if (head.Length > 65536) {
          break;
        }
      }
      string first = head.ToString().Split("\r\n")[0];
      string[] parts = first.Split(' ');
      string target = parts.Length >= 2 ? parts[1] : "";
      bool isRedirect = parts.Length >= 2 && parts[0] == "GET" && (target == "/" || target.StartsWith("/?", StringComparison.Ordinal));
      byte[] body = Encoding.UTF8.GetBytes(isRedirect ? Page : "");
      string status = isRedirect ? "200 OK" : "404 Not Found";
      byte[] header = Encoding.ASCII.GetBytes("HTTP/1.1 " + status + "\r\nContent-Type: text/html; charset=utf-8\r\nContent-Length: " + body.Length
        + "\r\nConnection: close\r\nCache-Control: no-store\r\n\r\n");
      await stream.WriteAsync(header, 0, header.Length, cancel).ConfigureAwait(false);
      await stream.WriteAsync(body, 0, body.Length, cancel).ConfigureAwait(false);
      await stream.FlushAsync(cancel).ConfigureAwait(false);
      if (!isRedirect) {
        return null;
      }
      Dictionary<string, string> query = new Dictionary<string, string>(StringComparer.Ordinal);
      int q = target.IndexOf('?');
      if (q >= 0) {
        foreach (string pair in target.Substring(q + 1).Split('&', StringSplitOptions.RemoveEmptyEntries)) {
          int eq = pair.IndexOf('=');
          string key = Uri.UnescapeDataString((eq < 0 ? pair : pair.Substring(0, eq)).Replace('+', ' '));
          string value = eq < 0 ? "" : Uri.UnescapeDataString(pair.Substring(eq + 1).Replace('+', ' '));
          query[key] = value;
        }
      }
      return query;
    }

    /// <summary>Stops listening.</summary>
    public void Dispose() {
      foreach (TcpListener t in this.listeners) {
        try {
          t.Stop();
        } catch (SocketException) {
        }
      }
    }
  }
}
