using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.Data.Sqlite;
using MDSHO.Common;
using MDSHO.Core;
using MDSHO.Data;
using MDSHO.ViewModels;
using Xunit;

namespace MDSHO.Tests {

  /// <summary>
  /// 1.30.0, database version 27: the groundwork for syncing between PCs. Every panel and button has a fixed SyncId and a
  /// change record (ChangedAt, ChangedBy) that moves only when a synced field changes; deleted items are remembered; one
  /// registry marks every column and setting as Synced or Local; each PC has its own device identity.
  /// </summary>
  public class SyncFoundationTests : IDisposable {
    private readonly TempDb db = new TempDb();
    private readonly long windowId;

    public SyncFoundationTests() {
      windowId = DataStore.Windows.GetAll().Single().ID;
    }

    public void Dispose() { db.Dispose(); }

    private static readonly Regex GuidText = new Regex("^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$");
    private static long Now() { return DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(); }

    private (long At, string By) ShortcutRecord(long id) {
      return (db.Scalar("SELECT ChangedAt FROM Shortcuts WHERE ID = " + id), db.ScalarText("SELECT ChangedBy FROM Shortcuts WHERE ID = " + id));
    }

    private (long At, string By) WindowRecord(long id) {
      return (db.Scalar("SELECT ChangedAt FROM MDSHOWindows WHERE ID = " + id), db.ScalarText("SELECT ChangedBy FROM MDSHOWindows WHERE ID = " + id));
    }

    private void SetShortcutRecord(long id, long at, string by = "other-pc") {
      db.Exec("UPDATE Shortcuts SET ChangedAt = " + at + ", ChangedBy = '" + by + "' WHERE ID = " + id);
    }

    private void SetWindowRecord(long id, long at, string by = "other-pc") {
      db.Exec("UPDATE MDSHOWindows SET ChangedAt = " + at + ", ChangedBy = '" + by + "' WHERE ID = " + id);
    }

    private List<DeletedItem> Deleted() { return DataStore.DeletedItems.GetAll(); }

    // ---- The registry ----

    private Dictionary<string, List<string>> SchemaColumns() {
      Dictionary<string, List<string>> tables = new Dictionary<string, List<string>>();
      using (SqliteConnection c = db.Open()) {
        List<string> names = new List<string>();
        using (SqliteCommand cmd = c.CreateCommand()) {
          cmd.CommandText = "SELECT name FROM sqlite_master WHERE type = 'table' AND name NOT LIKE 'sqlite_%' ORDER BY name";
          using (SqliteDataReader r = cmd.ExecuteReader()) { while (r.Read()) { names.Add(r.GetString(0)); } }
        }
        foreach (string table in names) {
          List<string> columns = new List<string>();
          using (SqliteCommand cmd = c.CreateCommand()) {
            cmd.CommandText = "SELECT name FROM pragma_table_info('" + table + "')";
            using (SqliteDataReader r = cmd.ExecuteReader()) { while (r.Read()) { columns.Add(r.GetString(0)); } }
          }
          tables[table] = columns;
        }
      }
      return tables;
    }

    [Fact]
    public void Every_column_of_every_table_is_classified_as_synced_or_local() {
      List<string> missing = new List<string>();
      HashSet<string> inSchema = new HashSet<string>();
      foreach (KeyValuePair<string, List<string>> table in SchemaColumns()) {
        foreach (string column in table.Value) {
          inSchema.Add(table.Key + "." + column);
          if (SyncFields.Of(table.Key, column) == null) {
            missing.Add(table.Key + "." + column);
          }
        }
      }
      Assert.True(missing.Count == 0, "Decide Synced or Local in SyncFields for: " + string.Join(", ", missing));
      // No stale entries, each column once, and every local one says why.
      Assert.All(SyncFields.Columns, f => Assert.Contains(f.Table + "." + f.Column, inSchema));
      Assert.Equal(SyncFields.Columns.Count, SyncFields.Columns.Select(f => f.Table + "." + f.Column).Distinct().Count());
      Assert.All(SyncFields.Columns.Where(f => f.Scope == SyncScope.Local), f => Assert.False(string.IsNullOrWhiteSpace(f.Reason), f.Table + "." + f.Column));
    }

