using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using MDSHO.Core;
using MDSHO.Data;
using MDSHO.ViewModels;
using Xunit;

namespace MDSHO.Tests {

  /// <summary>
  /// 2.1.0: the engine does all its file work through <see cref="ISyncStore"/>: a folder (as before), Google Drive or
  /// OneDrive. A sync is one kind everywhere: sync.json at the root of the location says which, and a PC set to another
  /// kind reads and writes nothing there.
  /// </summary>
  public class SyncStoreTests : IDisposable {
    private readonly SyncPcs pcs = new SyncPcs();
    private readonly SyncPc a;
    private readonly SyncPc b;
    private readonly FakeCloudStore cloud = new FakeCloudStore();

    public SyncStoreTests() {
      a = pcs.Add("Home PC");
      b = pcs.Add("Work PC");
    }

    public void Dispose() { pcs.Dispose(); }

    private static Shortcut Note(SyncPc pc, string name, string text) {
      pc.Use();
      Shortcut s = DataStore.Shortcuts.CreateNote(null, pc.FirstPanelId);
      DataStore.Shortcuts.UpdateName(s.ID, name);
      DataStore.Shortcuts.UpdateNoteText(s.ID, text);
      return DataStore.Shortcuts.Get(s.ID);
    }

    private void UseCloud() {
      a.Store = cloud;
      b.Store = cloud;
    }

    [Fact]
    public void Two_PCs_sync_through_a_cloud_store() {
      UseCloud();
      Shortcut home = Note(a, "Groceries", "milk");
      Shortcut work = Note(b, "Standup", "notes");
      a.Sync();
      b.Sync();
      a.Sync();
      Assert.Equal("milk", b.Button(home.SyncId).NoteText);
      Assert.Equal("notes", a.Button(work.SyncId).NoteText);
      Assert.Equal(a.Snapshot(), b.Snapshot());
      // Nothing went to the folder.
      Assert.Empty(Directory.GetDirectories(pcs.Folder));
    }

    [Fact]
    public void Item_files_are_written_before_the_index_which_is_replaced_in_one_call() {
      UseCloud();
      Note(a, "Groceries", "milk");
      a.Sync();
      List<string> writes = cloud.Calls.Where(c => c.StartsWith("write ", StringComparison.Ordinal) || c.StartsWith("index ", StringComparison.Ordinal)).ToList();
      Assert.Equal("index " + a.DeviceId, writes.Last());
      Assert.Single(writes, w => w.StartsWith("index ", StringComparison.Ordinal));
      Assert.Contains(writes, w => w.Contains("/items/", StringComparison.Ordinal));
      Assert.DoesNotContain(cloud.Calls, c => c.Contains("index.tmp", StringComparison.Ordinal));
    }

    [Fact]
    public void A_half_uploaded_item_waits_for_the_next_round() {
      UseCloud();
      Shortcut note = Note(a, "Groceries", "milk");
      cloud.HalfUploadNext = true;
      a.Sync();
      b.Sync();
      Assert.Null(b.Button(note.SyncId));
      // The upload finishes (the PC writes the file again on its next change round).
      string itemKey = cloud.Names().First(n => n.StartsWith(a.DeviceId + "/items/", StringComparison.Ordinal));
      a.Use();
      cloud.Put(itemKey, Encoding.UTF8.GetBytes("x"));
      DataStore.Shortcuts.UpdateName(note.ID, "Shopping");
      a.Sync();
      b.Sync();
      Assert.Equal("Shopping", b.Button(note.SyncId).Name);
    }

    [Fact]
    public void A_cloud_that_cannot_be_reached_changes_nothing() {
      UseCloud();
      Note(a, "Groceries", "milk");
      a.Sync();
      string before = b.Snapshot();
      cloud.Offline = true;
      SyncResult result = b.Sync();
      Assert.False(result.FolderReachable);
      Assert.Equal(before, b.Snapshot());
    }

