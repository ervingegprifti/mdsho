using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using Microsoft.Data.Sqlite;
using MDSHO.Common;
using MDSHO.Core;
using MDSHO.Data;
using MDSHO.ViewModels;
using Xunit;

namespace MDSHO.Tests {

  /// <summary>
  /// Database version 17 adds the ScreenSetups and ScreenSetupPlaces tables: the screen setups MDSHO has seen on this PC and,
  /// for each one, the place of every panel, clone and open note window. Not part of exports, and kept by an import that
  /// replaces the data (like AppSettings).
  /// </summary>
  public class DatabaseV17MigrationTests : IDisposable {
    private TempDb db;
    public void Dispose() { if (db != null) { db.Dispose(); } }

    // A version 16 database: the version 9 schema with the version 10 to 16 steps applied.
    private void BuildV16Db() {
      db = new TempDb(false);
      db.Exec("PRAGMA user_version = 9;" + DatabaseV10MigrationTests.V9Sql
        + "INSERT INTO MDSHOWindows (ID,Title,ScreenDeviceName,Left,Top,Width,Height,BorderColor,BackgroundColor,TextColor,TitleBackgroundColor,TitleTextColor,Notes,SolidIcons,SingleClickToOpen) VALUES "
        + "(1,'W1','D1',0,0,300,400,'#FF000001','#FF000002','#FF000003','#FF000004','#FF000005','',1,1);");
      using (SqliteConnection c = db.Open()) {
        foreach (int version in new[] { 10, 11, 12, 13, 14, 15, 16 }) {
          SchemaManager.Migrations.Single(m => m.Version == version).Apply(c);
        }
      }
      db.Exec("PRAGMA user_version = 16;");
    }

    private long Tables(string name) {
      return db.Scalar("SELECT count(*) FROM sqlite_master WHERE type = 'table' AND name = '" + name + "'");
    }

    private static ScreenSetupPlace Place(long kind, long itemID, long left, long dock = 0) {
      return new ScreenSetupPlace { Kind = kind, ItemID = itemID, ScreenDeviceName = @"\\.\DISPLAY1", Left = left, Top = 20, Width = 300, Height = 400, Dock = dock };
    }

    // A note button in the window, as a row (the note windows' places use the button's ID).
    private long AddNote(long windowID) {
      db.Exec("INSERT INTO Shortcuts (ParentMDSHOWindowID,Name,Target,Ext,Guid,IsGroup,IsGroupExpanded,Comment,IsMarked,MarkColor,IsProcessCommand,ProcessFileName,ProcessArguments,IsNote,NoteText) VALUES "
        + "(" + windowID + ",'N','','','',0,0,'',0,'',0,'','',1,'text');");
      return db.Scalar("SELECT max(ID) FROM Shortcuts");
    }

    [Fact]
    public void Version_17_is_a_known_step() {
      Assert.True(AppConstants.DatabaseVersion >= 17);
      Assert.Single(SchemaManager.Migrations, m => m.Version == 17);
    }

    [Fact]
    public void V16_database_migrates_to_17_with_empty_screen_setup_tables_and_keeps_the_rest() {
      BuildV16Db();
      DataStore.Upgrade();
      Assert.Equal(AppConstants.DatabaseVersion, db.Scalar("PRAGMA user_version"));
      Assert.Equal(1, Tables("ScreenSetups"));
      Assert.Equal(1, Tables("ScreenSetupPlaces"));
      Assert.Equal(0, db.Scalar("SELECT count(*) FROM ScreenSetups"));
      Assert.Equal(0, db.Scalar("SELECT count(*) FROM ScreenSetupPlaces"));
      Assert.Equal("W1", DataStore.Windows.GetAll().Single().Title);
      Assert.Empty(DataStore.ScreenSetups.GetAll());
    }

    [Fact]
    public void The_upgrade_to_17_makes_a_backup_first() {
      BuildV16Db();
      DataStore.Upgrade();
      Assert.Single(Directory.GetFiles(db.Folder, "test.sqlite3.bak-v16-*"));
    }

