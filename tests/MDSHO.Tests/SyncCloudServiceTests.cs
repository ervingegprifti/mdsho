using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using MDSHO.Data;
using MDSHO.ViewModels;
using Xunit;

namespace MDSHO.Tests {

  /// <summary>
  /// 2.1.0, SyncService with Google Drive and OneDrive: Connect signs in and keeps the refresh token on this PC (DPAPI)
  /// with the account; Disconnect revokes (when the provider can) and forgets; turning on needs a connection and makes the
  /// backup first, as for a folder. Without client IDs Connect says the choice is not set up yet.
  /// </summary>
  public class SyncCloudServiceTests : IDisposable {
    private readonly TempDb db = new TempDb();
    private readonly FakeClock clock = new FakeClock();
    private readonly BackupService backups;
    private readonly FakeHttp tokens = new FakeHttp(r => r.Url.Contains("revoke") ? FakeHttp.Status(HttpStatusCode.OK)
      : FakeHttp.Json("{\"access_token\":\"at\",\"refresh_token\":\"rt-123\",\"expires_in\":3600}"));
    private readonly FakeGoogleDrive drive = new FakeGoogleDrive();

    public SyncCloudServiceTests() {
      string profile = Path.Combine(db.Folder, "Profile");
      Directory.CreateDirectory(profile);
      backups = new BackupService(clock, "HOMEPC", profile, "2.1.0", new ScreenSetupTests.FakeTimer(), a => a());
      DeviceIdentity.Name = "Home PC";
    }

    public void Dispose() { db.Dispose(); }

    private SyncService Service(bool setUp = true) {
      return new SyncService(clock, "2.1.0", new ScreenSetupTests.FakeTimer(), a => a(), backups, () => new HashSet<long>(), r => { }) {
        ProviderFor = kind => kind == SyncKinds.GoogleDrive ? OAuthProviders.Google(setUp ? "gid" : "", "gsecret")
          : kind == SyncKinds.OneDrive ? OAuthProviders.Microsoft(setUp ? "mid" : "") : null,
        ClientFor = p => new OAuthClient(p, tokens),
        CloudHandler = drive.Http()
      };
    }

    // The browser: follows the sign-in page straight back to MDSHO with a code.
    private static void Browser(string url) {
      var q = HttpUtility.ParseQueryString(new Uri(url).Query);
      Task.Run(async () => {
        using (HttpClient b = new HttpClient()) {
          await b.GetStringAsync(q["redirect_uri"] + "?code=c&state=" + Uri.EscapeDataString(q["state"]));
        }
      });
    }

    [Fact]
    public async Task Connect_keeps_the_refresh_token_protected_and_the_account() {
      SyncService sync = Service();
      Assert.False(sync.IsConnected(SyncKinds.GoogleDrive));
      SyncConnect result = await sync.ConnectAsync(SyncKinds.GoogleDrive, Browser);
      Assert.True(result.Ok, result.Error);
      Assert.Equal("ann@example.com", result.Account);
      Assert.True(sync.IsConnected(SyncKinds.GoogleDrive));
      Assert.Equal("rt-123", SyncSettings.RefreshToken(SyncKinds.GoogleDrive));
      Assert.DoesNotContain("rt-123", DataStore.AppSettings.Get(SyncSettings.GoogleTokenKey));
      Assert.Equal("ann@example.com", SyncSettings.Account(SyncKinds.GoogleDrive));
      Assert.False(sync.IsConnected(SyncKinds.OneDrive));
    }

    [Fact]
    public async Task Without_a_client_id_Connect_says_it_is_not_set_up_yet() {
      SyncService sync = Service(setUp: false);
      SyncConnect result = await sync.ConnectAsync(SyncKinds.OneDrive, url => throw new InvalidOperationException("The browser was opened."));
      Assert.False(result.Ok);
      Assert.Equal("OneDrive is not set up in this version of MDSHO yet.", result.Error);
      Assert.False(sync.IsSetUp(SyncKinds.OneDrive));
      Assert.Null(SyncSettings.RefreshToken(SyncKinds.OneDrive));
    }