    [Fact]
    public void A_slow_cloud_at_exit_is_given_up_after_the_time_limit() {
      using (TempDb db = new TempDb()) {
        string folder = Path.Combine(db.Folder, "Sync");
        Directory.CreateDirectory(folder);
        SyncSettings.Save(true, folder);
        FakeCloudStore slow = new FakeCloudStore { Delay = TimeSpan.FromSeconds(3), Offline = true };
        SyncService sync = new SyncService(TimeProvider.System, "2.1.0", new ScreenSetupTests.FakeTimer(), action => action(), null, null, null) {
          WaitLimit = TimeSpan.FromMilliseconds(300),
          StoreFor = (kind, f) => slow
        };
        DateTime start = DateTime.UtcNow;
        sync.RunOnExit();
        Assert.True(DateTime.UtcNow - start < TimeSpan.FromSeconds(2));
      }
    }

    // ---- One kind per sync ----

    [Fact]
    public void The_first_PC_marks_the_location_with_its_kind() {
      a.Sync();
      string marker = File.ReadAllText(Path.Combine(pcs.Folder, SyncMarker.FileName));
      Assert.Contains("\"kind\": \"Folder\"", marker);
      Assert.Contains("\"format\": 1", marker);
      UseCloud();
      b.Sync();
      Assert.Contains("GoogleDrive", Encoding.UTF8.GetString(cloud.Peek(SyncMarker.FileName)));
    }

    [Fact]
    public void A_PC_set_to_another_kind_reads_and_writes_nothing_and_says_which_kind_is_there() {
      File.WriteAllText(Path.Combine(pcs.Folder, SyncMarker.FileName), "{ \"format\": 1, \"kind\": \"OneDrive\" }");
      string before = a.Snapshot();
      SyncResult result = a.Sync();
      Assert.True(result.FolderReachable);
      Assert.Equal(SyncKinds.OneDrive, result.OtherKind);
      Assert.False(result.Wrote);
      Assert.Empty(Directory.GetDirectories(pcs.Folder));
      Assert.Equal(before, a.Snapshot());
    }

    [Fact]
    public void A_folder_from_2_0_without_a_marker_gets_one_and_keeps_syncing() {
      Shortcut note = Note(a, "Groceries", "milk");
      a.Sync();
      File.Delete(Path.Combine(pcs.Folder, SyncMarker.FileName));
      b.Sync();
      Assert.Equal("milk", b.Button(note.SyncId).NoteText);
      Assert.True(File.Exists(Path.Combine(pcs.Folder, SyncMarker.FileName)));
    }

    [Fact]
    public void The_status_names_the_kind_that_uses_the_location() {
      using (TempDb db = new TempDb()) {
        string folder = Path.Combine(db.Folder, "Sync");
        Directory.CreateDirectory(folder);
        SyncSettings.Save(true, folder);
        SyncService sync = new SyncService(TimeProvider.System, "2.1.0", new ScreenSetupTests.FakeTimer(), action => action(), null, null, null, null,
          options => new SyncResult { FolderReachable = true, OtherKind = SyncKinds.OneDrive });
        sync.SyncNow();
        Assert.Equal(SyncState.OtherKind, sync.State);
        Assert.Equal("This sync folder is used by a OneDrive sync.", sync.StatusText);
      }
    }

    [Fact]
    public void The_status_says_which_cloud_cannot_be_reached() {
      using (TempDb db = new TempDb()) {
        SyncSettings.Save(true, "x");
        SyncSettings.Kind = SyncKinds.GoogleDrive;
        SyncService sync = new SyncService(TimeProvider.System, "2.1.0", new ScreenSetupTests.FakeTimer(), action => action(), null, null, null, null,
          options => new SyncResult { FolderReachable = false }) {
          StoreFor = (kind, f) => new FakeCloudStore()
        };
        sync.SyncNow();
        Assert.Equal(SyncState.Unreachable, sync.State);
        Assert.StartsWith("Can't reach Google Drive.", sync.StatusText);
      }
    }
  }
}
