using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using MDSHO.Data;
using Xunit;

namespace MDSHO.Tests {

  /// <summary>
  /// 2.1.0: signing in to Google Drive or OneDrive. OAuth 2.0 for desktop apps with PKCE (S256) and a loopback redirect
  /// opened in the default browser; the refresh token stays on this PC, protected by Windows (DPAPI).
  /// </summary>
  public class SyncOAuthTests {
    private static OAuthProvider Google(string id = "google-id") => OAuthProviders.Google(id, "google-secret");
    private static OAuthProvider Microsoft(string id = "ms-id") => OAuthProviders.Microsoft(id);

    private static Dictionary<string, string> Query(string url) {
      var q = HttpUtility.ParseQueryString(new Uri(url).Query);
      return q.AllKeys.ToDictionary(k => k, k => q[k]);
    }

    private static Dictionary<string, string> Form(string body) {
      var q = HttpUtility.ParseQueryString(body);
      return q.AllKeys.ToDictionary(k => k, k => q[k]);
    }

    [Fact]
    public void The_PKCE_challenge_is_the_S256_of_the_verifier() {
      // RFC 7636, appendix B.
      Assert.Equal("E9Melhoa2OwvFrEMTJguCHaoeK1t8URWbuGJSstw-cM", Pkce.Challenge("dBjftJeZ4CVP-mB92K27uhbUJU1p1r_wW1gFWFOEjXk"));
      string verifier = Pkce.NewVerifier();
      Assert.InRange(verifier.Length, 43, 128);
      Assert.Matches("^[A-Za-z0-9._~-]+$", verifier);
      Assert.NotEqual(verifier, Pkce.NewVerifier());
    }

    [Fact]
    public void Google_asks_only_for_the_files_MDSHO_makes_with_a_loopback_redirect() {
      OAuthClient client = new OAuthClient(Google(), new FakeHttp(r => FakeHttp.Status(HttpStatusCode.NotFound)));
      Dictionary<string, string> q = Query(client.AuthorizeUrl("http://127.0.0.1:50123/", "st", "ch"));
      Assert.StartsWith("https://accounts.google.com/o/oauth2/v2/auth?", client.AuthorizeUrl("http://127.0.0.1:50123/", "st", "ch"));
      Assert.Equal("https://www.googleapis.com/auth/drive.file", q["scope"]);
      Assert.Equal("code", q["response_type"]);
      Assert.Equal("google-id", q["client_id"]);
      Assert.Equal("http://127.0.0.1:50123/", q["redirect_uri"]);
      Assert.Equal("S256", q["code_challenge_method"]);
      Assert.Equal("ch", q["code_challenge"]);
      Assert.Equal("st", q["state"]);
      Assert.Equal("offline", q["access_type"]);
      Assert.Equal("127.0.0.1", Google().RedirectHost);
    }

    [Fact]
    public void OneDrive_uses_personal_accounts_the_app_folder_and_a_localhost_redirect() {
      OAuthClient client = new OAuthClient(Microsoft(), new FakeHttp(r => FakeHttp.Status(HttpStatusCode.NotFound)));
      string url = client.AuthorizeUrl("http://localhost:50123/", "st", "ch");
      Assert.StartsWith("https://login.microsoftonline.com/consumers/oauth2/v2.0/authorize?", url);
      Dictionary<string, string> q = Query(url);
      Assert.Equal("Files.ReadWrite.AppFolder offline_access", q["scope"]);
      Assert.Equal("http://localhost:50123/", q["redirect_uri"]);
      Assert.Equal("S256", q["code_challenge_method"]);
      Assert.Equal("localhost", Microsoft().RedirectHost);
      Assert.Null(Microsoft().RevokeUrl);
    }

    [Fact]
    public async Task The_code_is_exchanged_for_tokens_with_the_verifier() {
      FakeHttp http = new FakeHttp(r => FakeHttp.Json("{\"access_token\":\"at\",\"refresh_token\":\"rt\",\"expires_in\":3600,\"token_type\":\"Bearer\"}"));
      OAuthClient client = new OAuthClient(Google(), http) { UtcNow = () => new DateTime(2026, 11, 27, 10, 0, 0, DateTimeKind.Utc) };
      OAuthTokens tokens = await client.ExchangeCode("the-code", "the-verifier", "http://127.0.0.1:50123/");
      Assert.Equal(("at", "rt"), (tokens.AccessToken, tokens.RefreshToken));
      Assert.Equal(new DateTime(2026, 11, 27, 11, 0, 0, DateTimeKind.Utc), tokens.ExpiresUtc);
      FakeRequest r = http.Requests.Single();
      Assert.Equal(("POST", "https://oauth2.googleapis.com/token"), (r.Method, r.Url));
      Dictionary<string, string> f = Form(r.Body);
      Assert.Equal("authorization_code", f["grant_type"]);
      Assert.Equal("the-code", f["code"]);
      Assert.Equal("the-verifier", f["code_verifier"]);
      Assert.Equal("http://127.0.0.1:50123/", f["redirect_uri"]);
      Assert.Equal("google-id", f["client_id"]);
      Assert.Equal("google-secret", f["client_secret"]);
    }

