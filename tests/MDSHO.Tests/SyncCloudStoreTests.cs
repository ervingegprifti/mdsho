using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using MDSHO.Core;
using MDSHO.Data;
using Xunit;

namespace MDSHO.Tests {

  /// <summary>
  /// 2.1.0: the Google Drive (Drive API v3) and OneDrive (Microsoft Graph, app folder) stores, run against small fakes
  /// of those APIs; and the HTTP layer they share: bearer tokens, a refresh after 401, and retries with backoff for
  /// network errors and 429/503 (Retry-After).
  /// </summary>
  public class SyncCloudStoreTests : IDisposable {
    private readonly SyncPcs pcs = new SyncPcs();
    private readonly SyncPc a;
    private readonly SyncPc b;
    private readonly List<TimeSpan> waits = new List<TimeSpan>();

    public SyncCloudStoreTests() {
      a = pcs.Add("Home PC");
      b = pcs.Add("Work PC");
    }

    public void Dispose() { pcs.Dispose(); }

    private CloudHttp Http(HttpMessageHandler handler, Func<string> token = null) {
      return new CloudHttp(SyncKinds.GoogleDrive, handler, ct => Task.FromResult(token == null ? "at" : token()), () => { }) {
        Wait = (d, ct) => { waits.Add(d); return Task.CompletedTask; }
      };
    }

    private static Shortcut Note(SyncPc pc, string name, string text) {
      pc.Use();
      Shortcut s = DataStore.Shortcuts.CreateNote(null, pc.FirstPanelId);
      DataStore.Shortcuts.UpdateName(s.ID, name);
      DataStore.Shortcuts.UpdateNoteText(s.ID, text);
      return DataStore.Shortcuts.Get(s.ID);
    }

    private void TwoPcsSync(ISyncStore storeA, ISyncStore storeB) {
      a.Store = storeA;
      b.Store = storeB;
      Shortcut home = Note(a, "Groceries", "milk");
      Shortcut work = Note(b, "Standup", "notes");
      a.Sync();
      b.Sync();
      a.Sync();
      Assert.Equal("milk", b.Button(home.SyncId).NoteText);
      Assert.Equal("notes", a.Button(work.SyncId).NoteText);
      Assert.Equal(a.Snapshot(), b.Snapshot());
      // A change and a delete go too.
      a.Use();
      DataStore.Shortcuts.UpdateName(home.ID, "Shopping");
      a.Sync();
      b.Sync();
      Assert.Equal("Shopping", b.Button(home.SyncId).Name);
    }

    // ---- Google Drive ----

    [Fact]
    public void Two_PCs_sync_through_Google_Drive() {
      FakeGoogleDrive drive = new FakeGoogleDrive();
      TwoPcsSync(new GoogleDriveStore(Http(drive.Http())), new GoogleDriveStore(Http(drive.Http())));
      // One "MDSHO Sync" folder in My Drive, a subfolder per PC, the kind marker at its root.
      FakeGoogleDrive.File root = drive.Files.Single(f => f.Name == GoogleDriveStore.RootFolderName && f.Parent == "root");
      Assert.Equal("application/vnd.google-apps.folder", root.MimeType);
      Assert.Contains(drive.Files, f => f.Name == a.DeviceId && f.Parent == root.Id);
      Assert.Contains(drive.Files, f => f.Name == b.DeviceId && f.Parent == root.Id);
      Assert.Contains("GoogleDrive", Encoding.UTF8.GetString(drive.Files.Single(f => f.Name == SyncMarker.FileName && f.Parent == root.Id).Bytes));
    }

