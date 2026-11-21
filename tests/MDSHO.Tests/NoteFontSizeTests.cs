using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Data.Sqlite;
using MDSHO.Common;
using MDSHO.Core;
using MDSHO.Data;
using MDSHO.ViewModels;
using Xunit;

namespace MDSHO.Tests {

  /// <summary>
  /// Ctrl + mouse wheel makes the text of a Note or Comment window bigger or smaller through Word's sizes, 8 to 72 pt. A
  /// note starts at 12 pt and keeps its size in whole points in NoteWindows.FontSize (version 18 added it in DIPs, version
  /// 21 converts it; 0 = 12 pt); a comment keeps its size with its button (Shortcuts.CommentFontSize).
  /// </summary>
  public class NoteFontSizeTests : IDisposable {
    private TempDb db;
    public void Dispose() { if (db != null) { db.Dispose(); } }

    // ---- The size steps ----

    /// <summary>WPF's size (DIPs) of a size in points.</summary>
    internal static double Dip(double points) => points * 96 / 72;

    [Fact]
    public void The_sizes_are_word_s_list_in_whole_points() {
      Assert.Equal(new long[] { 8, 9, 10, 11, 12, 14, 16, 18, 20, 22, 24, 26, 28, 36, 48, 72 }, NoteWindowPlace.SizePresets);
      Assert.Equal(12, NoteWindowPlace.DefaultFontSize);
      Assert.Equal(8, NoteWindowPlace.MinFontSize);
      Assert.Equal(72, NoteWindowPlace.MaxFontSize);
      Assert.Equal("12 pt", NoteWindowPlace.SizeLabel(12));
      Assert.Equal("72 pt", NoteWindowPlace.SizeLabel(72));
      Assert.Equal(16, NoteWindowPlace.ToDips(12), 6);
      Assert.Equal(Dip(9), NoteWindowPlace.ToDips(9), 6);
    }

    [Fact]
    public void The_wheel_steps_through_the_list_and_stops_at_8_and_72() {
      Assert.Equal(14, NoteWindowPlace.NextFontSize(12, 120));
      Assert.Equal(11, NoteWindowPlace.NextFontSize(12, -120));
      Assert.Equal(36, NoteWindowPlace.NextFontSize(28, 120));
      Assert.Equal(72, NoteWindowPlace.NextFontSize(48, 120));
      Assert.Equal(72, NoteWindowPlace.NextFontSize(72, 120));
      Assert.Equal(8, NoteWindowPlace.NextFontSize(8, -120));
      Assert.Equal(12, NoteWindowPlace.NextFontSize(12, 0));
    }

    [Fact]
    public void A_wheel_turn_walks_every_size_up_and_down() {
      long size = NoteWindowPlace.SizePresets[0];
      List<long> up = new List<long> { size };
      for (int i = 0; i < 20; i++) { size = NoteWindowPlace.NextFontSize(size, 120); if (size != up.Last()) { up.Add(size); } }
      Assert.Equal(NoteWindowPlace.SizePresets, up);
      List<long> down = new List<long> { size };
      for (int i = 0; i < 20; i++) { size = NoteWindowPlace.NextFontSize(size, -120); if (size != down.Last()) { down.Add(size); } }
      Assert.Equal(NoteWindowPlace.SizePresets.Reverse(), down);
    }

    [Theory]
    [InlineData(13, 120, 14)]
    [InlineData(13, -120, 12)]
    [InlineData(30, 120, 36)]
    [InlineData(30, -120, 28)]
    [InlineData(60, 120, 72)]
    [InlineData(60, -120, 48)]
    public void A_size_not_in_the_list_goes_to_the_nearest_list_size_in_the_wheel_direction(long size, int delta, long expected) {
      Assert.Equal(expected, NoteWindowPlace.NextFontSize(size, delta));
    }