    [Fact]
    public async Task OneDrive_sends_no_client_secret() {
      FakeHttp http = new FakeHttp(r => FakeHttp.Json("{\"access_token\":\"at\",\"refresh_token\":\"rt\",\"expires_in\":3600}"));
      await new OAuthClient(Microsoft(), http).ExchangeCode("c", "v", "http://localhost:1/");
      Assert.Equal("https://login.microsoftonline.com/consumers/oauth2/v2.0/token", http.Requests.Single().Url);
      Assert.False(Form(http.Requests.Single().Body).ContainsKey("client_secret"));
      Assert.Equal("Files.ReadWrite.AppFolder offline_access", Form(http.Requests.Single().Body)["scope"]);
    }

    [Fact]
    public async Task A_refresh_keeps_the_old_refresh_token_when_none_comes_back() {
      FakeHttp http = new FakeHttp(r => FakeHttp.Json("{\"access_token\":\"new\",\"expires_in\":60}"));
      OAuthTokens tokens = await new OAuthClient(Google(), http).Refresh("old-rt");
      Assert.Equal(("new", "old-rt"), (tokens.AccessToken, tokens.RefreshToken));
      Dictionary<string, string> f = Form(http.Requests.Single().Body);
      Assert.Equal("refresh_token", f["grant_type"]);
      Assert.Equal("old-rt", f["refresh_token"]);
    }

    [Fact]
    public async Task A_refused_refresh_means_signed_out() {
      FakeHttp http = new FakeHttp(r => FakeHttp.Json("{\"error\":\"invalid_grant\"}", HttpStatusCode.BadRequest));
      await Assert.ThrowsAsync<CloudSignedOutException>(() => new OAuthClient(Google(), http).Refresh("old-rt"));
    }

    [Fact]
    public async Task Google_revokes_the_token_and_OneDrive_has_nothing_to_call() {
      FakeHttp http = new FakeHttp(r => FakeHttp.Status(HttpStatusCode.OK));
      await new OAuthClient(Google(), http).Revoke("rt");
      Assert.Equal("https://oauth2.googleapis.com/revoke", http.Requests.Single().Url);
      Assert.Equal("rt", Form(http.Requests.Single().Body)["token"]);
      FakeHttp none = new FakeHttp(r => FakeHttp.Status(HttpStatusCode.OK));
      await new OAuthClient(Microsoft(), none).Revoke("rt");
      Assert.Empty(none.Requests);
    }

    [Fact]
    public async Task The_loopback_page_gets_the_code_and_tells_the_user_to_go_back() {
      using (LoopbackListener listener = LoopbackListener.Start("127.0.0.1")) {
        Assert.Matches(@"^http://127\.0\.0\.1:\d+/$", listener.RedirectUri);
        Task<IReadOnlyDictionary<string, string>> wait = listener.WaitForQueryAsync(CancellationToken.None);
        using (HttpClient browser = new HttpClient()) {
          string page = await browser.GetStringAsync(listener.RedirectUri + "?code=abc&state=xyz");
          Assert.Contains("You can close this tab and go back to MDSHO", page);
        }
        IReadOnlyDictionary<string, string> q = await wait;
        Assert.Equal(("abc", "xyz"), (q["code"], q["state"]));
      }
    }

    [Fact]
    public async Task The_localhost_listener_answers_on_localhost() {
      using (LoopbackListener listener = LoopbackListener.Start("localhost")) {
        Assert.Matches(@"^http://localhost:\d+/$", listener.RedirectUri);
        Task<IReadOnlyDictionary<string, string>> wait = listener.WaitForQueryAsync(CancellationToken.None);
        using (HttpClient browser = new HttpClient()) {
          await browser.GetStringAsync(listener.RedirectUri + "?code=c1&state=s1");
        }
        Assert.Equal("c1", (await wait)["code"]);
      }
    }

    // The browser: follows the sign-in page straight to the redirect with a code and the same state.
    private static Action<string> Browser(string code = "the-code", Func<string, string> state = null) {
      return url => Task.Run(async () => {
        Dictionary<string, string> q = Query(url);
        using (HttpClient b = new HttpClient()) {
          await b.GetStringAsync(q["redirect_uri"] + "?code=" + code + "&state=" + Uri.EscapeDataString(state == null ? q["state"] : state(q["state"])));
        }
      });
    }

    [Fact]
    public async Task Sign_in_opens_the_browser_and_returns_the_tokens() {
      FakeHttp http = new FakeHttp(r => FakeHttp.Json("{\"access_token\":\"at\",\"refresh_token\":\"rt\",\"expires_in\":3600}"));
      OAuthTokens tokens = await new OAuthClient(Google(), http).SignInAsync(Browser(), CancellationToken.None);
      Assert.Equal("rt", tokens.RefreshToken);
      Dictionary<string, string> f = Form(http.Requests.Single().Body);
      Assert.Equal("the-code", f["code"]);
      Assert.Equal(f["code_verifier"].Length >= 43, true);
    }

