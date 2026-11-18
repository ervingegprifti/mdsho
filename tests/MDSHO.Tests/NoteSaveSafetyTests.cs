using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using MDSHO.Core;
using MDSHO.Data;
using MDSHO.ViewModels;
using Xunit;

namespace MDSHO.Tests {

  /// <summary>
  /// Typing in a note is never lost without a word: a failed save is tried again, one failed note doesn't stop the
  /// others from saving, and deleting a note closes its open window.
  /// </summary>
  public class NoteSaveSafetyTests {

    private sealed class Fixture {
      public TempDb Db;
      public AppViewModel App;
      public DesktopWindowViewModel Win;
      public ShortcutViewModel Group;
      public List<ErrorWindow> Errors = new List<ErrorWindow>();
      public List<string> Confirms = new List<string>();
      public bool ConfirmAnswer = true;

      public ShortcutViewModel NewNote() {
        Group.NewNoteShortcutCommand.Execute(null);
        return Group.Children.Last();
      }

      public TextEditWindow Open(ShortcutViewModel note) {
        TextEditWindow.ShowNoteFor(note);
        return TextEditWindow.OpenNoteWindows.Single(w => w.Shortcut == note);
      }

      public string Stored(ShortcutViewModel note) {
        return Db.ScalarText("SELECT NoteText FROM Shortcuts WHERE ID = " + note.ID);
      }

      // Every note text save fails (for one note only when an ID is given) until AllowSaves.
      public void BlockSaves(long? id = null) {
        Db.Exec("CREATE TRIGGER nosave BEFORE UPDATE OF NoteText ON Shortcuts " + (id == null ? "" : "WHEN OLD.ID = " + id + " ") +
          "BEGIN SELECT RAISE(ABORT, 'the disk is full'); END;");
      }

      public void AllowSaves() {
        Db.Exec("DROP TRIGGER nosave;");
      }
    }

    private static void Type(TextEditWindow window, string text) {
      ((TextBox)window.FindName("TextComment")).Text = text;
    }

    private static void WithNotes(Action<Fixture> body) {
      WpfAppHost.Run(() => {
        Action<ShortcutViewModel> oldNoteEditor = ShortcutViewModel.OpenNoteEditor;
        Action<ShortcutViewModel> oldDeleted = ShortcutViewModel.NotesDeleted;
        Action<ErrorWindow> oldShowError = ErrorDialog.ShowWindow;
        Action<TextEditWindow> oldPlace = TextEditWindow.PlaceNoteWindow;
        Func<IList<MDSHO.Common.ScreenRect>> oldAreas = TextEditWindow.WorkAreas;
        Fixture f = new Fixture();
        ShortcutViewModel.OpenNoteEditor = s => { };
        // As the app does at startup.
        ShortcutViewModel.NotesDeleted = TextEditWindow.CloseNotesOf;
        ErrorDialog.ShowWindow = w => f.Errors.Add(w);
        TextEditWindow.WorkAreas = () => new List<MDSHO.Common.ScreenRect> { new MDSHO.Common.ScreenRect(-20000, -20000, 15000, 15000) };
        TextEditWindow.PlaceNoteWindow = w => {
          w.WindowStartupLocation = WindowStartupLocation.Manual;
          w.Left = -10000;
          w.Top = -10000;
          w.ShowActivated = false;
        };
        try {
          using (TempDb db = new TempDb()) {
            f.Db = db;
            Dialogs.Confirm = (message, caption) => { f.Confirms.Add(message); return f.ConfirmAnswer; };
            f.App = new AppViewModel();
            f.Win = f.App.AddWindow("TEST", 0, 0);
            f.Group = ViewModelFactory.NewGroupShortcut(f.App, null, f.Win);
            f.Win.RootShortcuts.Add(f.Group);
            try {
              body(f);
            } finally {
              f.ConfirmAnswer = true;
              try { db.Exec("DROP TRIGGER IF EXISTS nosave;"); } catch (Exception) { }
              foreach (TextEditWindow w in TextEditWindow.OpenNoteWindows.ToList()) {
                w.Close();
              }
              WpfAppHost.Pump();
              TextEditWindow.IsExiting = false;
            }
          }
        } finally {
          ShortcutViewModel.OpenNoteEditor = oldNoteEditor;
          ShortcutViewModel.NotesDeleted = oldDeleted;
          ErrorDialog.ShowWindow = oldShowError;
          TextEditWindow.PlaceNoteWindow = oldPlace;
          TextEditWindow.WorkAreas = oldAreas;
        }
      });
    }

    // ---- 6: a failed save is tried again ----

