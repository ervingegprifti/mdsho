using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using MDSHO.Common;
using MDSHO.ViewModels;
using Xunit;

namespace MDSHO.Tests {

  /// <summary>
  /// The icon font's glyphs are drawn on a 16×16 pixel grid, so every glyph in a title bar is 16 px in a 16×16 box,
  /// like the menu icons. A smaller size scales the glyph off the pixel grid and blurs it.
  /// </summary>
  public class TitleBarIconSizeTests {

    private static bool InIconFont(TextBlock text) {
      return text.FontFamily.Source.EndsWith("#MDSHOFont", StringComparison.Ordinal);
    }

    private static IEnumerable<DependencyObject> Descendants(DependencyObject root) {
      for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++) {
        DependencyObject child = VisualTreeHelper.GetChild(root, i);
        yield return child;
        foreach (DependencyObject d in Descendants(child)) { yield return d; }
      }
    }

    // The box layout gives the glyph (its text line is taller, WPF clips it to the box like the menu icons).
    private static double BoxWidth(TextBlock t) { return t.DesiredSize.Width - t.Margin.Left - t.Margin.Right; }
    private static double BoxHeight(TextBlock t) { return t.DesiredSize.Height - t.Margin.Top - t.Margin.Bottom; }

    // Every shown icon-font glyph in the title bar: its name, size and box, for one readable failure message.
    private static void AssertIconsAre16(FrameworkElement bar, int atLeast) {
      bar.UpdateLayout();
      List<TextBlock> icons = Descendants(bar).OfType<TextBlock>()
        .Where(t => t.IsVisible && !string.IsNullOrEmpty(t.Text) && InIconFont(t)).ToList();
      Assert.True(icons.Count >= atLeast, "Icons found: " + icons.Count);
      List<string> wrong = icons
        .Where(t => t.FontSize != 16 || t.Width != 16 || t.Height != 16 || BoxWidth(t) != 16 || BoxHeight(t) != 16)
        .Select(t => (t.Name.Length > 0 ? t.Name : ((t.TemplatedParent as FrameworkElement)?.TemplatedParent as FrameworkElement)?.Name ?? "?")
          + " " + t.FontSize + " px, " + BoxWidth(t) + "×" + BoxHeight(t)).ToList();
      Assert.True(wrong.Count == 0, string.Join("; ", wrong));
    }

    private static void AssertButtonsAre40By32(FrameworkElement bar) {
      foreach (Button button in Descendants(bar).OfType<Button>().Where(b => b.IsVisible && b.Name != "ButtonSize")) {
        Assert.Equal(40, button.ActualWidth);
        Assert.Equal(32, button.ActualHeight);
      }
    }

    [Theory]
    [InlineData("note", 5)]
    [InlineData("comment", 4)]
    [InlineData("snippet", 4)]
    public void Text_window_title_bar_icons_are_16_px_in_a_16_px_box(string kind, int icons) {
      // The title icon, font, wrap text, close, and for a note the pin.
      LiveHighlightTests.WithGroup(f => {
        TextEditWindow window = LiveHighlightTests.Open(f, kind);
        LiveHighlightTests.Shown(window, () => {
          FrameworkElement bar = LiveHighlightTests.Named<FrameworkElement>(window, "TitleBar");
          AssertIconsAre16(bar, icons);
          AssertButtonsAre40By32(bar);
          Assert.Equal(14, LiveHighlightTests.Named<TextBlock>(window, "TextTitle").FontSize);
          // The text size button and the three dots of More options are text, not icons.
          Button size = LiveHighlightTests.Named<Button>(window, "ButtonSize");
          Assert.Equal("Segoe UI", size.FontFamily.Source);
          Assert.Equal(12, size.FontSize);
          Button more = LiveHighlightTests.Named<Button>(window, "ButtonMore");
          Assert.Equal("•••", more.Content);
          Assert.Equal("Segoe UI", more.FontFamily.Source);
          Assert.Equal(11, more.FontSize);
        });
      });
    }

    [Fact]
    public void A_pinned_note_keeps_its_pin_icon_16_px() {
      LiveHighlightTests.WithGroup(f => {
        TextEditWindow window = LiveHighlightTests.Open(f, "note");
        LiveHighlightTests.Shown(window, () => {
          LiveHighlightTests.Named<Button>(window, "ButtonPin").RaiseEvent(
            new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
          WpfAppHost.Pump();
          Assert.True(window.Topmost);
          AssertIconsAre16(LiveHighlightTests.Named<FrameworkElement>(window, "TitleBar"), 5);
        });
      });
    }

    [Fact]
    public void More_options_with_an_icon_font_glyph_is_16_px_too() {
      LiveHighlightTests.WithGroup(f => {
        TextEditWindow window = LiveHighlightTests.Open(f, "note");
        LiveHighlightTests.Shown(window, () => {
          MethodInfo show = typeof(TextEditWindow).GetMethod("ShowMoreIcon", BindingFlags.Instance | BindingFlags.NonPublic);
          show.Invoke(window, new object[] { UnicodeText.FromHex("e960") });
          WpfAppHost.Pump();
          Button more = LiveHighlightTests.Named<Button>(window, "ButtonMore");
          Assert.Equal(16, more.FontSize);
          AssertIconsAre16(LiveHighlightTests.Named<FrameworkElement>(window, "TitleBar"), 6);
          // And back to the three dots as they look today.
          show.Invoke(window, new object[] { "•••" });
          WpfAppHost.Pump();
          Assert.Equal("Segoe UI", more.FontFamily.Source);
          Assert.Equal(11, more.FontSize);
        });
      });
    }

    [Fact]
    public void Run_command_title_bar_icons_are_16_px_in_a_16_px_box() {
      RunCommandWindowTests.WithCommand("cmd.exe", "", f => {
        RunCommandWindow window = new RunCommandWindow(new RunCommandViewModel(f.Command, false));
        MessageWindowTests.Shown(window, () => {
          FrameworkElement bar = (FrameworkElement)window.FindName("TitleBar");
          // The run icon, Examples and Close.
          AssertIconsAre16(bar, 3);
          AssertButtonsAre40By32(bar);
          Assert.Equal(14, ((TextBlock)window.FindName("TextTitle")).FontSize);
        });
      });
    }

    [Fact]
    public void Run_command_examples_title_bar_icons_are_16_px_in_a_16_px_box() {
      RunCommandWindowTests.WithCommand("cmd.exe", "", f => {
        RunCommandExamplesWindow window = new RunCommandExamplesWindow(new RunCommandViewModel(f.Command, false));
        MessageWindowTests.Shown(window, () => {
          FrameworkElement bar = (FrameworkElement)window.FindName("TitleBar");
          AssertIconsAre16(bar, 1);
          AssertButtonsAre40By32(bar);
        });
      });
    }

    [Fact]
    public void Dialog_title_bar_icons_are_16_px_in_a_16_px_box() {
      WpfAppHost.Run(() => {
        MessageWindow window = new MessageWindow("Title", "Heading.", "", MessageIcon.Info, MessageButtons.OK, MessageResult.None);
        MessageWindowTests.Shown(window, () => {
          FrameworkElement bar = (FrameworkElement)window.FindName("TitleBar");
          // The title icon and Close.
          AssertIconsAre16(bar, 2);
          AssertButtonsAre40By32(bar);
          Assert.Equal(14, ((TextBlock)bar.FindName("TextTitle")).FontSize);
        });
      });
    }
  }
}
