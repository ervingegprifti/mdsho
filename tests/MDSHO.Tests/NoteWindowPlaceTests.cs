using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using MDSHO.Common;
using MDSHO.Core;
using MDSHO.Data;
using MDSHO.ViewModels;
using Xunit;

namespace MDSHO.Tests {

  /// <summary>
  /// Note windows remember their place, size and state and whether they were open: open notes come back where they were
  /// after MDSHO or Windows restarts, unsaved typing is saved at exit, and a note opened again later opens where it was.
  /// A saved place that is off every screen is moved to the middle of the main screen.
  /// </summary>
  public class NoteWindowPlaceTests {
    // A fake desktop that also covers the far-away test area, so test windows are not "lost" unless a test says so.
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
        ShortcutViewModel.OpenNoteEditor = s => { };
        TextEditWindow.PlaceNoteWindow = w => {
          w.WindowStartupLocation = WindowStartupLocation.Manual;
          w.Left = -10000;
          w.Top = -10000;
        };
        TextEditWindow.WorkAreas = () => new List<ScreenRect> { TestArea };
        try {
          using (TempDb db = new TempDb()) {
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
          TextEditWindow.IsExiting = false;
          ShortcutViewModel.OpenNoteEditor = oldNoteEditor;
          TextEditWindow.PlaceNoteWindow = oldPlace;
          TextEditWindow.WorkAreas = oldAreas;
        }
      });
    }

    private static TextEditWindow Open(ShortcutViewModel note) {
      TextEditWindow.ShowNoteFor(note);
      WpfAppHost.Pump();
      return TextEditWindow.OpenNoteWindows.Single(w => w.Shortcut == note);
    }

    [Fact]
    public void Opening_a_note_saves_its_window_as_open() {
      WithNote(f => {
        Open(f.Note);
        NoteWindowPlace place = DataStore.NoteWindows.Get(f.Note.ID);
        Assert.NotNull(place);
        Assert.True(place.IsOpen);
        Assert.Equal(-10000, place.Left);
        Assert.Equal(-10000, place.Top);
        Assert.Equal(NoteWindowPlace.Normal, place.State);
      });
    }

    [Fact]
    public void Moving_and_resizing_saves_the_new_place() {
      WithNote(f => {
        TextEditWindow window = Open(f.Note);
        window.Left = -9500;
        window.Top = -9400;
        window.Width = 700;
        window.Height = 600;
        WpfAppHost.Pump();
        window.SaveNotePlace();
        NoteWindowPlace place = DataStore.NoteWindows.Get(f.Note.ID);
        Assert.Equal((-9500L, -9400L, 700L, 600L, true), (place.Left, place.Top, place.Width, place.Height, place.IsOpen));
      });
    }

    [Fact]
    public void Closing_a_note_window_marks_it_closed_and_keeps_its_place() {
      WithNote(f => {
        TextEditWindow window = Open(f.Note);
        window.Left = -9300;
        WpfAppHost.Pump();
        window.Close();
        WpfAppHost.Pump();
        NoteWindowPlace place = DataStore.NoteWindows.Get(f.Note.ID);
        Assert.False(place.IsOpen);
        Assert.Equal(-9300, place.Left);
      });
    }

    [Fact]
    public void A_note_opened_again_opens_where_it_was_with_its_size() {
      WithNote(f => {
        DataStore.NoteWindows.Save(new NoteWindowPlace(f.Note.ID, -9200, -9100, 720, 610, NoteWindowPlace.Normal, isOpen: false));
        TextEditWindow window = Open(f.Note);
        Assert.Equal((-9200.0, -9100.0, 720.0, 610.0), (window.Left, window.Top, window.Width, window.Height));
      });
    }

    [Fact]
    public void A_maximized_note_opens_maximized_and_keeps_its_normal_place() {
      WithNote(f => {
        DataStore.NoteWindows.Save(new NoteWindowPlace(f.Note.ID, -9200, -9100, 720, 610, NoteWindowPlace.Maximized, isOpen: true));
        TextEditWindow window = Open(f.Note);
        Assert.Equal(WindowState.Maximized, window.WindowState);
        window.SaveNotePlace();
        NoteWindowPlace place = DataStore.NoteWindows.Get(f.Note.ID);
        Assert.Equal(NoteWindowPlace.Maximized, place.State);
        Assert.Equal((-9200L, -9100L, 720L, 610L), (place.Left, place.Top, place.Width, place.Height));
      });
    }

    [Fact]
    public void A_minimized_note_opens_minimized() {
      WithNote(f => {
        DataStore.NoteWindows.Save(new NoteWindowPlace(f.Note.ID, -9200, -9100, 720, 610, NoteWindowPlace.Minimized, isOpen: true));
        TextEditWindow window = Open(f.Note);
        Assert.Equal(WindowState.Minimized, window.WindowState);
      });
    }

    [Fact]
    public void A_place_off_every_screen_is_moved_to_the_middle_of_the_main_screen() {
      WithNote(f => {
        ScreenRect main = new ScreenRect(-12000, -12000, 1920, 1040);
        TextEditWindow.WorkAreas = () => new List<ScreenRect> { main };
        TextEditWindow.MainWorkArea = () => main;
        try {
          // Saved on a screen that is no longer connected.
          DataStore.NoteWindows.Save(new NoteWindowPlace(f.Note.ID, -30000, -30000, 600, 400, NoteWindowPlace.Normal, isOpen: true));
          TextEditWindow window = Open(f.Note);
          Rect r = MDSHO.Helpers.DialogPlacement.PhysicalBoundsOf(window);
          Assert.False(LostWindows.IsLost(new ScreenRect(r.X, r.Y, r.Width, r.Height), new[] { main }), "Still lost at " + r);
          Assert.InRange(r.X + r.Width / 2 - (main.X + main.Width / 2), -2, 2);
          Assert.InRange(r.Y + r.Height / 2 - (main.Y + main.Height / 2), -2, 2);
        } finally {
          TextEditWindow.MainWorkArea = null;
        }
      });
    }

    [Fact]
    public void At_exit_unsaved_typing_is_saved_and_open_notes_stay_open() {
      WithNote(f => {
        f.Note.NoteText = "saved before";
        TextEditWindow window = Open(f.Note);
        ((TextBox)window.FindName("TextComment")).Text = "typed but not saved";
        TextEditWindow.SaveOpenNotesForExit();
        window.Close();
        WpfAppHost.Pump();
        Assert.Equal("typed but not saved", f.Note.NoteText);
        Assert.Equal("typed but not saved", DataStore.Shortcuts.GetAll().Single(s => s.ID == f.Note.ID).NoteText);
        Assert.True(DataStore.NoteWindows.Get(f.Note.ID).IsOpen);
      });
    }

    [Fact]
    public void At_start_every_note_that_was_open_opens_again() {
      WithNote(f => {
        f.Win.NewNoteShortcutCommand.Execute(null);
        ShortcutViewModel closedNote = f.Win.RootShortcuts.Last();
        DataStore.NoteWindows.Save(new NoteWindowPlace(f.Note.ID, -9200, -9100, 720, 610, NoteWindowPlace.Normal, isOpen: true));
        DataStore.NoteWindows.Save(new NoteWindowPlace(closedNote.ID, -9000, -9000, 600, 400, NoteWindowPlace.Normal, isOpen: false));
        TextEditWindow.ReopenOpenNotes(f.App);
        WpfAppHost.Pump();
        TextEditWindow window = TextEditWindow.OpenNoteWindows.Single();
        Assert.Same(f.Note, window.Shortcut);
        Assert.Equal(-9200, window.Left);
      });
    }

    [Fact]
    public void At_start_a_saved_open_window_of_a_missing_note_is_marked_closed() {
      WithNote(f => {
        DataStore.NoteWindows.Save(new NoteWindowPlace(f.Note.ID, -9200, -9100, 720, 610, NoteWindowPlace.Normal, isOpen: true));
        // A fresh app view model that does not contain the note (as if its panel were gone from the loaded data).
        AppViewModel empty = new AppViewModel();
        TextEditWindow.ReopenOpenNotes(empty);
        WpfAppHost.Pump();
        Assert.Empty(TextEditWindow.OpenNoteWindows);
        Assert.False(DataStore.NoteWindows.Get(f.Note.ID).IsOpen);
      });
    }
  }
}
