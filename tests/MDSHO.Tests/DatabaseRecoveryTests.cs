using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MDSHO.Core;
using MDSHO.Data;
using MDSHO.ViewModels;
using Microsoft.Data.Sqlite;
using Xunit;

namespace MDSHO.Tests {

  /// <summary>
  /// A database that can't be opened (corrupt, not a database, a failed upgrade) must not lock the user out: startup
  /// offers to restore the latest backup, start with empty data (keeping the old file) or exit.
  /// </summary>
  public class DatabaseRecoveryTests : IDisposable {
    private readonly TempDb db = new TempDb();
    private readonly FakeClock clock = new FakeClock();
    private readonly ScreenSetupTests.FakeTimer timer = new ScreenSetupTests.FakeTimer();
    private readonly string profile;
    private readonly List<string> shown = new List<string>();
    private readonly List<(string message, bool hasBackup)> asked = new List<(string message, bool hasBackup)>();

    public DatabaseRecoveryTests() {
      profile = Path.Combine(db.Folder, "Profile");
      Directory.CreateDirectory(profile);
    }

    public void Dispose() { db.Dispose(); }

    private BackupService Backups(string pc = "HOMEPC") {
      return new BackupService(clock, pc, profile, "1.17.2", timer, action => action());
    }

    private string BackupFolder => Path.Combine(profile, AutoBackupNames.ExpectedFolderName);

    // Overwrites the database with bytes that are not a database.
    private void BreakDatabase() {
      SqliteConnection.ClearAllPools();
      File.WriteAllText(db.DbPath, "this is not a database, just some text that is long enough to be a header");
    }

    private bool Start(DatabaseRecoveryChoice choice, BackupService backups = null) {
      backups ??= Backups();
      return DatabaseStartup.TryUpgrade(() => DataStore.Upgrade(), (message, caption) => shown.Add(caption + "|" + message),
        (message, hasBackup) => { asked.Add((message, hasBackup)); return choice; }, backups.LatestBackup);
    }

    private string[] KeptFiles() {
      return Directory.GetFiles(db.Folder, Path.GetFileName(db.DbPath) + ".unreadable-*");
    }

    [Fact]
    public void A_broken_database_asks_what_to_do_and_Exit_stops_startup_without_touching_the_file() {
      BreakDatabase();
      byte[] before = File.ReadAllBytes(db.DbPath);

      Assert.False(Start(DatabaseRecoveryChoice.Exit));

      Assert.Single(asked);
      Assert.Contains("MDSHO could not open your data.", asked[0].message);
      Assert.False(asked[0].hasBackup);
      SqliteConnection.ClearAllPools();
      Assert.Equal(before, File.ReadAllBytes(db.DbPath));
      Assert.Empty(KeptFiles());
    }

    [Fact]
    public void Start_empty_keeps_the_old_file_and_starts_with_a_new_database() {
      BreakDatabase();
      byte[] before = File.ReadAllBytes(db.DbPath);

      Assert.True(Start(DatabaseRecoveryChoice.StartEmpty));

      string kept = Assert.Single(KeptFiles());
      Assert.Equal(before, File.ReadAllBytes(kept));
      // The new database works.
      Assert.NotNull(DataStore.Windows.GetAll());
      Assert.Contains(shown, s => s.Contains(kept));
    }

    [Fact]
    public void Restore_brings_back_the_latest_backup() {
      DesktopWindow w = DataStore.Windows.GetAll()[0];
      DataStore.Windows.UpdateTitle(w.ID, "From the backup");
      Assert.True(Backups().BackUpNow());
      string backup = Directory.GetFiles(BackupFolder).Single();
      BreakDatabase();

      Assert.True(Start(DatabaseRecoveryChoice.RestoreBackup));

      Assert.True(asked[0].hasBackup);
      Assert.Contains(Path.GetFileName(backup), asked[0].message);
      Assert.Contains(DataStore.Windows.GetAll(), x => x.Title == "From the backup");
      Assert.Single(KeptFiles());
      Assert.Contains(shown, s => s.Contains(Path.GetFileName(backup)));
    }

    [Fact]
    public void Other_errors_during_the_upgrade_also_offer_recovery() {
      Assert.False(DatabaseStartup.TryUpgrade(() => throw new InvalidOperationException("disk full"), (m, c) => shown.Add(m),
        (message, hasBackup) => { asked.Add((message, hasBackup)); return DatabaseRecoveryChoice.Exit; }, () => null));
      Assert.Contains("disk full", Assert.Single(asked).message);
    }

    [Fact]
    public void A_newer_database_is_still_never_offered_for_recovery() {
      Assert.False(DatabaseStartup.TryUpgrade(() => throw new DatabaseTooNewException(99, 18), (m, c) => shown.Add(m),
        (message, hasBackup) => { asked.Add((message, hasBackup)); return DatabaseRecoveryChoice.StartEmpty; }, () => null));
      Assert.Empty(asked);
      Assert.Single(shown);
    }

    [Fact]
    public void The_latest_backup_is_the_newest_of_this_PC_and_null_when_there_is_none() {
      Assert.Null(Backups().LatestBackup());
      Directory.CreateDirectory(BackupFolder);
      string older = Path.Combine(BackupFolder, BackupFiles.RegularName("HOMEPC", new DateTime(2026, 11, 1, 9, 0, 0)));
      string newer = Path.Combine(BackupFolder, BackupFiles.UpgradeName("HOMEPC", "1.17.2", new DateTime(2026, 11, 2, 9, 0, 0)));
      string otherPc = Path.Combine(BackupFolder, BackupFiles.RegularName("WORKPC", new DateTime(2026, 11, 3, 9, 0, 0)));
      foreach (string f in new[] { older, newer, otherPc }) { File.WriteAllText(f, "zip"); }
      File.SetLastWriteTimeUtc(older, new DateTime(2026, 11, 1));
      File.SetLastWriteTimeUtc(newer, new DateTime(2026, 11, 2));
      File.SetLastWriteTimeUtc(otherPc, new DateTime(2026, 11, 3));
      Assert.Equal(newer, Backups().LatestBackup());
      // A PC with no backup of its own gets the newest of any PC.
      Assert.Equal(otherPc, Backups("NEWPC").LatestBackup());
    }
  }
}
