using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using MDSHO.Core;
using MDSHO.Data;
using MDSHO.ViewModels;
using Xunit;

namespace MDSHO.Tests {

  /// <summary>
  /// The Comment window and the snippet editor save by themselves like a note (1.21.0): shortly after typing, on close,
  /// Esc and Ctrl+Enter, and at exit. They have no OK button. A failed save is tried again, and closing asks before the
  /// typing is lost.
  /// </summary>
  public class TextAutoSaveTests {

    private sealed class Fixture {
      public TempDb Db;
      public AppViewModel App;
      public DesktopWindowViewModel Win;
      public ShortcutViewModel Command;
      public ShortcutViewModel Snippet;
      public List<ErrorWindow> Errors = new List<ErrorWindow>();
      public List<string> Confirms = new List<string>();
      public bool ConfirmAnswer = true;

      public string StoredComment() => Db.ScalarText("SELECT Comment FROM Shortcuts WHERE ID = " + Command.ID);
      public string StoredSnippet() => Db.ScalarText("SELECT SnippetText FROM Shortcuts WHERE ID = " + Snippet.ID);

      // Every save of the column fails until AllowSaves.
      public void BlockSaves(string column) {
        Db.Exec("CREATE TRIGGER nosave BEFORE UPDATE OF " + column + " ON Shortcuts BEGIN SELECT RAISE(ABORT, 'the disk is full'); END;");
      }

      public void AllowSaves() {
        Db.Exec("DROP TRIGGER nosave;");
      }
    }

    private static void WithButtons(Action<Fixture> body) {
      WpfAppHost.Run(() => {
        Action<ErrorWindow> oldShowError = ErrorDialog.ShowWindow;
        Func<ModifierKeys> oldModifiers = TextEditWindow.KeyboardModifiers;
        Fixture f = new Fixture();
        ErrorDialog.ShowWindow = w => f.Errors.Add(w);
        try {
          using (TempDb db = new TempDb()) {
            f.Db = db;
            Dialogs.Confirm = (message, caption) => { f.Confirms.Add(message); return f.ConfirmAnswer; };
            f.App = new AppViewModel();
            f.Win = f.App.AddWindow("TEST", 0, 0);
            f.Command = ViewModelFactory.NewProcessCommandShortcut(f.App, null, f.Win);
            f.Win.RootShortcuts.Add(f.Command);
            f.Command.Name = "Ping";
            f.Win.NewSnippetShortcutCommand.Execute(null);
            f.Snippet = f.Win.RootShortcuts.Last();
            f.Snippet.CancelEdit();
            f.Snippet.Name = "Address";
            try {
              body(f);
            } finally {
              f.ConfirmAnswer = true;
              try { db.Exec("DROP TRIGGER IF EXISTS nosave;"); } catch (Exception) { }
              foreach (Window w in Application.Current.Windows.OfType<TextEditWindow>().ToList()) {
                w.Close();
              }
              WpfAppHost.Pump();
              TextEditWindow.IsExiting = false;
            }
          }
        } finally {
          ErrorDialog.ShowWindow = oldShowError;
          TextEditWindow.KeyboardModifiers = oldModifiers;
        }
      });
    }

    private static TextEditWindow Show(TextEditWindow window) {
      window.WindowStartupLocation = WindowStartupLocation.Manual;
      window.Left = -10000;
      window.Top = -10000;
      window.ShowActivated = false;
      window.ShowInTaskbar = false;
      window.Show();
      WpfAppHost.Pump();
      return window;
    }

    private static void Type(TextEditWindow window, string text) {
      ((TextBox)window.FindName("TextComment")).Text = text;
    }

    private static void Press(TextEditWindow window, Key key, ModifierKeys modifiers = ModifierKeys.None) {
      TextEditWindow.KeyboardModifiers = () => modifiers;
      KeyEventArgs e = new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(window), Environment.TickCount, key) { RoutedEvent = UIElement.PreviewKeyDownEvent };
      window.RaiseEvent(e);
      WpfAppHost.Pump();
    }

