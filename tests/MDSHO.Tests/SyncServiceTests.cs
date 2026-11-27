using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MDSHO.Core;
using MDSHO.Data;
using MDSHO.ViewModels;
using Xunit;

namespace MDSHO.Tests {

  /// <summary>
  /// 2.0.0: when sync runs (at start, about 10 seconds after a change, every minute, at exit and on Sync now), turning it
  /// on (a backup first, then a plain merge) and off, and the status Settings shows.
  /// </summary>
  public class SyncServiceTests : IDisposable {
    private readonly TempDb db = new TempDb();
    private readonly FakeClock clock = new FakeClock();
    private readonly ScreenSetupTests.FakeTimer timer = new ScreenSetupTests.FakeTimer();
    private readonly ScreenSetupTests.FakeTimer backupTimer = new ScreenSetupTests.FakeTimer();
    private readonly string folder;
    private readonly string profile;
    private readonly BackupService backups;
    private readonly List<SyncResult> applied = new List<SyncResult>();
    private int backgroundRuns;

    public SyncServiceTests() {
      folder = Path.Combine(db.Folder, "Sync");
      Directory.CreateDirectory(folder);
      profile = Path.Combine(db.Folder, "Profile");
      Directory.CreateDirectory(profile);
      backups = new BackupService(clock, "HOMEPC", profile, "2.0.0", backupTimer, action => action());
      DeviceIdentity.Name = "Home PC";
    }

    public void Dispose() { db.Dispose(); }

    private SyncService Service() {
      return new SyncService(clock, "2.0.0", timer, action => { backgroundRuns++; action(); }, backups,
        () => new HashSet<long>(), result => applied.Add(result));
    }

    private string MyFolder => Path.Combine(folder, DeviceIdentity.Id);
    private long MySeq => SyncFolder.ReadIndex(MyFolder).Index?.Seq ?? 0;

    // Another PC's index with one note, written straight to the folder.
    private string OtherPc(string name, int format = SyncIndex.CurrentFormat) {
      string device = Guid.NewGuid().ToString();
      string other = SyncFolder.DeviceFolder(folder, device);
      SyncIndex index = new SyncIndex { Device = device, Name = name, AppVersion = "2.0.0", Seq = 1, WrittenAt = clock.GetUtcNow().ToUnixTimeMilliseconds() };
      SyncFolder.WriteIndex(other, index);
      if (format != SyncIndex.CurrentFormat) {
        string path = Path.Combine(other, SyncFolder.IndexName);
        File.WriteAllText(path, File.ReadAllText(path).Replace("\"format\": 1", "\"format\": " + format));
      }
      return device;
    }

    [Fact]
    public void Sync_is_off_until_turned_on() {
      SyncService sync = Service();
      Assert.False(sync.IsOn);
      sync.Start();
      Assert.Equal(SyncState.Off, sync.State);
      Assert.False(Directory.Exists(MyFolder));
    }

    [Fact]
    public void Turning_on_backs_up_first_then_merges_without_asking() {
      SyncService sync = Service();
      SyncJoin join = sync.TurnOn(folder);
      Assert.True(join.Ok);
      Assert.True(File.Exists(join.BackupPath));
      Assert.StartsWith(backups.Folder, join.BackupPath);
      Assert.True(SyncSettings.IsOn);
      Assert.Equal(folder, SyncSettings.Folder);
      Assert.True(MySeq > 0);
      Assert.Equal(SyncState.UpToDate, sync.State);
      // Settings says once that a backup was made and Import data restores it.
      Assert.Contains("Import data", sync.TakeBackupNotice());
      Assert.Null(sync.TakeBackupNotice());
    }

    [Fact]
    public void Turning_on_with_a_folder_that_cannot_be_reached_keeps_sync_off() {
      SyncService sync = Service();
      SyncJoin join = sync.TurnOn(Path.Combine(db.Folder, "no-such-drive"));
      Assert.False(join.Ok);
      Assert.False(SyncSettings.IsOn);
      Assert.False(string.IsNullOrEmpty(join.Error));
    }

