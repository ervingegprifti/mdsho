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
  /// Snippet text sizes (1.21.1): each snippet keeps its own text size in Shortcuts.TextFontSize (whole points,
  /// 0 = 12 pt), database version 23. Copies, export, import and backups carry it; older exports import with 0.
  /// </summary>
  public class SnippetFontSizeTests : IDisposable {
    private TempDb db = new TempDb();
    private readonly AppViewModel app = new AppViewModel();
    private readonly DesktopWindowViewModel win;
    private readonly string work = Path.Combine(Path.GetTempPath(), "MDSHO.Tests", "snippet-size-" + Guid.NewGuid().ToString("N"));

    public SnippetFontSizeTests() {
      Directory.CreateDirectory(work);
      win = app.AddWindow("TEST", 0, 0);
    }

    public void Dispose() {
      db.Dispose();
      try { Directory.Delete(work, true); } catch (IOException) { }
    }

    private ShortcutViewModel NewSnippet() {
      win.NewSnippetShortcutCommand.Execute(null);
      ShortcutViewModel snippet = win.RootShortcuts.Last();
      snippet.CancelEdit();
      snippet.Name = "Home address";
      snippet.SaveSnippetText("Main Street 12");
      return snippet;
    }

    private long Columns(string table, string column) {
      return db.Scalar("SELECT count(*) FROM pragma_table_info('" + table + "') WHERE name = '" + column + "' COLLATE NOCASE");
    }

    // ---- The data ----

    [Fact]
    public void A_new_snippet_has_the_text_size_0() {
      ShortcutViewModel snippet = NewSnippet();
      Assert.Equal(0L, DataStore.Shortcuts.Get(snippet.ID).TextFontSize);
      Assert.Equal(0L, snippet.TextFontSize);
    }

    [Fact]
    public void The_repository_saves_the_text_size_and_leaves_the_comment_size() {
      ShortcutViewModel snippet = NewSnippet();
      DataStore.Shortcuts.UpdateTextFontSize(snippet.ID, 16);
      Shortcut row = DataStore.Shortcuts.Get(snippet.ID);
      Assert.Equal((16L, 0L), (row.TextFontSize, row.CommentFontSize));
    }

    [Fact]
    public void Saving_the_text_size_saves_it_at_once_and_tells_the_window() {
      ShortcutViewModel snippet = NewSnippet();
      List<string> raised = new List<string>();
      snippet.PropertyChanged += (s, e) => raised.Add(e.PropertyName);
      snippet.SaveTextFontSize(18);
      Assert.Equal(18L, snippet.TextFontSize);
      Assert.Equal(18L, DataStore.Shortcuts.Get(snippet.ID).TextFontSize);
      Assert.Contains(nameof(ShortcutViewModel.TextFontSize), raised);
    }

    [Fact]
    public void The_panel_reloaded_from_the_database_keeps_the_text_size() {
      ShortcutViewModel snippet = NewSnippet();
      snippet.SaveTextFontSize(14);
      Shortcut row = DataStore.Shortcuts.GetAll().Single(s => s.ID == snippet.ID);
      Assert.Equal(14L, ViewModelFactory.ShortcutToViewModel(app, null, row).TextFontSize);
    }

    [Fact]
    public void A_copy_carries_the_text_size() {
      ShortcutViewModel snippet = NewSnippet();
      snippet.SaveTextFontSize(20);
      ShortcutViewModel copy = ViewModelFactory.CopyShortcut(snippet, null, win);
      Assert.Equal(20L, copy.TextFontSize);
      Assert.Equal(20L, DataStore.Shortcuts.Get(copy.ID).TextFontSize);
    }

    // ---- Export, import and backups ----

    [Fact]
    public void Export_and_import_keep_the_text_size() {
      ShortcutViewModel snippet = NewSnippet();
      snippet.SaveTextFontSize(22);
      string zip = Path.Combine(work, "export.zip");
      DataExporter.Export(zip);
      JsonObject row = ReadData(zip)["shortcuts"].AsArray().Single(s => (long)s["id"] == snippet.ID).AsObject();
      Assert.Equal(22L, (long)row["textFontSize"]);
      DataStore.Shortcuts.UpdateTextFontSize(snippet.ID, 9);
      DataImporter.Import(zip, ImportMode.Replace);
      Assert.Equal(22L, DataStore.Shortcuts.Get(snippet.ID).TextFontSize);
      DataImporter.Import(zip, ImportMode.AddAsNew);
      Assert.All(DataStore.Shortcuts.GetAll().Where(s => s.IsSnippet == 1), s => Assert.Equal(22L, s.TextFontSize));
    }

    [Fact]
    public void An_export_from_before_snippet_sizes_imports_with_0() {
      ShortcutViewModel snippet = NewSnippet();
      snippet.SaveTextFontSize(20);
      string zip = Path.Combine(work, "export.zip");
      DataExporter.Export(zip);
      string old = Path.Combine(work, "old.zip");
      File.Copy(zip, old);
      using (ZipArchive archive = ZipFile.Open(old, ZipArchiveMode.Update)) {
        JsonNode data = JsonNode.Parse(ReadEntryText(archive, "data.json"));
        foreach (JsonNode s in data["shortcuts"].AsArray()) {
          s.AsObject().Remove("textFontSize");
        }
        JsonNode manifest = JsonNode.Parse(ReadEntryText(archive, "manifest.json"));
        manifest["schemaVersion"] = 22;
        WriteEntry(archive, "data.json", data.ToJsonString());
        WriteEntry(archive, "manifest.json", manifest.ToJsonString());
      }
      DataImporter.Import(old, ImportMode.Replace);
      Shortcut only = Assert.Single(DataStore.Shortcuts.GetAll());
      Assert.Equal(("Main Street 12", 0L), (only.SnippetText, only.TextFontSize));
    }

    // ---- Database version 23 ----

    [Fact]
    public void Version_23_is_a_known_step() {
      Assert.True(AppConstants.DatabaseVersion >= 23);
      Assert.Single(SchemaManager.Migrations, m => m.Version == 23);
    }

    [Fact]
    public void A_v22_database_gets_the_text_size_column_with_0_after_a_backup() {
      ShortcutViewModel snippet = NewSnippet();
      // Before version 27 there are no sync triggers.
      db.DropSyncTriggers();
      db.Exec("ALTER TABLE Shortcuts DROP COLUMN TextFontSize; PRAGMA user_version = 22;");
      Assert.Equal(0, Columns("Shortcuts", "TextFontSize"));
      DataStore.Upgrade();
      Assert.Equal(AppConstants.DatabaseVersion, db.Scalar("PRAGMA user_version"));
      Assert.Equal(1, Columns("Shortcuts", "TextFontSize"));
      Shortcut row = DataStore.Shortcuts.Get(snippet.ID);
      Assert.Equal(("Main Street 12", 0L), (row.SnippetText, row.TextFontSize));
      Assert.Single(Directory.GetFiles(db.Folder, "test.sqlite3.bak-v22-*"));
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
