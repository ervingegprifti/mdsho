using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using MDSHO.Converters;
using MDSHO.ViewModels;
using Xunit;

namespace MDSHO.Tests {

  /// <summary>
  /// The real Settings window, shown off screen: five colour buttons on the left (Border, Title Background, Title Text,
  /// Background, Text) and one always-visible palette on the right that edits the selected button's colour.
  /// </summary>
  public class SettingsWindowTests {

    private const string Border = "#FF102030";
    private const string TitleBackground = "#CC405060";
    private const string TitleText = "#FF0A0B0C";
    private const string Background = "#99708090";
    private const string Text = "#FF112233";

    /// <summary>Shows the Settings window of a new window (with five different colours) off screen, runs the check, closes it.</summary>
    private static void WithSettings(Action<SettingsWindow, DesktopWindowViewModel> check) {
      using (TempDb db = new TempDb()) {
        WpfAppHost.Run(() => {
          DesktopWindowViewModel w = new AppViewModel().AddWindow("TEST", 100, 100);
          w.Appearance.BorderColor = Border;
          w.Appearance.TitleBackgroundColor = TitleBackground;
          w.Appearance.TitleTextColor = TitleText;
          w.Appearance.BackgroundColor = Background;
          w.Appearance.TextColor = Text;
          SettingsWindow settings = new SettingsWindow(w) {
            WindowStartupLocation = WindowStartupLocation.Manual,
            Left = -10000,
            Top = -10000,
            ShowActivated = false,
            ShowInTaskbar = false
          };
          settings.Show();
          try {
            WpfAppHost.Pump();
            check(settings, w);
          } finally {
            settings.Close();
            WpfAppHost.Pump();
          }
        });
      }
    }

    private static T Named<T>(FrameworkElement root, string name) where T : class {
      return Assert.IsAssignableFrom<T>(root.FindName(name));
    }

    private static ListBox Buttons(SettingsWindow window) {
      return Named<ListBox>(window, "ListColorTargets");
    }

    /// <summary>The control that holds the palette's colour (the hex box); the square, hue and alpha sliders share its state.</summary>
    private static ColorPicker.PickerControlBase Palette(SettingsWindow window) {
      return Named<ColorPicker.PickerControlBase>(window, "PickerColor");
    }

    private static List<ListBoxItem> Items(SettingsWindow window) {
      ListBox list = Buttons(window);
      return Enumerable.Range(0, list.Items.Count).Select(i => (ListBoxItem)list.ItemContainerGenerator.ContainerFromIndex(i)).ToList();
    }

    private static string VisibleText(DependencyObject element) {
      return string.Join(" ", WpfAppHost.VisualDescendants<TextBlock>(element).Select(t => t.Text).Where(t => !string.IsNullOrEmpty(t)));
    }

    private static Color C(string hex) {
      return (Color)ColorConverter.ConvertFromString(hex);
    }

    private static string[] Colours(DesktopWindowViewModel w) {
      return new[] { w.Appearance.BorderColor, w.Appearance.TitleBackgroundColor, w.Appearance.TitleTextColor, w.Appearance.BackgroundColor, w.Appearance.TextColor };
    }

    private static void Select(SettingsWindow window, int index) {
      Buttons(window).SelectedIndex = index;
      WpfAppHost.Pump();
    }

    private static readonly string[] Expected = { Border, TitleBackground, TitleText, Background, Text };
    private static readonly string[] Labels = { "Border", "Title background", "Title text", "Background", "Text" };

    [Fact]
    public void Colour_buttons_are_border_title_background_title_text_background_text_without_the_word_color() {
      WithSettings((window, w) => {
        List<ListBoxItem> items = Items(window);
        Assert.Equal(Labels, items.Select(VisibleText).ToArray());
        Assert.Equal(new[] { "Border colour", "Title background colour", "Title text colour", "Background colour", "Text colour" },
          items.Select(i => AutomationProperties.GetName(i)).ToArray());
      });
    }

