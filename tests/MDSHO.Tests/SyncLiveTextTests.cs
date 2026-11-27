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
  /// 2.0.0: an open note, comment or snippet window with nothing typed since its last save takes a text another PC sent
  /// at once, keeping the caret and the old text in Text history. Only a window with unsaved typing is left alone.
  /// </summary>
  public class SyncLiveTextTests {
    private static readonly ScreenRect TestArea = new ScreenRect(-20000, -20000, 15000, 15000);

    private static void WithNote(Action<ShortcutViewModel> body) {
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
          using (TempDb tempDb = new TempDb()) {
            AppViewModel app = new AppViewModel();
            DesktopWindowViewModel win = app.AddWindow("TEST", 0, 0);
            win.NewNoteShortcutCommand.Execute(null);
            ShortcutViewModel note = win.RootShortcuts.Last();
            note.CancelEdit();
            note.Name = "Checklist";
            note.SaveNoteText("milk\r\neggs");
            body(note);
          }
        } finally {
          foreach (Window w in Application.Current.Windows.OfType<TextEditWindow>().ToList()) {
            w.Close();
          }
          WpfAppHost.Pump();
          ShortcutViewModel.OpenNoteEditor = oldNoteEditor;
          TextEditWindow.PlaceNoteWindow = oldPlace;
          TextEditWindow.WorkAreas = oldAreas;
        }
      });
    }

    private static TextEditWindow OpenNote(ShortcutViewModel note) {
      TextEditWindow.ShowNoteFor(note);
      WpfAppHost.Pump();
      return TextEditWindow.OpenNoteWindows.Single(w => w.Shortcut == note);
    }

    private static TextBox Text(Window window) => (TextBox)window.FindName("TextComment");

    [Fact]
    public void An_idle_open_note_shows_the_text_from_the_other_PC_and_keeps_the_old_one_in_history() {
      WithNote(note => {
        TextEditWindow window = OpenNote(note);
        Text(window).CaretIndex = 3;
        Assert.False(window.HasUnsavedTyping);
        Assert.DoesNotContain(note.ID, TextEditWindow.UnsavedTextShortcutIds());
        // Sync saved the other PC's text.
        DataStore.Shortcuts.UpdateNoteText(note.ID, "milk\r\neggs\r\nbread");
        Assert.True(window.TakeSyncedText("milk\r\neggs\r\nbread"));
        WpfAppHost.Pump();
        Assert.Equal("milk\r\neggs\r\nbread", Text(window).Text);
        Assert.Equal(3, Text(window).CaretIndex);
        Assert.Equal("milk\r\neggs\r\nbread", note.NoteText);
        Assert.False(window.HasUnsavedTyping);
        Assert.Contains(DataStore.History.Get(note.ID, HistoryFields.Text), h => h.Text == "milk\r\neggs");
        Assert.Equal("milk\r\neggs\r\nbread", DataStore.Shortcuts.Get(note.ID).NoteText);
      });
    }

    [Fact]
    public void After_a_round_every_idle_open_window_shows_its_saved_text() {
      WithNote(note => {
        TextEditWindow window = OpenNote(note);
        DataStore.Shortcuts.UpdateNoteText(note.ID, "from work");
        TextEditWindow.ShowSyncedTexts();
        WpfAppHost.Pump();
        Assert.Equal("from work", Text(window).Text);
      });
    }

    [Fact]
    public void A_note_with_unsaved_typing_is_left_alone() {
      WithNote(note => {
        TextEditWindow window = OpenNote(note);
        TextBox box = Text(window);
        box.CaretIndex = box.Text.Length;
        box.SelectedText = "!";
        WpfAppHost.Pump();
        Assert.True(window.HasUnsavedTyping);
        Assert.Contains(note.ID, TextEditWindow.UnsavedTextShortcutIds());
        Assert.False(window.TakeSyncedText("from work"));
        Assert.Equal("milk\r\neggs!", box.Text);
      });
    }
  }
}
