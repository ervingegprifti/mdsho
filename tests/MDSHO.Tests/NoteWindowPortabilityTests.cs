using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Data.Sqlite;
using MDSHO.Common;
using MDSHO.Core;
using MDSHO.Data;
using MDSHO.ViewModels;
using Xunit;

namespace MDSHO.Tests {

  /// <summary>
  /// Export and import carry the note window places (place, size, state, open, pinned, wrap and text size), so export
  /// then import gives back the same state. Export format 2 adds them; format 1 files still import, with default windows.
  /// </summary>
  public class NoteWindowPortabilityTests : IDisposable {
    private TempDb db = new TempDb();
    private readonly string work = Path.Combine(Path.GetTempPath(), "MDSHO.Tests", "portable-" + Guid.NewGuid().ToString("N"));

    public NoteWindowPortabilityTests() {
      Directory.CreateDirectory(work);
    }

    public void Dispose() {
      if (db != null) { db.Dispose(); }
      try { Directory.Delete(work, true); } catch (IOException) { }
    }

    private string Zip(string name = "export.zip") { return Path.Combine(work, name); }

    private static readonly JsonSerializerOptions Json = new JsonSerializerOptions { WriteIndented = false };

    // Every column but the row ID and the note's ID (IDs change with Add as new).
    private static string Row(NoteWindowPlace p) {
      return JsonSerializer.Serialize(new { p.Left, p.Top, p.Width, p.Height, p.State, p.IsOpen, p.IsTopmost, p.IsWrapText, p.FontSize }, Json);
    }

    // Two notes with places (one open, pinned, wrapped and sized, one closed and maximized) and one note without a place.
    private (Shortcut open, Shortcut closed, Shortcut none) Seed() {
      long windowId = DataStore.Windows.GetAll().First().ID;
      Shortcut open = DataStore.Shortcuts.CreateNote(null, windowId);
      Shortcut closed = DataStore.Shortcuts.CreateNote(null, windowId);
      Shortcut none = DataStore.Shortcuts.CreateNote(null, windowId);
      DataStore.NoteWindows.Save(new NoteWindowPlace(open.ID, 110, 120, 530, 440, NoteWindowPlace.Normal, isOpen: true) { IsTopmost = true, IsWrapText = true, FontSize = 22 });
      DataStore.NoteWindows.Save(new NoteWindowPlace(closed.ID, -1500, 60, 700, 500, NoteWindowPlace.Maximized, isOpen: false) { FontSize = 0 });
      return (open, closed, none);
    }

    private static JsonNode ReadEntry(string zip, string name) {
      using (ZipArchive z = ZipFile.OpenRead(zip))
      using (StreamReader r = new StreamReader(z.GetEntry(name).Open(), Encoding.UTF8)) {
        return JsonNode.Parse(r.ReadToEnd());
      }
    }

    private string MutateJson(string source, string entryName, Action<JsonNode> change, string target = "mutated.zip") {
      string dest = Zip(target);
      if (File.Exists(dest)) { File.Delete(dest); }
      using (ZipArchive src = ZipFile.OpenRead(source))
      using (ZipArchive dst = ZipFile.Open(dest, ZipArchiveMode.Create)) {
        foreach (ZipArchiveEntry e in src.Entries) {
          byte[] bytes;
          using (Stream s = e.Open()) using (MemoryStream ms = new MemoryStream()) { s.CopyTo(ms); bytes = ms.ToArray(); }
          if (e.FullName == entryName) {
            JsonNode n = JsonNode.Parse(Encoding.UTF8.GetString(bytes));
            change(n);
            bytes = new UTF8Encoding(false).GetBytes(n.ToJsonString());
          }
          using (Stream w = dst.CreateEntry(e.FullName).Open()) { w.Write(bytes, 0, bytes.Length); }
        }
      }
      return dest;
    }

    // ---- Export ----

    [Fact]
    public void Export_format_is_2_and_the_manifest_counts_the_note_windows() {
      Seed();
      ExportManifest m = DataExporter.Export(Zip());
      Assert.Equal(2, ExportManifest.CurrentFormatVersion);
      Assert.Equal(2, m.FormatVersion);
      Assert.Equal(2, m.Counts.NoteWindows);
      Assert.Equal(2, (int)ReadEntry(Zip(), "manifest.json")["counts"]["noteWindows"]);
      Assert.Equal(2, DataImporter.Inspect(Zip()).Counts.NoteWindows);
    }

