using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.Json.Nodes;
using Microsoft.Data.Sqlite;
using MDSHO.Common;
using MDSHO.Core;
using MDSHO.Data;
using MDSHO.ViewModels;
using Xunit;

namespace MDSHO.Tests {

  /// <summary>
  /// Note fonts (1.20.0): each note and snippet has its own font, stored in Shortcuts.TextFont (database version 20,
  /// Courier New by default). Copies, export, import and backups carry it.
  /// </summary>
  public class NoteFontTests : IDisposable {
    private TempDb db = new TempDb();
    private readonly AppViewModel app = new AppViewModel();
    private readonly DesktopWindowViewModel win;
    private readonly string work = Path.Combine(Path.GetTempPath(), "MDSHO.Tests", "font-" + Guid.NewGuid().ToString("N"));

    public NoteFontTests() {
      Directory.CreateDirectory(work);
      win = app.AddWindow("TEST", 0, 0);
    }

    public void Dispose() {
      db.Dispose();
      try { Directory.Delete(work, true); } catch (IOException) { }
    }

    // ---- The font list ----

    [Fact]
    public void The_default_font_is_courier_new() {
      Assert.Equal("Courier New", NoteFonts.Default);
    }

    [Fact]
    public void The_menu_lists_five_windows_fonts_then_five_that_come_with_mdsho() {
      Assert.Equal(new[] { "Courier New", "Consolas", "Arial", "Verdana", "Calibri" }, NoteFonts.FromWindows);
      Assert.Equal(new[] { "JetBrains Mono", "Source Code Pro", "IBM Plex Mono", "Latin Modern Roman", "Latin Modern Mono" }, NoteFonts.ComesWithMdsho);
      Assert.Equal(NoteFonts.FromWindows.Concat(NoteFonts.ComesWithMdsho), NoteFonts.All);
      Assert.Equal("From Windows", NoteFonts.FromWindowsLabel);
      Assert.Equal("Comes with MDSHO", NoteFonts.ComesWithMdshoLabel);
    }

    [Theory]
    [InlineData("Consolas", "Consolas")]
    [InlineData("verdana", "Verdana")]
    [InlineData("Calibri", "Calibri")]
    [InlineData("jetbrains mono", "JetBrains Mono")]
    [InlineData("Comic Sans MS", "Courier New")]
    [InlineData("", "Courier New")]
    [InlineData(null, "Courier New")]
    public void A_stored_font_that_is_not_in_the_list_means_courier_new(string stored, string expected) {
      Assert.Equal(expected, NoteFonts.OrDefault(stored));
    }

    [Theory]
    [InlineData("Courier New", false)]
    [InlineData("Arial", false)]
    [InlineData("IBM Plex Mono", true)]
    [InlineData("Latin Modern Mono", true)]
    public void Only_the_fonts_that_come_with_mdsho_are_shipped(string name, bool shipped) {
      Assert.Equal(shipped, NoteFonts.IsShipped(name));
    }

    // ---- The size in points (WPF sizes are DIPs: 96 per inch, points are 72 per inch) ----

    [Theory]
    [InlineData(12, "9 pt")]
    [InlineData(14, "10.5 pt")]
    [InlineData(16, "12 pt")]
    [InlineData(8, "6 pt")]
    [InlineData(72, "54 pt")]
    [InlineData(13, "10 pt")]      // 9.75 rounds to the nearest half: 10
    [InlineData(18.6667, "14 pt")]
    [InlineData(15, "11.5 pt")]    // 11.25 rounds up to 11.5 (half way goes up)
    public void The_size_label_shows_points_rounded_to_the_nearest_half(double dips, string label) {
      Assert.Equal(label, NoteFonts.PointsLabel(dips));
    }

    // ---- Database version 20 ----

    private long Columns(string table, string column) {
      return db.Scalar("SELECT count(*) FROM pragma_table_info('" + table + "') WHERE name = '" + column + "' COLLATE NOCASE");
    }