    [Fact]
    public void Running_the_v17_step_twice_does_nothing_the_second_time() {
      BuildV16Db();
      DataStore.Upgrade();
      long setup = DataStore.ScreenSetups.Ensure("K", "1 screen: 1920×1080");
      DataStore.ScreenSetups.SavePlace(setup, Place(0, 1, 10));
      using (SqliteConnection c = db.Open()) {
        SchemaManager.Migrations.Single(m => m.Version == 17).Apply(c);
      }
      Assert.Single(DataStore.ScreenSetups.GetPlaces(setup));
    }

    [Fact]
    public void A_new_database_has_the_tables() {
      db = new TempDb();
      Assert.Equal(1, Tables("ScreenSetups"));
      Assert.Equal(1, Tables("ScreenSetupPlaces"));
    }

    // ---- The repository ----

    [Fact]
    public void Ensure_adds_a_setup_once_and_keeps_its_ID() {
      db = new TempDb();
      long a = DataStore.ScreenSetups.Ensure("A", "1 screen: 1920×1080");
      long b = DataStore.ScreenSetups.Ensure("B", "2 screens: 1920×1080, 2560×1440");
      Assert.Equal(a, DataStore.ScreenSetups.Ensure("A", "1 screen: 1920×1080"));
      Assert.NotEqual(a, b);
      Assert.Equal(new[] { "A", "B" }, DataStore.ScreenSetups.GetAll().Select(s => s.Key).ToArray());
      Assert.Equal("2 screens: 1920×1080, 2560×1440", DataStore.ScreenSetups.Get("B").Name);
      Assert.Null(DataStore.ScreenSetups.Get("C"));
    }

    [Fact]
    public void A_place_is_saved_per_setup_and_item_and_replaced_by_saving_again() {
      db = new TempDb();
      long a = DataStore.ScreenSetups.Ensure("A", "a");
      long b = DataStore.ScreenSetups.Ensure("B", "b");
      DataStore.ScreenSetups.SavePlace(a, Place(0, 1, 10));
      DataStore.ScreenSetups.SavePlace(a, Place(1, 1, 20)); // a clone with the same ID as the panel is another item
      DataStore.ScreenSetups.SavePlace(b, Place(0, 1, 30, dock: 2));
      DataStore.ScreenSetups.SavePlace(a, Place(0, 1, 40));
      Assert.Equal(new long[] { 40, 20 }, DataStore.ScreenSetups.GetPlaces(a).OrderBy(p => p.Kind).Select(p => p.Left).ToArray());
      ScreenSetupPlace inB = DataStore.ScreenSetups.GetPlaces(b).Single();
      Assert.Equal(30, inB.Left);
      Assert.Equal(2, inB.Dock);
      Assert.Equal(@"\\.\DISPLAY1", inB.ScreenDeviceName);
      Assert.Equal(400, inB.Height);
      Assert.Equal(3, db.Scalar("SELECT count(*) FROM ScreenSetupPlaces"));
    }

    [Fact]
    public void DeleteAllExcept_forgets_the_other_setups_and_their_places() {
      db = new TempDb();
      long a = DataStore.ScreenSetups.Ensure("A", "a");
      long b = DataStore.ScreenSetups.Ensure("B", "b");
      long c = DataStore.ScreenSetups.Ensure("C", "c");
      DataStore.ScreenSetups.SavePlace(a, Place(0, 1, 10));
      DataStore.ScreenSetups.SavePlace(b, Place(0, 1, 20));
      DataStore.ScreenSetups.SavePlace(c, Place(0, 1, 30));
      DataStore.ScreenSetups.DeleteAllExcept(b);
      Assert.Equal(new[] { "B" }, DataStore.ScreenSetups.GetAll().Select(s => s.Key).ToArray());
      Assert.Equal(1, db.Scalar("SELECT count(*) FROM ScreenSetupPlaces"));
      Assert.Equal(20, DataStore.ScreenSetups.GetPlaces(b).Single().Left);
    }