    [Fact]
    public void A_saved_size_of_0_or_out_of_range_means_12_or_the_nearest_limit() {
      Assert.Equal(12, NoteWindowPlace.FontSizeOrDefault(0));
      Assert.Equal(12, NoteWindowPlace.FontSizeOrDefault(-4));
      Assert.Equal(13, NoteWindowPlace.FontSizeOrDefault(13));
      Assert.Equal(72, NoteWindowPlace.FontSizeOrDefault(500));
      Assert.Equal(8, NoteWindowPlace.FontSizeOrDefault(3));
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(-4, 0)]
    [InlineData(12, 9)]
    [InlineData(16, 12)]
    [InlineData(22, 17)]
    [InlineData(18, 14)]
    [InlineData(60, 45)]
    [InlineData(100, 72)]
    [InlineData(6, 8)]
    public void A_size_in_dips_converts_to_whole_points(long dips, long points) {
      Assert.Equal(points, NoteWindowPlace.FontSizeFromDips(dips));
    }

    // ---- Database version 18 ----

    private long Columns(string table, string column) {
      return db.Scalar("SELECT count(*) FROM pragma_table_info('" + table + "') WHERE name = '" + column + "' COLLATE NOCASE");
    }

    // A version 17 database with one note window place.
    private void BuildV17Db() {
      db = new TempDb(false);
      db.Exec("PRAGMA user_version = 9;" + DatabaseV10MigrationTests.V9Sql
        + "INSERT INTO MDSHOWindows (ID,Title,ScreenDeviceName,Left,Top,Width,Height,BorderColor,BackgroundColor,TextColor,TitleBackgroundColor,TitleTextColor,Notes,SolidIcons,SingleClickToOpen) VALUES "
        + "(1,'W1','D1',0,0,300,400,'#FF000001','#FF000002','#FF000003','#FF000004','#FF000005','',1,1);"
        + "INSERT INTO Shortcuts VALUES (1,NULL,1,'N','','','g1',0,0,'',0,'#F',0,0,'','',0,'',1,'text');"
        + "INSERT INTO NoteWindows (ShortcutID,Left,Top,Width,Height,State,IsOpen,IsTopmost) VALUES (1,10,20,300,200,0,1,1);");
      using (SqliteConnection c = db.Open()) {
        foreach (int version in new[] { 10, 11, 12, 13, 14, 15, 16, 17 }) {
          SchemaManager.Migrations.Single(m => m.Version == version).Apply(c);
        }
      }
      db.Exec("PRAGMA user_version = 17;");
    }

    [Fact]
    public void Version_18_is_a_known_step() {
      Assert.True(AppConstants.DatabaseVersion >= 18);
      Assert.Single(SchemaManager.Migrations, m => m.Version == 18);
    }

    [Fact]
    public void V17_database_migrates_to_18_with_font_size_0_and_keeps_the_place() {
      BuildV17Db();
      DataStore.Upgrade();
      Assert.Equal(AppConstants.DatabaseVersion, db.Scalar("PRAGMA user_version"));
      Assert.Equal(1, Columns("NoteWindows", "FontSize"));
      NoteWindowPlace place = DataStore.NoteWindows.Get(1);
      Assert.Equal((10L, 20L, true, true, 0L), (place.Left, place.Top, place.IsOpen, place.IsTopmost, place.FontSize));
      Assert.Single(Directory.GetFiles(db.Folder, "test.sqlite3.bak-v17-*"));
    }

    [Fact]
    public void Running_the_v18_step_twice_does_nothing_the_second_time() {
      BuildV17Db();
      DataStore.Upgrade();
      using (SqliteConnection c = db.Open()) {
        SchemaManager.Migrations.Single(m => m.Version == 18).Apply(c);
      }
      Assert.Equal(1, Columns("NoteWindows", "FontSize"));
    }

