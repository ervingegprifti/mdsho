using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using MDSHO.Common;
using MDSHO.Core;
using MDSHO.Data;
using MDSHO.Helpers;
using MDSHO.ViewModels;
using Xunit;

namespace MDSHO.Tests {

  /// <summary>
  /// Button comments: right-click a button or group → "Comment..." opens the Comment window; the comment shows in the
  /// button's tooltip (below the target or command line, cut after 10 lines) and a small marker shows which buttons have one.
  /// </summary>
  public class CommentTests {

    private sealed class Fixture {
      public AppViewModel App;
      public DesktopWindowViewModel Win;
      public ShortcutViewModel Group;
      public ShortcutViewModel Command;
      public List<ShortcutViewModel> Opened = new List<ShortcutViewModel>();
    }

    private static void WithButtons(Action<Fixture> body) {
      WpfAppHost.Run(() => {
        Action<ShortcutViewModel, bool> oldRunEditor = ShortcutViewModel.OpenRunCommandEditor;
        Action<ShortcutViewModel> oldCommentEditor = ShortcutViewModel.OpenCommentEditor;
        Action<ProcessStartInfo> oldStarter = ShortcutViewModel.ProcessStarter;
        Fixture f = new Fixture();
        ShortcutViewModel.OpenRunCommandEditor = (s, isNew) => { };
        ShortcutViewModel.OpenCommentEditor = s => f.Opened.Add(s);
        ShortcutViewModel.ProcessStarter = psi => { };
        try {
          using (TempDb db = new TempDb()) {
            f.App = new AppViewModel();
            f.Win = f.App.AddWindow("TEST", 0, 0);
            f.Group = ViewModelFactory.NewGroupShortcut(f.App, null, f.Win);
            f.Win.RootShortcuts.Add(f.Group);
            f.Command = ViewModelFactory.NewProcessCommandShortcut(f.App, f.Group, null);
            f.Group.Children.Add(f.Command);
            f.Command.Name = "Ping SRV01";
            f.Command.ProcessFileName = "cmd.exe";
            f.Command.ProcessArguments = "/K ping SRV01";
            body(f);
          }
        } finally {
          ShortcutViewModel.OpenRunCommandEditor = oldRunEditor;
          ShortcutViewModel.OpenCommentEditor = oldCommentEditor;
          ShortcutViewModel.ProcessStarter = oldStarter;
        }
      });
    }

    private static string StoredComment(long id) {
      return DataStore.Shortcuts.GetAll().Single(s => s.ID == id).Comment;
    }

    // ---- View model ----

    [Fact]
    public void A_new_button_has_no_comment() {
      WithButtons(f => {
        Assert.Equal("", f.Command.Comment ?? "");
        Assert.False(f.Command.HasComment);
        Assert.Null(f.Command.CommentPreview);
      });
    }

    [Fact]
    public void Setting_the_comment_saves_it_and_shows_the_marker_flag() {
      WithButtons(f => {
        f.Command.Comment = "Call Anna first.";
        Assert.True(f.Command.HasComment);
        Assert.Equal("Call Anna first.", StoredComment(f.Command.ID));
        f.Command.Comment = "";
        Assert.False(f.Command.HasComment);
        Assert.Equal("", StoredComment(f.Command.ID));
      });
    }

    [Fact]
    public void A_comment_of_only_spaces_and_new_lines_counts_as_none() {
      WithButtons(f => {
        f.Command.Comment = "  \r\n  ";
        Assert.False(f.Command.HasComment);
        Assert.Null(f.Command.CommentPreview);
      });
    }

    [Fact]
    public void The_preview_keeps_up_to_ten_lines_and_counts_the_rest() {
      WithButtons(f => {
        f.Command.Comment = "one\r\ntwo";
        Assert.Equal("one" + Environment.NewLine + "two", f.Command.CommentPreview);
        f.Command.Comment = string.Join("\r\n", Enumerable.Range(1, 12).Select(i => "line " + i));
        string[] lines = f.Command.CommentPreview.Split(Environment.NewLine);
        Assert.Equal(10, lines.Length);
        Assert.Equal("line 10", lines[9]);
        Assert.Equal("… 2 more lines", f.Command.CommentMoreLines);
      });
    }

