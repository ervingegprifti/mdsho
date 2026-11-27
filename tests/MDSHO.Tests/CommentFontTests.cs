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
  /// Comment fonts and sizes (1.21.0): each button's comment keeps its own font and text size, stored in
  /// Shortcuts.CommentFont (Courier New) and Shortcuts.CommentFontSize (whole points, 0 = 12 pt), database version 22.
  /// Copies, export, import and backups carry them; older exports import with the defaults.
  /// </summary>
  public class CommentFontTests : IDisposable {
    private TempDb db = new TempDb();
    private readonly AppViewModel app = new AppViewModel();
    private readonly DesktopWindowViewModel win;
    private readonly string work = Path.Combine(Path.GetTempPath(), "MDSHO.Tests", "comment-font-" + Guid.NewGuid().ToString("N"));

    public CommentFontTests() {
      Directory.CreateDirectory(work);
      win = app.AddWindow("TEST", 0, 0);
    }

    public void Dispose() {
      db.Dispose();
      try { Directory.Delete(work, true); } catch (IOException) { }
    }

    private ShortcutViewModel NewCommand() {
      ShortcutViewModel command = ViewModelFactory.NewProcessCommandShortcut(app, null, win);
      win.RootShortcuts.Add(command);
      command.Name = "Ping";
      command.Comment = "Checks the server";
      return command;
    }

    private long Columns(string table, string column) {
      return db.Scalar("SELECT count(*) FROM pragma_table_info('" + table + "') WHERE name = '" + column + "' COLLATE NOCASE");
    }

    // ---- The data ----

    [Fact]
    public void A_new_button_has_the_comment_font_courier_new_and_size_0() {
      ShortcutViewModel command = NewCommand();
      Shortcut row = DataStore.Shortcuts.Get(command.ID);
      Assert.Equal(("Courier New", 0L), (row.CommentFont, row.CommentFontSize));
      Assert.Equal(("Courier New", 0L), (command.CommentFont, command.CommentFontSize));
    }

    [Fact]
    public void The_repository_saves_the_comment_font_and_size() {
      ShortcutViewModel command = NewCommand();
      DataStore.Shortcuts.UpdateCommentFont(command.ID, "Consolas");
      DataStore.Shortcuts.UpdateCommentFontSize(command.ID, 16);
      Assert.Equal(("Consolas", 16L), (DataStore.Shortcuts.Get(command.ID).CommentFont, DataStore.Shortcuts.Get(command.ID).CommentFontSize));
      DataStore.Shortcuts.UpdateCommentFont(command.ID, null);
      Assert.Equal("Courier New", DataStore.Shortcuts.Get(command.ID).CommentFont);
      Assert.Equal(NoteFonts.Default, DataStore.Shortcuts.Get(command.ID).TextFont);
    }

    [Fact]
    public void Saving_the_comment_font_and_size_tells_the_window_and_leaves_the_text_font() {
      ShortcutViewModel command = NewCommand();
      List<string> raised = new List<string>();
      command.PropertyChanged += (s, e) => raised.Add(e.PropertyName);
      command.SaveCommentFont("IBM Plex Mono");
      command.SaveCommentFontSize(18);
      Assert.Equal(("IBM Plex Mono", 18L), (command.CommentFont, command.CommentFontSize));
      Assert.Equal(("IBM Plex Mono", 18L), (DataStore.Shortcuts.Get(command.ID).CommentFont, DataStore.Shortcuts.Get(command.ID).CommentFontSize));
      Assert.Equal(NoteFonts.Default, DataStore.Shortcuts.Get(command.ID).TextFont);
      Assert.Contains(nameof(ShortcutViewModel.CommentFont), raised);
      Assert.Contains(nameof(ShortcutViewModel.CommentFontSize), raised);
      command.SaveCommentFont("Wingdings");
      Assert.Equal("Courier New", command.CommentFont);
    }

    [Fact]
    public void The_panel_reloaded_from_the_database_keeps_the_comment_font_and_size() {
      ShortcutViewModel command = NewCommand();
      command.SaveCommentFont("Consolas");
      command.SaveCommentFontSize(14);
      List<Shortcut> all = DataStore.Shortcuts.GetAll();
      Shortcut row = all.Single(s => s.ID == command.ID);
      ShortcutViewModel reloaded = ViewModelFactory.ShortcutToViewModel(app, null, row);
      Assert.Equal(("Consolas", 14L), (reloaded.CommentFont, reloaded.CommentFontSize));
    }

    [Fact]
    public void A_copy_carries_the_comment_font_and_size() {
      ShortcutViewModel command = NewCommand();
      command.SaveCommentFont("Arial");
      command.SaveCommentFontSize(20);
      ShortcutViewModel copy = ViewModelFactory.CopyShortcut(command, null, win);
      Assert.Equal(("Arial", 20L), (copy.CommentFont, copy.CommentFontSize));
      Assert.Equal(("Arial", 20L), (DataStore.Shortcuts.Get(copy.ID).CommentFont, DataStore.Shortcuts.Get(copy.ID).CommentFontSize));
    }

    // ---- Export, import and backups ----

    [Fact]
    public void Export_and_import_keep_the_comment_font_and_size() {
      ShortcutViewModel command = NewCommand();
      command.SaveCommentFont("Latin Modern Mono");
      command.SaveCommentFontSize(22);
      string zip = Path.Combine(work, "export.zip");
      DataExporter.Export(zip);
      JsonObject row = ReadData(zip)["shortcuts"].AsArray().Single(s => (long)s["id"] == command.ID).AsObject();
      Assert.Equal("Latin Modern Mono", (string)row["commentFont"]);
      Assert.Equal(22L, (long)row["commentFontSize"]);
      DataStore.Shortcuts.UpdateCommentFont(command.ID, "Arial");
      DataStore.Shortcuts.UpdateCommentFontSize(command.ID, 9);
      DataImporter.Import(zip, ImportMode.Replace);
      Assert.Equal(("Latin Modern Mono", 22L), (DataStore.Shortcuts.Get(command.ID).CommentFont, DataStore.Shortcuts.Get(command.ID).CommentFontSize));
      DataImporter.Import(zip, ImportMode.AddAsNew);
      Assert.All(DataStore.Shortcuts.GetAll().Where(s => s.IsProcessCommand == 1), s => Assert.Equal(22L, s.CommentFontSize));
    }

    [Fact]
    public void An_export_from_before_comment_fonts_imports_with_the_defaults() {
      ShortcutViewModel command = NewCommand();
      command.SaveCommentFont("Arial");
      command.SaveCommentFontSize(20);
      string zip = Path.Combine(work, "export.zip");
      DataExporter.Export(zip);
      string old = Path.Combine(work, "old.zip");
      File.Copy(zip, old);
      using (ZipArchive archive = ZipFile.Open(old, ZipArchiveMode.Update)) {
        JsonNode data = JsonNode.Parse(ReadEntryText(archive, "data.json"));
        foreach (JsonNode s in data["shortcuts"].AsArray()) {
          s.AsObject().Remove("commentFont");
          s.AsObject().Remove("commentFontSize");
        }
        JsonNode manifest = JsonNode.Parse(ReadEntryText(archive, "manifest.json"));
        manifest["schemaVersion"] = 21;
        WriteEntry(archive, "data.json", data.ToJsonString());
        WriteEntry(archive, "manifest.json", manifest.ToJsonString());
      }
      DataImporter.Import(old, ImportMode.Replace);
      Shortcut only = Assert.Single(DataStore.Shortcuts.GetAll());
      Assert.Equal(("Courier New", 0L), (only.CommentFont, only.CommentFontSize));
    }

    // ---- Database version 22 ----

    [Fact]
    public void Version_22_is_a_known_step() {
      Assert.True(AppConstants.DatabaseVersion >= 22);
      Assert.Single(SchemaManager.Migrations, m => m.Version == 22);
    }

    [Fact]
    public void A_v21_database_gets_the_comment_columns_with_the_defaults_after_a_backup() {
      ShortcutViewModel command = NewCommand();
      // Before version 27 there are no sync triggers.
      db.DropSyncTriggers();
      db.Exec("ALTER TABLE Shortcuts DROP COLUMN CommentFont; ALTER TABLE Shortcuts DROP COLUMN CommentFontSize; PRAGMA user_version = 21;");
      Assert.Equal(0, Columns("Shortcuts", "CommentFont"));
      DataStore.Upgrade();
      Assert.Equal(AppConstants.DatabaseVersion, db.Scalar("PRAGMA user_version"));
      Assert.Equal(1, Columns("Shortcuts", "CommentFont"));
      Assert.Equal(1, Columns("Shortcuts", "CommentFontSize"));
      Shortcut row = DataStore.Shortcuts.Get(command.ID);
      Assert.Equal(("Checks the server", "Courier New", 0L), (row.Comment, row.CommentFont, row.CommentFontSize));
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