    [Fact]
    public void Each_colour_button_shows_a_swatch_of_its_colour() {
      WithSettings((window, w) => {
        List<ListBoxItem> items = Items(window);
        for (int i = 0; i < items.Count; i++) {
          System.Windows.Controls.Border swatch = WpfAppHost.VisualDescendants<System.Windows.Controls.Border>(items[i]).Single(b => b.Name.StartsWith("Swatch", StringComparison.Ordinal));
          Assert.Equal(C(Expected[i]), ((SolidColorBrush)swatch.Background).Color);
        }
      });
    }

    [Fact]
    public void Palette_is_compact_with_a_square_hue_and_alpha_sliders_and_a_hex_box_and_no_big_picker() {
      WithSettings((window, w) => {
        Assert.Empty(WpfAppHost.VisualDescendants<ColorPicker.PortableColorPicker>(window));
        Assert.Empty(WpfAppHost.VisualDescendants<ColorPicker.StandardColorPicker>(window));
        Assert.Empty(WpfAppHost.VisualDescendants<ColorPicker.SquarePicker>(window)); // the colour wheel
        Assert.Empty(WpfAppHost.VisualDescendants<ColorPicker.ColorSliders>(window)); // the HSV/HSL/RGB tabs
        Assert.IsType<ColorPicker.HexColorTextBox>(Palette(window));
        Assert.Single(WpfAppHost.VisualDescendants<ColorPicker.HexColorTextBox>(window));
        ColorPicker.AlphaSlider alpha = Assert.Single(WpfAppHost.VisualDescendants<ColorPicker.AlphaSlider>(window));
        Slider hue = Named<Slider>(window, "SliderHue");
        Assert.Equal(0, hue.Minimum);
        Assert.Equal(360, hue.Maximum);
        FrameworkElement square = Named<FrameworkElement>(window, "SquareSaturationValue");
        Assert.InRange(square.ActualHeight, 140, 160);
        // Everything sits in one white bordered panel, the square filling its width.
        System.Windows.Controls.Border panel = Named<System.Windows.Controls.Border>(window, "PanelPalette");
        Assert.Equal(Colors.White, ((SolidColorBrush)panel.Background).Color);
        Assert.True(panel.BorderThickness.Left > 0);
        foreach (FrameworkElement part in new FrameworkElement[] { square, hue, alpha, Palette(window), Named<FrameworkElement>(window, "SwatchCurrent"), Named<FrameworkElement>(window, "TextRgba") }) {
          Assert.True(WpfAppHost.VisualDescendants<FrameworkElement>(panel).Contains(part), part.Name + " is not inside the palette panel.");
        }
        double inner = panel.ActualWidth - panel.BorderThickness.Left - panel.BorderThickness.Right - panel.Padding.Left - panel.Padding.Right;
        Assert.Equal(inner, square.ActualWidth, 0.5); // half a pixel for layout rounding at 125 % scaling
        // The panel's top lines up with the first colour button; "Editing: ..." is under the panel.
        FrameworkElement client = (FrameworkElement)VisualTreeHelper.GetChild(window, 0);
        double panelTop = panel.TranslatePoint(new Point(0, 0), client).Y;
        Assert.Equal(Items(window)[0].TranslatePoint(new Point(0, 0), client).Y, panelTop, 1);
        TextBlock editing = Named<TextBlock>(window, "TextEditing");
        Assert.True(editing.TranslatePoint(new Point(0, 0), client).Y >= panelTop + panel.ActualHeight - 0.5);
        Assert.True(Palette(window).IsVisible);
      });
    }

    [Fact]
    public void Palette_shows_the_selected_colour_as_swatch_hex_and_rgba_values() {
      WithSettings((window, w) => {
        Select(window, 1); // Title Background #CC405060
        Assert.Equal(C(TitleBackground), ((SolidColorBrush)Named<System.Windows.Controls.Border>(window, "SwatchCurrent").Background).Color);
        Assert.Equal("R 64   G 80   B 96   A 204", Named<TextBlock>(window, "TextRgba").Text);
        TextBox hex = WpfAppHost.VisualDescendants<TextBox>(Palette(window)).Single();
        Assert.Equal("#CC405060", hex.Text);
        ColorPicker.AlphaSlider alpha = WpfAppHost.VisualDescendants<ColorPicker.AlphaSlider>(window).Single();
        Assert.Equal(C(TitleBackground), alpha.SelectedColor);
      });
    }