    [Theory]
    [InlineData("MDSHOWindows", "Title", SyncScope.Synced)]
    [InlineData("MDSHOWindows", "BorderColor", SyncScope.Synced)]
    [InlineData("MDSHOWindows", "SortKey", SyncScope.Synced)]
    [InlineData("MDSHOWindows", "Left", SyncScope.Local)]
    [InlineData("MDSHOWindows", "ScreenDeviceName", SyncScope.Local)]
    [InlineData("MDSHOWindowClones", "ParentMDSHOWindowID", SyncScope.Local)]
    [InlineData("MDSHOWindowClones", "Width", SyncScope.Local)]
    [InlineData("Shortcuts", "Name", SyncScope.Synced)]
    [InlineData("Shortcuts", "Target", SyncScope.Synced)]
    [InlineData("Shortcuts", "NoteText", SyncScope.Synced)]
    [InlineData("Shortcuts", "TextFont", SyncScope.Synced)]
    [InlineData("Shortcuts", "NoteHighlights", SyncScope.Synced)]
    [InlineData("Shortcuts", "MarkColor", SyncScope.Synced)]
    [InlineData("Shortcuts", "ParentShortcutID", SyncScope.Synced)]
    [InlineData("Shortcuts", "SyncId", SyncScope.Synced)]
    [InlineData("Shortcuts", "ID", SyncScope.Local)]
    [InlineData("NoteWindows", "Left", SyncScope.Local)]
    [InlineData("NoteWindows", "IsOpen", SyncScope.Local)]
    [InlineData("NoteWindows", "FontSize", SyncScope.Synced)]
    [InlineData("ScreenSetups", "Key", SyncScope.Local)]
    [InlineData("ScreenSetupPlaces", "Left", SyncScope.Local)]
    [InlineData("DeletedItems", "SyncId", SyncScope.Synced)]
    public void Columns_are_classified_as_agreed(string table, string column, SyncScope scope) {
      Assert.Equal(scope, SyncFields.Of(table, column).Scope);
    }

    [Fact]
    public void Every_app_setting_is_classified_and_the_device_ones_are_local() {
      string page = File.ReadAllText(Path.Combine(DocsTests.RepoRoot(), "docs", "database.md"));
      string settings = page.Substring(page.IndexOf("#### The settings", StringComparison.Ordinal));
      settings = settings.Substring(0, settings.IndexOf("\n### ", StringComparison.Ordinal));
      List<string> documented = Regex.Matches(settings, @"^\| `(\w+)` \|", RegexOptions.Multiline).Select(m => m.Groups[1].Value).ToList();
      Assert.NotEmpty(documented);
      Assert.Equal(documented.OrderBy(k => k), SyncFields.Settings.Where(s => s.InDatabase).Select(s => s.Key).OrderBy(k => k));
      foreach (string key in new[] { "SyncFolder", "StartWithWindows", "BackupFolder", "TextHistoryKeep", DeviceIdentity.IdKey, DeviceIdentity.NameKey }) {
        SyncSetting setting = SyncFields.Settings.Single(s => s.Key == key);
        Assert.Equal(SyncScope.Local, setting.Scope);
        Assert.False(string.IsNullOrWhiteSpace(setting.Reason));
      }
    }

    [Fact]
    public void App_settings_and_default_colours_stay_on_each_PC() {
      // The owner's rule: only panels and buttons sync; each PC keeps its own settings and default colours.
      Assert.All(SyncFields.Settings, s => {
        Assert.Equal(SyncScope.Local, s.Scope);
        Assert.False(string.IsNullOrWhiteSpace(s.Reason));
      });
      Assert.All(SyncFields.Columns.Where(f => f.Table == "DefaultColors"), f => Assert.Equal(SyncScope.Local, f.Scope));
    }