    [Fact]
    public void A_new_database_has_the_column_and_the_size_is_saved_with_the_place() {
      db = new TempDb();
      Assert.Equal(1, Columns("NoteWindows", "FontSize"));
      long windowId = DataStore.Windows.GetAll().First().ID;
      Shortcut note = DataStore.Shortcuts.CreateNote(null, windowId);
      DataStore.NoteWindows.Save(new NoteWindowPlace(note.ID, 1, 2, 300, 200, NoteWindowPlace.Normal, isOpen: true) { FontSize = 24 });
      Assert.Equal(24, DataStore.NoteWindows.Get(note.ID).FontSize);
      DataStore.NoteWindows.Save(new NoteWindowPlace(note.ID, 1, 2, 300, 200, NoteWindowPlace.Normal, isOpen: true) { FontSize = 10 });
      Assert.Equal(10, DataStore.NoteWindows.Get(note.ID).FontSize);
    }

    // ---- Database version 21: sizes in whole points ----

    [Fact]
    public void Version_21_is_a_known_step() {
      Assert.True(AppConstants.DatabaseVersion >= 21);
      Assert.Single(SchemaManager.Migrations, m => m.Version == 21);
    }

    [Fact]
    public void V20_database_converts_note_sizes_from_dips_to_whole_points_after_a_backup() {
      db = new TempDb();
      long windowId = DataStore.Windows.GetAll().First().ID;
      long[] dips = { 0, 12, 16, 22, 60, 100, 6, -4 };
      List<long> notes = new List<long>();
      foreach (long size in dips) {
        long id = DataStore.Shortcuts.CreateNote(null, windowId).ID;
        notes.Add(id);
        DataStore.NoteWindows.Save(new NoteWindowPlace(id, 10, 20, 300, 200, NoteWindowPlace.Normal, isOpen: false) { FontSize = size });
      }
      db.Exec("PRAGMA user_version = 20;");

      DataStore.Upgrade();

      Assert.Equal(AppConstants.DatabaseVersion, db.Scalar("PRAGMA user_version"));
      Assert.Equal(new long[] { 0, 9, 12, 17, 45, 72, 8, 0 }, notes.Select(id => DataStore.NoteWindows.Get(id).FontSize).ToArray());
      Assert.Equal((10L, 20L, 300L), (DataStore.NoteWindows.Get(notes[1]).Left, DataStore.NoteWindows.Get(notes[1]).Top, DataStore.NoteWindows.Get(notes[1]).Width));
      Assert.Single(Directory.GetFiles(db.Folder, "test.sqlite3.bak-v20-*"));
    }

    [Fact]
    public void A_new_database_starts_at_the_current_version_and_keeps_points_as_they_are() {
      db = new TempDb();
      Assert.Equal(AppConstants.DatabaseVersion, db.Scalar("PRAGMA user_version"));
      long windowId = DataStore.Windows.GetAll().First().ID;
      long id = DataStore.Shortcuts.CreateNote(null, windowId).ID;
      DataStore.NoteWindows.Save(new NoteWindowPlace(id, 1, 2, 300, 200, NoteWindowPlace.Normal, isOpen: true) { FontSize = 14 });
      DataStore.Upgrade();
      Assert.Equal(14, DataStore.NoteWindows.Get(id).FontSize);
    }

    // ---- The window ----

    private static readonly ScreenRect TestArea = new ScreenRect(-20000, -20000, 15000, 15000);

    private sealed class Fixture {
      public AppViewModel App;
      public DesktopWindowViewModel Win;
      public ShortcutViewModel Note;
    }