    [Fact]
    public void A_failed_note_save_is_tried_again_on_the_next_save() {
      WithNotes(f => {
        ShortcutViewModel note = f.NewNote();
        TextEditWindow window = f.Open(note);
        f.BlockSaves();
        Type(window, "typed");

        Assert.False(window.SaveNoteNow());
        Assert.Equal("", f.Stored(note));
        // Memory is not ahead of the database, so the text still counts as unsaved.
        Assert.Equal("", note.NoteText ?? "");
        Assert.Single(f.Errors);

        f.AllowSaves();
        Assert.True(window.SaveNoteNow());
        Assert.Equal("typed", f.Stored(note));
        Assert.Equal("typed", note.NoteText);
      });
    }

    [Fact]
    public void The_error_shows_once_while_saves_keep_failing() {
      WithNotes(f => {
        ShortcutViewModel note = f.NewNote();
        TextEditWindow window = f.Open(note);
        f.BlockSaves();
        Type(window, "one");
        window.SaveNoteNow();
        Type(window, "two");
        window.SaveNoteNow();
        Assert.Single(f.Errors);
      });
    }

    [Fact]
    public void Closing_a_note_that_can_not_be_saved_asks_first_and_No_keeps_it_open() {
      WithNotes(f => {
        ShortcutViewModel note = f.NewNote();
        TextEditWindow window = f.Open(note);
        f.BlockSaves();
        Type(window, "typed");
        f.ConfirmAnswer = false;

        window.Close();
        WpfAppHost.Pump();

        Assert.Single(f.Confirms);
        Assert.Contains("could not be saved", f.Confirms[0]);
        Assert.Contains(window, TextEditWindow.OpenNoteWindows);

        // Once saving works again, closing saves and closes.
        f.AllowSaves();
        window.Close();
        WpfAppHost.Pump();
        Assert.Equal("typed", f.Stored(note));
        Assert.DoesNotContain(window, TextEditWindow.OpenNoteWindows);
      });
    }

    // ---- 5: each note saves on its own ----

    [Fact]
    public void One_note_that_can_not_be_saved_does_not_stop_the_others() {
      WithNotes(f => {
        ShortcutViewModel first = f.NewNote();
        ShortcutViewModel second = f.NewNote();
        first.Name = "Shopping";
        TextEditWindow firstWindow = f.Open(first);
        TextEditWindow secondWindow = f.Open(second);
        f.BlockSaves(first.ID);
        Type(firstWindow, "milk");
        Type(secondWindow, "bread");

        IReadOnlyList<string> failed = TextEditWindow.SaveOpenNotes();

        Assert.Equal("bread", f.Stored(second));
        Assert.Equal("", f.Stored(first));
        Assert.Equal(new[] { "Shopping" }, failed);
      });
    }

    [Fact]
    public void Saving_for_exit_tries_every_note_and_reports_the_ones_that_failed() {
      WithNotes(f => {
        ShortcutViewModel first = f.NewNote();
        ShortcutViewModel second = f.NewNote();
        first.Name = "Shopping";
        TextEditWindow firstWindow = f.Open(first);
        TextEditWindow secondWindow = f.Open(second);
        f.BlockSaves(first.ID);
        Type(firstWindow, "milk");
        Type(secondWindow, "bread");

        IReadOnlyList<string> failed = TextEditWindow.SaveOpenNotesForExit();

        Assert.Equal("bread", f.Stored(second));
        Assert.Equal(new[] { "Shopping" }, failed);
        Assert.Contains("Shopping", TextEditWindow.UnsavedNotesMessage(failed));
      });
    }

    // ---- 3: deleting a note closes its window ----

    [Fact]
    public void Deleting_a_note_closes_its_open_window() {
      WithNotes(f => {
        ShortcutViewModel note = f.NewNote();
        TextEditWindow window = f.Open(note);
        Type(window, "typed after");

        note.DeleteShortcutCommand.Execute(null);
        WpfAppHost.Pump();

        Assert.Empty(TextEditWindow.OpenNoteWindows);
        Assert.Empty(f.Errors);
        Assert.Single(f.Confirms); // only the delete question, no "could not be saved"
      });
    }

    [Fact]
    public void Deleting_a_group_or_panel_closes_the_note_windows_inside() {
      WithNotes(f => {
        ShortcutViewModel note = f.NewNote();
        ShortcutViewModel other = ViewModelFactory.NewGroupShortcut(f.App, null, f.Win);
        f.Win.RootShortcuts.Add(other);
        other.NewNoteShortcutCommand.Execute(null);
        ShortcutViewModel keep = other.Children.Last();
        f.Open(note);
        f.Open(keep);

        f.Group.DeleteShortcutCommand.Execute(null);
        WpfAppHost.Pump();
        Assert.Equal(new[] { keep }, TextEditWindow.OpenNoteWindows.Select(w => w.Shortcut));

        f.Win.DeleteWindowAndShortcuts();
        WpfAppHost.Pump();
        Assert.Empty(TextEditWindow.OpenNoteWindows);
        Assert.Empty(f.Errors);
      });
    }
  }
}