    private static void PumpUntil(Func<bool> done) {
      DateTime end = DateTime.UtcNow.AddSeconds(5);
      while (!done() && DateTime.UtcNow < end) {
        WpfAppHost.Pump();
        System.Threading.Thread.Sleep(20);
      }
      Assert.True(done(), "Timed out");
    }

    // ---- No OK button ----

    [Fact]
    public void No_window_has_an_OK_button() {
      WithButtons(f => {
        foreach (TextEditWindow window in new[] { TextEditWindow.ForComment(f.Command), TextEditWindow.ForSnippet(f.Snippet), TextEditWindow.ForNote(f.Command) }) {
          Assert.Null(window.FindName("ButtonOk"));
          window.Close();
        }
      });
    }

    [Fact]
    public void The_close_button_says_close_not_close_without_saving() {
      WithButtons(f => {
        TextEditWindow window = Show(TextEditWindow.ForComment(f.Command));
        Button close = (Button)window.FindName("ButtonClose");
        Assert.Equal("Close (Esc)", close.ToolTip);
        Assert.Equal("Close", System.Windows.Automation.AutomationProperties.GetName(close));
      });
    }

    // ---- Saving while typing ----

    [Fact]
    public void Typing_in_a_comment_saves_it_shortly_after_and_keeps_the_window_open() {
      WithButtons(f => {
        TextEditWindow window = Show(TextEditWindow.ForComment(f.Command));
        Type(window, "line 1\r\nline 2");
        Assert.Equal("", f.StoredComment());
        PumpUntil(() => f.StoredComment() == "line 1\r\nline 2");
        Assert.Equal("line 1\r\nline 2", f.Command.Comment);
        Assert.True(window.IsVisible);
        Assert.True(window.Saved);
      });
    }

    [Fact]
    public void Typing_in_the_snippet_editor_saves_it_shortly_after() {
      WithButtons(f => {
        TextEditWindow window = Show(TextEditWindow.ForSnippet(f.Snippet));
        Type(window, "Main Street 12");
        PumpUntil(() => f.StoredSnippet() == "Main Street 12");
        Assert.Equal("Main Street 12", f.Snippet.SnippetText);
        Assert.True(window.IsVisible);
      });
    }

    // ---- Saving when the window closes ----

