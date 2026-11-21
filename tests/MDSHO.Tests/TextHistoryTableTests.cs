using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.Json.Nodes;
using MDSHO.Common;
using MDSHO.Core;
using MDSHO.Data;
using MDSHO.ViewModels;
using Xunit;

namespace MDSHO.Tests {

  /// <summary>
  /// The text history table (1.22.0, database version 24): History rows per button and field ("Text" for a note's or
  /// snippet's text, "Comment" for any button's comment), newest first, at most "keep N" (Settings, default 10, 0 to 50,
  /// 0 = off). A copied button starts with none; exports and backups carry them; older data moves its two earlier
  /// copies into the table.
  /// </summary>
  public class TextHistoryTableTests : IDisposable {
    private TempDb db = new TempDb();
    private readonly AppViewModel app = new AppViewModel();
    private readonly DesktopWindowViewModel win;
    private readonly string work = Path.Combine(Path.GetTempPath(), "MDSHO.Tests", "history-table-" + Guid.NewGuid().ToString("N"));
    private DateTime now = new DateTime(2026, 11, 21, 9, 0, 0);

    public TextHistoryTableTests() {
      Directory.CreateDirectory(work);
      win = app.AddWindow("TEST", 0, 0);
    }

    public void Dispose() {
      db.Dispose();
      try { Directory.Delete(work, true); } catch (IOException) { }
    }

    private ShortcutViewModel NewNote(string text = "milk") {
      win.NewNoteShortcutCommand.Execute(null);
      ShortcutViewModel note = win.RootShortcuts.Last();
      note.CancelEdit();
      note.Name = "Checklist";
      note.SaveNoteText(text);
      return note;
    }

    private HistoryEntry Keep(ShortcutViewModel s, string field, string text) {
      now = now.AddMinutes(1);
      return s.KeepHistory(field, text, now);
    }

    private static string[] Texts(ShortcutViewModel s, string field) => s.History(field).Select(h => h.Text).ToArray();

    private long Columns(string table, string column) {
      return db.Scalar("SELECT count(*) FROM pragma_table_info('" + table + "') WHERE name = '" + column + "' COLLATE NOCASE");
    }

    // ---- Keeping copies ----

    [Fact]
    public void A_new_button_has_no_history() {
      ShortcutViewModel note = NewNote();
      Assert.Empty(note.History(HistoryFields.Text));
      Assert.Empty(note.History(HistoryFields.Comment));
      Assert.Empty(DataStore.History.GetAll());
    }

    [Fact]
    public void Copies_are_listed_newest_first_for_each_field_with_their_time() {
      ShortcutViewModel note = NewNote();
      Keep(note, HistoryFields.Text, "a");
      Keep(note, HistoryFields.Text, "b");
      HistoryEntry c = Keep(note, HistoryFields.Comment, "c");
      Assert.Equal(new[] { "b", "a" }, Texts(note, HistoryFields.Text));
      Assert.Equal(new[] { "c" }, Texts(note, HistoryFields.Comment));
      Assert.Equal("2026-11-21T09:03:00", c.KeptAt);
      HistoryEntry row = DataStore.History.Get(note.ID, HistoryFields.Comment).Single();
      Assert.Equal((note.ID, "Comment", "c", "2026-11-21T09:03:00"), (row.ShortcutID, row.Field, row.Text, row.KeptAt));
    }

    [Fact]
    public void A_copy_equal_to_the_newest_is_not_kept_but_an_empty_text_is() {
      ShortcutViewModel note = NewNote();
      Assert.NotNull(Keep(note, HistoryFields.Text, "a"));
      Assert.Null(Keep(note, HistoryFields.Text, "a"));
      Assert.NotNull(Keep(note, HistoryFields.Text, "   "));
      Assert.Null(Keep(note, HistoryFields.Text, ""));
      Assert.Equal(new[] { "", "a" }, Texts(note, HistoryFields.Text));
    }

    [Fact]
    public void At_most_keep_n_copies_stay_and_the_oldest_drop() {
      TextHistorySetting.SetKeep(3);
      ShortcutViewModel note = NewNote();
      foreach (string t in new[] { "1", "2", "3", "4", "5" }) {
        Keep(note, HistoryFields.Text, t);
      }
      Keep(note, HistoryFields.Comment, "c");
      Assert.Equal(new[] { "5", "4", "3" }, Texts(note, HistoryFields.Text));
      Assert.Equal(new[] { "c" }, Texts(note, HistoryFields.Comment));
    }

    [Fact]
    public void Keep_0_turns_it_off() {
      TextHistorySetting.SetKeep(0);
      ShortcutViewModel note = NewNote();
      Assert.Null(Keep(note, HistoryFields.Text, "a"));
      Assert.Empty(DataStore.History.GetAll());
    }