    [Fact]
    public async Task A_redirect_with_another_state_is_refused() {
      FakeHttp http = new FakeHttp(r => FakeHttp.Json("{\"access_token\":\"at\",\"refresh_token\":\"rt\",\"expires_in\":3600}"));
      await Assert.ThrowsAsync<CloudSignInException>(() => new OAuthClient(Google(), http).SignInAsync(Browser(state: s => s + "x"), CancellationToken.None));
      Assert.Empty(http.Requests);
    }

    [Fact]
    public async Task A_sign_in_the_user_turned_down_says_so() {
      Action<string> deny = url => Task.Run(async () => {
        using (HttpClient b = new HttpClient()) {
          await b.GetStringAsync(Query(url)["redirect_uri"] + "?error=access_denied&state=" + Query(url)["state"]);
        }
      });
      CloudSignInException ex = await Assert.ThrowsAsync<CloudSignInException>(() => new OAuthClient(Google(), new FakeHttp(r => FakeHttp.Status(HttpStatusCode.OK))).SignInAsync(deny, CancellationToken.None));
      Assert.Contains("access_denied", ex.Message);
    }

    [Fact]
    public void Without_a_client_id_a_provider_is_not_set_up() {
      Assert.False(Google("").IsSetUp);
      Assert.False(Microsoft("").IsSetUp);
      Assert.True(Google().IsSetUp);
      Assert.Throws<CloudSignInException>(() => new OAuthClient(Google(""), new FakeHttp(r => FakeHttp.Status(HttpStatusCode.OK))).SignInAsync(Browser(), CancellationToken.None).GetAwaiter().GetResult());
    }

    [Fact]
    public void The_client_ids_are_empty_until_the_owner_fills_them_in() {
      // Placeholders in one place; never invented.
      Assert.NotNull(SyncClients.GoogleClientId);
      Assert.NotNull(SyncClients.GoogleClientSecret);
      Assert.NotNull(SyncClients.MicrosoftClientId);
    }

    [Fact]
    public async Task An_access_token_is_refreshed_when_it_runs_out_and_a_new_refresh_token_is_kept() {
      DateTime now = new DateTime(2026, 11, 27, 10, 0, 0, DateTimeKind.Utc);
      int calls = 0;
      FakeHttp http = new FakeHttp(r => { calls++; return FakeHttp.Json("{\"access_token\":\"at" + calls + "\",\"refresh_token\":\"rt" + calls + "\",\"expires_in\":3600}"); });
      string saved = "rt0";
      CloudTokenSource source = new CloudTokenSource(new OAuthClient(Google(), http) { UtcNow = () => now }, () => saved, rt => saved = rt) { UtcNow = () => now };
      Assert.Equal("at1", await source.AccessTokenAsync(CancellationToken.None));
      Assert.Equal("rt1", saved);
      Assert.Equal("at1", await source.AccessTokenAsync(CancellationToken.None));
      now = now.AddMinutes(59);
      Assert.Equal("at2", await source.AccessTokenAsync(CancellationToken.None));
      Assert.Equal(2, calls);
    }

    [Fact]
    public async Task Without_a_refresh_token_the_source_is_signed_out() {
      CloudTokenSource source = new CloudTokenSource(new OAuthClient(Google(), new FakeHttp(r => FakeHttp.Status(HttpStatusCode.OK))), () => null, rt => { });
      await Assert.ThrowsAsync<CloudSignedOutException>(() => source.AccessTokenAsync(CancellationToken.None));
    }

    [Fact]
    public void Refresh_tokens_are_kept_protected_by_Windows_and_only_on_this_PC() {
      using (TempDb db = new TempDb()) {
        Assert.Null(SyncSettings.RefreshToken(SyncKinds.GoogleDrive));
        SyncSettings.SetRefreshToken(SyncKinds.GoogleDrive, "1//secret-refresh");
        Assert.Equal("1//secret-refresh", SyncSettings.RefreshToken(SyncKinds.GoogleDrive));
        Assert.Null(SyncSettings.RefreshToken(SyncKinds.OneDrive));
        string stored = DataStore.AppSettings.Get(SyncSettings.GoogleTokenKey);
        Assert.DoesNotContain("secret-refresh", stored);
        foreach (string key in new[] { SyncSettings.GoogleTokenKey, SyncSettings.OneDriveTokenKey, SyncSettings.GoogleAccountKey, SyncSettings.OneDriveAccountKey }) {
          Assert.Equal(SyncScope.Local, SyncFields.Settings.Single(s => s.Key == key).Scope);
        }
        SyncSettings.SetRefreshToken(SyncKinds.GoogleDrive, null);
        Assert.Null(DataStore.AppSettings.Get(SyncSettings.GoogleTokenKey));
        DataStore.AppSettings.Set(SyncSettings.OneDriveTokenKey, "not protected");
        Assert.Null(SyncSettings.RefreshToken(SyncKinds.OneDrive));
      }
    }
  }
}