    // ---- Sync IDs ----

    [Fact]
    public void Every_new_panel_and_button_gets_its_own_fixed_sync_id() {
      DesktopWindow w = DataStore.Windows.Create("S", 0, 0);
      Shortcut g = DataStore.Shortcuts.CreateGroup(null, w.ID);
      List<Shortcut> made = new List<Shortcut> {
        g,
        DataStore.Shortcuts.Create(g.ID, null, "f", @"C:\f.exe", ".lnk", Identifiers.NewGuid()),
        DataStore.Shortcuts.Create(g.ID, null, "w", "https://example.com", ".url", Identifiers.NewGuid()),
        DataStore.Shortcuts.CreateNote(g.ID, null),
        DataStore.Shortcuts.CreateSnippet(g.ID, null),
        DataStore.Shortcuts.CreateProcessCommand(null, w.ID),
      };
      List<string> ids = made.Select(s => s.SyncId).Append(w.SyncId).Append(DataStore.Windows.Get(windowId).SyncId).ToList();
      Assert.All(ids, id => Assert.Matches(GuidText, id));
      Assert.Equal(ids.Count, ids.Distinct().Count());
      DataStore.Shortcuts.UpdateName(g.ID, "renamed");
      DataStore.Windows.UpdateTitle(w.ID, "renamed");
      Assert.Equal(g.SyncId, DataStore.Shortcuts.Get(g.ID).SyncId);
      Assert.Equal(w.SyncId, DataStore.Windows.Get(w.ID).SyncId);
      // A full row update can't change it either.
      Shortcut row = DataStore.Shortcuts.Get(g.ID);
      row.SyncId = Guid.NewGuid().ToString();
      DataStore.Shortcuts.Update(row);
      Assert.Equal(g.SyncId, DataStore.Shortcuts.Get(g.ID).SyncId);
    }

    [Fact]
    public void Sync_ids_are_unique_in_the_database() {
      Shortcut a = DataStore.Shortcuts.CreateGroup(null, windowId);
      Shortcut b = DataStore.Shortcuts.CreateGroup(null, windowId);
      Assert.ThrowsAny<SqliteException>(() => db.Exec("UPDATE Shortcuts SET SyncId = '" + a.SyncId + "' WHERE ID = " + b.ID));
    }

    [Fact]
    public void A_copy_gets_new_sync_ids_and_a_move_keeps_them() {
      AppViewModel app = new AppViewModel();
      DesktopWindowViewModel win = app.AddWindow("TEST", 0, 0);
      ShortcutViewModel group = ViewModelFactory.NewGroupShortcut(app, null, win);
      win.RootShortcuts.Add(group);
      ShortcutViewModel note = ViewModelFactory.NewNoteShortcut(app, group, null);
      group.Children.Add(note);
      string groupId = DataStore.Shortcuts.Get(group.ID).SyncId, noteId = DataStore.Shortcuts.Get(note.ID).SyncId;

      ShortcutViewModel copy = ViewModelFactory.CopyShortcut(group, null, win);
      Assert.NotEqual(groupId, DataStore.Shortcuts.Get(copy.ID).SyncId);
      Assert.NotEqual(noteId, DataStore.Shortcuts.Get(copy.Children.Single().ID).SyncId);

      ShortcutViewModel other = ViewModelFactory.NewGroupShortcut(app, null, win);
      win.RootShortcuts.Add(other);
      ViewModelFactory.ApplyShortcutMove(group.Children, other.Children, 0, other, null, new List<ShortcutViewModel> { note }, null);
      Shortcut moved = DataStore.Shortcuts.Get(note.ID);
      Assert.Equal(other.ID, moved.ParentShortcutID);
      Assert.Equal(noteId, moved.SyncId);
    }

