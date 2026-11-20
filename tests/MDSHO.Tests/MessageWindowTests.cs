using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using MDSHO.Common;
using MDSHO.Controls;
using MDSHO.ViewModels;
using Xunit;

namespace MDSHO.Tests {

  /// <summary>
  /// The one MDSHO dialog for every message: the rounded frame, MDSHO's title bar with the logo, a 28 px Info or Warning
  /// icon, one font size and Theme.Text for all the text (the first line may be SemiBold), and OK, OK/Cancel, Yes/No or
  /// Yes/No/Cancel with the first button dark and the others light. Windows are shown off screen.
  /// </summary>
  public class MessageWindowTests {

    internal static void Shown(Window window, Action check) {
      window.WindowStartupLocation = WindowStartupLocation.Manual;
      window.Left = -10000;
      window.Top = -10000;
      window.ShowActivated = false;
      window.ShowInTaskbar = false;
      bool closed = false;
      window.Closed += (s, e) => closed = true;
      window.Show();
      try {
        WpfAppHost.Pump();
        check();
      } finally {
        if (!closed) { window.Close(); }
        WpfAppHost.Pump();
      }
    }

    private static T Named<T>(Window window, string name) where T : class {
      return Assert.IsAssignableFrom<T>(window.FindName(name));
    }

    private static Color ColorOf(object brush) {
      return Assert.IsType<SolidColorBrush>(brush).Color;
    }