    private static void WithNote(Action<Fixture> body) {
      WpfAppHost.Run(() => {
        Action<ShortcutViewModel> oldNoteEditor = ShortcutViewModel.OpenNoteEditor;
        Action<TextEditWindow> oldPlace = TextEditWindow.PlaceNoteWindow;
        Func<IList<ScreenRect>> oldAreas = TextEditWindow.WorkAreas;
        Func<ModifierKeys> oldModifiers = TextEditWindow.KeyboardModifiers;
        ShortcutViewModel.OpenNoteEditor = s => { };
        TextEditWindow.PlaceNoteWindow = w => {
          w.WindowStartupLocation = WindowStartupLocation.Manual;
          w.Left = -10000;
          w.Top = -10000;
        };
        TextEditWindow.WorkAreas = () => new List<ScreenRect> { TestArea };
        try {
          using (TempDb tempDb = new TempDb()) {
            Fixture f = new Fixture();
            f.App = new AppViewModel();
            f.Win = f.App.AddWindow("TEST", 0, 0);
            f.Win.NewNoteShortcutCommand.Execute(null);
            f.Note = f.Win.RootShortcuts.Last();
            f.Note.Name = "Checklist";
            body(f);
          }
        } finally {
          foreach (TextEditWindow w in TextEditWindow.OpenNoteWindows.ToList()) {
            w.Close();
          }
          WpfAppHost.Pump();
          ShortcutViewModel.OpenNoteEditor = oldNoteEditor;
          TextEditWindow.PlaceNoteWindow = oldPlace;
          TextEditWindow.WorkAreas = oldAreas;
          TextEditWindow.KeyboardModifiers = oldModifiers;
        }
      });
    }

    private static TextEditWindow Open(ShortcutViewModel note) {
      TextEditWindow.ShowNoteFor(note);
      WpfAppHost.Pump();
      return TextEditWindow.OpenNoteWindows.Single(w => w.Shortcut == note);
    }

    private static TextBox Text(Window window) {
      return (TextBox)window.FindName("TextComment");
    }

    // Turns the mouse wheel over the text, with or without Ctrl; true when the window handled it.
    private static bool Wheel(TextEditWindow window, int delta, bool ctrl) {
      TextEditWindow.KeyboardModifiers = () => ctrl ? ModifierKeys.Control : ModifierKeys.None;
      MouseWheelEventArgs e = new MouseWheelEventArgs(Mouse.PrimaryDevice, Environment.TickCount, delta) { RoutedEvent = UIElement.PreviewMouseWheelEvent };
      Text(window).RaiseEvent(e);
      WpfAppHost.Pump();
      return e.Handled;
    }

    [Fact]
    public void A_new_note_shows_its_text_at_12_pt() {
      WithNote(f => {
        Assert.Equal(16, Text(Open(f.Note)).FontSize, 6);
      });
    }

    [Fact]
    public void Ctrl_wheel_up_makes_the_text_bigger_and_down_smaller_and_is_handled() {
      WithNote(f => {
        TextEditWindow window = Open(f.Note);
        Assert.True(Wheel(window, 120, ctrl: true));
        Assert.Equal(Dip(14), Text(window).FontSize, 6);
        Assert.True(Wheel(window, 120, ctrl: true));
        Assert.Equal(Dip(16), Text(window).FontSize, 6);
        Assert.True(Wheel(window, -120, ctrl: true));
        Assert.Equal(Dip(14), Text(window).FontSize, 6);
      });
    }

    [Fact]
    public void The_wheel_without_ctrl_leaves_the_size_and_scrolls_as_before() {
      WithNote(f => {
        TextEditWindow window = Open(f.Note);
        Assert.False(Wheel(window, 120, ctrl: false));
        Assert.Equal(16, Text(window).FontSize, 6);
      });
    }

    [Fact]
    public void The_size_stops_at_8_and_72_pt() {
      WithNote(f => {
        TextEditWindow window = Open(f.Note);
        for (int i = 0; i < 40; i++) { Wheel(window, 120, ctrl: true); }
        Assert.Equal(96, Text(window).FontSize, 6);
        for (int i = 0; i < 40; i++) { Wheel(window, -120, ctrl: true); }
        Assert.Equal(Dip(8), Text(window).FontSize, 6);
      });
    }