    [Theory]
    [InlineData("close")]
    [InlineData("escape")]
    [InlineData("ctrl+enter")]
    public void Closing_a_comment_saves_it_at_once(string way) {
      WithButtons(f => {
        TextEditWindow window = Show(TextEditWindow.ForComment(f.Command));
        Type(window, "typed");
        if (way == "close") {
          Button close = (Button)window.FindName("ButtonClose");
          close.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent, close));
          WpfAppHost.Pump();
        } else if (way == "escape") {
          Press(window, Key.Escape);
        } else {
          Press(window, Key.Enter, ModifierKeys.Control);
        }
        Assert.False(window.IsVisible, way + " did not close the window");
        Assert.Equal("typed", f.StoredComment());
        Assert.Equal("typed", f.Command.Comment);
      });
    }

    [Theory]
    [InlineData("escape")]
    [InlineData("ctrl+enter")]
    public void Closing_the_snippet_editor_saves_the_value_at_once(string way) {
      WithButtons(f => {
        TextEditWindow window = Show(TextEditWindow.ForSnippet(f.Snippet));
        Type(window, "secret value");
        Press(window, way == "escape" ? Key.Escape : Key.Enter, way == "escape" ? ModifierKeys.None : ModifierKeys.Control);
        Assert.False(window.IsVisible);
        Assert.Equal("secret value", f.StoredSnippet());
      });
    }

    [Fact]
    public void Plain_enter_adds_a_line_and_does_not_close() {
      WithButtons(f => {
        TextEditWindow window = Show(TextEditWindow.ForComment(f.Command));
        Press(window, Key.Enter);
        Assert.True(window.IsVisible);
      });
    }

    [Fact]
    public void Only_spaces_clears_the_comment_when_it_closes() {
      WithButtons(f => {
        f.Command.Comment = "old";
        TextEditWindow window = Show(TextEditWindow.ForComment(f.Command));
        Type(window, "   \r\n ");
        window.Close();
        WpfAppHost.Pump();
        Assert.Equal("", f.StoredComment());
      });
    }

    [Fact]
    public void Closing_without_a_change_writes_nothing() {
      WithButtons(f => {
        f.Command.Comment = "old";
        TextEditWindow window = Show(TextEditWindow.ForComment(f.Command));
        window.Close();
        WpfAppHost.Pump();
        Assert.False(window.Saved);
        Assert.Equal("old", f.StoredComment());
      });
    }

    [Fact]
    public void A_new_snippet_closed_empty_stays_like_a_new_note() {
      WithButtons(f => {
        TextEditWindow window = Show(TextEditWindow.ForSnippet(f.Snippet));
        Press(window, Key.Escape);
        Assert.False(window.IsVisible);
        Assert.Contains(f.Win.RootShortcuts, s => s.ID == f.Snippet.ID);
        Assert.NotNull(DataStore.Shortcuts.Get(f.Snippet.ID));
        Assert.Equal("", f.StoredSnippet());
      });
    }

    // ---- Save safety ----

    [Fact]
    public void A_failed_comment_save_is_tried_again_and_the_error_shows_once() {
      WithButtons(f => {
        TextEditWindow window = Show(TextEditWindow.ForComment(f.Command));
        f.BlockSaves("Comment");
        Type(window, "one");
        Assert.False(window.SaveNoteNow());
        Type(window, "two");
        Assert.False(window.SaveNoteNow());
        Assert.Single(f.Errors);
        Assert.Equal("", f.Command.Comment ?? "");
        f.AllowSaves();
        Assert.True(window.SaveNoteNow());
        Assert.Equal("two", f.StoredComment());
      });
    }

    [Fact]
    public void Closing_after_a_failed_save_asks_and_no_keeps_the_window_open() {
      WithButtons(f => {
        TextEditWindow window = Show(TextEditWindow.ForComment(f.Command));
        f.BlockSaves("Comment");
        Type(window, "typed");
        f.ConfirmAnswer = false;
        window.Close();
        WpfAppHost.Pump();
        Assert.True(window.IsVisible);
        Assert.Contains("This comment could not be saved", Assert.Single(f.Confirms));
        f.ConfirmAnswer = true;
        window.Close();
        WpfAppHost.Pump();
        Assert.False(window.IsVisible);
      });
    }

    [Fact]
    public void A_failed_snippet_save_asks_before_the_value_is_lost() {
      WithButtons(f => {
        TextEditWindow window = Show(TextEditWindow.ForSnippet(f.Snippet));
        f.BlockSaves("SnippetText");
        Type(window, "secret value");
        f.ConfirmAnswer = false;
        Press(window, Key.Escape);
        Assert.True(window.IsVisible);
        Assert.Contains("This snippet could not be saved", Assert.Single(f.Confirms));
      });
    }

    // ---- At exit ----

    [Fact]
    public void Exit_saves_an_open_comment_and_snippet_and_names_the_ones_that_fail() {
      WithButtons(f => {
        TextEditWindow comment = Show(TextEditWindow.ForComment(f.Command));
        TextEditWindow snippet = Show(TextEditWindow.ForSnippet(f.Snippet));
        Type(comment, "comment text");
        f.BlockSaves("SnippetText");
        Type(snippet, "value");
        IReadOnlyList<string> failed = TextEditWindow.SaveOpenNotesForExit();
        Assert.Equal("comment text", f.StoredComment());
        Assert.Equal(new[] { "Address" }, failed);
      });
    }
  }
}