    private static void Click(Button button) {
      button.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent, button));
      WpfAppHost.Pump();
    }

    private static List<Button> Buttons(MessageWindow window) {
      return WpfAppHost.VisualDescendants<Button>(Named<FrameworkElement>(window, "PanelButtons")).ToList();
    }

    private static string AutomationNameOf(DependencyObject d) {
      return System.Windows.Automation.AutomationProperties.GetName(d);
    }

    [Fact]
    public void The_title_bar_shows_the_mdsho_logo_and_the_title() {
      WpfAppHost.Run(() => {
        MessageWindow window = new MessageWindow("Delete window confirmation", "Delete it?", "", MessageIcon.Warning, MessageButtons.YesNo, MessageResult.No);
        Shown(window, () => {
          Assert.StartsWith("Delete window confirmation", window.Title);
          DialogTitleBar bar = Named<DialogTitleBar>(window, "TitleBar");
          Assert.Equal(new IconGlyphs(true).MDSHO, bar.Glyph);
          Assert.Equal(new IconGlyphs(true).Times, bar.CloseGlyph);
        });
      });
    }

    [Fact]
    public void An_empty_title_is_mdsho() {
      WpfAppHost.Run(() => {
        MessageWindow window = new MessageWindow("", "Hi.", "", MessageIcon.Info, MessageButtons.OK, MessageResult.None);
        Shown(window, () => Assert.StartsWith("MDSHO", window.Title));
      });
    }

    [Theory]
    [InlineData(MessageIcon.Info)]
    [InlineData(MessageIcon.Warning)]
    public void The_body_has_a_28_pixel_info_or_warning_icon_on_the_left(MessageIcon icon) {
      WpfAppHost.Run(() => {
        MessageWindow window = new MessageWindow("T", "Heading.", "Text.", icon, MessageButtons.OK, MessageResult.None);
        Shown(window, () => {
          IconGlyphs icons = new IconGlyphs(true);
          TextBlock glyph = Named<TextBlock>(window, "TextIcon");
          Assert.Equal(icon == MessageIcon.Info ? icons.Info : icons.Warning, glyph.Text);
          Assert.Equal(28, glyph.FontSize);
          Assert.Equal(icon == MessageIcon.Info ? "Information" : "Warning", AutomationNameOf(glyph));
          TextBlock heading = Named<TextBlock>(window, "TextHeading");
          Assert.True(glyph.TranslatePoint(new Point(0, 0), window).X < heading.TranslatePoint(new Point(0, 0), window).X);
        });
      });
    }

    [Fact]
    public void All_the_text_is_one_size_and_theme_text_and_only_the_heading_is_semibold() {
      WpfAppHost.Run(() => {
        MessageWindow window = new MessageWindow("T", "Heading.", "The text.", MessageIcon.Warning, MessageButtons.OK, MessageResult.None);
        window.Note = "A note.";
        Shown(window, () => {
          Color text = ColorOf(window.FindResource("Theme.Text"));
          TextBlock heading = Named<TextBlock>(window, "TextHeading");
          TextBlock message = Named<TextBlock>(window, "TextMessage");
          TextBlock note = Named<TextBlock>(window, "TextNote");
          TextBlock icon = Named<TextBlock>(window, "TextIcon");
          Assert.Equal("Heading.", heading.Text);
          Assert.Equal("The text.", message.Text);
          Assert.Equal("A note.", note.Text);
          foreach (TextBlock block in new[] { heading, message, note, icon }) {
            Assert.Equal(text, ColorOf(block.Foreground));
          }
          foreach (TextBlock block in new[] { heading, message, note }) {
            Assert.Equal(window.FontSize, block.FontSize);
          }
          Assert.Equal(FontWeights.SemiBold, heading.FontWeight);
          Assert.Equal(FontWeights.Normal, message.FontWeight);
          Assert.Equal(FontWeights.Normal, note.FontWeight);
        });
      });
    }

    [Fact]
    public void An_empty_text_or_note_takes_no_space() {
      WpfAppHost.Run(() => {
        MessageWindow window = new MessageWindow("T", "Only a heading.", "", MessageIcon.Info, MessageButtons.OK, MessageResult.None);
        Shown(window, () => {
          Assert.Equal(Visibility.Collapsed, Named<FrameworkElement>(window, "TextMessage").Visibility);
          Assert.Equal(Visibility.Collapsed, Named<FrameworkElement>(window, "TextNote").Visibility);
          Assert.Equal(Visibility.Visible, Named<FrameworkElement>(window, "TextHeading").Visibility);
        });
      });
    }

    [Fact]
    public void Message_and_button_bar_have_the_26_pixel_outer_margin() {
      WpfAppHost.Run(() => {
        MessageWindow window = new MessageWindow("T", "H.", "", MessageIcon.Info, MessageButtons.OK, MessageResult.None);
        Shown(window, () => {
          double m = AboutWindow.OuterMargin;
          Assert.Equal(new Thickness(m), Named<FrameworkElement>(window, "PanelMessage").Margin);
          Assert.Equal(new Thickness(m), Named<Border>(window, "ButtonBar").Padding);
        });
      });
    }

    [Theory]
    [InlineData(MessageButtons.OK, "OK")]
    [InlineData(MessageButtons.OKCancel, "OK,Cancel")]
    [InlineData(MessageButtons.YesNo, "Yes,No")]
    [InlineData(MessageButtons.YesNoCancel, "Yes,No,Cancel")]
    public void The_first_button_is_dark_and_the_others_light(MessageButtons set, string labels) {
      WpfAppHost.Run(() => {
        MessageWindow window = new MessageWindow("T", "H.", "", MessageIcon.Warning, set, MessageResult.None);
        Shown(window, () => {
          List<Button> buttons = Buttons(window);
          Assert.Equal(labels, string.Join(",", buttons.Select(b => (string)b.Content)));
          Assert.Same(window.FindResource("StyleDarkButton"), buttons[0].Style);
          Assert.All(buttons.Skip(1), b => Assert.Same(window.FindResource("StyleLightButton"), b.Style));
          Assert.All(buttons, b => Assert.Same(b, window.FindName("Button" + b.Content)));
        });
      });
    }

    [Fact]
    public void The_asked_default_is_the_default_button_and_the_cancel_answer_is_the_cancel_button() {
      WpfAppHost.Run(() => {
        MessageWindow window = new MessageWindow("T", "H.", "", MessageIcon.Warning, MessageButtons.YesNo, MessageResult.No);
        Shown(window, () => {
          Button yes = Named<Button>(window, "ButtonYes");
          Button no = Named<Button>(window, "ButtonNo");
          Assert.False(yes.IsDefault);
          Assert.True(no.IsDefault);
          Assert.False(yes.IsCancel);
          Assert.True(no.IsCancel);
        });
      });
    }

    [Fact]
    public void Ok_alone_is_default_and_cancel() {
      WpfAppHost.Run(() => {
        MessageWindow window = new MessageWindow("T", "H.", "", MessageIcon.Info, MessageButtons.OK, MessageResult.None);
        Shown(window, () => {
          Button ok = Named<Button>(window, "ButtonOK");
          Assert.True(ok.IsDefault);
          Assert.True(ok.IsCancel);
        });
      });
    }

    [Theory]
    [InlineData(MessageButtons.YesNoCancel, "Yes", MessageResult.Yes)]
    [InlineData(MessageButtons.YesNoCancel, "No", MessageResult.No)]
    [InlineData(MessageButtons.YesNoCancel, "Cancel", MessageResult.Cancel)]
    [InlineData(MessageButtons.OKCancel, "OK", MessageResult.OK)]
    public void A_button_closes_the_window_with_its_answer(MessageButtons set, string label, MessageResult expected) {
      WpfAppHost.Run(() => {
        MessageWindow window = new MessageWindow("T", "H.", "", MessageIcon.Warning, set, MessageResult.None);
        bool closed = false;
        window.Closed += (s, e) => closed = true;
        Shown(window, () => {
          Click(Named<Button>(window, "Button" + label));
          Assert.True(closed);
          Assert.Equal(expected, window.Result);
        });
      });
    }

    [Theory]
    [InlineData(MessageButtons.OK, MessageResult.OK)]
    [InlineData(MessageButtons.OKCancel, MessageResult.Cancel)]
    [InlineData(MessageButtons.YesNo, MessageResult.No)]
    [InlineData(MessageButtons.YesNoCancel, MessageResult.Cancel)]
    public void Closing_without_a_button_answers_cancel_or_no(MessageButtons set, MessageResult expected) {
      WpfAppHost.Run(() => {
        MessageWindow window = new MessageWindow("T", "H.", "", MessageIcon.Warning, set, MessageResult.None);
        Shown(window, () => {
          window.Close();
          WpfAppHost.Pump();
          Assert.Equal(expected, window.Result);
        });
      });
    }

    [Fact]
    public void Extra_buttons_are_light_on_the_left_and_do_not_close_the_window() {
      WpfAppHost.Run(() => {
        MessageWindow window = new MessageWindow("T", "H.", "", MessageIcon.Warning, MessageButtons.OK, MessageResult.None);
        int copied = 0;
        window.AddExtraButton("ButtonCopyDetails", "Copy details", () => copied++);
        window.AddExtraButton("ButtonOpenLogs", "Open logs folder", () => { });
        bool closed = false;
        window.Closed += (s, e) => closed = true;
        Shown(window, () => {
          Button copy = Named<Button>(window, "ButtonCopyDetails");
          Button logs = Named<Button>(window, "ButtonOpenLogs");
          Button ok = Named<Button>(window, "ButtonOK");
          Assert.Same(window.FindResource("StyleLightButton"), copy.Style);
          Assert.Same(window.FindResource("StyleLightButton"), logs.Style);
          double copyX = copy.TranslatePoint(new Point(0, 0), window).X;
          double logsX = logs.TranslatePoint(new Point(0, 0), window).X;
          double okX = ok.TranslatePoint(new Point(0, 0), window).X;
          Assert.True(copyX < logsX && logsX < okX);
          Click(copy);
          Assert.Equal(1, copied);
          Assert.False(closed);
        });
      });
    }

    [Fact]
    public void Show_builds_the_window_and_returns_the_answer_through_the_test_hook() {
      WpfAppHost.Run(() => {
        Func<MessageWindow, MessageResult> old = MdshoMessage.ShowWindow;
        List<MessageWindow> shown = new List<MessageWindow>();
        MdshoMessage.ShowWindow = w => { shown.Add(w); return MessageResult.Yes; };
        try {
          MessageResult answer = MdshoMessage.Show(null, "Exit MDSHO", "Exit anyway?\n\nIt will be lost.", MessageIcon.Warning, MessageButtons.YesNo, MessageResult.No);
          Assert.Equal(MessageResult.Yes, answer);
          MessageWindow window = Assert.Single(shown);
          Assert.StartsWith("Exit MDSHO", window.Title);
          Assert.Equal("Exit anyway?", Named<TextBlock>(window, "TextHeading").Text);
          Assert.Equal("It will be lost.", Named<TextBlock>(window, "TextMessage").Text);
          Assert.True(Named<Button>(window, "ButtonNo").IsDefault);
          window.Close();
        } finally {
          MdshoMessage.ShowWindow = old;
        }
      });
    }

    [Fact]
    public void A_long_text_scrolls_inside_the_window() {
      WpfAppHost.Run(() => {
        MessageWindow window = new MessageWindow("T", "H.", string.Join("\n", Enumerable.Repeat("line", 200)), MessageIcon.Info, MessageButtons.OK, MessageResult.None);
        Shown(window, () => {
          Assert.True(window.ActualHeight <= window.MaxHeight);
          Assert.NotNull(WpfAppHost.VisualDescendants<ScrollViewer>(Named<FrameworkElement>(window, "PanelMessage")).FirstOrDefault());
        });
      });
    }
  }
}