    [Fact]
    public void Turning_off_stops_syncing_and_leaves_the_folder_alone() {
      SyncService sync = Service();
      sync.TurnOn(folder);
      long seq = MySeq;
      DateTime written = File.GetLastWriteTimeUtc(Path.Combine(MyFolder, SyncFolder.IndexName));
      sync.TurnOff();
      Assert.False(SyncSettings.IsOn);
      Assert.Equal(SyncState.Off, sync.State);
      DataStore.Windows.UpdateTitle(DataStore.Windows.GetAll()[0].ID, "Changed while off");
      clock.Now = clock.Now.AddMinutes(5);
      timer.Fire();
      sync.RunOnExit();
      Assert.Equal(seq, MySeq);
      Assert.Equal(written, File.GetLastWriteTimeUtc(Path.Combine(MyFolder, SyncFolder.IndexName)));
      Assert.True(Directory.Exists(MyFolder));
    }

    [Fact]
    public void It_syncs_at_start_when_on() {
      SyncSettings.Save(true, folder);
      SyncService sync = Service();
      sync.Start();
      Assert.True(MySeq > 0);
      Assert.Equal(1, backgroundRuns);
      Assert.Equal(SyncService.CheckEvery, timer.Delay);
    }

    [Fact]
    public void A_local_change_syncs_at_the_next_check_and_otherwise_once_a_minute() {
      SyncSettings.Save(true, folder);
      SyncService sync = Service();
      sync.Start();
      long seq = MySeq;
      // No change and under a minute: nothing.
      clock.Now = clock.Now.AddSeconds(10);
      timer.Fire();
      Assert.Equal(1, backgroundRuns);
      // A change: the next check (about 10 seconds later) syncs it.
      DataStore.Windows.UpdateTitle(DataStore.Windows.GetAll()[0].ID, "Changed");
      clock.Now = clock.Now.AddSeconds(10);
      timer.Fire();
      Assert.Equal(2, backgroundRuns);
      Assert.Equal(seq + 1, MySeq);
      // A minute later the other PCs are read again.
      clock.Now = clock.Now.AddSeconds(61);
      timer.Fire();
      Assert.Equal(3, backgroundRuns);
      Assert.Equal(SyncService.CheckEvery, timer.Delay);
    }

    [Fact]
    public void Sync_now_runs_at_once() {
      SyncSettings.Save(true, folder);
      SyncService sync = Service();
      sync.SyncNow();
      Assert.Equal(1, backgroundRuns);
      Assert.True(MySeq > 0);
    }

    [Fact]
    public void Exit_writes_the_last_changes() {
      SyncSettings.Save(true, folder);
      SyncService sync = Service();
      sync.Start();
      long seq = MySeq;
      DataStore.Windows.UpdateTitle(DataStore.Windows.GetAll()[0].ID, "Changed before exit");
      sync.RunOnExit();
      Assert.Equal(seq + 1, MySeq);
    }

    [Fact]
    public void Exit_gives_up_after_the_time_limit_when_a_round_is_stuck() {
      SyncSettings.Save(true, folder);
      using (System.Threading.ManualResetEventSlim started = new System.Threading.ManualResetEventSlim())
      using (System.Threading.ManualResetEventSlim release = new System.Threading.ManualResetEventSlim()) {
        SyncService sync = new SyncService(clock, "2.0.0", timer, action => System.Threading.Tasks.Task.Run(action), backups,
          () => new HashSet<long>(), result => { }, null, options => {
            started.Set();
            release.Wait(TimeSpan.FromSeconds(30));
            return SyncEngine.Run(options);
          }) { WaitLimit = TimeSpan.FromMilliseconds(300) };
        // A round already running hangs on the folder; exit waits only up to the limit.
        sync.SyncNow();
        Assert.True(started.Wait(TimeSpan.FromSeconds(10)));
        System.Diagnostics.Stopwatch watch = System.Diagnostics.Stopwatch.StartNew();
        sync.RunOnExit();
        Assert.True(watch.Elapsed < TimeSpan.FromSeconds(5), "Exit waited " + watch.Elapsed);
        release.Set();
      }
    }

    [Fact]
    public void Exit_gives_up_after_the_time_limit_when_its_own_round_hangs() {
      SyncSettings.Save(true, folder);
      using (System.Threading.ManualResetEventSlim release = new System.Threading.ManualResetEventSlim()) {
        SyncService sync = new SyncService(clock, "2.0.0", timer, action => action(), backups, () => new HashSet<long>(), result => { }, null,
          options => { release.Wait(TimeSpan.FromSeconds(30)); return SyncEngine.Run(options); }) { WaitLimit = TimeSpan.FromMilliseconds(300) };
        System.Diagnostics.Stopwatch watch = System.Diagnostics.Stopwatch.StartNew();
        sync.RunOnExit();
        Assert.True(watch.Elapsed < TimeSpan.FromSeconds(5), "Exit waited " + watch.Elapsed);
        release.Set();
      }
      Assert.Equal(TimeSpan.FromSeconds(5), new SyncService(clock, "2.0.0", timer, a => a(), backups, null, null).WaitLimit);
    }

