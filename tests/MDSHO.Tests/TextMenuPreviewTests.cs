using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Shapes;
using MDSHO.Core;
using MDSHO.Helpers;
using MDSHO.ViewModels;
using Xunit;

namespace MDSHO.Tests {

  /// <summary>
  /// The Text size and Font menus of notes, comments and snippets (the size and font buttons, and the More options
  /// submenus) preview a size or font while it is highlighted. A click keeps it; anything else puts the old one back.
  /// The text size button and the three dots are bold with solid icons and normal with outline icons.
  /// </summary>
  public class TextMenuPreviewTests {

    // ---- Bold text buttons ----

    [Theory]
    [InlineData("note")]
    [InlineData("comment")]
    [InlineData("snippet")]
    public void The_size_button_and_the_dots_follow_solid_icons(string kind) {
      LiveHighlightTests.WithGroup(f => {
        TextEditWindow window = LiveHighlightTests.Open(f, kind);
        LiveHighlightTests.Shown(window, () => {
          Button size = LiveHighlightTests.Named<Button>(window, "ButtonSize");
          Button more = LiveHighlightTests.Named<Button>(window, "ButtonMore");
          IconGlyphs icons = f.Win.Appearance.Icons;
          foreach (bool solid in new[] { icons.SolidIcons, !icons.SolidIcons, icons.SolidIcons }) {
            icons.SolidIcons = solid;
            WpfAppHost.Pump();
            FontWeight expected = solid ? FontWeights.Bold : FontWeights.Normal;
            Assert.Equal(expected, size.FontWeight);
            Assert.Equal(expected, more.FontWeight);
          }
          Assert.Equal("•••", more.Content);
          Assert.Equal("Segoe UI", more.FontFamily.Source);
          Assert.Equal(11, more.FontSize);
          Assert.Equal("Segoe UI", size.FontFamily.Source);
          Assert.Equal(12, size.FontSize);
        });
      });
    }

    // ---- Helpers ----

    private static TextBox Text(Window window) => LiveHighlightTests.Named<TextBox>(window, "TextComment");

    private static double Dips(long points) => NoteWindowPlace.ToDips(points);

    private static void Hover(MenuItem item) {
      item.RaiseEvent(new MouseEventArgs(Mouse.PrimaryDevice, 0) { RoutedEvent = Mouse.MouseEnterEvent });
      WpfAppHost.Pump();
      Assert.True(item.IsHighlighted, item.Header + " is not highlighted");
    }

    // Arrow keys onto an item: it takes the keyboard focus and is highlighted. (A raised MouseEnter doesn't highlight a
    // submenu item in the test host.)
    private static void KeyTo(MenuItem item) {
      item.Focus();
      WpfAppHost.Pump();
      Assert.True(item.IsHighlighted, item.Header + " is not highlighted");
    }

    private static void Leave(MenuItem item) {
      item.RaiseEvent(new MouseEventArgs(Mouse.PrimaryDevice, 0) { RoutedEvent = Mouse.MouseLeaveEvent });
      WpfAppHost.Pump();
    }