    [Fact]
    public void Hue_slider_and_square_edit_only_the_selected_colour() {
      WithSettings((window, w) => {
        Select(window, 3); // Background #99708090
        Slider hue = Named<Slider>(window, "SliderHue");
        hue.Value = 0;
        WpfAppHost.Pump();
        Color red = C(w.Appearance.BackgroundColor);
        Assert.Equal(0x99, red.A);
        Assert.True(red.R > red.G && red.R > red.B && red.G == red.B, "Hue 0 should give a red tint: " + w.Appearance.BackgroundColor);
        window.PickSaturationAndValue(0, 1); // top-left of the square: white, alpha kept
        WpfAppHost.Pump();
        Assert.Equal("#99FFFFFF", w.Appearance.BackgroundColor);
        window.PickSaturationAndValue(1, 0); // bottom: black
        WpfAppHost.Pump();
        Assert.Equal("#99000000", w.Appearance.BackgroundColor);
        Assert.Equal(new[] { Border, TitleBackground, TitleText, "#99000000", Text }, Colours(w));
      });
    }

    [Fact]
    public void Window_is_at_most_580_high_and_the_content_is_not_clipped() {
      WithSettings((window, w) => {
        Assert.InRange(window.Height, 500, 580);
        ScrollViewer scroll = Named<ScrollViewer>(window, "ScrollBody");
        FrameworkElement body = Named<FrameworkElement>(window, "PanelBody");
        body.Measure(new Size(scroll.ViewportWidth, double.PositiveInfinity));
        Assert.True(body.DesiredSize.Height <= scroll.ViewportHeight + 0.5,
          $"The content needs {body.DesiredSize.Height:0.#} but only {scroll.ViewportHeight:0.#} is visible.");
        Assert.Equal(0, scroll.ScrollableHeight, 1);
        // And not much empty space left over.
        Assert.True(scroll.ViewportHeight - body.DesiredSize.Height < 40, $"{scroll.ViewportHeight - body.DesiredSize.Height:0.#} px of empty space.");
      });
    }

    [Fact]
    public void Border_is_selected_when_the_window_opens() {
      WithSettings((window, w) => {
        Assert.Equal(0, Buttons(window).SelectedIndex);
        Assert.Equal(C(Border), Palette(window).SelectedColor);
        Assert.Equal("Editing: Border", Named<TextBlock>(window, "TextEditing").Text);
      });
    }

    [Fact]
    public void Selecting_each_button_shows_its_colour_in_the_palette() {
      WithSettings((window, w) => {
        for (int i = 0; i < Labels.Length; i++) {
          Select(window, i);
          Assert.Equal(C(Expected[i]), Palette(window).SelectedColor);
          Assert.Equal("Editing: " + Labels[i], Named<TextBlock>(window, "TextEditing").Text);
        }
      });
    }

    [Fact]
    public void Switching_selection_never_changes_any_colour() {
      WithSettings((window, w) => {
        foreach (int index in new[] { 1, 2, 3, 4, 0, 4, 2, 0, 3, 1, 1, 0 }) {
          Select(window, index);
          Assert.Equal(Expected, Colours(w));
        }
        // A Ctrl+click on the selected button would unselect it in a plain ListBox: one button stays selected.
        Buttons(window).SelectedItem = null;
        WpfAppHost.Pump();
        Assert.Equal(0, Buttons(window).SelectedIndex);
        Assert.Equal(Expected, Colours(w));
      });
    }

