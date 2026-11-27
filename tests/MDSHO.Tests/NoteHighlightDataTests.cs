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
  /// Saved colour highlights of notes (1.26.0): the terms and their colours are kept in Shortcuts.NoteHighlights, apart
  /// from the note's text, database version 25. Copies, export, import and backups carry them; older exports import with none.
  /// </summary>
  public class NoteHighlightDataTests : IDisposable {
    private TempDb db = new TempDb();
    private readonly AppViewModel app = new AppViewModel();
    private readonly DesktopWindowViewModel win;
    private readonly string work = Path.Combine(Path.GetTempPath(), "MDSHO.Tests", "note-highlights-" + Guid.NewGuid().ToString("N"));

    public NoteHighlightDataTests() {
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
      note.Name = "Weekly list";
      note.SaveNoteText("Pay the gas bill\r\nBook the dentist");
      return note;
    }

    private static string Highlights(params (string Term, string Color)[] terms) {
      string stored = "";
      foreach ((string term, string color) in terms) {
        stored = NoteHighlights.With(stored, term, color);
      }
      return stored;
    }

    private long Columns(string table, string column) {
      return db.Scalar("SELECT count(*) FROM pragma_table_info('" + table + "') WHERE name = '" + column + "' COLLATE NOCASE");
    }

    // ---- The data ----

    [Fact]
    public void A_new_note_has_no_highlights() {
      ShortcutViewModel note = NewNote();
      Assert.Equal("", DataStore.Shortcuts.Get(note.ID).NoteHighlights);
      Assert.Equal("", note.NoteHighlights);
    }

    [Fact]
    public void Saving_highlights_saves_them_at_once_apart_from_the_text_and_tells_the_window() {
      ShortcutViewModel note = NewNote();
      List<string> raised = new List<string>();
      note.PropertyChanged += (s, e) => raised.Add(e.PropertyName);
      string stored = Highlights(("pay", "Yellow"), ("dentist", "Green"));
      note.SaveNoteHighlights(stored);
      Shortcut row = DataStore.Shortcuts.Get(note.ID);
      Assert.Equal(stored, row.NoteHighlights);
      Assert.Equal(stored, note.NoteHighlights);
      // The note's text stays plain.
      Assert.Equal("Pay the gas bill\r\nBook the dentist", row.NoteText);
      Assert.Contains(nameof(ShortcutViewModel.NoteHighlights), raised);
    }

    [Fact]
    public void The_panel_reloaded_from_the_database_keeps_the_highlights() {
      ShortcutViewModel note = NewNote();
      string stored = Highlights(("pay", "Yellow"));
      note.SaveNoteHighlights(stored);
      Shortcut row = DataStore.Shortcuts.GetAll().Single(s => s.ID == note.ID);
      Assert.Equal(stored, ViewModelFactory.ShortcutToViewModel(app, null, row).NoteHighlights);
    }

    [Fact]
    public void A_copy_carries_the_highlights() {
      ShortcutViewModel note = NewNote();
      string stored = Highlights(("dentist", "Pink"));
      note.SaveNoteHighlights(stored);
      ShortcutViewModel copy = ViewModelFactory.CopyShortcut(note, null, win);
      Assert.Equal(stored, copy.NoteHighlights);
      Assert.Equal(stored, DataStore.Shortcuts.Get(copy.ID).NoteHighlights);
    }

    // ---- Export, import and backups ----

    [Fact]
    public void Export_and_import_keep_the_highlights() {
      ShortcutViewModel note = NewNote();
      string stored = Highlights(("pay", "Yellow"), ("dentist", "Green"));
      note.SaveNoteHighlights(stored);
      string zip = Path.Combine(work, "export.zip");
      DataExporter.Export(zip);
      JsonObject row = ReadData(zip)["shortcuts"].AsArray().Single(s => (long)s["id"] == note.ID).AsObject();
      Assert.Equal(stored, (string)row["noteHighlights"]);
      note.SaveNoteHighlights("");
      DataImporter.Import(zip, ImportMode.Replace);
      Assert.Equal(stored, DataStore.Shortcuts.Get(note.ID).NoteHighlights);
      DataImporter.Import(zip, ImportMode.AddAsNew);
      Assert.All(DataStore.Shortcuts.GetAll().Where(s => s.IsNote == 1), s => Assert.Equal(stored, s.NoteHighlights));
    }

    [Fact]
    public void An_export_from_before_highlights_imports_with_none() {
      ShortcutViewModel note = NewNote();
      note.SaveNoteHighlights(Highlights(("pay", "Yellow")));
      string zip = Path.Combine(work, "export.zip");
      DataExporter.Export(zip);
      string old = Path.Combine(work, "old.zip");
      File.Copy(zip, old);
      using (ZipArchive archive = ZipFile.Open(old, ZipArchiveMode.Update)) {
        JsonNode data = JsonNode.Parse(ReadEntryText(archive, "data.json"));
        foreach (JsonNode s in data["shortcuts"].AsArray()) {
          s.AsObject().Remove("noteHighlights");
        }
        JsonNode manifest = JsonNode.Parse(ReadEntryText(archive, "manifest.json"));
        manifest["schemaVersion"] = 24;
        WriteEntry(archive, "data.json", data.ToJsonString());
        WriteEntry(archive, "manifest.json", manifest.ToJsonString());
      }
      DataImporter.Import(old, ImportMode.Replace);
      Shortcut only = Assert.Single(DataStore.Shortcuts.GetAll());
      Assert.Equal(("Pay the gas bill\r\nBook the dentist", ""), (only.NoteText, only.NoteHighlights));
    }

    // ---- Database version 25 ----

    [Fact]
    public void Version_25_is_a_known_step() {
      Assert.True(AppConstants.DatabaseVersion >= 25);
      Assert.Single(SchemaManager.Migrations, m => m.Version == 25);
    }

    [Fact]
    public void A_v24_database_gets_the_highlights_column_empty_after_a_backup() {
      ShortcutViewModel note = NewNote();
      db.Exec("ALTER TABLE Shortcuts DROP COLUMN NoteHighlights; PRAGMA user_version = 24;");
      Assert.Equal(0, Columns("Shortcuts", "NoteHighlights"));
      DataStore.Upgrade();
      Assert.Equal(AppConstants.DatabaseVersion, db.Scalar("PRAGMA user_version"));
      Assert.Equal(1, Columns("Shortcuts", "NoteHighlights"));
      Shortcut row = DataStore.Shortcuts.Get(note.ID);
      Assert.Equal(("Pay the gas bill\r\nBook the dentist", ""), (row.NoteText, row.NoteHighlights));
      Assert.Single(Directory.GetFiles(db.Folder, "test.sqlite3.bak-v24-*"));
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