    [Fact]
    public void The_tooltip_shows_when_there_is_a_target_or_a_comment() {
      WithButtons(f => {
        Assert.True(f.Command.HasToolTip);   // the command line
        f.Group.Comment = "";
        Assert.False(f.Group.HasToolTip);    // a group has no target
        f.Group.Comment = "Servers of team A";
        Assert.True(f.Group.HasToolTip);
      });
    }

    [Fact]
    public void Comment_command_opens_the_comment_editor_for_the_button() {
      WithButtons(f => {
        f.Command.EditCommentCommand.Execute(null);
        f.Group.EditCommentCommand.Execute(null);
        Assert.Equal(new[] { f.Command, f.Group }, f.Opened);
      });
    }

    [Fact]
    public void Comment_changes_raise_the_flags_the_view_binds_to() {
      WithButtons(f => {
        List<string> raised = new List<string>();
        f.Command.PropertyChanged += (s, e) => raised.Add(e.PropertyName);
        f.Command.Comment = "x";
        Assert.Contains(nameof(ShortcutViewModel.HasComment), raised);
        Assert.Contains(nameof(ShortcutViewModel.CommentPreview), raised);
        Assert.Contains(nameof(ShortcutViewModel.HasToolTip), raised);
      });
    }

    // ---- Comment window ----

    private static void Shown(Window window, Action check) {
      window.WindowStartupLocation = WindowStartupLocation.Manual;
      window.Left = -10000;
      window.Top = -10000;
      window.ShowActivated = false;
      window.Show();
      try {
        WpfAppHost.Pump();
        check();
      } finally {
        if (window.IsVisible) {
          window.Close();
        }
        WpfAppHost.Pump();
      }
    }

    private static T Named<T>(Window window, string name) where T : class {
      return Assert.IsAssignableFrom<T>(window.FindName(name));
    }