    [Fact]
    public void Turning_on_with_a_folder_that_hangs_gives_up_after_the_time_limit() {
      SyncService sync = Service();
      sync.WaitLimit = TimeSpan.FromMilliseconds(300);
      sync.FolderExists = path => { System.Threading.Thread.Sleep(10000); return true; };
      System.Diagnostics.Stopwatch watch = System.Diagnostics.Stopwatch.StartNew();
      SyncJoin join = sync.TurnOn(folder);
      Assert.True(watch.Elapsed < TimeSpan.FromSeconds(5), "Turning on waited " + watch.Elapsed);
      Assert.False(join.Ok);
      Assert.False(SyncSettings.IsOn);
      Assert.Empty(Directory.Exists(backups.Folder) ? Directory.GetFiles(backups.Folder) : new string[0]);
    }

    [Fact]
    public void A_folder_that_cannot_be_reached_says_so_and_retries() {
      SyncSettings.Save(true, Path.Combine(db.Folder, "gone"));
      SyncService sync = Service();
      sync.Start();
      Assert.Equal(SyncState.Unreachable, sync.State);
      Assert.Equal("Can't reach the folder. Will try again in a minute.", sync.StatusText);
      Assert.Contains("Your changes are kept", sync.StatusHint);
      Assert.True(timer.Running);
    }

    [Fact]
    public void A_PC_with_a_newer_MDSHO_is_named() {
      OtherPc("Work PC", SyncIndex.CurrentFormat + 1);
      SyncSettings.Save(true, folder);
      SyncService sync = Service();
      sync.Start();
      Assert.Equal(SyncState.NewerPc, sync.State);
      Assert.Equal("Work PC has a newer MDSHO. Update this PC to sync with it.", sync.StatusText);
      Assert.Equal("The other PCs keep syncing.", sync.StatusHint);
    }

    [Fact]
    public void Up_to_date_says_when_it_last_synced_and_lists_the_other_PCs() {
      OtherPc("Work PC");
      SyncSettings.Save(true, folder);
      SyncService sync = Service();
      sync.Start();
      Assert.Equal("Up to date. Last sync just now.", sync.StatusText);
      clock.Now = clock.Now.AddMinutes(2);
      Assert.Equal("Up to date. Last sync 2 minutes ago.", sync.StatusText);
      SyncPeerRow row = sync.PeerRows().Single();
      Assert.Equal(("Work PC", "2.0.0"), (row.Name, row.Version));
      Assert.Equal("Today 09:12", row.LastSync);
    }

    [Fact]
    public void Changes_from_other_PCs_are_handed_to_the_app() {
      SyncSettings.Save(true, folder);
      SyncService sync = Service();
      sync.Start();
      // Another PC with a panel of its own.
      string device = Guid.NewGuid().ToString();
      SyncItem panel = new SyncItem { Kind = SyncItem.PanelKind, SyncId = Guid.NewGuid().ToString(), ChangedAt = 5, ChangedBy = device };
      foreach (string column in new[] { "Title", "BorderColor", "BackgroundColor", "TextColor", "TitleBackgroundColor", "TitleTextColor", "Notes", "SortKey" }) {
        panel.Fields[column] = column == "SortKey" ? "a0" : "x";
      }
      panel.Fields["SolidIcons"] = 1L;
      panel.Fields["SingleClickToOpen"] = 0L;
      panel.Fields["ShowVersionInTitle"] = 1L;
      string other = SyncFolder.DeviceFolder(folder, device);
      string hash = SyncFolder.WriteItem(other, panel.ToBytes());
      SyncIndex index = new SyncIndex { Device = device, Name = "Work PC", AppVersion = "2.0.0", Seq = 1 };
      index.Items[panel.SyncId] = new SyncIndexItem { Hash = hash, ChangedAt = 5, ChangedBy = device, Seq = 1 };
      SyncFolder.WriteIndex(other, index);
      sync.SyncNow();
      SyncResult result = applied.Single();
      Assert.Single(result.AddedPanels);
    }
  }
}