    [Fact]
    public void Export_writes_every_column_of_the_note_window_places_but_the_row_id() {
      (Shortcut open, _, _) = Seed();
      DataExporter.Export(Zip());
      JsonArray places = ReadEntry(Zip(), "data.json")["noteWindows"].AsArray();
      Assert.Equal(2, places.Count);
      JsonObject first = places.Single(p => (long)p["shortcutID"] == open.ID).AsObject();
      Assert.Equal(
        new[] { "fontSize", "height", "isOpen", "isTopmost", "isWrapText", "left", "shortcutID", "state", "top", "width" },
        first.Select(kv => kv.Key).OrderBy(k => k, StringComparer.Ordinal).ToArray());
      Assert.Equal((110L, 120L, 530L, 440L, 0L, true, true, true, 22L),
        ((long)first["left"], (long)first["top"], (long)first["width"], (long)first["height"], (long)first["state"],
         (bool)first["isOpen"], (bool)first["isTopmost"], (bool)first["isWrapText"], (long)first["fontSize"]));
    }

    [Fact]
    public void The_data_hash_changes_when_a_note_window_moves() {
      (Shortcut open, _, _) = Seed();
      string before = DataExporter.DataHash();
      DataStore.NoteWindows.Save(new NoteWindowPlace(open.ID, 111, 120, 530, 440, NoteWindowPlace.Normal, isOpen: true) { IsTopmost = true, IsWrapText = true, FontSize = 22 });
      Assert.NotEqual(before, DataExporter.DataHash());
    }

    // ---- Import ----

    [Fact]
    public void Round_trip_into_an_empty_database_gives_the_same_note_window_places() {
      (Shortcut open, Shortcut closed, Shortcut none) = Seed();
      Dictionary<long, string> expected = new[] { open, closed }.ToDictionary(s => s.ID, s => Row(DataStore.NoteWindows.Get(s.ID)));
      DataExporter.Export(Zip());
      db.Dispose();
      db = new TempDb();
      foreach (DesktopWindow w in DataStore.Windows.GetAll()) { DataStore.Windows.Delete(w.ID); }
      Assert.Empty(DataStore.Shortcuts.GetAll());

      DataImporter.Import(Zip(), ImportMode.Replace);

      Assert.Equal(expected[open.ID], Row(DataStore.NoteWindows.Get(open.ID)));
      Assert.Equal(expected[closed.ID], Row(DataStore.NoteWindows.Get(closed.ID)));
      Assert.Null(DataStore.NoteWindows.Get(none.ID));
      Assert.Equal(new[] { open.ID }, DataStore.NoteWindows.GetOpen().Select(p => p.ShortcutID).ToArray());
    }

    [Fact]
    public void Replace_puts_back_the_places_of_the_file_and_drops_the_current_ones() {
      (Shortcut open, Shortcut closed, _) = Seed();
      string expected = Row(DataStore.NoteWindows.Get(open.ID));
      DataExporter.Export(Zip());
      DataStore.NoteWindows.Save(new NoteWindowPlace(open.ID, 1, 2, 300, 200, NoteWindowPlace.Minimized, isOpen: false) { FontSize = 40 });
      long extra = DataStore.Shortcuts.CreateNote(null, DataStore.Windows.GetAll().First().ID).ID;
      DataStore.NoteWindows.Save(new NoteWindowPlace(extra, 5, 5, 300, 200, NoteWindowPlace.Normal, isOpen: true));

      DataImporter.Import(Zip(), ImportMode.Replace);

      Assert.Equal(expected, Row(DataStore.NoteWindows.Get(open.ID)));
      Assert.Null(DataStore.NoteWindows.Get(extra));
      Assert.False(DataStore.NoteWindows.Get(closed.ID).IsOpen);
      Assert.Equal(new[] { open.ID }, DataStore.NoteWindows.GetOpen().Select(p => p.ShortcutID).ToArray());
    }

    [Fact]
    public void Add_as_new_links_each_place_to_the_new_copy_of_its_note() {
      (Shortcut open, Shortcut closed, Shortcut none) = Seed();
      DataExporter.Export(Zip());
      HashSet<long> before = DataStore.Shortcuts.GetAll().Select(s => s.ID).ToHashSet();

      DataImporter.Import(Zip(), ImportMode.AddAsNew);

      List<Shortcut> added = DataStore.Shortcuts.GetAll().Where(s => !before.Contains(s.ID)).OrderBy(s => s.ID).ToList();
      Assert.Equal(3, added.Count);
      // Added in the same order as the originals (same parent, positions kept).
      Assert.Equal(Row(DataStore.NoteWindows.Get(open.ID)), Row(DataStore.NoteWindows.Get(added[0].ID)));
      Assert.Equal(Row(DataStore.NoteWindows.Get(closed.ID)), Row(DataStore.NoteWindows.Get(added[1].ID)));
      Assert.Null(DataStore.NoteWindows.Get(added[2].ID));
      // The originals keep their places.
      Assert.Equal(22, DataStore.NoteWindows.Get(open.ID).FontSize);
    }