    // ---- Change records ----

    [Fact]
    public void New_rows_record_when_and_on_which_PC_they_were_made() {
      long before = Now();
      DesktopWindow w = DataStore.Windows.Create("S", 0, 0);
      Shortcut s = DataStore.Shortcuts.CreateNote(null, w.ID);
      long after = Now();
      foreach ((long at, string by) in new[] { WindowRecord(w.ID), ShortcutRecord(s.ID) }) {
        Assert.InRange(at, before - 1000, after + 1000);
        Assert.Equal(DeviceIdentity.Id, by);
      }
      Assert.Equal(ShortcutRecord(s.ID).At, s.ChangedAt);
      Assert.Equal(DeviceIdentity.Id, s.ChangedBy);
    }

    public static IEnumerable<object[]> SyncedShortcutChanges() {
      yield return new object[] { "name", (Action<long>)(id => DataStore.Shortcuts.UpdateName(id, "new name")) };
      yield return new object[] { "comment", (Action<long>)(id => DataStore.Shortcuts.UpdateComment(id, "a comment")) };
      yield return new object[] { "note text", (Action<long>)(id => DataStore.Shortcuts.UpdateNoteText(id, "text")) };
      yield return new object[] { "snippet text", (Action<long>)(id => DataStore.Shortcuts.UpdateSnippetText(id, "text")) };
      yield return new object[] { "mark", (Action<long>)(id => DataStore.Shortcuts.UpdateIsMarked(id, true)) };
      yield return new object[] { "mark colour", (Action<long>)(id => DataStore.Shortcuts.UpdateMarkColor(id, "#FF00FF00")) };
      yield return new object[] { "font", (Action<long>)(id => DataStore.Shortcuts.UpdateTextFont(id, "Consolas")) };
      yield return new object[] { "comment size", (Action<long>)(id => DataStore.Shortcuts.UpdateCommentFontSize(id, 20)) };
      yield return new object[] { "note colours", (Action<long>)(id => DataStore.Shortcuts.UpdateNoteColors(id, "#FF000001", "", "", "", "")) };
      yield return new object[] { "highlights", (Action<long>)(id => DataStore.Shortcuts.UpdateNoteHighlights(id, "[{\"term\":\"x\",\"color\":\"Yellow\"}]")) };
      yield return new object[] { "view options", (Action<long>)(id => DataStore.Shortcuts.UpdateViewOptions(id, true, new TextViewOptions(false, true, true, true))) };
      yield return new object[] { "arguments", (Action<long>)(id => DataStore.Shortcuts.UpdateProcessArguments(id, "/c dir")) };
      yield return new object[] { "working folder", (Action<long>)(id => DataStore.Shortcuts.UpdateProcessWorkingFolder(id, @"C:\x")) };
      yield return new object[] { "full row", (Action<long>)(id => { Shortcut s = DataStore.Shortcuts.Get(id); s.Target = @"C:\other.exe"; DataStore.Shortcuts.Update(s); }) };
    }

    [Theory]
    [MemberData(nameof(SyncedShortcutChanges))]
    public void A_change_of_a_synced_button_field_updates_its_change_record(string what, Action<long> change) {
      Shortcut s = DataStore.Shortcuts.CreateNote(null, windowId);
      SetShortcutRecord(s.ID, 5);
      long before = Now();
      change(s.ID);
      (long at, string by) = ShortcutRecord(s.ID);
      Assert.True(at >= before - 1000, what + ": " + at);
      Assert.Equal(DeviceIdentity.Id, by);
    }

    [Fact]
    public void A_move_updates_the_change_record_of_the_moved_button() {
      Shortcut g = DataStore.Shortcuts.CreateGroup(null, windowId);
      Shortcut s = DataStore.Shortcuts.CreateNote(null, windowId);
      SetShortcutRecord(s.ID, 5);
      DataStore.Shortcuts.UpdateParentShortcutIDAndParentWindowID(s.ID, g.ID, null);
      Assert.NotEqual(5, ShortcutRecord(s.ID).At);
    }