    // A version 19 database with a note, a snippet and a run command.
    private void BuildV19Db() {
      db.Dispose();
      db = new TempDb(false);
      db.Exec("PRAGMA user_version = 9;" + DatabaseV10MigrationTests.V9Sql
        + "INSERT INTO MDSHOWindows (ID,Title,ScreenDeviceName,Left,Top,Width,Height,BorderColor,BackgroundColor,TextColor,TitleBackgroundColor,TitleTextColor,Notes,SolidIcons,SingleClickToOpen) VALUES "
        + "(1,'W1','D1',0,0,300,400,'#FF000001','#FF000002','#FF000003','#FF000004','#FF000005','',1,1);"
        + "INSERT INTO Shortcuts VALUES (1,NULL,1,'N','','','g1',0,0,'',0,'#F',0,0,'','',0,'',1,'text');"
        + "INSERT INTO Shortcuts VALUES (2,NULL,1,'C','','','g2',0,0,'c',0,'#F',1,1,'cmd.exe','/K dir',0,'',0,'');");
      using (SqliteConnection c = db.Open()) {
        foreach (int version in new[] { 10, 11, 12, 13, 14, 15, 16, 17, 18, 19 }) {
          SchemaManager.Migrations.Single(m => m.Version == version).Apply(c);
        }
      }
      db.Exec("INSERT INTO Shortcuts (ID,ParentShortcutID,ParentMDSHOWindowID,Name,Target,Ext,Guid,IsGroup,IsGroupExpanded,Comment,IsMarked,MarkColor,Position,IsProcessCommand,ProcessFileName,ProcessArguments,IsSnippet,SnippetText) "
        + "VALUES (3,NULL,1,'S','','','g3',0,0,'',0,'#F',2,0,'','',1,'value');"
        + "PRAGMA user_version = 19;");
    }

    [Fact]
    public void Version_is_20_and_the_last_migration_is_20() {
      Assert.Equal(20, AppConstants.DatabaseVersion);
      Assert.Equal(20, SchemaManager.Migrations.Last().Version);
    }

    [Fact]
    public void V19_database_migrates_to_20_with_courier_new_for_every_button_and_keeps_the_rest() {
      BuildV19Db();
      DataStore.Upgrade();
      Assert.Equal(20, db.Scalar("PRAGMA user_version"));
      Assert.Equal(1, Columns("Shortcuts", "TextFont"));
      Shortcut note = DataStore.Shortcuts.Get(1), command = DataStore.Shortcuts.Get(2), snippet = DataStore.Shortcuts.Get(3);
      Assert.Equal(("text", "Courier New"), (note.NoteText, note.TextFont));
      Assert.Equal(("/K dir", "Courier New"), (command.ProcessArguments, command.TextFont));
      Assert.Equal(("value", "Courier New"), (snippet.SnippetText, snippet.TextFont));
      Assert.Single(Directory.GetFiles(db.Folder, "test.sqlite3.bak-v19-*"));
    }

    [Fact]
    public void Running_the_v20_step_twice_does_nothing_the_second_time() {
      BuildV19Db();
      DataStore.Upgrade();
      using (SqliteConnection c = db.Open()) {
        SchemaManager.Migrations.Single(m => m.Version == 20).Apply(c);
      }
      Assert.Equal(1, Columns("Shortcuts", "TextFont"));
    }

    [Fact]
    public void A_new_database_has_the_column_and_new_notes_and_snippets_start_with_courier_new() {
      Assert.Equal(1, Columns("Shortcuts", "TextFont"));
      Shortcut note = DataStore.Shortcuts.CreateNote(null, win.ID);
      Shortcut snippet = DataStore.Shortcuts.CreateSnippet(null, win.ID);
      Assert.Equal("Courier New", note.TextFont);
      Assert.Equal("Courier New", snippet.TextFont);
      DataStore.Shortcuts.UpdateTextFont(note.ID, "Consolas");
      Assert.Equal("Consolas", DataStore.Shortcuts.Get(note.ID).TextFont);
      DataStore.Shortcuts.UpdateTextFont(note.ID, null);
      Assert.Equal("Courier New", DataStore.Shortcuts.Get(note.ID).TextFont);
    }