    [Fact]
    public void A_format_1_file_without_note_windows_still_imports_and_notes_get_default_windows() {
      (Shortcut open, _, _) = Seed();
      DataExporter.Export(Zip());
      string old = MutateJson(Zip(), "data.json", n => n.AsObject().Remove("noteWindows"), "old-data.zip");
      old = MutateJson(old, "manifest.json", n => { n["formatVersion"] = 1; n["counts"].AsObject().Remove("noteWindows"); }, "old.zip");

      Assert.Equal(0, DataImporter.Inspect(old).Counts.NoteWindows);
      DataImporter.Import(old, ImportMode.Replace);

      Assert.NotNull(DataStore.Shortcuts.GetAll().SingleOrDefault(s => s.ID == open.ID));
      Assert.Null(DataStore.NoteWindows.Get(open.ID));
      Assert.Empty(DataStore.NoteWindows.GetOpen());
    }

    [Fact]
    public void A_format_3_file_is_rejected() {
      Seed();
      DataExporter.Export(Zip());
      string newer = MutateJson(Zip(), "manifest.json", n => n["formatVersion"] = 3);
      DataPortabilityException ex = Assert.Throws<DataPortabilityException>(() => DataImporter.Inspect(newer));
      Assert.Contains("formatVersion", ex.Message);
    }

    [Fact]
    public void A_place_of_a_note_that_is_not_in_the_file_is_skipped() {
      (Shortcut open, _, _) = Seed();
      DataExporter.Export(Zip());
      string zip = MutateJson(Zip(), "data.json", n => n["noteWindows"].AsArray().First(p => (long)p["shortcutID"] == open.ID)["shortcutID"] = 99999);

      DataImporter.Import(zip, ImportMode.Replace);

      Assert.Null(DataStore.NoteWindows.Get(open.ID));
      Assert.Single(DataStore.NoteWindows.GetAll());
    }

    // ---- Backups made before a database upgrade read the old tables ----

    [Fact]
    public void A_backup_before_the_upgrade_reads_old_note_window_rows_without_the_newer_columns() {
      db.Dispose();
      db = new TempDb(false);
      db.Exec("PRAGMA user_version = 9;" + DatabaseV10MigrationTests.V9Sql
        + "INSERT INTO MDSHOWindows (ID,Title,ScreenDeviceName,Left,Top,Width,Height,BorderColor,BackgroundColor,TextColor,TitleBackgroundColor,TitleTextColor,Notes,SolidIcons,SingleClickToOpen) VALUES "
        + "(1,'W1','D1',0,0,300,400,'#FF000001','#FF000002','#FF000003','#FF000004','#FF000005','',1,1);"
        + "INSERT INTO Shortcuts VALUES (1,NULL,1,'N','','','g1',0,0,'',0,'#F',0,0,'','',0,'',1,'text');"
        + "INSERT INTO NoteWindows (ShortcutID,Left,Top,Width,Height,State,IsOpen,IsTopmost) VALUES (1,10,20,300,200,0,1,1);");

      ExportManifest m = DataExporter.Export(Zip(), new ExportOptions { BeforeUpgrade = true });

      Assert.Equal(1, m.Counts.NoteWindows);
      JsonObject place = ReadEntry(Zip(), "data.json")["noteWindows"][0].AsObject();
      Assert.Equal((1L, 10L, true, true, false, 0L), ((long)place["shortcutID"], (long)place["left"], (bool)place["isOpen"], (bool)place["isTopmost"], (bool)place["isWrapText"], (long)place["fontSize"]));
    }

    [Fact]
    public void A_backup_before_the_upgrade_of_a_database_without_note_windows_has_none() {
      db.Dispose();
      db = new TempDb(false);
      db.Exec("PRAGMA user_version = 9;" + DatabaseV10MigrationTests.V9Sql
        + "INSERT INTO MDSHOWindows (ID,Title,ScreenDeviceName,Left,Top,Width,Height,BorderColor,BackgroundColor,TextColor,TitleBackgroundColor,TitleTextColor,Notes,SolidIcons,SingleClickToOpen) VALUES "
        + "(1,'W1','D1',0,0,300,400,'#FF000001','#FF000002','#FF000003','#FF000004','#FF000005','',1,1);"
        + "DROP TABLE NoteWindows;");

      ExportManifest m = DataExporter.Export(Zip(), new ExportOptions { BeforeUpgrade = true });

      Assert.Equal(0, m.Counts.NoteWindows);
      Assert.Empty(ReadEntry(Zip(), "data.json")["noteWindows"].AsArray());
    }
  }

  /// <summary>
  /// After an import MDSHO restarts. The note windows still open from the old data must not save over the imported
  /// places on the way out, and the notes the file had open come back where they were.
  /// </summary>
  public class NoteWindowsAfterImportTests {
    private static readonly ScreenRect TestArea = new ScreenRect(-20000, -20000, 15000, 15000);

