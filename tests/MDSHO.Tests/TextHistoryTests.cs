using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using MDSHO.Common;
using MDSHO.Core;
using MDSHO.Data;
using MDSHO.ViewModels;
using Xunit;

namespace MDSHO.Tests {

  /// <summary>
  /// Text history (1.21.0): two earlier copies of a note's or snippet's text (Shortcuts.TextPrevious1/2) and of a comment
  /// (Shortcuts.CommentPrevious1/2), each with the time it was kept, database version 22. The History button in the
  /// title bar lists them and puts one back. The history goes wherever the text goes.
  /// </summary>
  public class TextHistoryTests : IDisposable {
    private TempDb db = new TempDb();
    private readonly AppViewModel app = new AppViewModel();
    private readonly DesktopWindowViewModel win;
    private readonly string work = Path.Combine(Path.GetTempPath(), "MDSHO.Tests", "history-" + Guid.NewGuid().ToString("N"));
    private static readonly TextHistorySlots Some = new TextHistorySlots("before text", "2026-11-21T08:00:00", "earlier text", "2026-11-20T18:30:00");

    public TextHistoryTests() {
      Directory.CreateDirectory(work);
      win = app.AddWindow("TEST", 0, 0);
    }

    public void Dispose() {
      db.Dispose();
      try { Directory.Delete(work, true); } catch (IOException) { }
    }

    private ShortcutViewModel NewNote() {
      win.NewNoteShortcutCommand.Execute(null);
      ShortcutViewModel note = win.RootShortcuts.Last();
      note.CancelEdit();
      note.Name = "Checklist";
      return note;
    }

    private ShortcutViewModel NewCommand() {
      ShortcutViewModel command = ViewModelFactory.NewProcessCommandShortcut(app, null, win);
      win.RootShortcuts.Add(command);
      command.Name = "Ping";
      return command;
    }

    private long Columns(string column) {
      return db.Scalar("SELECT count(*) FROM pragma_table_info('Shortcuts') WHERE name = '" + column + "' COLLATE NOCASE");
    }

    private static readonly string[] HistoryColumns = {
      "TextPrevious1", "TextPrevious1Time", "TextPrevious2", "TextPrevious2Time",
      "CommentPrevious1", "CommentPrevious1Time", "CommentPrevious2", "CommentPrevious2Time" };

    // ---- The data ----

    [Fact]
    public void A_new_button_has_an_empty_history() {
      ShortcutViewModel note = NewNote();
      Shortcut row = DataStore.Shortcuts.Get(note.ID);
      Assert.Equal(TextHistorySlots.Empty, Slots(row.TextPrevious1, row.TextPrevious1Time, row.TextPrevious2, row.TextPrevious2Time));
      Assert.Equal(TextHistorySlots.Empty, Slots(row.CommentPrevious1, row.CommentPrevious1Time, row.CommentPrevious2, row.CommentPrevious2Time));
      Assert.Equal(TextHistorySlots.Empty, note.TextHistory);
      Assert.Equal(TextHistorySlots.Empty, note.CommentHistory);
    }

    private static TextHistorySlots Slots(string p1, string t1, string p2, string t2) => new TextHistorySlots(p1, t1, p2, t2);

    [Fact]
    public void The_view_model_saves_each_history_apart_and_a_reload_keeps_them() {
      ShortcutViewModel note = NewNote();
      List<string> raised = new List<string>();
      note.PropertyChanged += (s, e) => raised.Add(e.PropertyName);
      note.SaveTextHistory(Some);
      note.SaveCommentHistory(Some with { Previous1 = "comment before" });
      Assert.Contains(nameof(ShortcutViewModel.TextHistory), raised);
      Assert.Contains(nameof(ShortcutViewModel.CommentHistory), raised);
      ShortcutViewModel reloaded = ViewModelFactory.ShortcutToViewModel(app, null, DataStore.Shortcuts.Get(note.ID));
      Assert.Equal(Some, reloaded.TextHistory);
      Assert.Equal("comment before", reloaded.CommentHistory.Previous1);
    }

