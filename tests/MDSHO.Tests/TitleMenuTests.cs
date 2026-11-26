using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using MDSHO.Common;
using MDSHO.Core;
using MDSHO.ViewModels;
using Xunit;

namespace MDSHO.Tests {

  /// <summary>
  /// The title bar's right-click menu of the Note, Comment and snippet windows: Text history, Wrap text, Keep on top,
  /// Font, Note colours, Clear all highlights and Close, only those that apply. The Text history button is gone.
  /// </summary>
  public class TitleMenuTests {

    private static string[] Headers(ContextMenu menu) {
      return menu.Items.Cast<object>().Select(i => i is MenuItem m ? (string)m.Header : "-").ToArray();
    }

    private static MenuItem Item(ContextMenu menu, string tag) {
      return menu.Items.OfType<MenuItem>().Single(i => (string)i.Tag == tag);
    }

    private static void Click(MenuItem item) {
      item.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent, item));
      WpfAppHost.Pump();
    }

    [Fact]
    public void A_note_has_every_item() {
      LiveHighlightTests.WithGroup(f => {
        TextEditWindow window = LiveHighlightTests.Open(f, "note");
        LiveHighlightTests.Shown(window, () => {
          Assert.Equal(new[] { "Text history", "-", "Wrap text", "Keep on top", "Font", "Note colours...", "-", "Clear all highlights", "-", "Close" },
            Headers(window.BuildTitleMenu()));
        });
      });
    }

    [Theory]
    [InlineData("comment")]
    [InlineData("snippet")]
    public void A_comment_or_snippet_has_only_the_items_that_apply(string kind) {
      LiveHighlightTests.WithGroup(f => {
        TextEditWindow window = LiveHighlightTests.Open(f, kind);
        LiveHighlightTests.Shown(window, () => {
          Assert.Equal(new[] { "Text history", "-", "Wrap text", "Font", "-", "Close" }, Headers(window.BuildTitleMenu()));
        });
      });
    }

    [Theory]
    [InlineData("note")]
    [InlineData("comment")]
    [InlineData("snippet")]
    public void The_history_button_is_gone_from_the_title_bar(string kind) {
      LiveHighlightTests.WithGroup(f => {
        TextEditWindow window = LiveHighlightTests.Open(f, kind);
        LiveHighlightTests.Shown(window, () => Assert.Null(window.FindName("ButtonHistory")));
      });
    }

    [Fact]
    public void The_items_show_the_app_icons_and_a_tick_while_on() {
      LiveHighlightTests.WithGroup(f => {
        TextEditWindow window = LiveHighlightTests.Open(f, "note");
        LiveHighlightTests.Shown(window, () => {
          IconGlyphs icons = f.Win.Appearance.Icons;
          ContextMenu menu = window.BuildTitleMenu();
          Assert.Equal(icons.Time, ((TextBlock)Item(menu, "History").Icon).Text);
          Assert.Equal(icons.Font, ((TextBlock)Item(menu, "Font").Icon).Text);
          Assert.Equal(icons.Brush, ((TextBlock)Item(menu, "Colours").Icon).Text);
          Assert.Equal(icons.Times, ((TextBlock)Item(menu, "Close").Icon).Text);
          Assert.False(Item(menu, "Wrap").IsChecked);
          Assert.False(Item(menu, "Pin").IsChecked);
          Click(Item(menu, "Wrap"));
          Click(Item(menu, "Pin"));
          Assert.True(window.IsWrapText);
          Assert.True(window.Topmost);
          menu = window.BuildTitleMenu();
          Assert.True(Item(menu, "Wrap").IsChecked);
          Assert.True(Item(menu, "Pin").IsChecked);
          Click(Item(menu, "Wrap"));
          Assert.False(window.IsWrapText);
        });
      });
    }

    [Fact]
    public void Text_history_lists_the_same_rows_as_the_history_menu() {
      LiveHighlightTests.WithGroup(f => {
        TextEditWindow window = LiveHighlightTests.Open(f, "note");
        window.Shortcut.KeepHistory(HistoryFields.Text, "older list", new DateTime(2026, 11, 20, 9, 0, 0));
        LiveHighlightTests.Shown(window, () => {
          MenuItem history = Item(window.BuildTitleMenu(), "History");
          Assert.Equal(window.BuildHistoryMenu().Items.OfType<MenuItem>().Where(i => i.Tag as string != "Header").Select(i => i.Header).ToArray(),
            history.Items.OfType<MenuItem>().Select(i => i.Header).ToArray());
          Assert.Contains(history.Items.OfType<MenuItem>(), i => i.Tag is HistoryEntry h && h.Text == "older list");
        });
      });
    }

    [Fact]
    public void Font_lists_the_fonts_and_a_pick_applies() {
      LiveHighlightTests.WithGroup(f => {
        TextEditWindow window = LiveHighlightTests.Open(f, "note");
        LiveHighlightTests.Shown(window, () => {
          MenuItem font = Item(window.BuildTitleMenu(), "Font");
          MenuItem arial = font.Items.OfType<MenuItem>().Single(i => (string)i.Header == "Arial");
          Click(arial);
          Assert.Equal("Arial", window.Shortcut.TextFont);
          Assert.True(Item(window.BuildTitleMenu(), "Font").Items.OfType<MenuItem>().Single(i => (string)i.Header == "Arial").IsChecked);
        });
      });
    }

    [Fact]
    public void Note_colours_opens_the_colours_window() {
      LiveHighlightTests.WithGroup(f => {
        TextEditWindow window = LiveHighlightTests.Open(f, "note");
        Func<Window, bool?> old = TextEditWindow.ShowNoteColoursDialog;
        Window shown = null;
        TextEditWindow.ShowNoteColoursDialog = w => { shown = w; return false; };
        try {
          LiveHighlightTests.Shown(window, () => {
            Click(Item(window.BuildTitleMenu(), "Colours"));
            Assert.IsType<ColorsWindow>(shown);
          });
        } finally {
          TextEditWindow.ShowNoteColoursDialog = old;
        }
      });
    }

    [Fact]
    public void Clear_all_highlights_removes_every_saved_colour() {
      LiveHighlightTests.WithGroup(f => {
        TextEditWindow window = LiveHighlightTests.Open(f, "note");
        LiveHighlightTests.Shown(window, () => {
          Assert.False(Item(window.BuildTitleMenu(), "ClearHighlights").IsEnabled);
          window.Shortcut.SaveNoteHighlights(NoteHighlights.With(NoteHighlights.With("", "pay", "Yellow"), "rent", "Green"));
          MenuItem clear = Item(window.BuildTitleMenu(), "ClearHighlights");
          Assert.True(clear.IsEnabled);
          Click(clear);
          Assert.Equal("", window.Shortcut.NoteHighlights);
          Assert.Equal("", MDSHO.Data.DataStore.Shortcuts.Get(window.Shortcut.ID).NoteHighlights);
        });
      });
    }

    [Fact]
    public void Close_closes_the_window() {
      LiveHighlightTests.WithGroup(f => {
        TextEditWindow window = LiveHighlightTests.Open(f, "comment");
        LiveHighlightTests.Shown(window, () => {
          Click(Item(window.BuildTitleMenu(), "Close"));
          Assert.False(window.IsVisible);
        });
      });
    }

    [Fact]
    public void A_right_click_on_the_title_bar_opens_the_menu_instead_of_the_system_menu() {
      LiveHighlightTests.WithGroup(f => {
        TextEditWindow window = LiveHighlightTests.Open(f, "note");
        LiveHighlightTests.Shown(window, () => {
          const int WM_NCRBUTTONUP = 0x00A5, HTCAPTION = 2, HTCLIENT = 1;
          Assert.False(window.HandleTitleMessage(WM_NCRBUTTONUP, new IntPtr(HTCLIENT)));
          Assert.Null(window.TitleMenu);
          try {
            Assert.True(window.HandleTitleMessage(WM_NCRBUTTONUP, new IntPtr(HTCAPTION)));
            WpfAppHost.Pump();
            Assert.True(window.TitleMenu.IsOpen);
            Assert.Equal(System.Windows.Controls.Primitives.PlacementMode.MousePoint, window.TitleMenu.Placement);
          } finally {
            window.TitleMenu.IsOpen = false;
            WpfAppHost.Pump();
          }
          // The title text takes the mouse itself, so it has the menu too.
          FrameworkElement bar = LiveHighlightTests.Named<FrameworkElement>(window, "TitleBar");
          Assert.NotNull(bar.ContextMenu);
        });
      });
    }
  }
}
