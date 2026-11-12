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
    public void The_preview_keeps_up_to_ten_lines_and_marks_the_cut() {
      WithButtons(f => {
        f.Command.Comment = "one\r\ntwo";
        Assert.Equal("one" + Environment.NewLine + "two", f.Command.CommentPreview);
        f.Command.Comment = string.Join("\r\n", Enumerable.Range(1, 12).Select(i => "line " + i));
        string[] lines = f.Command.CommentPreview.Split(Environment.NewLine);
        Assert.Equal(11, lines.Length);
        Assert.Equal("line 10", lines[9]);
        Assert.Equal("…", lines[10]);
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
    public void Save_stores_the_comment_and_closes() {
      WithButtons(f => {
        TextEditWindow window = TextEditWindow.ForComment(f.Command);
        Shown(window, () => {
          Named<TextBox>(window, "TextComment").Text = "line 1\r\nline 2";
          Click(Named<Button>(window, "ButtonOk"));
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
          Click(Named<Button>(window, "ButtonOk"));
        });
        Assert.Equal("", f.Command.Comment);
        Assert.Equal("", StoredComment(f.Command.ID));
      });
    }

    [Fact]
    public void Close_and_escape_keep_the_old_comment() {
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
          Assert.Equal("old", f.Command.Comment);
          Assert.Equal("old", StoredComment(f.Command.ID));
        });
      }
    }

    [Fact]
    public void OK_uses_the_ok_icon_of_the_solid_or_outline_set() {
      WithButtons(f => {
        IconGlyphs icons = f.Command.OwningWindow.Appearance.Icons;
        bool solid = icons.SolidIcons;
        TextEditWindow window = TextEditWindow.ForComment(f.Command);
        try {
          Shown(window, () => {
            Button ok = Named<Button>(window, "ButtonOk");
            Assert.NotEqual("Segoe MDL2 Assets", ok.FontFamily.Source);
            Assert.Equal(icons.Ok, ok.Content);
            icons.SolidIcons = !solid;
            WpfAppHost.Pump();
            Assert.Equal(icons.Ok, ok.Content);
          });
        } finally {
          icons.SolidIcons = solid;
        }
      });
    }

    [Fact]
    public void OK_is_greyed_out_until_the_comment_changes() {
      WithButtons(f => {
        f.Command.Comment = "old";
        TextEditWindow window = TextEditWindow.ForComment(f.Command);
        Shown(window, () => {
          Button ok = Named<Button>(window, "ButtonOk");
          TextBox text = Named<TextBox>(window, "TextComment");
          Assert.False(ok.IsEnabled);
          text.Text = "old and new";
          Assert.True(ok.IsEnabled);
          text.Text = "old";
          Assert.False(ok.IsEnabled);
        });
      });
    }

    [Fact]
    public void Only_spaces_in_an_empty_comment_is_no_change() {
      WithButtons(f => {
        TextEditWindow window = TextEditWindow.ForComment(f.Command);
        Shown(window, () => {
          Named<TextBox>(window, "TextComment").Text = "  \r\n ";
          Assert.False(Named<Button>(window, "ButtonOk").IsEnabled);
        });
      });
    }

    [Fact]
    public void Ctrl_Enter_does_nothing_while_there_is_no_change() {
      WithButtons(f => {
        f.Command.Comment = "old";
        TextEditWindow window = TextEditWindow.ForComment(f.Command);
        Shown(window, () => {
          PressKey(window, Key.Enter, ModifierKeys.Control);
          Assert.True(window.IsVisible);
          Assert.False(window.Saved);
        });
      });
    }

    [Fact]
    public void Ctrl_Enter_saves_and_plain_Enter_does_not() {
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
        tip.DataContext = f.Command;
        tip.ApplyTemplate();
        StackPanel panel = Assert.IsType<StackPanel>(tip.Content);
        panel.DataContext = f.Command;
        panel.Measure(new Size(1000, 1000));
        List<TextBlock> texts = panel.Children.OfType<TextBlock>().ToList();
        Assert.Equal("cmd.exe /K ping SRV01", texts[0].Text);
        Assert.Contains(panel.Children.OfType<Separator>(), s => s.Visibility == Visibility.Visible);
        Assert.Equal("Call Anna first.", texts[1].Text);
        Assert.True(ToolTipService.GetIsEnabled(button));
      }));
    }
  }
}