    [Fact]
    public void The_setting_is_10_by_default_and_stays_in_0_to_50() {
      Assert.Equal(10, TextHistorySetting.DefaultKeep);
      Assert.Equal(10, TextHistorySetting.Keep);
      TextHistorySetting.SetKeep(50);
      Assert.Equal("50", DataStore.AppSettings.Get(TextHistorySetting.SettingKey));
      Assert.Equal(50, TextHistorySetting.Keep);
      TextHistorySetting.SetKeep(80);
      Assert.Equal(50, TextHistorySetting.Keep);
      TextHistorySetting.SetKeep(-1);
      Assert.Equal(0, TextHistorySetting.Keep);
      DataStore.AppSettings.Set(TextHistorySetting.SettingKey, "many");
      Assert.Equal(10, TextHistorySetting.Keep);
      Assert.Equal(Enumerable.Range(0, 51), TextHistorySetting.Choices);
    }

    [Fact]
    public void Lowering_the_setting_trims_the_oldest_of_every_button_at_once() {
      ShortcutViewModel a = NewNote();
      ShortcutViewModel b = NewNote();
      foreach (string t in new[] { "1", "2", "3", "4" }) {
        Keep(a, HistoryFields.Text, t);
        Keep(b, HistoryFields.Comment, t);
      }
      TextHistorySetting.SetKeep(2);
      Assert.Equal(new[] { "4", "3" }, Texts(a, HistoryFields.Text));
      Assert.Equal(new[] { "4", "3" }, Texts(b, HistoryFields.Comment));
      TextHistorySetting.SetKeep(0);
      Assert.Empty(DataStore.History.GetAll());
    }

    [Fact]
    public void Clearing_removes_one_field_of_one_button() {
      ShortcutViewModel a = NewNote();
      ShortcutViewModel b = NewNote();
      Keep(a, HistoryFields.Text, "t");
      Keep(a, HistoryFields.Comment, "c");
      Keep(b, HistoryFields.Text, "t");
      a.ClearHistory(HistoryFields.Text);
      Assert.Empty(a.History(HistoryFields.Text));
      Assert.Single(a.History(HistoryFields.Comment));
      Assert.Single(b.History(HistoryFields.Text));
    }

    [Fact]
    public void Deleting_one_copy_leaves_the_others() {
      ShortcutViewModel a = NewNote();
      HistoryEntry first = Keep(a, HistoryFields.Text, "1");
      Keep(a, HistoryFields.Text, "2");
      a.DeleteHistory(first.ID);
      Assert.Equal(new[] { "2" }, Texts(a, HistoryFields.Text));
    }

    [Fact]
    public void Deleting_the_button_deletes_its_history() {
      ShortcutViewModel a = NewNote();
      Keep(a, HistoryFields.Text, "t");
      DataStore.Shortcuts.Delete(a.ID);
      Assert.Empty(DataStore.History.GetAll());
    }

    [Fact]
    public void A_copied_button_starts_with_no_history() {
      ShortcutViewModel a = NewNote();
      Keep(a, HistoryFields.Text, "t");
      Keep(a, HistoryFields.Comment, "c");
      ShortcutViewModel copy = ViewModelFactory.CopyShortcut(a, null, win);
      Assert.Empty(copy.History(HistoryFields.Text));
      Assert.Empty(copy.History(HistoryFields.Comment));
      Assert.Equal(2, DataStore.History.GetAll().Count);
    }

    // ---- The table and database version 24 ----

    [Fact]
    public void The_history_table_has_its_columns_index_and_cascade() {
      foreach (string column in new[] { "ID", "ShortcutID", "Field", "Text", "KeptAt" }) {
        Assert.Equal(1, Columns("History", column));
      }
      Assert.Equal(1, db.Scalar("SELECT count(*) FROM pragma_index_list('History') l JOIN pragma_index_info(l.name) i WHERE i.name = 'ShortcutID'"));
      Assert.Equal("CASCADE", db.ScalarText("SELECT on_delete FROM pragma_foreign_key_list('History') WHERE \"table\" = 'Shortcuts'"));
      foreach (string old in new[] { "TextPrevious1", "TextPrevious1Time", "TextPrevious2", "TextPrevious2Time",
          "CommentPrevious1", "CommentPrevious1Time", "CommentPrevious2", "CommentPrevious2Time" }) {
        Assert.Equal(0, Columns("Shortcuts", old));
      }
    }

    [Fact]
    public void Version_24_is_a_known_step() {
      Assert.True(AppConstants.DatabaseVersion >= 24);
      Assert.Single(SchemaManager.Migrations, m => m.Version == 24);
    }