    [Fact]
    public void Google_Drive_replaces_the_index_in_one_content_update_after_the_item_files() {
      FakeGoogleDrive drive = new FakeGoogleDrive();
      FakeHttp http = drive.Http();
      a.Store = new GoogleDriveStore(Http(http));
      Note(a, "Groceries", "milk");
      a.Sync();
      int before = http.Requests.Count;
      a.Use();
      DataStore.Shortcuts.UpdateName(DataStore.Shortcuts.GetAll().First(s => s.Name == "Groceries").ID, "Shopping");
      a.Sync();
      List<FakeRequest> writes = http.Requests.Skip(before).Where(r => r.Method != "GET").ToList();
      string indexId = drive.Files.Single(f => f.Name == SyncFolder.IndexName).Id;
      FakeRequest last = writes.Last();
      Assert.Equal("PATCH", last.Method);
      Assert.Contains("/upload/drive/v3/files/" + indexId + "?uploadType=media", last.Url);
      Assert.Single(writes, w => w.Url.Contains(indexId));
      Assert.DoesNotContain(drive.Files, f => f.Name == "index.tmp");
    }

    [Fact]
    public void Google_Drive_reads_every_page_of_a_listing() {
      FakeGoogleDrive drive = new FakeGoogleDrive();
      ISyncStore store = new GoogleDriveStore(Http(drive.Http()));
      for (int i = 0; i < 5; i++) {
        store.Write("pc" + i, "items/x.json", new byte[] { 1 });
      }
      Assert.Equal(5, store.Devices().Count);
    }

    [Fact]
    public void A_missing_Google_Drive_file_is_null() {
      FakeGoogleDrive drive = new FakeGoogleDrive();
      ISyncStore store = new GoogleDriveStore(Http(drive.Http()));
      Assert.Null(store.Read("pc", "index.json"));
      Assert.Null(store.ReadRoot(SyncMarker.FileName));
      Assert.Empty(store.List("pc", "items"));
      store.Delete("pc", "items/gone.json");
    }

    [Fact]
    public void Google_Drive_tells_the_account() {
      FakeGoogleDrive drive = new FakeGoogleDrive();
      Assert.Equal("ann@example.com", new GoogleDriveStore(Http(drive.Http())).Account());
    }

    // ---- OneDrive ----

    [Fact]
    public void Two_PCs_sync_through_OneDrive() {
      FakeOneDrive one = new FakeOneDrive();
      TwoPcsSync(new OneDriveStore(Http(one.Http())), new OneDriveStore(Http(one.Http())));
      Assert.Contains(SyncMarker.FileName, one.Files.Keys);
      Assert.Contains(one.Files.Keys, k => k.StartsWith(a.DeviceId + "/items/", StringComparison.OrdinalIgnoreCase));
      Assert.Contains(a.DeviceId + "/index.json", one.Files.Keys);
      Assert.Contains("OneDrive", Encoding.UTF8.GetString(one.Files[SyncMarker.FileName].Bytes));
    }

    [Fact]
    public void OneDrive_replaces_the_index_in_one_call_and_never_overwrites_the_marker() {
      FakeOneDrive one = new FakeOneDrive();
      FakeHttp http = one.Http();
      ISyncStore store = new OneDriveStore(Http(http));
      Assert.True(store.CreateRoot(SyncMarker.FileName, new byte[] { 1 }));
      Assert.False(store.CreateRoot(SyncMarker.FileName, new byte[] { 2 }));
      Assert.Equal(new byte[] { 1 }, store.ReadRoot(SyncMarker.FileName));
      int before = http.Requests.Count;
      store.ReplaceIndex("pc", new byte[] { 9 });
      FakeRequest put = http.Requests.Skip(before).Single();
      Assert.Equal("PUT", put.Method);
      Assert.Contains("/me/drive/special/approot:/pc/index.json:/content", put.Url);
    }

    [Fact]
    public void OneDrive_lists_with_paging_and_tells_modified_times() {
      FakeOneDrive one = new FakeOneDrive();
      ISyncStore store = new OneDriveStore(Http(one.Http()));
      for (int i = 0; i < 5; i++) {
        store.Write("pc", "items/f" + i + ".json", new byte[] { 1 });
      }
      IReadOnlyList<SyncStoreFile> files = store.List("pc", "items");
      Assert.Equal(5, files.Count);
      Assert.All(files, f => Assert.True(DateTime.UtcNow - f.ModifiedUtc < TimeSpan.FromMinutes(5)));
      Assert.Equal(new[] { "pc" }, store.Devices());
      Assert.Empty(store.List("other", "items"));
      store.Delete("pc", "items/f0.json");
      store.Delete("pc", "items/gone.json");
      Assert.Equal(4, store.List("pc", "items").Count);
    }