    [Fact]
    public void A_change_of_a_synced_panel_field_updates_its_change_record() {
      foreach (Action change in new Action[] {
        () => DataStore.Windows.UpdateTitle(windowId, "T"),
        () => DataStore.Windows.UpdateBorderColor(windowId, "#FF010203"),
        () => DataStore.Windows.UpdateTitleTextColor(windowId, "#FF010204"),
        () => DataStore.Windows.UpdateSolidIcons(windowId, false),
        () => DataStore.Windows.UpdateSingleClickToOpen(windowId, true),
        () => DataStore.Windows.UpdateShowVersionInTitle(windowId, false),
      }) {
        SetWindowRecord(windowId, 5);
        change();
        Assert.NotEqual(5, WindowRecord(windowId).At);
        Assert.Equal(DeviceIdentity.Id, WindowRecord(windowId).By);
      }
    }

    [Fact]
    public void A_change_of_a_local_field_leaves_the_change_record() {
      Shortcut g = DataStore.Shortcuts.CreateGroup(null, windowId);
      Shortcut note = DataStore.Shortcuts.CreateNote(null, windowId);
      SetShortcutRecord(g.ID, 5);
      SetShortcutRecord(note.ID, 5);
      SetWindowRecord(windowId, 5);
      DataStore.Shortcuts.UpdateIsGroupExpanded(g.ID, false);
      DataStore.Windows.UpdateLeftTopWidthHeight(windowId, 11, 12, 300, 400);
      DataStore.Windows.UpdateLeftTopScreenDeviceName(windowId, 13, 14, "OTHER");
      DataStore.Clones.Create(windowId, "S2", 1, 2);
      DataStore.NoteWindows.Save(new NoteWindowPlace { ShortcutID = note.ID, Left = 1, Top = 2, Width = 300, Height = 200 });
      DataStore.NoteWindows.Save(new NoteWindowPlace { ShortcutID = note.ID, Left = 5, Top = 6, Width = 310, Height = 210, State = 2, IsOpen = true, IsTopmost = true });
      DataStore.NoteWindows.SetOpen(note.ID, false);
      Assert.Equal((5L, "other-pc"), ShortcutRecord(g.ID));
      Assert.Equal((5L, "other-pc"), ShortcutRecord(note.ID));
      Assert.Equal((5L, "other-pc"), WindowRecord(windowId));
    }

    [Fact]
    public void Saving_the_same_value_again_is_not_a_change() {
      Shortcut s = DataStore.Shortcuts.CreateNote(null, windowId);
      DataStore.Shortcuts.UpdateName(s.ID, "same");
      SetShortcutRecord(s.ID, 5);
      DataStore.Shortcuts.UpdateName(s.ID, "same");
      DataStore.Shortcuts.Update(DataStore.Shortcuts.Get(s.ID));
      Assert.Equal(5, ShortcutRecord(s.ID).At);
    }

    [Fact]
    public void The_text_size_and_wrap_of_a_note_window_count_as_a_change_of_the_note() {
      Shortcut note = DataStore.Shortcuts.CreateNote(null, windowId);
      SetShortcutRecord(note.ID, 5);
      DataStore.NoteWindows.Save(new NoteWindowPlace { ShortcutID = note.ID, Left = 1, Top = 2, Width = 300, Height = 200, FontSize = 16 });
      Assert.NotEqual(5, ShortcutRecord(note.ID).At);
      SetShortcutRecord(note.ID, 5);
      DataStore.NoteWindows.Save(new NoteWindowPlace { ShortcutID = note.ID, Left = 1, Top = 2, Width = 300, Height = 200, FontSize = 16, IsWrapText = true });
      Assert.NotEqual(5, ShortcutRecord(note.ID).At);
    }