    // ---- The view model ----

    private ShortcutViewModel NewNote() {
      win.NewNoteShortcutCommand.Execute(null);
      ShortcutViewModel note = win.RootShortcuts.Last();
      note.CancelEdit();
      note.Name = "Checklist";
      return note;
    }

    private ShortcutViewModel NewSnippet() {
      win.NewSnippetShortcutCommand.Execute(null);
      ShortcutViewModel snippet = win.RootShortcuts.Last();
      snippet.CancelEdit();
      snippet.Name = "Address";
      snippet.SnippetText = "Main Street 12";
      return snippet;
    }

    [Fact]
    public void A_new_note_and_a_new_snippet_use_courier_new() {
      Assert.Equal("Courier New", NewNote().TextFont);
      Assert.Equal("Courier New", NewSnippet().TextFont);
    }

    [Fact]
    public void Picking_a_font_saves_it_at_once_and_tells_the_window() {
      ShortcutViewModel note = NewNote();
      List<string> raised = new List<string>();
      note.PropertyChanged += (s, e) => raised.Add(e.PropertyName);
      note.SaveTextFont("IBM Plex Mono");
      Assert.Equal("IBM Plex Mono", note.TextFont);
      Assert.Equal("IBM Plex Mono", DataStore.Shortcuts.Get(note.ID).TextFont);
      Assert.Contains(nameof(ShortcutViewModel.TextFont), raised);
    }

    [Fact]
    public void A_font_not_in_the_list_is_saved_as_courier_new() {
      ShortcutViewModel snippet = NewSnippet();
      snippet.SaveTextFont("Wingdings");
      Assert.Equal("Courier New", snippet.TextFont);
      Assert.Equal("Courier New", DataStore.Shortcuts.Get(snippet.ID).TextFont);
    }

    [Fact]
    public void The_panel_reloaded_from_the_database_keeps_each_font() {
      ShortcutViewModel note = NewNote();
      ShortcutViewModel snippet = NewSnippet();
      note.SaveTextFont("Consolas");
      snippet.SaveTextFont("Latin Modern Mono");
      AppViewModel reloaded = ViewModelFactory.CreateAppViewModel(work);
      List<ShortcutViewModel> all = reloaded.Windows.SelectMany(w => w.RootShortcuts).ToList();
      Assert.Equal("Consolas", all.Single(s => s.ID == note.ID).TextFont);
      Assert.Equal("Latin Modern Mono", all.Single(s => s.ID == snippet.ID).TextFont);
    }

    [Fact]
    public void Copy_and_paste_of_a_note_or_snippet_carries_its_font() {
      ShortcutViewModel note = NewNote();
      ShortcutViewModel snippet = NewSnippet();
      note.SaveTextFont("Arial");
      snippet.SaveTextFont("Source Code Pro");
      ShortcutViewModel noteCopy = ViewModelFactory.CopyShortcut(note, null, win);
      ShortcutViewModel snippetCopy = ViewModelFactory.CopyShortcut(snippet, null, win);
      Assert.Equal("Arial", noteCopy.TextFont);
      Assert.Equal("Arial", DataStore.Shortcuts.Get(noteCopy.ID).TextFont);
      Assert.Equal("Source Code Pro", snippetCopy.TextFont);
      Assert.Equal("Source Code Pro", DataStore.Shortcuts.Get(snippetCopy.ID).TextFont);
    }

    [Fact]
    public void Copying_a_group_carries_the_fonts_inside() {
      ShortcutViewModel group = ViewModelFactory.NewGroupShortcut(app, null, win);
      win.RootShortcuts.Add(group);
      group.NewNoteShortcutCommand.Execute(null);
      ShortcutViewModel note = group.Children.Single();
      note.CancelEdit();
      note.SaveTextFont("JetBrains Mono");
      ShortcutViewModel copy = ViewModelFactory.CopyShortcut(group, null, win);
      Assert.Equal("JetBrains Mono", copy.Children.Single().TextFont);
      Assert.Equal("JetBrains Mono", DataStore.Shortcuts.Get(copy.Children.Single().ID).TextFont);
    }