    [Fact]
    public void The_title_bar_keeps_its_size() {
      WithNote(f => {
        TextEditWindow window = Open(f.Note);
        double title = ((TextBlock)window.FindName("TextTitle")).FontSize;
        double titleEdit = ((TextBox)window.FindName("TextTitleEdit")).FontSize;
        double close = ((Button)window.FindName("ButtonClose")).FontSize;
        double wrap = ((Button)window.FindName("ButtonWrap")).FontSize;
        Wheel(window, 120, ctrl: true);
        Wheel(window, 120, ctrl: true);
        Assert.Equal(Dip(16), Text(window).FontSize, 6);
        Assert.Equal(title, ((TextBlock)window.FindName("TextTitle")).FontSize);
        Assert.Equal(titleEdit, ((TextBox)window.FindName("TextTitleEdit")).FontSize);
        Assert.Equal(close, ((Button)window.FindName("ButtonClose")).FontSize);
        Assert.Equal(wrap, ((Button)window.FindName("ButtonWrap")).FontSize);
      });
    }

    [Fact]
    public void A_note_saves_its_size_in_whole_points_and_reopens_at_it() {
      WithNote(f => {
        TextEditWindow window = Open(f.Note);
        Wheel(window, 120, ctrl: true);
        Wheel(window, 120, ctrl: true);
        Assert.Equal(16, DataStore.NoteWindows.Get(f.Note.ID).FontSize);
        window.Close();
        WpfAppHost.Pump();
        Assert.Equal(16, DataStore.NoteWindows.Get(f.Note.ID).FontSize);
        Assert.Equal(Dip(16), Text(Open(f.Note)).FontSize, 6);
      });
    }

    [Fact]
    public void A_note_saved_with_size_0_opens_at_the_default_12_pt() {
      WithNote(f => {
        DataStore.NoteWindows.Save(new NoteWindowPlace(f.Note.ID, -9200, -9100, 720, 610, NoteWindowPlace.Normal, isOpen: true));
        Assert.Equal(16, Text(Open(f.Note)).FontSize, 6);
      });
    }

    [Fact]
    public void A_note_saved_at_a_size_not_in_the_list_keeps_it_until_the_wheel_moves() {
      WithNote(f => {
        DataStore.NoteWindows.Save(new NoteWindowPlace(f.Note.ID, -9200, -9100, 720, 610, NoteWindowPlace.Normal, isOpen: true) { FontSize = 17 });
        TextEditWindow window = Open(f.Note);
        Assert.Equal(Dip(17), Text(window).FontSize, 6);
        Assert.Equal(17, DataStore.NoteWindows.Get(f.Note.ID).FontSize);
        Wheel(window, -120, ctrl: true);
        Assert.Equal(Dip(16), Text(window).FontSize, 6);
        Assert.Equal(16, DataStore.NoteWindows.Get(f.Note.ID).FontSize);
      });
    }

    [Fact]
    public void A_comment_window_steps_through_the_list_and_keeps_its_size_next_time() {
      WithNote(f => {
        ShortcutViewModel command = ViewModelFactory.NewProcessCommandShortcut(f.App, null, f.Win);
        f.Win.RootShortcuts.Add(command);
        command.Name = "Ping";
        TextEditWindow first = ShowOffScreen(TextEditWindow.ForComment(command));
        try {
          Assert.Equal(16, Text(first).FontSize, 6);
          Assert.True(Wheel(first, 120, ctrl: true));
          Assert.Equal(Dip(14), Text(first).FontSize, 6);
          for (int i = 0; i < 20; i++) { Wheel(first, 120, ctrl: true); }
          Assert.Equal(96, Text(first).FontSize, 6);
          Wheel(first, -120, ctrl: true);
          Assert.Equal(64, Text(first).FontSize, 6);
        } finally {
          first.Close();
          WpfAppHost.Pump();
        }
        TextEditWindow second = ShowOffScreen(TextEditWindow.ForComment(command));
        try {
          Assert.Equal(64, Text(second).FontSize, 6);
        } finally {
          second.Close();
          WpfAppHost.Pump();
        }
      });
    }

    private static TextEditWindow ShowOffScreen(TextEditWindow window) {
      window.WindowStartupLocation = WindowStartupLocation.Manual;
      window.Left = -10000;
      window.Top = -10000;
      window.ShowActivated = false;
      window.Show();
      WpfAppHost.Pump();
      return window;
    }
  }
}