    private static void Click(MenuItem item) {
      item.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent, item));
      WpfAppHost.Pump();
    }

    private static void CloseMenu(ContextMenu menu) {
      menu.IsOpen = false;
      WpfAppHost.Pump();
    }

    private static MenuItem Item(ItemsControl menu, string header) =>
      menu.Items.OfType<MenuItem>().Single(i => i.Header as string == header);

    private static ContextMenu OpenSizeMenu(TextEditWindow window) {
      Button size = LiveHighlightTests.Named<Button>(window, "ButtonSize");
      size.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent, size));
      WpfAppHost.Pump();
      Assert.True(window.SizeMenu.IsOpen);
      return window.SizeMenu;
    }

    private static ContextMenu OpenFontMenu(TextEditWindow window) {
      Button font = LiveHighlightTests.Named<Button>(window, "ButtonFont");
      font.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent, font));
      WpfAppHost.Pump();
      Assert.True(window.FontMenu.IsOpen);
      return window.FontMenu;
    }

    // The Text size or Font submenu of More options, open.
    private static MenuItem OpenSubmenu(TextEditWindow window, string tag) {
      window.OpenMoreMenu();
      WpfAppHost.Pump();
      MenuItem sub = window.TitleMenu.Items.OfType<MenuItem>().Single(i => i.Tag as string == tag);
      ShowSubmenu(sub);
      return sub;
    }

    private static void ShowSubmenu(MenuItem sub) {
      sub.IsSubmenuOpen = true;
      // The submenu's popup shows a moment later.
      for (int i = 0; i < 50 && !sub.Items.OfType<MenuItem>().All(m => m.IsLoaded); i++) {
        WpfAppHost.Pump();
      }
      Assert.True(sub.Items.OfType<MenuItem>().All(m => m.IsLoaded), "The submenu did not show");
    }

    private static string FontName(Window window) => Text(window).FontFamily.Source;

    private static string Resolved(string font) => NoteFontFamilies.Resolve(font).Source;

    // ---- Text size ----

    [Theory]
    [InlineData("note")]
    [InlineData("comment")]
    [InlineData("snippet")]
    public void Hovering_a_size_previews_it_and_leaving_puts_it_back(string kind) {
      LiveHighlightTests.WithGroup(f => {
        TextEditWindow window = LiveHighlightTests.Open(f, kind);
        LiveHighlightTests.Shown(window, () => {
          Button button = LiveHighlightTests.Named<Button>(window, "ButtonSize");
          double before = Text(window).FontSize;
          object label = button.Content;
          ContextMenu menu = OpenSizeMenu(window);
          try {
            List<bool> ticks = menu.Items.OfType<MenuItem>().Select(i => i.IsChecked).ToList();
            Hover(Item(menu, "24 pt"));
            Assert.Equal(Dips(24), Text(window).FontSize, 3);
            // The label and the ticks change only on a click.
            Assert.Equal(label, button.Content);
            Assert.Equal(ticks, menu.Items.OfType<MenuItem>().Select(i => i.IsChecked).ToList());
            // The next size, then off the list (onto the separator or a header).
            Leave(Item(menu, "24 pt"));
            Hover(Item(menu, "36 pt"));
            Assert.Equal(Dips(36), Text(window).FontSize, 3);
            Leave(Item(menu, "36 pt"));
            Assert.Equal(before, Text(window).FontSize, 3);
          } finally {
            CloseMenu(menu);
          }
          Assert.Equal(before, Text(window).FontSize, 3);
        });
      });
    }

    [Theory]
    [InlineData("note")]
    [InlineData("comment")]
    [InlineData("snippet")]
    public void Closing_the_size_menu_without_a_click_puts_the_size_back(string kind) {
      LiveHighlightTests.WithGroup(f => {
        TextEditWindow window = LiveHighlightTests.Open(f, kind);
        LiveHighlightTests.Shown(window, () => {
          double before = Text(window).FontSize;
          ContextMenu menu = OpenSizeMenu(window);
          Hover(Item(menu, "48 pt"));
          Assert.Equal(Dips(48), Text(window).FontSize, 3);
          CloseMenu(menu);
          Assert.Equal(before, Text(window).FontSize, 3);
          Assert.Equal("12 pt", LiveHighlightTests.Named<Button>(window, "ButtonSize").Content);
        });
      });
    }

    [Theory]
    [InlineData("note")]
    [InlineData("comment")]
    [InlineData("snippet")]
    public void Arrow_keys_preview_a_size_too(string kind) {
      LiveHighlightTests.WithGroup(f => {
        TextEditWindow window = LiveHighlightTests.Open(f, kind);
        LiveHighlightTests.Shown(window, () => {
          double before = Text(window).FontSize;
          ContextMenu menu = OpenSizeMenu(window);
          Item(menu, "18 pt").Focus();
          WpfAppHost.Pump();
          Assert.Equal(Dips(18), Text(window).FontSize, 3);
          CloseMenu(menu);
          Assert.Equal(before, Text(window).FontSize, 3);
        });
      });
    }

    [Theory]
    [InlineData("note")]
    [InlineData("comment")]
    [InlineData("snippet")]
    public void A_click_keeps_the_previewed_size_and_moves_the_label(string kind) {
      LiveHighlightTests.WithGroup(f => {
        TextEditWindow window = LiveHighlightTests.Open(f, kind);
        LiveHighlightTests.Shown(window, () => {
          ContextMenu menu = OpenSizeMenu(window);
          Hover(Item(menu, "24 pt"));
          Click(Item(menu, "24 pt"));
          CloseMenu(menu);
          Assert.Equal(Dips(24), Text(window).FontSize, 3);
          Assert.Equal("24 pt", LiveHighlightTests.Named<Button>(window, "ButtonSize").Content);
          Assert.Equal(new[] { "24 pt" }, window.BuildSizeMenu().Items.OfType<MenuItem>().Where(i => i.IsChecked).Select(i => (string)i.Header));
        });
      });
    }

    [Fact]
    public void The_current_line_follows_the_previewed_size() {
      LiveHighlightTests.WithGroup(f => {
        TextEditWindow window = LiveHighlightTests.Open(f, "note");
        LiveHighlightTests.Shown(window, () => {
          Text(window).Text = "Buy milk\r\nCall the dentist";
          Text(window).CaretIndex = 0;
          WpfAppHost.Pump();
          Rectangle line = LiveHighlightTests.Named<Canvas>(window, "LineLayer").Children.OfType<Rectangle>().Single();
          double before = line.Height;
          ContextMenu menu = OpenSizeMenu(window);
          Hover(Item(menu, "48 pt"));
          WpfAppHost.Pump();
          Rectangle bigger = LiveHighlightTests.Named<Canvas>(window, "LineLayer").Children.OfType<Rectangle>().Single();
          Assert.True(bigger.Height > before * 2, before + " -> " + bigger.Height);
          CloseMenu(menu);
          Rectangle back = LiveHighlightTests.Named<Canvas>(window, "LineLayer").Children.OfType<Rectangle>().Single();
          Assert.Equal(before, back.Height, 1);
        });
      });
    }

    [Theory]
    [InlineData("note")]
    [InlineData("comment")]
    [InlineData("snippet")]
    public void The_more_options_size_submenu_previews_and_puts_back(string kind) {
      LiveHighlightTests.WithGroup(f => {
        TextEditWindow window = LiveHighlightTests.Open(f, kind);
        LiveHighlightTests.Shown(window, () => {
          double before = Text(window).FontSize;
          MenuItem sub = OpenSubmenu(window, "Size");
          KeyTo(Item(sub, "36 pt"));
          Assert.Equal(Dips(36), Text(window).FontSize, 3);
          // Onto another item of the title menu, which isn't a size.
          KeyTo(window.TitleMenu.Items.OfType<MenuItem>().Single(i => i.Tag as string == "Wrap"));
          Assert.False(Item(sub, "36 pt").IsHighlighted);
          Assert.Equal(before, Text(window).FontSize, 3);
          ShowSubmenu(sub);
          KeyTo(Item(sub, "18 pt"));
          Assert.Equal(Dips(18), Text(window).FontSize, 3);
          CloseMenu(window.TitleMenu);
          Assert.Equal(before, Text(window).FontSize, 3);
          // A click keeps it.
          sub = OpenSubmenu(window, "Size");
          KeyTo(Item(sub, "18 pt"));
          Click(Item(sub, "18 pt"));
          CloseMenu(window.TitleMenu);
          Assert.Equal(Dips(18), Text(window).FontSize, 3);
          Assert.Equal("18 pt", LiveHighlightTests.Named<Button>(window, "ButtonSize").Content);
        });
      });
    }

    // ---- Font ----

    [Theory]
    [InlineData("note")]
    [InlineData("comment")]
    [InlineData("snippet")]
    public void Hovering_a_font_previews_it_and_closing_puts_it_back(string kind) {
      LiveHighlightTests.WithGroup(f => {
        TextEditWindow window = LiveHighlightTests.Open(f, kind);
        LiveHighlightTests.Shown(window, () => {
          string before = FontName(window);
          Assert.NotEqual(Resolved("Consolas"), before);
          ContextMenu menu = OpenFontMenu(window);
          List<bool> ticks = menu.Items.OfType<MenuItem>().Select(i => i.IsChecked).ToList();
          Hover(Item(menu, "Consolas"));
          Assert.Equal(Resolved("Consolas"), FontName(window));
          Assert.Equal(ticks, menu.Items.OfType<MenuItem>().Select(i => i.IsChecked).ToList());
          Leave(Item(menu, "Consolas"));
          Assert.Equal(before, FontName(window));
          Item(menu, "Arial").Focus();
          WpfAppHost.Pump();
          Assert.Equal(Resolved("Arial"), FontName(window));
          CloseMenu(menu);
          Assert.Equal(before, FontName(window));
        });
      });
    }

    [Theory]
    [InlineData("note")]
    [InlineData("comment")]
    [InlineData("snippet")]
    public void A_click_keeps_the_previewed_font(string kind) {
      LiveHighlightTests.WithGroup(f => {
        TextEditWindow window = LiveHighlightTests.Open(f, kind);
        LiveHighlightTests.Shown(window, () => {
          ContextMenu menu = OpenFontMenu(window);
          Hover(Item(menu, "Consolas"));
          Click(Item(menu, "Consolas"));
          CloseMenu(menu);
          Assert.Equal(Resolved("Consolas"), FontName(window));
          Assert.Equal("Consolas", kind == "comment" ? window.Shortcut.CommentFont : window.Shortcut.TextFont);
        });
      });
    }

    [Theory]
    [InlineData("note")]
    [InlineData("comment")]
    [InlineData("snippet")]
    public void The_more_options_font_submenu_previews_and_puts_back(string kind) {
      LiveHighlightTests.WithGroup(f => {
        TextEditWindow window = LiveHighlightTests.Open(f, kind);
        LiveHighlightTests.Shown(window, () => {
          string before = FontName(window);
          MenuItem sub = OpenSubmenu(window, "Font");
          KeyTo(Item(sub, "Consolas"));
          Assert.Equal(Resolved("Consolas"), FontName(window));
          CloseMenu(window.TitleMenu);
          Assert.Equal(before, FontName(window));
          sub = OpenSubmenu(window, "Font");
          KeyTo(Item(sub, "Consolas"));
          Click(Item(sub, "Consolas"));
          CloseMenu(window.TitleMenu);
          Assert.Equal(Resolved("Consolas"), FontName(window));
        });
      });
    }
  }
}