    [Fact]
    public void A_panel_clone_shows_the_same_note_with_the_same_font() {
      ShortcutViewModel note = NewNote();
      note.SaveTextFont("Consolas");
      // A clone shows its panel's buttons: the same view models, so the same font.
      Assert.Same(note, win.RootShortcuts.Single(s => s.ID == note.ID));
      Assert.Equal("Consolas", win.RootShortcuts.Single(s => s.ID == note.ID).TextFont);
    }

    // ---- Export, import and backups ----

    [Fact]
    public void Export_and_replace_import_keep_the_font() {
      ShortcutViewModel note = NewNote();
      note.SaveTextFont("Latin Modern Roman");
      string zip = Path.Combine(work, "export.zip");
      DataExporter.Export(zip);
      JsonObject row = ReadData(zip)["shortcuts"].AsArray().Single(s => (long)s["id"] == note.ID).AsObject();
      Assert.Equal("Latin Modern Roman", (string)row["textFont"]);
      DataStore.Shortcuts.UpdateTextFont(note.ID, "Arial");
      DataImporter.Import(zip, ImportMode.Replace);
      Assert.Equal("Latin Modern Roman", DataStore.Shortcuts.Get(note.ID).TextFont);
    }

    [Fact]
    public void Add_as_new_import_keeps_the_font() {
      ShortcutViewModel snippet = NewSnippet();
      snippet.SaveTextFont("IBM Plex Mono");
      string zip = Path.Combine(work, "export.zip");
      DataExporter.Export(zip);
      DataImporter.Import(zip, ImportMode.AddAsNew);
      List<Shortcut> snippets = DataStore.Shortcuts.GetAll().Where(s => s.IsSnippet == 1).ToList();
      Assert.Equal(2, snippets.Count);
      Assert.All(snippets, s => Assert.Equal("IBM Plex Mono", s.TextFont));
    }

    [Fact]
    public void An_export_from_before_fonts_imports_with_courier_new() {
      NewNote().SaveTextFont("Arial");
      string zip = Path.Combine(work, "export.zip");
      DataExporter.Export(zip);
      string old = Path.Combine(work, "old.zip");
      File.Copy(zip, old);
      using (ZipArchive archive = ZipFile.Open(old, ZipArchiveMode.Update)) {
        JsonNode data = JsonNode.Parse(ReadEntryText(archive, "data.json"));
        foreach (JsonNode s in data["shortcuts"].AsArray()) {
          s.AsObject().Remove("textFont");
        }
        JsonNode manifest = JsonNode.Parse(ReadEntryText(archive, "manifest.json"));
        manifest["schemaVersion"] = 19;
        manifest["appVersion"] = "1.19.2";
        WriteEntry(archive, "data.json", data.ToJsonString());
        WriteEntry(archive, "manifest.json", manifest.ToJsonString());
      }
      DataImporter.Import(old, ImportMode.Replace);
      Shortcut only = Assert.Single(DataStore.Shortcuts.GetAll());
      Assert.Equal("Courier New", only.TextFont);
    }

    [Fact]
    public void A_backup_before_the_upgrade_from_19_reads_the_buttons_with_courier_new() {
      BuildV19Db();
      string zip = Path.Combine(work, "before.zip");
      ExportManifest m = DataExporter.Export(zip, new ExportOptions { BeforeUpgrade = true });
      Assert.Equal(19, m.SchemaVersion);
      JsonArray rows = ReadData(zip)["shortcuts"].AsArray();
      Assert.Equal(3, rows.Count);
      Assert.All(rows, r => Assert.Equal("Courier New", (string)r["textFont"]));
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