    [Fact]
    public void A_change_after_a_newer_change_from_another_PC_still_counts_as_newer() {
      Shortcut s = DataStore.Shortcuts.CreateNote(null, windowId);
      long future = Now() + 3_600_000;
      SetShortcutRecord(s.ID, future);
      DataStore.Shortcuts.UpdateName(s.ID, "edited here");
      Assert.Equal(future + 1, ShortcutRecord(s.ID).At);
    }

    [Fact]
    public void A_writer_that_sets_the_change_record_itself_keeps_it() {
      Shortcut s = DataStore.Shortcuts.CreateNote(null, windowId);
      db.Exec("UPDATE Shortcuts SET Name = 'from pc2', ChangedAt = 123, ChangedBy = 'pc2' WHERE ID = " + s.ID);
      Assert.Equal((123L, "pc2"), ShortcutRecord(s.ID));
    }

    // ---- Deleted items ----

    [Fact]
    public void Deleting_a_group_remembers_it_and_everything_inside() {
      Shortcut g = DataStore.Shortcuts.CreateGroup(null, windowId);
      Shortcut inner = DataStore.Shortcuts.CreateGroup(g.ID, null);
      Shortcut note = DataStore.Shortcuts.CreateNote(inner.ID, null);
      Shortcut file = DataStore.Shortcuts.Create(g.ID, null, "f", @"C:\f.exe", ".lnk", Identifiers.NewGuid());
      long before = Now();
      DataStore.Shortcuts.Delete(g.ID);
      List<DeletedItem> deleted = Deleted();
      Assert.Equal(new[] { g.SyncId, inner.SyncId, note.SyncId, file.SyncId }.OrderBy(x => x), deleted.Select(d => d.SyncId).OrderBy(x => x));
      Assert.All(deleted, d => {
        Assert.InRange(d.DeletedAt, before - 1000, Now() + 1000);
        Assert.Equal(DeviceIdentity.Id, d.DeletedBy);
      });
      Assert.Equal("Group", deleted.Single(d => d.SyncId == inner.SyncId).Kind);
      Assert.Equal("Note", deleted.Single(d => d.SyncId == note.SyncId).Kind);
    }

    [Fact]
    public void Deleting_a_panel_remembers_it_and_its_buttons_but_not_its_clones() {
      DesktopWindow w = DataStore.Windows.Create("S", 0, 0);
      DataStore.Clones.Create(w.ID, "S2", 1, 2);
      Shortcut g = DataStore.Shortcuts.CreateGroup(null, w.ID);
      Shortcut s = DataStore.Shortcuts.CreateSnippet(g.ID, null);
      DataStore.Windows.Delete(w.ID);
      Assert.Equal(new[] { w.SyncId, g.SyncId, s.SyncId }.OrderBy(x => x), Deleted().Select(d => d.SyncId).OrderBy(x => x));
      Assert.Equal("Panel", Deleted().Single(d => d.SyncId == w.SyncId).Kind);
    }

    [Fact]
    public void A_deleted_item_records_its_kind() {
      var made = new (Shortcut Row, string Kind)[] {
        (DataStore.Shortcuts.CreateGroup(null, windowId), "Group"),
        (DataStore.Shortcuts.Create(null, windowId, "f", @"C:\f.exe", ".lnk", Identifiers.NewGuid()), "Shortcut"),
        (DataStore.Shortcuts.Create(null, windowId, "w", "https://example.com", ".url", Identifiers.NewGuid()), "Link"),
        (DataStore.Shortcuts.CreateNote(null, windowId), "Note"),
        (DataStore.Shortcuts.CreateSnippet(null, windowId), "Snippet"),
        (DataStore.Shortcuts.CreateProcessCommand(null, windowId), "RunCommand"),
      };
      foreach (var item in made) {
        DataStore.Shortcuts.Delete(item.Row.ID);
        Assert.Equal(item.Kind, Deleted().Single(d => d.SyncId == item.Row.SyncId).Kind);
      }
    }