    [Fact]
    public void Editing_the_palette_changes_only_the_selected_colour_and_apply_saves_it() {
      WithSettings((window, w) => {
        long id = w.ID;
        for (int i = 0; i < Labels.Length; i++) {
          Select(window, i);
          Color picked = Color.FromArgb(0xFF, (byte)(0x10 + i), 0x80, 0x34);
          Palette(window).SelectedColor = picked;
          WpfAppHost.Pump();
          string[] expected = (string[])Expected.Clone();
          for (int j = 0; j <= i; j++) {
            expected[j] = Color.FromArgb(0xFF, (byte)(0x10 + j), 0x80, 0x34).ToHexAARRGGBB();
          }
          Assert.Equal(expected, Colours(w));
        }
        Named<Button>(window, "ButtonApply").RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
        WpfAppHost.Pump();
        MDSHO.Core.DesktopWindow saved = MDSHO.Data.DataStore.Windows.LoadAll().Single(r => r.ID == id);
        Assert.Equal("#FF108034", saved.BorderColor);
        Assert.Equal("#FF118034", saved.TitleBackgroundColor);
        Assert.Equal("#FF128034", saved.TitleTextColor);
        Assert.Equal("#FF138034", saved.BackgroundColor);
        Assert.Equal("#FF148034", saved.TextColor);
      });
    }

    [Fact]
    public void Changing_the_selected_colour_elsewhere_updates_the_palette() {
      WithSettings((window, w) => {
        Select(window, 3);
        w.Appearance.BackgroundColor = "#FF445566";
        WpfAppHost.Pump();
        Assert.Equal(C("#FF445566"), Palette(window).SelectedColor);
      });
    }

    [Fact]
    public void Selected_button_has_a_light_blue_fill_a_2px_blue_border_and_semibold_text_others_no_border() {
      WithSettings((window, w) => {
        for (int selected = 0; selected < Labels.Length; selected++) {
          Select(window, selected);
          List<ListBoxItem> items = Items(window);
          for (int i = 0; i < items.Count; i++) {
            ListBoxItem item = items[i];
            if (i == selected) {
              Assert.Equal(new Thickness(2), item.BorderThickness);
              Assert.Equal(C("#FF1F6FC5"), ((SolidColorBrush)item.BorderBrush).Color);
              Assert.Equal(C("#FFDCEBFA"), ((SolidColorBrush)item.Background).Color);
              Assert.Equal(FontWeights.SemiBold, item.FontWeight);
            } else {
              Assert.Equal(new Thickness(0), item.BorderThickness);
              Assert.NotEqual(FontWeights.SemiBold, item.FontWeight);
            }
          }
        }
      });
    }

    [Fact]
    public void Colour_buttons_can_be_reached_and_used_with_the_keyboard() {
      WithSettings((window, w) => {
        ListBox list = Buttons(window);
        Assert.True(list.IsTabStop || Items(window).All(i => i.IsTabStop));
        Assert.All(Items(window), i => Assert.True(i.Focusable));
        Assert.Equal(SelectionMode.Single, list.SelectionMode);
      });
    }

    [Fact]
    public void Window_is_660_wide_and_everything_fits_without_scrolling() {
      WithSettings((window, w) => {
        Assert.Equal(660, window.Width);
        Assert.Equal(ResizeMode.NoResize, window.ResizeMode);
        FrameworkElement client = (FrameworkElement)VisualTreeHelper.GetChild(window, 0);
        FrameworkElement footer = Named<FrameworkElement>(window, "Footer");
        double bottom = footer.TranslatePoint(new Point(0, footer.ActualHeight), client).Y;
        Assert.True(bottom <= client.ActualHeight + 0.5, $"The footer ends at {bottom:0.#}, below the visible height {client.ActualHeight:0.#}.");
        foreach (ScrollViewer scroll in WpfAppHost.VisualDescendants<ScrollViewer>(window).Where(s => s.Name == "ScrollBody")) {
          Assert.Equal(0, scroll.ScrollableHeight, 1);
        }
        FrameworkElement last = Named<FrameworkElement>(window, "CheckSingleClickToOpen");
        double lastBottom = last.TranslatePoint(new Point(0, last.ActualHeight), client).Y;
        Assert.True(lastBottom <= footer.TranslatePoint(new Point(0, 0), client).Y, "Single Click To Open is hidden behind the footer.");
      });
    }

