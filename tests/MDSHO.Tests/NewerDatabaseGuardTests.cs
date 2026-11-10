using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using MDSHO.Common;
using MDSHO.Core;
using MDSHO.Data;
using MDSHO.ViewModels;
using Xunit;

namespace MDSHO.Tests {

  /// <summary>
  /// A database written by a newer MDSHO (PRAGMA user_version above what this app knows) must never be migrated,
  /// written or backed up; the app reports it and stops.
  /// </summary>
  public class NewerDatabaseGuardTests : IDisposable {
    private readonly TempDb db;
    private const int NewerVersion = AppConstants.DatabaseVersion + 1;

    public NewerDatabaseGuardTests() {
      db = new TempDb(); // latest schema, created by the normal code path
      db.Exec("INSERT INTO MDSHOWindows (Title, ScreenDeviceName, Left, Top, Width, Height, BorderColor, BackgroundColor, TextColor, TitleBackgroundColor, TitleTextColor, Notes, SolidIcons, SingleClickToOpen) VALUES ('W','D',1,2,300,400,'#1','#2','#3','#4','#5','n',1,0);");
      db.Exec("PRAGMA user_version = " + NewerVersion + ";");
      Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
    }

    public void Dispose() { db.Dispose(); }

    private string Hash() {
      using (FileStream fs = new FileStream(db.DbPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
      using (SHA256 sha = SHA256.Create()) {
        return Convert.ToHexString(sha.ComputeHash(fs));
      }
    }

    private string[] FolderFiles() {
      return Directory.GetFiles(db.Folder).Select(Path.GetFileName).OrderBy(n => n).ToArray();
    }

    [Fact]
    public void Upgrade_of_newer_database_throws_typed_exception_with_versions() {
      DatabaseTooNewException ex = Assert.Throws<DatabaseTooNewException>(() => DataStore.Upgrade());
      Assert.Equal(NewerVersion, ex.DatabaseVersion);
      Assert.Equal(AppConstants.DatabaseVersion, ex.SupportedVersion);
      Assert.Contains("newer version of MDSHO", ex.Message);
    }

    [Fact]
    public void Upgrade_of_newer_database_does_not_touch_the_file_or_back_it_up() {
      string hashBefore = Hash();
      DateTime writeBefore = File.GetLastWriteTimeUtc(db.DbPath);
      string[] filesBefore = FolderFiles();

      Assert.Throws<DatabaseTooNewException>(() => DataStore.Upgrade());
      Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();

      Assert.Equal(hashBefore, Hash());
      Assert.Equal(writeBefore, File.GetLastWriteTimeUtc(db.DbPath));
      Assert.Equal(filesBefore, FolderFiles()); // no .bak-v*, no journal left behind
      Assert.Empty(Directory.GetFiles(db.Folder, "*.bak-v*"));
      Assert.Equal(NewerVersion, db.Scalar("PRAGMA user_version"));
    }

    [Fact]
    public void ApplyMigrations_with_only_known_steps_refuses_newer_database() {
      Assert.Throws<DatabaseTooNewException>(() => SchemaManager.ApplyMigrations(SchemaManager.Migrations));
      Assert.Empty(Directory.GetFiles(db.Folder, "*.bak-v*"));
      Assert.Equal(NewerVersion, db.Scalar("PRAGMA user_version"));
    }

    [Fact]
    public void ApplyMigrations_with_a_step_newer_than_the_database_still_migrates() {
      // Boundary: "too new" means newer than every version this app knows, including its migration steps.
      bool ran = false;
      List<DbMigration> list = SchemaManager.Migrations.ToList();
      list.Add(new DbMigration(NewerVersion + 1, "test step", conn => { ran = true; }));
      SchemaManager.ApplyMigrations(list);
      Assert.True(ran);
      Assert.Equal(NewerVersion + 1, db.Scalar("PRAGMA user_version"));
    }

    [Fact]
    public void Startup_check_shows_update_message_and_says_stop_for_newer_database() {
      List<string> shown = new List<string>();
      bool proceed = DatabaseStartup.TryUpgrade(() => DataStore.Upgrade(), (message, caption) => shown.Add(caption + "|" + message));
      Assert.False(proceed);
      Assert.Single(shown);
      Assert.Contains("This data was created by a newer version of MDSHO. Please update MDSHO.", shown[0]);
      Assert.Equal(NewerVersion, db.Scalar("PRAGMA user_version"));
    }

    [Fact]
    public void Startup_check_proceeds_silently_when_upgrade_succeeds() {
      List<string> shown = new List<string>();
      bool proceed = DatabaseStartup.TryUpgrade(() => { }, (message, caption) => shown.Add(message));
      Assert.True(proceed);
      Assert.Empty(shown);
    }

    [Fact]
    public void Startup_check_does_not_swallow_other_errors() {
      Assert.Throws<InvalidOperationException>(() =>
        DatabaseStartup.TryUpgrade(() => throw new InvalidOperationException("boom"), (message, caption) => { }));
    }
  }
}