    [Fact]
    public void Adding_an_item_back_removes_its_deleted_record() {
      Shortcut s = DataStore.Shortcuts.CreateNote(null, windowId);
      DataStore.Shortcuts.Delete(s.ID);
      Assert.Single(Deleted());
      s.ID = 0;
      DataStore.Shortcuts.Add(s);
      Assert.Empty(Deleted());
      Assert.Equal(s.SyncId, DataStore.Shortcuts.Get(s.ID).SyncId);
    }

    // ---- Device identity ----

    [Fact]
    public void This_PC_has_a_fixed_device_id_and_a_name_that_defaults_to_the_computer_name() {
      string id = DeviceIdentity.Id;
      Assert.Matches(GuidText, id);
      Assert.Equal(id, DeviceIdentity.Id);
      Assert.Equal(id, DataStore.AppSettings.Get(DeviceIdentity.IdKey));
      Assert.Equal(Environment.MachineName, DeviceIdentity.Name);
      DeviceIdentity.Name = "Home PC";
      Assert.Equal("Home PC", DeviceIdentity.Name);
      DeviceIdentity.Name = "  ";
      Assert.Equal(Environment.MachineName, DeviceIdentity.Name);
      Assert.Equal(id, DeviceIdentity.Id);
      Assert.DoesNotContain(DeviceIdentity.IdKey, PortableSettings.Keys);
      Assert.DoesNotContain(DeviceIdentity.NameKey, PortableSettings.Keys);
    }

    [Fact]
    public void A_lost_device_id_is_made_again_once() {
      db.Exec("DELETE FROM AppSettings WHERE Key = 'DeviceId'");
      string id = DeviceIdentity.Id;
      Assert.Matches(GuidText, id);
      Assert.Equal(id, DeviceIdentity.Id);
    }

    // ---- Database version 27 ----

    [Fact]
    public void Version_27_is_a_known_step() {
      Assert.True(AppConstants.DatabaseVersion >= 27);
      Assert.Single(SchemaManager.Migrations, m => m.Version == 27);
    }

    /// <summary>
    /// Turns the current database back into version 26: no sync columns, triggers, indexes or DeletedItems table, no
    /// device id, and an integer Position with the given values (by ID).
    /// </summary>
    internal static void MakeV26(TempDb db, IDictionary<long, long> positions) {
      db.DropSyncTriggers();
      db.Exec("DROP INDEX IF EXISTS UX_MDSHOWindows_SyncId; DROP INDEX IF EXISTS UX_Shortcuts_SyncId; DROP TABLE IF EXISTS DeletedItems;"
        + "DELETE FROM AppSettings WHERE Key = 'DeviceId';");
      foreach (string table in new[] { "MDSHOWindows", "Shortcuts" }) {
        foreach (string column in new[] { "SyncId", "SortKey", "ChangedAt", "ChangedBy" }) {
          db.Exec("ALTER TABLE " + table + " DROP COLUMN " + column + ";");
        }
      }
      db.Exec("ALTER TABLE Shortcuts ADD COLUMN Position INTEGER NOT NULL DEFAULT 0;");
      foreach (KeyValuePair<long, long> p in positions) {
        db.Exec("UPDATE Shortcuts SET Position = " + p.Value + " WHERE ID = " + p.Key);
      }
      db.Exec("PRAGMA user_version = 26;");
    }