    [Fact]
    public void OneDrive_tells_the_account() {
      Assert.Equal("Ann", new OneDriveStore(Http(new FakeOneDrive().Http())).Account());
    }

    // ---- The shared HTTP layer ----

    [Fact]
    public void A_rate_limit_waits_as_long_as_Retry_After_says_and_tries_again() {
      int calls = 0;
      FakeHttp http = new FakeHttp(r => ++calls == 1 ? FakeHttp.Status((HttpStatusCode)429, "7") : FakeHttp.Json("{}"));
      using (HttpResponseMessage r = Http(http).Send(() => new HttpRequestMessage(HttpMethod.Get, "https://example.com/x"))) {
        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
      }
      Assert.Equal(new[] { TimeSpan.FromSeconds(7) }, waits);
    }

    [Fact]
    public void Server_trouble_and_network_errors_are_tried_again_with_a_growing_wait() {
      int calls = 0;
      FakeHttp http = new FakeHttp(r => {
        calls++;
        if (calls == 1) {
          return FakeHttp.Status(HttpStatusCode.ServiceUnavailable);
        }
        if (calls == 2) {
          throw new HttpRequestException("network down");
        }
        return FakeHttp.Json("{}");
      });
      using (HttpResponseMessage r = Http(http).Send(() => new HttpRequestMessage(HttpMethod.Get, "https://example.com/x"))) {
        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
      }
      Assert.Equal(2, waits.Count);
      Assert.True(waits[1] > waits[0]);
    }

    [Fact]
    public void After_the_last_try_the_cloud_counts_as_unreachable() {
      FakeHttp http = new FakeHttp(r => FakeHttp.Status(HttpStatusCode.ServiceUnavailable));
      Assert.Throws<SyncStoreException>(() => Http(http).Send(() => new HttpRequestMessage(HttpMethod.Get, "https://example.com/x")).Dispose());
      Assert.Equal(CloudHttp.MaxAttempts, http.Requests.Count);
      Assert.All(waits, w => Assert.True(w <= CloudHttp.MaxWait));
    }

    [Fact]
    public void An_expired_access_token_is_refreshed_once() {
      string token = "old";
      int invalidated = 0;
      FakeHttp http = new FakeHttp(r => r.Authorization == "Bearer new" ? FakeHttp.Json("{}") : FakeHttp.Status(HttpStatusCode.Unauthorized));
      CloudHttp cloud = new CloudHttp(SyncKinds.OneDrive, http, ct => Task.FromResult(token), () => { invalidated++; token = "new"; });
      using (HttpResponseMessage r = cloud.Send(() => new HttpRequestMessage(HttpMethod.Get, "https://example.com/x"))) {
        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
      }
      Assert.Equal(1, invalidated);
      Assert.Equal(new[] { "Bearer old", "Bearer new" }, http.Requests.Select(q => q.Authorization));
    }

    [Fact]
    public void A_sync_through_a_cloud_that_is_down_changes_nothing() {
      FakeGoogleDrive drive = new FakeGoogleDrive();
      Note(a, "Groceries", "milk");
      a.Store = new GoogleDriveStore(Http(drive.Http()));
      a.Sync();
      drive.Before = r => FakeHttp.Status(HttpStatusCode.ServiceUnavailable);
      b.Store = new GoogleDriveStore(Http(drive.Http()));
      string before = b.Snapshot();
      SyncResult result = b.Sync();
      Assert.False(result.FolderReachable);
      Assert.Equal(before, b.Snapshot());
    }

    [Fact]
    public void A_PC_that_was_signed_out_counts_as_unreachable() {
      FakeGoogleDrive drive = new FakeGoogleDrive();
      CloudHttp signedOut = new CloudHttp(SyncKinds.GoogleDrive, drive.Http(), ct => throw new CloudSignedOutException("signed out"), () => { });
      b.Store = new GoogleDriveStore(signedOut);
      SyncResult result = b.Sync();
      Assert.False(result.FolderReachable);
      Assert.IsType<CloudSignedOutException>(result.Error);
    }
  }
}