    [Fact]
    public void Places_of_deleted_panels_clones_and_notes_are_cleaned_up() {
      db = new TempDb();
      DesktopWindow kept = DataStore.Windows.Create("D", 0, 0);
      DesktopWindow gone = DataStore.Windows.Create("D", 0, 0);
      DesktopWindowClone clone = DataStore.Clones.Create(kept.ID, "D", 0, 0);
      long note = AddNote(kept.ID);
      long setup = DataStore.ScreenSetups.Ensure("A", "a");
      DataStore.ScreenSetups.SavePlace(setup, Place(0, kept.ID, 1));
      DataStore.ScreenSetups.SavePlace(setup, Place(0, gone.ID, 2));
      DataStore.ScreenSetups.SavePlace(setup, Place(1, clone.ID, 3));
      DataStore.ScreenSetups.SavePlace(setup, Place(1, clone.ID + 100, 4));
      DataStore.ScreenSetups.SavePlace(setup, Place(2, note, 5));
      DataStore.ScreenSetups.SavePlace(setup, Place(2, note + 100, 6));
      DataStore.Windows.Delete(gone.ID);
      Assert.Equal(3, DataStore.ScreenSetups.DeletePlacesOfMissingItems());
      Assert.Equal(new long[] { 1, 3, 5 }, DataStore.ScreenSetups.GetPlaces(setup).Select(p => p.Left).OrderBy(x => x).ToArray());
    }

    [Fact]
    public void Deleting_a_panel_keeps_the_places_until_the_clean_up() {
      // No cascade from MDSHOWindows: an import that replaces the data deletes and adds the panels again with the same IDs.
      db = new TempDb();
      DesktopWindow w = DataStore.Windows.Create("D", 0, 0);
      long setup = DataStore.ScreenSetups.Ensure("A", "a");
      DataStore.ScreenSetups.SavePlace(setup, Place(0, w.ID, 1));
      DataStore.Windows.Delete(w.ID);
      Assert.Single(DataStore.ScreenSetups.GetPlaces(setup));
    }

    [Fact]
    public void Touch_sets_last_used_and_GetAll_returns_it() {
      db = new TempDb();
      long a = DataStore.ScreenSetups.Ensure("A", "a");
      Assert.Equal("", DataStore.ScreenSetups.Get("A").LastUsed);
      DataStore.ScreenSetups.Touch(a, "2026-11-15T10:00:00.0000000Z");
      Assert.Equal("2026-11-15T10:00:00.0000000Z", DataStore.ScreenSetups.GetAll().Single().LastUsed);
    }

    [Fact]
    public void Rekey_Delete_and_CopyMissingPlaces_merge_two_setups() {
      db = new TempDb();
      long keep = DataStore.ScreenSetups.Ensure("old1", "x");
      long drop = DataStore.ScreenSetups.Ensure("old2", "y");
      DataStore.ScreenSetups.SavePlace(keep, Place(0, 1, 10));
      DataStore.ScreenSetups.SavePlace(drop, Place(0, 1, 20)); // the kept setup has this one: it stays 10
      DataStore.ScreenSetups.SavePlace(drop, Place(2, 5, 30)); // missing in the kept setup: copied
      DataStore.ScreenSetups.CopyMissingPlaces(drop, keep);
      DataStore.ScreenSetups.Delete(drop);
      DataStore.ScreenSetups.Rekey(keep, "new", "1 screen: 1920×1080");
      ScreenSetupRecord merged = DataStore.ScreenSetups.GetAll().Single();
      Assert.Equal(("new", "1 screen: 1920×1080", keep), (merged.Key, merged.Name, merged.ID));
      Assert.Equal(new long[] { 10, 30 }, DataStore.ScreenSetups.GetPlaces(keep).Select(p => p.Left).ToArray());
      Assert.Equal(2, db.Scalar("SELECT count(*) FROM ScreenSetupPlaces"));
    }

    // The tables exactly as MDSHO 1.7.0 created them (database version 17 without ScreenSetups.LastUsed).
    internal const string V17Of170Sql = @"
        CREATE TABLE ScreenSetups (
          ID INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT UNIQUE,
          Key TEXT NOT NULL UNIQUE,
          Name TEXT NOT NULL);
        CREATE TABLE ScreenSetupPlaces (
          ID INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT UNIQUE,
          SetupID INTEGER NOT NULL,
          Kind INTEGER NOT NULL,
          ItemID INTEGER NOT NULL,
          ScreenDeviceName TEXT NOT NULL,
          Left INTEGER NOT NULL,
          Top INTEGER NOT NULL,
          Width INTEGER NOT NULL,
          Height INTEGER NOT NULL,
          Dock INTEGER NOT NULL DEFAULT 0,
          UNIQUE(SetupID, Kind, ItemID),
          FOREIGN KEY(SetupID) REFERENCES ScreenSetups(ID) ON DELETE CASCADE);
        PRAGMA user_version = 17;";