    // Makes the database look like version 23: the two earlier copies in Shortcuts, no History table.
    private void MakeVersion23(long noteId, long otherId) {
      db.Exec("DROP TABLE History;");
      foreach (string column in new[] { "TextPrevious1", "TextPrevious1Time", "TextPrevious2", "TextPrevious2Time",
          "CommentPrevious1", "CommentPrevious1Time", "CommentPrevious2", "CommentPrevious2Time" }) {
        db.Exec("ALTER TABLE Shortcuts ADD COLUMN " + column + " TEXT NOT NULL DEFAULT '';");
      }
      db.Exec("UPDATE Shortcuts SET TextPrevious1 = 'before', TextPrevious1Time = '2026-11-20T18:30:00', TextPrevious2 = 'earlier', "
        + "TextPrevious2Time = '2026-11-19T10:00:00', CommentPrevious1 = 'old comment', CommentPrevious1Time = '2026-11-18T08:00:00' WHERE ID = " + noteId + ";");
      db.Exec("UPDATE Shortcuts SET CommentPrevious2 = 'lone', CommentPrevious2Time = '2026-11-17T07:00:00' WHERE ID = " + otherId + ";");
      db.Exec("PRAGMA user_version = 23;");
    }

    [Fact]
    public void A_v23_database_moves_the_earlier_copies_into_history_and_drops_the_columns_after_a_backup() {
      ShortcutViewModel note = NewNote("milk");
      ShortcutViewModel other = NewNote("bread");
      MakeVersion23(note.ID, other.ID);
      DataStore.Upgrade();
      Assert.Equal(AppConstants.DatabaseVersion, db.Scalar("PRAGMA user_version"));
      Assert.Single(Directory.GetFiles(db.Folder, "test.sqlite3.bak-v23-*"));
      Assert.Equal(0, Columns("Shortcuts", "TextPrevious1"));
      Assert.Equal(0, Columns("Shortcuts", "CommentPrevious2Time"));
      Assert.Equal(new[] { ("before", "2026-11-20T18:30:00"), ("earlier", "2026-11-19T10:00:00") },
        DataStore.History.Get(note.ID, HistoryFields.Text).Select(h => (h.Text, h.KeptAt)).ToArray());
      Assert.Equal(new[] { "old comment" }, DataStore.History.Get(note.ID, HistoryFields.Comment).Select(h => h.Text).ToArray());
      Assert.Equal(new[] { "lone" }, DataStore.History.Get(other.ID, HistoryFields.Comment).Select(h => h.Text).ToArray());
      Assert.Empty(DataStore.History.Get(other.ID, HistoryFields.Text));
      Assert.Equal(("milk", "bread"), (DataStore.Shortcuts.Get(note.ID).NoteText, DataStore.Shortcuts.Get(other.ID).NoteText));
    }

    [Fact]
    public void The_export_made_before_the_upgrade_carries_the_earlier_copies_as_history() {
      ShortcutViewModel note = NewNote("milk");
      ShortcutViewModel other = NewNote("bread");
      MakeVersion23(note.ID, other.ID);
      string zip = Path.Combine(work, "before.zip");
      DataExporter.Export(zip, new ExportOptions { BeforeUpgrade = true });
      JsonArray history = ReadData(zip)["history"].AsArray();
      Assert.Equal(4, history.Count);
      Assert.Contains(history, h => (long)h["shortcutID"] == note.ID && (string)h["field"] == "Text" && (string)h["text"] == "earlier" && (string)h["keptAt"] == "2026-11-19T10:00:00");
      Assert.Contains(history, h => (long)h["shortcutID"] == other.ID && (string)h["field"] == "Comment" && (string)h["text"] == "lone");
    }

    // ---- Export, import and backups ----

    [Fact]
    public void Export_and_import_keep_the_history() {
      ShortcutViewModel note = NewNote();
      Keep(note, HistoryFields.Text, "t1");
      Keep(note, HistoryFields.Text, "t2");
      Keep(note, HistoryFields.Comment, "c1");
      string zip = Path.Combine(work, "export.zip");
      DataExporter.Export(zip);
      JsonArray history = ReadData(zip)["history"].AsArray();
      Assert.Equal(3, history.Count);
      Assert.All(history, h => Assert.Equal(note.ID, (long)h["shortcutID"]));
      Assert.Contains(history, h => (string)h["text"] == "t2" && (string)h["field"] == "Text" && (string)h["keptAt"] == "2026-11-21T09:02:00");
      note.ClearHistory(HistoryFields.Text);
      DataImporter.Import(zip, ImportMode.Replace);
      Assert.Equal(new[] { "t2", "t1" }, DataStore.History.Get(note.ID, HistoryFields.Text).Select(h => h.Text).ToArray());
      Assert.Equal(new[] { "c1" }, DataStore.History.Get(note.ID, HistoryFields.Comment).Select(h => h.Text).ToArray());
      DataImporter.Import(zip, ImportMode.AddAsNew);
      Shortcut added = DataStore.Shortcuts.GetAll().Single(s => s.ID != note.ID);
      Assert.Equal(new[] { "t2", "t1" }, DataStore.History.Get(added.ID, HistoryFields.Text).Select(h => h.Text).ToArray());
      Assert.Equal(6, DataStore.History.GetAll().Count);
    }