    [Fact]
    public void A_copy_carries_the_history() {
      ShortcutViewModel note = NewNote();
      note.SaveTextHistory(Some);
      note.SaveCommentHistory(Some);
      ShortcutViewModel copy = ViewModelFactory.CopyShortcut(note, null, win);
      Assert.Equal(Some, copy.TextHistory);
      Assert.Equal(Some, ViewModelFactory.ShortcutToViewModel(app, null, DataStore.Shortcuts.Get(copy.ID)).CommentHistory);
    }

    [Fact]
    public void Deleting_the_button_deletes_its_history() {
      ShortcutViewModel note = NewNote();
      note.SaveTextHistory(Some);
      DataStore.Shortcuts.Delete(note.ID);
      Assert.Equal(0, db.Scalar("SELECT count(*) FROM Shortcuts WHERE TextPrevious1 = 'before text'"));
    }

    [Fact]
    public void Export_and_import_keep_the_history() {
      ShortcutViewModel note = NewNote();
      note.SaveTextHistory(Some);
      note.SaveCommentHistory(Some);
      string zip = Path.Combine(work, "export.zip");
      DataExporter.Export(zip);
      JsonObject row = ReadData(zip)["shortcuts"].AsArray().Single(s => (long)s["id"] == note.ID).AsObject();
      Assert.Equal("before text", (string)row["textPrevious1"]);
      Assert.Equal("2026-11-20T18:30:00", (string)row["commentPrevious2Time"]);
      DataStore.Shortcuts.UpdateTextHistory(note.ID, TextHistorySlots.Empty);
      DataImporter.Import(zip, ImportMode.Replace);
      Shortcut back = DataStore.Shortcuts.Get(note.ID);
      Assert.Equal(Some, Slots(back.TextPrevious1, back.TextPrevious1Time, back.TextPrevious2, back.TextPrevious2Time));
      Assert.Equal(Some, Slots(back.CommentPrevious1, back.CommentPrevious1Time, back.CommentPrevious2, back.CommentPrevious2Time));
    }

    [Fact]
    public void An_export_from_before_history_imports_with_an_empty_history() {
      ShortcutViewModel note = NewNote();
      note.SaveTextHistory(Some);
      string zip = Path.Combine(work, "export.zip");
      DataExporter.Export(zip);
      string old = Path.Combine(work, "old.zip");
      File.Copy(zip, old);
      using (ZipArchive archive = ZipFile.Open(old, ZipArchiveMode.Update)) {
        JsonNode data = JsonNode.Parse(ReadEntryText(archive, "data.json"));
        foreach (JsonNode s in data["shortcuts"].AsArray()) {
          foreach (string column in HistoryColumns) {
            s.AsObject().Remove(char.ToLowerInvariant(column[0]) + column.Substring(1));
          }
        }
        JsonNode manifest = JsonNode.Parse(ReadEntryText(archive, "manifest.json"));
        manifest["schemaVersion"] = 21;
        WriteEntry(archive, "data.json", data.ToJsonString());
        WriteEntry(archive, "manifest.json", manifest.ToJsonString());
      }
      DataImporter.Import(old, ImportMode.Replace);
      Shortcut only = Assert.Single(DataStore.Shortcuts.GetAll());
      Assert.Equal(TextHistorySlots.Empty, Slots(only.TextPrevious1, only.TextPrevious1Time, only.TextPrevious2, only.TextPrevious2Time));
    }

    [Fact]
    public void A_v21_database_gets_the_history_columns_empty_after_a_backup() {
      ShortcutViewModel note = NewNote();
      DataStore.Shortcuts.UpdateNoteText(note.ID, "kept text");
      db.Exec(string.Join(" ", HistoryColumns.Select(c => "ALTER TABLE Shortcuts DROP COLUMN " + c + ";"))
        + " ALTER TABLE Shortcuts DROP COLUMN CommentFont; ALTER TABLE Shortcuts DROP COLUMN CommentFontSize; PRAGMA user_version = 21;");
      DataStore.Upgrade();
      Assert.All(HistoryColumns, c => Assert.Equal(1, Columns(c)));
      Shortcut row = DataStore.Shortcuts.Get(note.ID);
      Assert.Equal(("kept text", "", ""), (row.NoteText, row.TextPrevious1, row.CommentPrevious2Time));
      Assert.Single(Directory.GetFiles(db.Folder, "test.sqlite3.bak-v21-*"));
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