    [Fact]
    public void A_v26_database_gets_sync_ids_change_records_and_sort_keys_in_the_same_order() {
      DesktopWindow w2 = DataStore.Windows.Create("S", 0, 0);
      Shortcut a = DataStore.Shortcuts.CreateGroup(null, windowId);
      Shortcut b = DataStore.Shortcuts.CreateNote(null, windowId);
      Shortcut c = DataStore.Shortcuts.CreateSnippet(null, windowId);
      Shortcut d = DataStore.Shortcuts.CreateNote(null, windowId);
      Shortcut a1 = DataStore.Shortcuts.CreateNote(a.ID, null);
      Shortcut a2 = DataStore.Shortcuts.CreateNote(a.ID, null);
      Shortcut e = DataStore.Shortcuts.CreateNote(null, w2.ID);
      // Roots of the first panel: c (0), then b and d tie at 1 (ID decides), then a (7). In the group: a2, a1.
      MakeV26(db, new Dictionary<long, long> { [a.ID] = 7, [b.ID] = 1, [c.ID] = 0, [d.ID] = 1, [a1.ID] = 3, [a2.ID] = 2, [e.ID] = 0 });
      long before = Now();
      DataStore.Upgrade();
      long after = Now();

      Assert.Equal(AppConstants.DatabaseVersion, db.Scalar("PRAGMA user_version"));
      Assert.Single(Directory.GetFiles(db.Folder, "test.sqlite3.bak-v26-*"));
      Assert.Equal(0, db.Scalar("SELECT count(*) FROM pragma_table_info('Shortcuts') WHERE name = 'Position'"));
      Assert.Equal(new[] { c.ID, b.ID, d.ID, a.ID }, DataStore.Shortcuts.GetRoots(windowId).Select(s => s.ID));
      Assert.Equal(new[] { a2.ID, a1.ID }, DataStore.Shortcuts.GetChildren(a.ID).Select(s => s.ID));
      Assert.Equal(new[] { windowId, w2.ID }, DataStore.Windows.GetAll().Select(w => w.ID));
      List<DesktopWindow> loaded = DataStore.Windows.LoadAll();
      Assert.Equal(new[] { c.ID, b.ID, d.ID, a.ID }, loaded[0].RootShortcuts.Select(s => s.ID));

      string device = DeviceIdentity.Id;
      Assert.Matches(GuidText, device);
      List<Shortcut> rows = DataStore.Shortcuts.GetAll();
      List<DesktopWindow> windows = DataStore.Windows.GetAll();
      List<string> ids = rows.Select(s => s.SyncId).Concat(windows.Select(w => w.SyncId)).ToList();
      Assert.All(ids, id => Assert.Matches(GuidText, id));
      Assert.Equal(ids.Count, ids.Distinct().Count());
      Assert.All(rows.Select(s => (s.ChangedAt, s.ChangedBy)).Concat(windows.Select(w => (w.ChangedAt, w.ChangedBy))), r => {
        Assert.InRange(r.ChangedAt, before - 1000, after + 1000);
        Assert.Equal(device, r.ChangedBy);
      });
      Assert.All(rows.Select(s => s.SortKey).Concat(windows.Select(w => w.SortKey)), k => Assert.True(SortKeys.IsValid(k), k));
      Assert.Empty(Deleted());
      // The triggers work after the upgrade.
      DataStore.Shortcuts.Delete(e.ID);
      Assert.Single(Deleted());
    }

    [Fact]
    public void Running_the_v27_step_again_changes_nothing() {
      Shortcut s = DataStore.Shortcuts.CreateNote(null, windowId);
      Shortcut before = DataStore.Shortcuts.Get(s.ID);
      string device = DeviceIdentity.Id;
      using (SqliteConnection c = db.Open()) {
        SchemaManager.Migrations.Single(m => m.Version == 27).Apply(c);
      }
      Shortcut again = DataStore.Shortcuts.Get(s.ID);
      Assert.Equal((before.SyncId, before.SortKey, before.ChangedAt, before.ChangedBy), (again.SyncId, again.SortKey, again.ChangedAt, again.ChangedBy));
      Assert.Equal(device, DeviceIdentity.Id);
    }
  }
}