    [Fact]
    public void An_import_keeps_at_most_the_setting() {
      ShortcutViewModel note = NewNote();
      foreach (string t in new[] { "1", "2", "3" }) {
        Keep(note, HistoryFields.Text, t);
      }
      string zip = Path.Combine(work, "export.zip");
      DataExporter.Export(zip);
      DataStore.AppSettings.Set(TextHistorySetting.SettingKey, "2");
      DataImporter.Import(zip, ImportMode.Replace);
      Assert.Equal(new[] { "3", "2" }, DataStore.History.Get(note.ID, HistoryFields.Text).Select(h => h.Text).ToArray());
    }

    [Fact]
    public void The_setting_goes_with_exports() {
      Assert.Contains(TextHistorySetting.SettingKey, PortableSettings.Keys);
    }

    [Fact]
    public void An_export_from_before_version_24_imports_its_earlier_copies_as_history() {
      ShortcutViewModel note = NewNote("milk");
      string zip = Path.Combine(work, "export.zip");
      DataExporter.Export(zip);
      string old = Path.Combine(work, "old.zip");
      File.Copy(zip, old);
      using (ZipArchive archive = ZipFile.Open(old, ZipArchiveMode.Update)) {
        JsonNode data = JsonNode.Parse(ReadEntryText(archive, "data.json"));
        data.AsObject().Remove("history");
        JsonObject s = data["shortcuts"].AsArray().Single().AsObject();
        s["textPrevious1"] = "before";
        s["textPrevious1Time"] = "2026-11-20T18:30:00";
        s["textPrevious2"] = "earlier";
        s["textPrevious2Time"] = "2026-11-19T10:00:00";
        s["commentPrevious1"] = "";
        s["commentPrevious1Time"] = "";
        s["commentPrevious2"] = "old comment";
        s["commentPrevious2Time"] = "2026-11-18T08:00:00";
        JsonNode manifest = JsonNode.Parse(ReadEntryText(archive, "manifest.json"));
        manifest["schemaVersion"] = 23;
        WriteEntry(archive, "data.json", data.ToJsonString());
        WriteEntry(archive, "manifest.json", manifest.ToJsonString());
      }
      DataImporter.Import(old, ImportMode.Replace);
      Assert.Equal(new[] { ("before", "2026-11-20T18:30:00"), ("earlier", "2026-11-19T10:00:00") },
        DataStore.History.Get(note.ID, HistoryFields.Text).Select(h => (h.Text, h.KeptAt)).ToArray());
      Assert.Equal(new[] { "old comment" }, DataStore.History.Get(note.ID, HistoryFields.Comment).Select(h => h.Text).ToArray());
      Assert.Equal("milk", DataStore.Shortcuts.Get(note.ID).NoteText);
    }

    [Fact]
    public void An_export_with_history_of_a_button_not_in_the_file_is_refused() {
      ShortcutViewModel note = NewNote();
      Keep(note, HistoryFields.Text, "t");
      string zip = Path.Combine(work, "export.zip");
      DataExporter.Export(zip);
      using (ZipArchive archive = ZipFile.Open(zip, ZipArchiveMode.Update)) {
        JsonNode data = JsonNode.Parse(ReadEntryText(archive, "data.json"));
        data["history"][0]["shortcutID"] = 9999;
        WriteEntry(archive, "data.json", data.ToJsonString());
      }
      Assert.Throws<DataPortabilityException>(() => DataImporter.Inspect(zip));
    }

    private static JsonNode ReadData(string zip) {
      using (ZipArchive archive = ZipFile.OpenRead(zip)) {
        return JsonNode.Parse(ReadEntryText(archive, "data.json"));
      }
    }

    private static string ReadEntryText(ZipArchive archive, string name) {
      using (StreamReader reader = new StreamReader(archive.GetEntry(name).Open())) {
        return reader.ReadToEnd();
      }
    }

    private static void WriteEntry(ZipArchive archive, string name, string text) {
      archive.GetEntry(name).Delete();
      using (StreamWriter writer = new StreamWriter(archive.CreateEntry(name).Open())) {
        writer.Write(text);
      }
    }
  }
}