    [Fact]
    public void Google_Drive_is_set_up_when_the_build_has_its_secret() {
      SyncService sync = new SyncService(clock, "2.1.0", new ScreenSetupTests.FakeTimer(), a => a(), backups, null, null);
      Assert.Equal(!string.IsNullOrEmpty(SyncClients.GoogleClientSecret), sync.IsSetUp(SyncKinds.GoogleDrive));
      Assert.Equal(!string.IsNullOrEmpty(SyncClients.MicrosoftClientId), sync.IsSetUp(SyncKinds.OneDrive));
      Assert.True(sync.IsSetUp(SyncKinds.Folder));
    }

    [Fact]
    public async Task Disconnect_revokes_the_Google_token_and_forgets_it() {
      SyncService sync = Service();
      await sync.ConnectAsync(SyncKinds.GoogleDrive, Browser);
      await sync.DisconnectAsync(SyncKinds.GoogleDrive);
      Assert.False(sync.IsConnected(SyncKinds.GoogleDrive));
      Assert.Null(DataStore.AppSettings.Get(SyncSettings.GoogleTokenKey));
      Assert.Equal("", SyncSettings.Account(SyncKinds.GoogleDrive));
      FakeRequest revoke = tokens.Requests.Single(r => r.Url.Contains("revoke"));
      Assert.Contains("token=rt-123", revoke.Body);
    }

    [Fact]
    public async Task Disconnect_turns_sync_off_when_it_went_through_that_cloud() {
      SyncService sync = Service();
      await sync.ConnectAsync(SyncKinds.GoogleDrive, Browser);
      Assert.True(sync.TurnOn(SyncKinds.GoogleDrive, "").Ok);
      await sync.DisconnectAsync(SyncKinds.GoogleDrive);
      Assert.False(sync.IsOn);
    }

    [Fact]
    public void Turning_on_a_cloud_needs_a_connection() {
      SyncService sync = Service();
      SyncJoin join = sync.TurnOn(SyncKinds.GoogleDrive, "");
      Assert.False(join.Ok);
      Assert.Equal("Connect to Google Drive first.", join.Error);
      Assert.False(sync.IsOn);
    }

    [Fact]
    public async Task Turning_on_a_connected_cloud_backs_up_first_and_syncs_there() {
      SyncService sync = Service();
      await sync.ConnectAsync(SyncKinds.GoogleDrive, Browser);
      SyncJoin join = sync.TurnOn(SyncKinds.GoogleDrive, "");
      Assert.True(join.Ok, join.Error);
      Assert.NotNull(join.BackupPath);
      Assert.True(File.Exists(join.BackupPath));
      Assert.Equal(SyncKinds.GoogleDrive, SyncSettings.Kind);
      Assert.True(sync.IsOn);
      Assert.Equal(SyncState.UpToDate, sync.State);
      Assert.Contains(drive.Files, f => f.Name == DeviceIdentity.Id);
      Assert.Contains(drive.Files, f => f.Name == SyncMarker.FileName);
    }

    [Fact]
    public void A_folder_sync_still_turns_on_as_before() {
      string folder = Path.Combine(db.Folder, "Sync");
      Directory.CreateDirectory(folder);
      SyncService sync = Service();
      Assert.True(sync.TurnOn(SyncKinds.Folder, folder).Ok);
      Assert.Equal(SyncKinds.Folder, SyncSettings.Kind);
      Assert.True(sync.IsOn);
    }

    [Fact]
    public void A_cloud_that_is_not_connected_has_no_store_and_counts_as_unreachable() {
      SyncSettings.Save(true, "");
      SyncSettings.Kind = SyncKinds.OneDrive;
      SyncService sync = Service();
      Assert.Null(sync.StoreFor(SyncKinds.OneDrive, ""));
      sync.SyncNow();
      Assert.Equal(SyncState.Unreachable, sync.State);
      Assert.StartsWith("Can't reach OneDrive.", sync.StatusText);
      Assert.Contains("signed out", sync.StatusHint);
    }
  }
}