    [Fact]
    public void Content_starts_one_apply_button_height_from_the_top_and_the_apply_button_is_a_bit_larger() {
      WithSettings((window, w) => {
        FrameworkElement body = Named<FrameworkElement>(window, "PanelBody");
        Button apply = Named<Button>(window, "ButtonApply");
        double h = apply.ActualHeight;
        Assert.Equal(new Thickness(h), body.Margin);
        ScrollViewer scroll = Named<ScrollViewer>(window, "ScrollBody");
        Label heading = WpfAppHost.VisualDescendants<Label>(body).First();
        Assert.Equal(h, heading.TranslatePoint(new Point(0, 0), scroll).Y, 0.5); // half a pixel for layout rounding at 125 % scaling
        Assert.Equal(new Thickness(22, 5, 22, 5), apply.Padding);
        System.Windows.Controls.Border chrome = WpfAppHost.VisualDescendants<System.Windows.Controls.Border>(apply).First();
        Assert.Equal(new Thickness(22, 5, 22, 5), chrome.Padding);
      });
    }

    [Fact]
    public void Every_outer_margin_of_the_content_and_the_apply_bar_is_the_apply_button_height() {
      WithSettings((window, w) => {
        FrameworkElement client = (FrameworkElement)VisualTreeHelper.GetChild(window, 0);
        FrameworkElement body = Named<FrameworkElement>(window, "PanelBody");
        Button apply = Named<Button>(window, "ButtonApply");
        double h = apply.ActualHeight;
        Assert.True(h > 10, "Apply height " + h);
        Point topLeft = body.TranslatePoint(new Point(0, 0), client);
        Assert.Equal(h, topLeft.X, 1);
        Assert.Equal(h, topLeft.Y, 1);
        Assert.Equal(client.ActualWidth - h, topLeft.X + body.ActualWidth, 1);
        // The palette reaches the right margin.
        FrameworkElement palette = Named<FrameworkElement>(window, "PanelPalette");
        Assert.Equal(client.ActualWidth - h, palette.TranslatePoint(new Point(palette.ActualWidth, 0), client).X, 1);
        // The window is just tall enough that the content's bottom margin is the button height too.
        FrameworkElement footer = Named<FrameworkElement>(window, "Footer");
        double footerTop = footer.TranslatePoint(new Point(0, 0), client).Y;
        Assert.Equal(footerTop - h, topLeft.Y + body.ActualHeight, 1.5);
        Point applyTopLeft = apply.TranslatePoint(new Point(0, 0), footer);
        Assert.Equal(h, applyTopLeft.Y, 1);
        Assert.Equal(footer.ActualHeight - h, applyTopLeft.Y + apply.ActualHeight, 1);
        Assert.Equal(footer.ActualWidth - h, applyTopLeft.X + apply.ActualWidth, 1);
        Assert.Equal(537, window.Height);
      });
    }

    [Fact]
    public void Icons_appearance_and_single_click_to_open_still_work() {
      WithSettings((window, w) => {
        List<RadioButton> radios = WpfAppHost.VisualDescendants<RadioButton>(window).ToList();
        Assert.Equal(new[] { "Solid icons", "Outlined icons" }, radios.Select(r => (string)r.Content).ToArray());
        radios[1].IsChecked = true;
        WpfAppHost.Pump();
        Assert.False(w.Appearance.SolidIcons);
        CheckBox single = Named<CheckBox>(window, "CheckSingleClickToOpen");
        Assert.Equal("Single click to open", single.Content);
        bool before = w.Appearance.SingleClickToOpen;
        single.IsChecked = !before;
        WpfAppHost.Pump();
        Assert.Equal(!before, w.Appearance.SingleClickToOpen);
      });
    }
  }
}
