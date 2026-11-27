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
  /// The More options settings of each note, comment and snippet (1.27.0, database version 26): Highlight current line,
  /// Highlight whole words only, Highlight matching case only and (comments and snippets) Wrap text. The text of a note or
  /// snippet and the comment have their own values (Text* and Comment* columns). They replace the app-wide
  /// HighlightCurrentLine setting and the panel's CommentWrapText, which migration 26 copies into every row.
  /// </summary>
  public class ViewOptionsDataTests : IDisposable {
    private TempDb db = new TempDb();
    private readonly AppViewModel app = new AppViewModel();
    private readonly DesktopWindowViewModel win;
    private readonly ShortcutViewModel group;
    private readonly string work = Path.Combine(Path.GetTempPath(), "MDSHO.Tests", "view-options-" + Guid.NewGuid().ToString("N"));

    private static readonly string[] NewColumns = { "TextCurrentLine", "TextWholeWords", "TextMatchCase", "TextWrapText",
      "CommentCurrentLine", "CommentWholeWords", "CommentMatchCase", "CommentWrapText" };

    public ViewOptionsDataTests() {
      Directory.CreateDirectory(work);
      win = app.AddWindow("TEST", 0, 0);
      group = ViewModelFactory.NewGroupShortcut(app, null, win);
      win.RootShortcuts.Add(group);
    }

    public void Dispose() {
      db.Dispose();
      try { Directory.Delete(work, true); } catch (IOException) { }
    }

    private ShortcutViewModel NewNote() {
      win.NewNoteShortcutCommand.Execute(null);
      ShortcutViewModel note = win.RootShortcuts.Last();
      note.CancelEdit();
      return note;
    }

    // A snippet inside the group, so its panel is found through its parent.
    private ShortcutViewModel NewSnippet() {
      group.NewSnippetShortcutCommand.Execute(null);
      ShortcutViewModel snippet = group.Children.Last();
      snippet.CancelEdit();
      return snippet;
    }

    private long Columns(string table, string column) {
      return db.Scalar("SELECT count(*) FROM pragma_table_info('" + table + "') WHERE name = '" + column + "' COLLATE NOCASE");
    }

    private static TextViewOptions Text(Shortcut s) {
      return new TextViewOptions(s.TextCurrentLine == 1, s.TextWholeWords == 1, s.TextMatchCase == 1, s.TextWrapText == 1);
    }

    private static TextViewOptions Comment(Shortcut s) {
      return new TextViewOptions(s.CommentCurrentLine == 1, s.CommentWholeWords == 1, s.CommentMatchCase == 1, s.CommentWrapText == 1);
    }

    private static readonly TextViewOptions Changed = new TextViewOptions(CurrentLine: false, WholeWords: true, MatchCase: true, WrapText: true);

    // ---- The data ----

    [Fact]
    public void New_items_have_current_line_on_and_the_rest_off() {
      Assert.Equal(new TextViewOptions(true, false, false, false), TextViewOptions.Default);
      foreach (ShortcutViewModel item in new[] { NewNote(), NewSnippet(), group }) {
        Shortcut row = DataStore.Shortcuts.Get(item.ID);
        Assert.Equal(TextViewOptions.Default, Text(row));
        Assert.Equal(TextViewOptions.Default, Comment(row));
        Assert.Equal(TextViewOptions.Default, item.TextViewOptions);
        Assert.Equal(TextViewOptions.Default, item.CommentViewOptions);
      }
      Assert.Equal(TextViewOptions.Default, Text(new Shortcut()));
      Assert.Equal(TextViewOptions.Default, Comment(new Shortcut()));
    }

    [Fact]
    public void The_text_and_the_comment_are_saved_apart_and_tell_the_windows() {
      ShortcutViewModel note = NewNote();
      List<string> raised = new List<string>();
      note.PropertyChanged += (s, e) => raised.Add(e.PropertyName);
      note.SaveViewOptions(false, Changed);
      Shortcut row = DataStore.Shortcuts.Get(note.ID);
      Assert.Equal(Changed, Text(row));
      Assert.Equal(TextViewOptions.Default, Comment(row));
      Assert.Equal(Changed, note.TextViewOptions);
      Assert.Equal(new[] { nameof(ShortcutViewModel.TextViewOptions) }, raised.ToArray());
      TextViewOptions comment = new TextViewOptions(true, false, true, false);
      note.SaveViewOptions(true, comment);
      row = DataStore.Shortcuts.Get(note.ID);
      Assert.Equal((Changed, comment), (Text(row), Comment(row)));
      Assert.Equal(comment, note.CommentViewOptions);
      Assert.Contains(nameof(ShortcutViewModel.CommentViewOptions), raised);
    }

    [Fact]
    public void Each_item_has_its_own_values() {
      ShortcutViewModel a = NewSnippet();
      ShortcutViewModel b = NewSnippet();
      a.SaveViewOptions(false, Changed);
      Assert.Equal(TextViewOptions.Default, Text(DataStore.Shortcuts.Get(b.ID)));
      Assert.Equal(TextViewOptions.Default, b.TextViewOptions);
    }

    [Fact]
    public void The_panel_reloaded_from_the_database_keeps_them() {
      ShortcutViewModel snippet = NewSnippet();
      snippet.SaveViewOptions(false, Changed);
      snippet.SaveViewOptions(true, new TextViewOptions(false, true, false, true));
      Shortcut row = DataStore.Shortcuts.GetAll().Single(s => s.ID == snippet.ID);
      ShortcutViewModel loaded = ViewModelFactory.ShortcutToViewModel(app, null, row);
      Assert.Equal(Changed, loaded.TextViewOptions);
      Assert.Equal(new TextViewOptions(false, true, false, true), loaded.CommentViewOptions);
    }

    [Fact]
    public void A_copy_carries_them() {
      ShortcutViewModel note = NewNote();
      note.SaveViewOptions(false, Changed);
      note.SaveViewOptions(true, new TextViewOptions(false, false, true, true));
      ShortcutViewModel copy = ViewModelFactory.CopyShortcut(note, null, win);
      Shortcut row = DataStore.Shortcuts.Get(copy.ID);
      Assert.Equal((Changed, new TextViewOptions(false, false, true, true)), (Text(row), Comment(row)));
      Assert.Equal(Changed, copy.TextViewOptions);
      Shortcut repoCopy = DataStore.Shortcuts.Append(DataStore.Shortcuts.Get(note.ID));
      Assert.Equal((Changed, new TextViewOptions(false, false, true, true)), (Text(repoCopy), Comment(repoCopy)));
    }

    [Fact]
    public void The_old_shared_settings_are_gone() {
      Assert.Null(typeof(DesktopWindow).GetProperty("CommentWrapText"));
      Assert.Null(typeof(DesktopWindowAppearance).GetProperty("CommentWrapText"));
      Assert.DoesNotContain("HighlightCurrentLine", PortableSettings.Keys);
      Assert.Equal(0, Columns("MDSHOWindows", "CommentWrapText"));
      foreach (string column in NewColumns) {
        Assert.Equal(1, Columns("Shortcuts", column));
      }
    }

    // ---- Export, import and backups ----

    [Fact]
    public void Export_and_import_keep_them() {
      ShortcutViewModel snippet = NewSnippet();
      snippet.SaveViewOptions(false, Changed);
      snippet.SaveViewOptions(true, new TextViewOptions(false, true, false, false));
      string zip = Path.Combine(work, "export.zip");
      DataExporter.Export(zip);
      JsonObject row = ReadData(zip)["shortcuts"].AsArray().Single(s => (long)s["id"] == snippet.ID).AsObject();
      Assert.Equal((0L, 1L, 1L, 1L), ((long)row["textCurrentLine"], (long)row["textWholeWords"], (long)row["textMatchCase"], (long)row["textWrapText"]));
      Assert.Equal((0L, 1L, 0L, 0L), ((long)row["commentCurrentLine"], (long)row["commentWholeWords"], (long)row["commentMatchCase"], (long)row["commentWrapText"]));
      Assert.False(ReadData(zip)["windows"].AsArray()[0].AsObject().ContainsKey("commentWrapText"));
      snippet.SaveViewOptions(false, TextViewOptions.Default);
      DataImporter.Import(zip, ImportMode.Replace);
      Shortcut imported = DataStore.Shortcuts.Get(snippet.ID);
      Assert.Equal((Changed, new TextViewOptions(false, true, false, false)), (Text(imported), Comment(imported)));
    }

    // An export of database version 25: no per-item values, the panel's commentWrapText, maybe the HighlightCurrentLine setting.
    private string OldExport(bool commentWrapText, string currentLine) {
      string zip = Path.Combine(work, "export.zip");
      DataExporter.Export(zip);
      string old = Path.Combine(work, "old-" + Guid.NewGuid().ToString("N") + ".zip");
      File.Copy(zip, old);
      File.Delete(zip);
      using (ZipArchive archive = ZipFile.Open(old, ZipArchiveMode.Update)) {
        JsonNode data = JsonNode.Parse(ReadEntryText(archive, "data.json"));
        foreach (JsonNode s in data["shortcuts"].AsArray()) {
          foreach (string column in NewColumns) {
            s.AsObject().Remove(char.ToLowerInvariant(column[0]) + column.Substring(1));
          }
        }
        foreach (JsonNode w in data["windows"].AsArray()) {
          w["commentWrapText"] = commentWrapText;
        }
        if (currentLine == null) {
          data["settings"].AsObject().Remove("HighlightCurrentLine");
        } else {
          data["settings"]["HighlightCurrentLine"] = currentLine;
        }
        JsonNode manifest = JsonNode.Parse(ReadEntryText(archive, "manifest.json"));
        manifest["schemaVersion"] = 25;
        WriteEntry(archive, "data.json", data.ToJsonString());
        WriteEntry(archive, "manifest.json", manifest.ToJsonString());
      }
      return old;
    }

    [Fact]
    public void An_older_export_without_the_old_settings_imports_with_the_defaults() {
      NewNote();
      NewSnippet();
      string old = OldExport(false, null);
      DataImporter.Import(old, ImportMode.Replace);
      Assert.All(DataStore.Shortcuts.GetAll(), s => Assert.Equal((TextViewOptions.Default, TextViewOptions.Default), (Text(s), Comment(s))));
      Assert.Null(DataStore.AppSettings.Get("HighlightCurrentLine"));
    }

    [Fact]
    public void An_older_export_keeps_what_the_user_saw_its_panel_wrap_and_current_line() {
      ShortcutViewModel note = NewNote();
      ShortcutViewModel snippet = NewSnippet();
      string old = OldExport(true, "Off");
      DataImporter.Import(old, ImportMode.Replace);
      TextViewOptions wrapped = new TextViewOptions(false, false, false, true);
      TextViewOptions plain = new TextViewOptions(false, false, false, false);
      Shortcut s = DataStore.Shortcuts.Get(snippet.ID);
      Assert.Equal((wrapped, wrapped), (Text(s), Comment(s)));
      Shortcut n = DataStore.Shortcuts.Get(note.ID);
      // A note keeps its own wrap with its window place.
      Assert.Equal((plain, wrapped), (Text(n), Comment(n)));
      Shortcut g = DataStore.Shortcuts.Get(group.ID);
      Assert.Equal((plain, wrapped), (Text(g), Comment(g)));
      Assert.Null(DataStore.AppSettings.Get("HighlightCurrentLine"));
      DataImporter.Import(old, ImportMode.AddAsNew);
      Assert.All(DataStore.Shortcuts.GetAll(), r => Assert.Equal(wrapped, Comment(r)));
    }

    // ---- Database version 26 ----

    [Fact]
    public void Version_26_is_a_known_step() {
      Assert.True(AppConstants.DatabaseVersion >= 26);
      Assert.Single(SchemaManager.Migrations, m => m.Version == 26);
    }

    // The database as version 25 had it: no per-item columns, the panel's CommentWrapText and maybe the app setting.
    private void MakeV25(long commentWrapText, string currentLine) {
      db.DropSyncTriggers(); // before version 27 there are no sync triggers
      foreach (string column in NewColumns) {
        db.Exec("ALTER TABLE Shortcuts DROP COLUMN " + column + ";");
      }
      db.Exec("ALTER TABLE MDSHOWindows ADD COLUMN CommentWrapText INTEGER NOT NULL DEFAULT 0;"
        + "UPDATE MDSHOWindows SET CommentWrapText = " + commentWrapText + ";"
        + (currentLine == null ? "" : "INSERT INTO AppSettings (Key, Value) VALUES ('HighlightCurrentLine', '" + currentLine + "');")
        + "PRAGMA user_version = 25;");
    }

    [Fact]
    public void A_v25_database_copies_the_panel_wrap_and_the_current_line_into_every_item_after_a_backup() {
      ShortcutViewModel note = NewNote();
      ShortcutViewModel snippet = NewSnippet();
      MakeV25(1, "Off");
      DataStore.Upgrade();
      Assert.Equal(AppConstants.DatabaseVersion, db.Scalar("PRAGMA user_version"));
      foreach (string column in NewColumns) {
        Assert.Equal(1, Columns("Shortcuts", column));
      }
      Assert.Equal(0, Columns("MDSHOWindows", "CommentWrapText"));
      Assert.Equal(0, db.Scalar("SELECT count(*) FROM AppSettings WHERE Key = 'HighlightCurrentLine'"));
      TextViewOptions wrapped = new TextViewOptions(false, false, false, true);
      TextViewOptions plain = new TextViewOptions(false, false, false, false);
      Shortcut s = DataStore.Shortcuts.Get(snippet.ID);
      Assert.Equal((wrapped, wrapped), (Text(s), Comment(s)));
      Shortcut n = DataStore.Shortcuts.Get(note.ID);
      Assert.Equal((plain, wrapped), (Text(n), Comment(n)));
      Shortcut g = DataStore.Shortcuts.Get(group.ID);
      Assert.Equal((plain, wrapped), (Text(g), Comment(g)));
      Assert.Single(Directory.GetFiles(db.Folder, "test.sqlite3.bak-v25-*"));
    }

    [Fact]
    public void A_v25_database_with_the_old_defaults_gets_the_new_defaults() {
      NewNote();
      NewSnippet();
      MakeV25(0, null);
      DataStore.Upgrade();
      Assert.All(DataStore.Shortcuts.GetAll(), s => Assert.Equal((TextViewOptions.Default, TextViewOptions.Default), (Text(s), Comment(s))));
    }

    [Fact]
    public void Running_the_v26_step_twice_does_nothing_the_second_time() {
      ShortcutViewModel snippet = NewSnippet();
      MakeV25(1, "Off");
      DataStore.Upgrade();
      snippet.SaveViewOptions(true, new TextViewOptions(true, true, false, false));
      using (Microsoft.Data.Sqlite.SqliteConnection c = db.Open()) {
        SchemaManager.Migrations.Single(m => m.Version == 26).Apply(c);
      }
      Assert.Equal(new TextViewOptions(true, true, false, false), Comment(DataStore.Shortcuts.Get(snippet.ID)));
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