    private static void WithApp(Action body) {
      WpfAppHost.Run(() => {
        Action<ShortcutViewModel> oldNoteEditor = ShortcutViewModel.OpenNoteEditor;
        Action<TextEditWindow> oldPlace = TextEditWindow.PlaceNoteWindow;
        Func<IList<ScreenRect>> oldAreas = TextEditWindow.WorkAreas;
        ShortcutViewModel.OpenNoteEditor = s => { };
        TextEditWindow.PlaceNoteWindow = w => {
          w.WindowStartupLocation = WindowStartupLocation.Manual;
          w.Left = -10000;
          w.Top = -10000;
        };
        TextEditWindow.WorkAreas = () => new List<ScreenRect> { TestArea };
        try {
          body();
        } finally {
          foreach (TextEditWindow w in TextEditWindow.OpenNoteWindows.ToList()) {
            w.Close();
          }
          WpfAppHost.Pump();
          TextEditWindow.IsExiting = false;
          ShortcutViewModel.OpenNoteEditor = oldNoteEditor;
          TextEditWindow.PlaceNoteWindow = oldPlace;
          TextEditWindow.WorkAreas = oldAreas;
        }
      });
    }

    private static ShortcutViewModel NewNote(AppViewModel app) {
      DesktopWindowViewModel win = app.Windows.Count > 0 ? app.Windows[0] : app.AddWindow("TEST", 0, 0);
      win.NewNoteShortcutCommand.Execute(null);
      return win.RootShortcuts.Last();
    }

    [Fact]
    public void Forgetting_the_open_notes_closes_them_without_saving_over_the_imported_places() {
      WithApp(() => {
        using (TempDb db = new TempDb()) {
          AppViewModel app = new AppViewModel();
          ShortcutViewModel note = NewNote(app);
          TextEditWindow.ShowNoteFor(note);
          WpfAppHost.Pump();
          TextEditWindow window = TextEditWindow.OpenNoteWindows.Single();
          ((TextBox)window.FindName("TextComment")).Text = "typed before the import";

          TextEditWindow.SaveOpenNotes();
          Assert.Equal("typed before the import", DataStore.Shortcuts.GetAll().Single(s => s.ID == note.ID).NoteText);

          // What the import wrote for this note ID.
          DataStore.NoteWindows.Save(new NoteWindowPlace(note.ID, -9300, -9200, 640, 480, NoteWindowPlace.Normal, isOpen: true) { FontSize = 30 });
          TextEditWindow.ForgetOpenNotes();
          WpfAppHost.Pump();
          TextEditWindow.SaveOpenNotesForExit();

          Assert.Empty(TextEditWindow.OpenNoteWindows);
          Assert.False(window.IsVisible);
          NoteWindowPlace place = DataStore.NoteWindows.Get(note.ID);
          Assert.Equal((-9300L, -9200L, 640L, 30L, true), (place.Left, place.Top, place.Width, place.FontSize, place.IsOpen));
        }
      });
    }

    [Fact]
    public void Notes_open_in_the_file_reopen_where_they_were_after_the_import() {
      string zip = Path.Combine(Path.GetTempPath(), "MDSHO.Tests", "reopen-" + Guid.NewGuid().ToString("N") + ".zip");
      try {
        WithApp(() => {
          long noteId;
          using (TempDb source = new TempDb()) {
            noteId = DataStore.Shortcuts.CreateNote(null, DataStore.Windows.GetAll().First().ID).ID;
            DataStore.NoteWindows.Save(new NoteWindowPlace(noteId, -9400, -9300, 610, 470, NoteWindowPlace.Normal, isOpen: true) { FontSize = 20, IsWrapText = true });
            Directory.CreateDirectory(Path.GetDirectoryName(zip));
            DataExporter.Export(zip);
          }
          using (TempDb target = new TempDb()) {
            DataImporter.Import(zip, ImportMode.Replace);
            // The restart: a new app loads the data and reopens the notes that were open.
            AppViewModel app = ViewModelFactory.CreateAppViewModel();
            TextEditWindow.ReopenOpenNotes(app);
            WpfAppHost.Pump();
            TextEditWindow window = TextEditWindow.OpenNoteWindows.Single();
            Assert.Equal(noteId, window.Shortcut.ID);
            Assert.Equal((-9400.0, -9300.0, 610.0, 470.0), (window.Left, window.Top, window.Width, window.Height));
            TextBox text = (TextBox)window.FindName("TextComment");
            Assert.Equal(20, text.FontSize);
            Assert.Equal(TextWrapping.Wrap, text.TextWrapping);
          }
        });
      } finally {
        if (File.Exists(zip)) { File.Delete(zip); }
      }
    }
  }
}