    private static void Click(Button button) {
      button.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent, button));
      WpfAppHost.Pump();
    }

    private static void PressKey(Window window, Key key, ModifierKeys modifiers = ModifierKeys.None) {
      TextEditWindow.KeyboardModifiers = () => modifiers;
      try {
        KeyEventArgs args = new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(window), 0, key) { RoutedEvent = Keyboard.PreviewKeyDownEvent };
        window.RaiseEvent(args);
        WpfAppHost.Pump();
      } finally {
        TextEditWindow.KeyboardModifiers = () => Keyboard.Modifiers;
      }
    }

    [Fact]
    public void The_window_shows_the_button_name_and_its_comment() {
      WithButtons(f => {
        f.Command.Comment = "Call Anna first.";
        TextEditWindow window = TextEditWindow.ForComment(f.Command);
        Shown(window, () => {
          Assert.Equal("Ping SRV01", Named<TextBlock>(window, "TextTitle").Text);
          Assert.Equal("Ping SRV01", window.Title);
          TextBox text = Named<TextBox>(window, "TextComment");
          Assert.Equal("Call Anna first.", text.Text);
          Assert.True(text.AcceptsReturn);
          Assert.Equal(TextWrapping.NoWrap, text.TextWrapping); // the same look as the Note window
          Assert.Equal(f.Command.OwningWindow.Appearance.Icons.Comment, Named<TextBlock>(window, "TextTitleIcon").Text);
        });
      });
    }

    [Fact]
    public void Closing_stores_the_comment() {
      WithButtons(f => {
        TextEditWindow window = TextEditWindow.ForComment(f.Command);
        Shown(window, () => {
          Named<TextBox>(window, "TextComment").Text = "line 1\r\nline 2";
          Click(Named<Button>(window, "ButtonClose"));
          Assert.False(window.IsVisible);
        });
        Assert.Equal("line 1\r\nline 2", f.Command.Comment);
        Assert.Equal("line 1\r\nline 2", StoredComment(f.Command.ID));
      });
    }

    [Fact]
    public void Saving_only_spaces_clears_the_comment() {
      WithButtons(f => {
        f.Command.Comment = "old";
        TextEditWindow window = TextEditWindow.ForComment(f.Command);
        Shown(window, () => {
          Named<TextBox>(window, "TextComment").Text = "   \r\n ";
          Click(Named<Button>(window, "ButtonClose"));
        });
        Assert.Equal("", f.Command.Comment);
        Assert.Equal("", StoredComment(f.Command.ID));
      });
    }

    [Fact]
    public void Close_and_escape_save_the_comment() {
      foreach (string way in new[] { "ButtonClose", "Escape" }) {
        WithButtons(f => {
          f.Command.Comment = "old";
          TextEditWindow window = TextEditWindow.ForComment(f.Command);
          Shown(window, () => {
            Named<TextBox>(window, "TextComment").Text = "changed";
            if (way == "Escape") {
              PressKey(window, Key.Escape);
            } else {
              Click(Named<Button>(window, way));
            }
            Assert.False(window.IsVisible, way + " did not close the window");
          });
          Assert.Equal("changed", f.Command.Comment);
          Assert.Equal("changed", StoredComment(f.Command.ID));
        });
      }
    }

    [Fact]
    public void There_is_no_OK_button() {
      WithButtons(f => {
        TextEditWindow window = TextEditWindow.ForComment(f.Command);
        Shown(window, () => Assert.Null(window.FindName("ButtonOk")));
      });
    }

    [Fact]
    public void Ctrl_Enter_closes_and_saves_and_plain_Enter_does_not() {
      WithButtons(f => {
        TextEditWindow window = TextEditWindow.ForComment(f.Command);
        Shown(window, () => {
          Named<TextBox>(window, "TextComment").Text = "typed";
          PressKey(window, Key.Enter);
          Assert.True(window.IsVisible);
          Assert.Equal("", f.Command.Comment ?? "");
          PressKey(window, Key.Enter, ModifierKeys.Control);
          Assert.False(window.IsVisible);
        });
        Assert.Equal("typed", f.Command.Comment);
      });
    }

    [Fact]
    public void The_window_icon_follows_the_solid_or_outline_setting() {
      WithButtons(f => {
        TextEditWindow window = TextEditWindow.ForComment(f.Command);
        Shown(window, () => {
          Assert.Equal(f.Command.OwningWindow.Appearance.Icons.Comment, GlyphIcon.GlyphOf(window.Icon));
        });
      });
    }

    // ---- Button view: menu item, tooltip and marker ----

    private static void WithView(Fixture f, Action<DesktopWindowView, Button> check) {
      DesktopWindowView view = new DesktopWindowView(f.Win) { ShowActivated = false, Left = -10000, Top = -10000 };
      try {
        view.Show();
        WpfAppHost.Pump();
        f.Group.IsGroupExpanded = true;
        WpfAppHost.Pump();
        TreeView tree = (TreeView)view.FindName("TreeViewShortcuts");
        TreeViewItem item = WpfAppHost.VisualDescendants<TreeViewItem>(tree).Single(i => i.DataContext == f.Command);
        Button button = WpfAppHost.VisualDescendants<Button>(item).First(b => b.DataContext == f.Command);
        check(view, button);
      } finally {
        view.Close();
        WpfAppHost.Pump();
      }
    }

    [Fact]
    public void Buttons_and_groups_have_a_Comment_menu_item_with_the_comment_icon() {
      WithButtons(f => WithView(f, (view, button) => {
        MenuItem comment = button.ContextMenu.Items.OfType<MenuItem>().Single(m => (m.Header as string) == "Comment...");
        Assert.Equal(nameof(ShortcutViewModel.EditCommentCommand), BindingOperations.GetBinding(comment, MenuItem.CommandProperty).Path.Path);
        Assert.Equal("OwningWindow.Appearance.Icons.Comment", BindingOperations.GetBinding((TextBlock)comment.Icon, TextBlock.TextProperty).Path.Path);
        Assert.Equal(Visibility.Visible, comment.Visibility);
      }));
    }

    [Fact]
    public void The_Comment_window_opens_beside_the_panel_level_with_its_button() {
      WithButtons(f => WithView(f, (view, button) => {
        ScreenInfo screen = new ScreenInfo("D1", new ScreenRect(0, 0, 1920, 1080), new ScreenRect(0, 0, 1920, 1040), 1.0, true);
        CommentPlacementTests.WithFakes(screen, fake => {
          // The panel at the left of the screen; the command's button 300 px down.
          fake.Rects[view] = new Rect(0, 0, 320, 1040);
          fake.Rects[button] = new Rect(10, 300, 300, 24);
          TextEditWindow window = TextEditWindow.ForComment(f.Command);
          fake.Rects[window] = new Rect(0, 0, 300, 150);
          TextEditWindow.PlaceComment(window, view);
          window.ShowActivated = false;
          window.Show();
          try {
            WpfAppHost.Pump();
            // 20 px right of the panel; the title bar's middle (1 px border + 16 px) level with the button's middle.
            Assert.Equal(new Point(320 + 20, 312 - 17), fake.Rects[window].TopLeft);
            Assert.Same(view, window.Owner);
          } finally {
            window.Close();
          }
        });
      }));
    }

    // Edit snippet... opens next to the panel the same way Comment... does: one shared placement.
    [Theory]
    [InlineData("comment")]
    [InlineData("snippet")]
    public void Comment_and_edit_snippet_open_beside_the_panel_level_with_the_button(string kind) {
      WithButtons(f => WithView(f, (view, button) => {
        ScreenInfo screen = new ScreenInfo("D1", new ScreenRect(0, 0, 1920, 1080), new ScreenRect(0, 0, 1920, 1040), 1.0, true);
        CommentPlacementTests.WithFakes(screen, fake => {
          fake.Rects[view] = new Rect(0, 0, 320, 1040);
          fake.Rects[button] = new Rect(10, 300, 300, 24);
          Func<TextEditWindow, bool?> oldShow = TextEditWindow.ShowTextDialog;
          TextEditWindow shown = null;
          TextEditWindow.ShowTextDialog = w => {
            shown = w;
            fake.Rects[w] = new Rect(0, 0, 420, 150);
            w.ShowActivated = false;
            w.Show();
            WpfAppHost.Pump();
            return null;
          };
          try {
            view.Activate();
            if (kind == "comment") {
              TextEditWindow.ShowCommentFor(f.Command);
            } else {
              TextEditWindow.ShowSnippetFor(f.Command);
            }
            Assert.NotNull(shown);
            Assert.Equal(kind == "snippet", shown.IsSnippetWindow);
            Assert.Equal(new Point(320 + 20, 312 - 17), fake.Rects[shown].TopLeft);
            Assert.Same(view, shown.Owner);
          } finally {
            TextEditWindow.ShowTextDialog = oldShow;
            shown?.Close();
          }
        });
      }));
    }

    [Fact]
    public void The_marker_shows_only_on_buttons_with_a_comment() {
      WithButtons(f => WithView(f, (view, button) => {
        TextBlock marker = WpfAppHost.VisualDescendants<TextBlock>(button).Single(t => t.Name == "TextCommentMarker");
        Assert.Equal(Visibility.Collapsed, marker.Visibility);
        f.Command.Comment = "Call Anna first.";
        WpfAppHost.Pump();
        Assert.Equal(Visibility.Visible, marker.Visibility);
        Assert.Equal(f.Win.Appearance.Icons.Comment, marker.Text);
        // The same size as the other icons of a button (the kind icon and the mark), not smaller.
        Assert.Equal(16, marker.FontSize);
        Assert.Equal(16, marker.Width);
      }));
    }

    [Fact]
    public void The_tooltip_shows_the_command_line_and_the_comment_below_a_divider() {
      WithButtons(f => WithView(f, (view, button) => {
        f.Command.Comment = "Call Anna first.";
        WpfAppHost.Pump();
        ToolTip tip = Assert.IsType<ToolTip>(button.ToolTip);
        tip.PlacementTarget = button;
        tip.IsOpen = true;
        WpfAppHost.Pump();
        StackPanel panel = Assert.IsType<StackPanel>(tip.Content);
        List<TextBlock> texts = panel.Children.OfType<TextBlock>().ToList();
        string Shown(TextBlock t) => string.Concat(t.Inlines.OfType<System.Windows.Documents.Run>().Select(r => r.Text));
        Assert.Equal("Runs: cmd.exe /K ping SRV01", Shown(texts[0]));
        Assert.Contains(panel.Children.OfType<Separator>(), s => s.Visibility == Visibility.Visible);
        // texts[1] is the group-value error line, hidden without an error; texts[2] the snippet paste hint.
        Assert.NotEqual(Visibility.Visible, texts[1].Visibility);
        Assert.NotEqual(Visibility.Visible, texts[2].Visibility);
        Assert.Equal("Comment: Call Anna first.", Shown(texts[3]));
        Assert.True(ToolTipService.GetIsEnabled(button));
        tip.IsOpen = false;
        WpfAppHost.Pump();
      }));
    }
  }
}