    // The owner's 1.7.0 data: one panel (ID 1) and, for about 3 real setups, 6 setups under the old keys (device names
    // renumbered, offsets changed). The newest row of each real setup has the panel at 300 + its ID.
    private void BuildOwners170Db() {
      BuildV16Db();
      db.Exec(V17Of170Sql + @"
        INSERT INTO ScreenSetups (ID, Key, Name) VALUES
          (1, '\\.\DISPLAY1@0,0,1920x1080|\\.\DISPLAY2@1920,0,800x600', '2 screens: 1920×1080, 800×600'),
          (2, '\\.\DISPLAY1@0,0,1920x1080', '1 screen: 1920×1080'),
          (3, '\\.\DISPLAY1@0,0,1920x1080|\\.\DISPLAY2@1920,0,1920x1080|\\.\DISPLAY3@3840,0,800x600', '3 screens: 1920×1080, 1920×1080, 800×600'),
          (4, '\\.\DISPLAY1@0,0,1920x1080|\\.\DISPLAY5@1920,0,1920x1080|\\.\DISPLAY6@3840,0,800x600', '3 screens: 1920×1080, 1920×1080, 800×600'),
          (5, '\\.\DISPLAY1@0,0,1920x1080|\\.\DISPLAY7@1920,0,1920x1080|\\.\DISPLAY8@3840,100,800x600', '3 screens: 1920×1080, 1920×1080, 800×600'),
          (6, '\\.\DISPLAY1@0,0,1920x1080|\\.\DISPLAY4@1920,40,800x600', '2 screens: 1920×1080, 800×600');");
      foreach (long id in new long[] { 1, 2, 3, 4, 5, 6 }) {
        db.Exec("INSERT INTO ScreenSetupPlaces (SetupID,Kind,ItemID,ScreenDeviceName,Left,Top,Width,Height,Dock) VALUES (" + id + ",0,1,'D'," + (300 + id) + ",10,300,400,0);");
      }
      // A second panel that only the oldest 3-screen row had a place for.
      db.Exec("INSERT INTO MDSHOWindows (ID,Title,ScreenDeviceName,Left,Top,Width,Height,BorderColor,BackgroundColor,TextColor,TitleBackgroundColor,TitleTextColor,Notes,SolidIcons,SingleClickToOpen) VALUES "
        + "(2,'W2','D1',0,0,300,400,'#FF000001','#FF000002','#FF000003','#FF000004','#FF000005','',1,1);");
      db.Exec("INSERT INTO ScreenSetupPlaces (SetupID,Kind,ItemID,ScreenDeviceName,Left,Top,Width,Height,Dock) VALUES (3,0,2,'D',7,7,300,400,0);");
    }

    [Fact]
    public void A_1_7_0_database_gets_the_last_used_column_without_losing_its_setups() {
      BuildOwners170Db();
      DataStore.Upgrade();
      Assert.Equal(AppConstants.DatabaseVersion, db.Scalar("PRAGMA user_version"));
      Assert.Equal(1, db.Scalar("SELECT count(*) FROM pragma_table_info('ScreenSetups') WHERE name = 'LastUsed'"));
      Assert.Equal(6, DataStore.ScreenSetups.GetAll().Count);
      // Running it again changes nothing.
      DataStore.Upgrade();
      Assert.Equal(6, DataStore.ScreenSetups.GetAll().Count);
    }

    [Fact]
    public void The_owners_duplicates_are_merged_into_one_setup_per_real_setup_at_startup() {
      BuildOwners170Db();
      DataStore.Upgrade();
      ScreenSetupTrackerTests.FakeScreens screens = new ScreenSetupTrackerTests.FakeScreens();
      screens.Screens.Add(ScreenSetupTests.Screen(@"\\.\DISPLAY1", 0, 0, 1920, 1080, primary: true));
      ScreenSetupTracker tracker = new ScreenSetupTracker(screens, new ScreenSetupTests.FakeTimer());
      tracker.Start();
      ScreenSetupRecord[] setups = DataStore.ScreenSetups.GetAll().OrderBy(s => s.ID).ToArray();
      Assert.Equal(new[] {
        "1920x1080*@c0r0",
        "1920x1080*@c0r0|1920x1080@c1r0|800x600@c2r0",
        "1920x1080*@c0r0|800x600@c1r0"
      }, setups.Select(s => s.Key).ToArray());
      // Without a last-used time in 1.7.0, the newest row of each real setup is kept, with its places.
      Assert.Equal(new long[] { 2, 5, 6 }, setups.Select(s => s.ID).ToArray());
      Assert.Equal(306, DataStore.ScreenSetups.GetPlaces(6).Single().Left);
      Assert.Equal(305, DataStore.ScreenSetups.GetPlaces(5).Single(p => p.ItemID == 1).Left);
      // A place only an older duplicate had is kept too.
      Assert.Equal(7, DataStore.ScreenSetups.GetPlaces(5).Single(p => p.ItemID == 2).Left);
      // No more numbered names in the tray list.
      Assert.DoesNotContain(tracker.KnownSetups(), s => s.Name.Contains("("));
      Assert.Equal(3, tracker.KnownSetups().Count);
    }

    [Fact]
    public void Merging_keeps_the_places_of_the_most_recently_used_duplicate() {
      db = new TempDb();
      long older = DataStore.ScreenSetups.Ensure(@"\\.\DISPLAY1@0,0,1920x1080", "1 screen: 1920×1080");
      long newer = DataStore.ScreenSetups.Ensure(@"\\.\DISPLAY4@0,0,1920x1080", "1 screen: 1920×1080");
      DataStore.ScreenSetups.Touch(older, "2026-11-15T12:00:00.0000000Z"); // used last, although added first
      DataStore.ScreenSetups.Touch(newer, "2026-11-15T09:00:00.0000000Z");
      DataStore.ScreenSetups.SavePlace(older, Place(0, 1, 111));
      DataStore.ScreenSetups.SavePlace(newer, Place(0, 1, 222));
      ScreenSetupTrackerTests.FakeScreens screens = new ScreenSetupTrackerTests.FakeScreens();
      screens.Screens.Add(ScreenSetupTests.Screen(@"\\.\DISPLAY9", 0, 0, 2560, 1440, primary: true));
      new ScreenSetupTracker(screens, new ScreenSetupTests.FakeTimer()).Start();
      ScreenSetupRecord merged = DataStore.ScreenSetups.Get("1920x1080*@c0r0");
      Assert.Equal(older, merged.ID);
      Assert.Equal(111, DataStore.ScreenSetups.GetPlaces(older).Single().Left);
      Assert.Null(DataStore.ScreenSetups.GetAll().FirstOrDefault(s => s.ID == newer));
    }

    // ---- On this PC only ----

    [Fact]
    public void Export_leaves_the_screen_setups_out_and_replace_import_keeps_them_but_clears_their_places() {
      db = new TempDb();
      DesktopWindow w = DataStore.Windows.Create("D", 0, 0);
      long setup = DataStore.ScreenSetups.Ensure("A", "1 screen: 1920×1080");
      DataStore.ScreenSetups.SavePlace(setup, Place(0, w.ID, 77));
      string zip = Path.Combine(db.Folder, "export.zip");
      DataExporter.Export(zip);
      using (ZipArchive archive = ZipFile.OpenRead(zip)) {
        foreach (ZipArchiveEntry entry in archive.Entries.Where(e => e.FullName.EndsWith(".json", StringComparison.Ordinal))) {
          using (StreamReader reader = new StreamReader(entry.Open())) {
            string text = reader.ReadToEnd();
            // Only the on/off setting (RememberScreenSetupPlaces) travels, from 1.16.0.
            Assert.DoesNotContain("ScreenSetups", text, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("ScreenSetupPlace\"", text, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("1920×1080", text);
          }
        }
      }
      DataImporter.Import(zip, ImportMode.Replace);
      Assert.Equal("1 screen: 1920×1080", DataStore.ScreenSetups.GetAll().Single().Name);
      // From 1.16.0 the places go, so the imported places show after the restart.
      Assert.Empty(DataStore.ScreenSetups.GetPlaces(setup));
    }
  }
}
