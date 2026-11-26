using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using MDSHO.Common;
using MDSHO.ViewModels;
using Xunit;

namespace MDSHO.Tests {

  /// <summary>
  /// The More options button (⋯) just left of Close in the Note, Comment and snippet title bars: it opens the title menu
  /// below it, right edges lined up, and looks pressed while the menu is open. Alt+F opens the menu too.
  /// </summary>
  public class MoreButtonTests {

    private static void Click(Button button) {
      button.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent, button));
      WpfAppHost.Pump();
    }

    private static Rect BoundsIn(FrameworkElement element, FrameworkElement root) {
      return element.TransformToAncestor(root).TransformBounds(new Rect(element.RenderSize));
    }

    [Theory]
    [InlineData("note")]
    [InlineData("comment")]
    [InlineData("snippet")]
    public void The_button_sits_just_left_of_close_with_its_tooltip(string kind) {
      LiveHighlightTests.WithGroup(f => {
        TextEditWindow window = LiveHighlightTests.Open(f, kind);
        LiveHighlightTests.Shown(window, () => {
          window.UpdateLayout();
          Button more = LiveHighlightTests.Named<Button>(window, "ButtonMore");
          Button close = LiveHighlightTests.Named<Button>(window, "ButtonClose");
          FrameworkElement bar = LiveHighlightTests.Named<FrameworkElement>(window, "TitleBar");
          Assert.Equal(Visibility.Visible, more.Visibility);
          Assert.Equal(BoundsIn(close, bar).Left, BoundsIn(more, bar).Right, 1);
          Assert.Equal(close.ActualWidth, more.ActualWidth, 1);
          Assert.Equal("More options", more.ToolTip);
          Assert.Equal("More options", System.Windows.Automation.AutomationProperties.GetName(more));
          Assert.True(System.Windows.Shell.WindowChrome.GetIsHitTestVisibleInChrome(more));
          Assert.False(more.Focusable);
          Assert.Equal(f.Win.Appearance.Icons.More, more.Content);
          // It looks like the other title bar buttons: their style and colour.
          Assert.Same(close.Style, more.Style);
          Assert.Equal(close.Foreground.ToString(), more.Foreground.ToString());
        });
      });
    }

    [Fact]
    public void The_icon_is_three_dots_in_a_ui_font_until_the_icon_font_has_one() {
      Assert.Equal("•••", new IconGlyphs(true).More);
      Assert.Equal("•••", new IconGlyphs(false).More);
      // A glyph of the icon font (the private use area) uses the icon font, any other text Segoe UI: swapping in a glyph
      // later only changes IconGlyphs.More.
      Assert.False(TextEditWindow.IsIconFontGlyph("•••"));
      Assert.True(TextEditWindow.IsIconFontGlyph(UnicodeText.FromHex("e960")));
      LiveHighlightTests.WithGroup(f => {
        TextEditWindow window = LiveHighlightTests.Open(f, "note");
        LiveHighlightTests.Shown(window, () => {
          Button more = LiveHighlightTests.Named<Button>(window, "ButtonMore");
          Assert.Equal("Segoe UI", more.FontFamily.Source);
          f.Win.Appearance.Icons.SolidIcons = !f.Win.Appearance.Icons.SolidIcons;
          WpfAppHost.Pump();
          Assert.Equal(f.Win.Appearance.Icons.More, more.Content);
        });
      });
    }

    [Theory]
    [InlineData("note")]
    [InlineData("comment")]
    public void A_click_opens_the_title_menu_below_the_button_with_right_edges_lined_up(string kind) {
      LiveHighlightTests.WithGroup(f => {
        TextEditWindow window = LiveHighlightTests.Open(f, kind);
        LiveHighlightTests.Shown(window, () => {
          Button more = LiveHighlightTests.Named<Button>(window, "ButtonMore");
          Click(more);
          ContextMenu menu = window.TitleMenu;
          try {
            Assert.True(menu.IsOpen);
            Assert.Same(more, menu.PlacementTarget);
            Assert.Equal(PlacementMode.Custom, menu.Placement);
            CustomPopupPlacement first = menu.CustomPopupPlacementCallback(new Size(200, 150), new Size(40, 32), new Point())[0];
            Assert.Equal(new Point(-160, 32), first.Point);
            Assert.Equal(window.BuildTitleMenu().Items.Count, menu.Items.Count);
          } finally {
            menu.IsOpen = false;
            WpfAppHost.Pump();
          }
        });
      });
    }

    [Fact]
    public void The_button_looks_pressed_while_the_menu_is_open() {
      LiveHighlightTests.WithGroup(f => {
        TextEditWindow window = LiveHighlightTests.Open(f, "note");
        LiveHighlightTests.Shown(window, () => {
          Button more = LiveHighlightTests.Named<Button>(window, "ButtonMore");
          Click(more);
          Assert.Same(window.TryFindResource("Theme.TitleButtonPressed"), more.Background);
          window.TitleMenu.IsOpen = false;
          WpfAppHost.Pump();
          Assert.Equal(DependencyProperty.UnsetValue, more.ReadLocalValue(Control.BackgroundProperty));
        });
      });
    }

    [Theory]
    [InlineData("note")]
    [InlineData("snippet")]
    public void Alt_F_opens_the_title_menu_below_the_button(string kind) {
      LiveHighlightTests.WithGroup(f => {
        TextEditWindow window = LiveHighlightTests.Open(f, kind);
        Func<ModifierKeys> old = TextEditWindow.KeyboardModifiers;
        TextEditWindow.KeyboardModifiers = () => ModifierKeys.Alt;
        try {
          LiveHighlightTests.Shown(window, () => {
            TextBox editor = LiveHighlightTests.Named<TextBox>(window, "TextComment");
            KeyEventArgs args = new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(editor), 0, Key.F) { RoutedEvent = Keyboard.PreviewKeyDownEvent };
            typeof(KeyEventArgs).GetMethod("MarkSystem", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).Invoke(args, null);
            editor.RaiseEvent(args);
            WpfAppHost.Pump();
            try {
              Assert.True(args.Handled);
              Assert.True(window.TitleMenu.IsOpen);
              Assert.Same(LiveHighlightTests.Named<Button>(window, "ButtonMore"), window.TitleMenu.PlacementTarget);
            } finally {
              window.TitleMenu.IsOpen = false;
              WpfAppHost.Pump();
            }
          });
        } finally {
          TextEditWindow.KeyboardModifiers = old;
        }
      });
    }

    [Fact]
    public void F_without_alt_is_typed_and_shift_f10_stays_with_the_text() {
      LiveHighlightTests.WithGroup(f => {
        TextEditWindow window = LiveHighlightTests.Open(f, "note");
        LiveHighlightTests.Shown(window, () => {
          TextBox editor = LiveHighlightTests.Named<TextBox>(window, "TextComment");
          KeyEventArgs args = new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(editor), 0, Key.F) { RoutedEvent = Keyboard.PreviewKeyDownEvent };
          editor.RaiseEvent(args);
          WpfAppHost.Pump();
          Assert.False(args.Handled);
          Assert.Null(window.TitleMenu);
          // The text box keeps its own menu (Shift+F10 and the Menu key open it).
          Assert.NotNull(editor.ContextMenu);
        });
      });
    }
  }
}
