using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using MDSHO.Core;
using MDSHO.Data;
using MDSHO.Helpers;
using MDSHO.ViewModels;
using Xunit;

namespace MDSHO.Tests {

  /// <summary>
  /// The Presets row of the Colours window: under the window colours, eight small window pictures, one per theme. A click
  /// puts all five colours on screen at once, like Default colours: saved only with Apply. Works for panels and notes.
  /// </summary>
  public class ColorPresetsWindowTests {

    private static void Shown(Window window, Action check) {
      window.WindowStartupLocation = WindowStartupLocation.Manual;
      window.Left = -10000;
      window.Top = -10000;
      window.ShowActivated = false;
      window.ShowInTaskbar = false;
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

    /// <summary>A new panel and its Colours window, shown off screen.</summary>
    private static void WithPanel(Action<ColorsWindow, DesktopWindowViewModel> check) {
      using (TempDb db = new TempDb()) {
        WpfAppHost.Run(() => {
          DesktopWindowViewModel w = new AppViewModel().AddWindow("TEST", 100, 100);
          ColorsWindow window = new ColorsWindow(w);
          Shown(window, () => check(window, w));
        });
      }
    }

    /// <summary>A new note and its Colours window, shown off screen.</summary>
    private static void WithNote(Action<ColorsWindow, ShortcutViewModel> check) {
      WpfAppHost.Run(() => {
        Action<ShortcutViewModel> oldNoteEditor = ShortcutViewModel.OpenNoteEditor;
        ShortcutViewModel.OpenNoteEditor = s => { };
        try {
          using (TempDb db = new TempDb()) {
            DesktopWindowViewModel w = new AppViewModel().AddWindow("TEST", 0, 0);
            w.NewNoteShortcutCommand.Execute(null);
            ShortcutViewModel note = w.RootShortcuts.Last();
            ColorsWindow window = new ColorsWindow(note);
            Shown(window, () => check(window, note));
          }
        } finally {
          ShortcutViewModel.OpenNoteEditor = oldNoteEditor;
        }
      });
    }

    private static T Named<T>(FrameworkElement root, string name) where T : class {
      return Assert.IsAssignableFrom<T>(root.FindName(name));
    }

    private static List<Button> Presets(ColorsWindow window) {
      return WpfAppHost.VisualDescendants<Button>(Named<FrameworkElement>(window, "ListPresets")).ToList();
    }

    private static T Part<T>(Button preset, string name) where T : FrameworkElement {
      return WpfAppHost.VisualDescendants<T>(preset).Single(e => e.Name == name);
    }

    private static void Click(Button button) {
      button.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent, button));
      WpfAppHost.Pump();
    }

    private static Color C(string hex) {
      return (Color)ColorConverter.ConvertFromString(hex);
    }

    private static Color ColorOf(object brush) {
      return Assert.IsType<SolidColorBrush>(brush).Color;
    }

    private static string[] Colours(ColorPreset p) {
      return new[] { p.BorderColor, p.TitleBackgroundColor, p.TitleTextColor, p.BackgroundColor, p.TextColor };
    }

    private static string[] Colours(DesktopWindowAppearance a) {
      return new[] { a.BorderColor, a.TitleBackgroundColor, a.TitleTextColor, a.BackgroundColor, a.TextColor };
    }

    private static string[] Colours(NoteColors c) {
      return new[] { c.BorderColor, c.TitleBackgroundColor, c.TitleTextColor, c.BackgroundColor, c.TextColor };
    }

    // ---- Layout ----

    [Fact]
    public void A_Presets_heading_like_Window_colours_sits_under_the_colours_with_eight_pictures_in_one_row() {
      WithPanel((window, w) => {
        Label heading = Named<Label>(window, "LabelPresetsHeading");
        Assert.Equal("Presets", heading.Content);
        Assert.Same(window.FindResource("StyleSettingsCategory"), heading.Style);
        FrameworkElement body = Named<FrameworkElement>(window, "PanelBody");
        FrameworkElement editing = Named<FrameworkElement>(window, "TextEditing");
        double headingTop = heading.TranslatePoint(new Point(0, 0), body).Y;
        Assert.True(headingTop > editing.TranslatePoint(new Point(0, editing.ActualHeight), body).Y, "Presets is not under the colours.");
        Assert.Equal(Named<Label>(window, "LabelAppearanceHeading").TranslatePoint(new Point(0, 0), body).X, heading.TranslatePoint(new Point(0, 0), body).X, 0.5);

        List<Button> presets = Presets(window);
        Assert.Equal(ColorPresets.All.Select(p => p.Name).ToArray(), presets.Select(b => AutomationProperties.GetName(b)).ToArray());
        double top = presets[0].TranslatePoint(new Point(0, 0), body).Y;
        Assert.True(top > headingTop + heading.ActualHeight - 0.5);
        double left = heading.TranslatePoint(new Point(0, 0), body).X;
        for (int i = 0; i < presets.Count; i++) {
          Point at = presets[i].TranslatePoint(new Point(0, 0), body);
          Assert.Equal(top, at.Y, 0.5);
          Assert.Equal(64, presets[i].ActualWidth, 0.5);
          Assert.Equal(50, presets[i].ActualHeight, 0.5);
          Assert.Equal(left + i * (64 + 9), at.X, 0.5); // 9 px gaps, the first lined up with the heading
        }
        Assert.True(presets[^1].TranslatePoint(new Point(64, 0), body).X <= body.ActualWidth + 0.5, "The row is wider than the window.");
      });
    }

    [Fact]
    public void Each_picture_is_a_tiny_window_in_the_preset_colours() {
      WithPanel((window, w) => {
        List<Button> presets = Presets(window);
        for (int i = 0; i < presets.Count; i++) {
          ColorPreset p = ColorPresets.All[i];
          Border frame = Part<Border>(presets[i], "PresetFrame");
          Assert.Equal(new Thickness(2), frame.BorderThickness);
          Assert.Equal(C(p.BorderColor), ColorOf(frame.BorderBrush));
          Assert.Equal(C(p.BackgroundColor), ColorOf(frame.Background));
          Border title = Part<Border>(presets[i], "PresetTitle");
          Assert.Equal(15, title.ActualHeight, 0.5);
          Assert.Equal(C(p.TitleBackgroundColor), ColorOf(title.Background));
          TextBlock aa = Part<TextBlock>(presets[i], "PresetTitleText");
          Assert.Equal("Aa", aa.Text);
          Assert.Equal(C(p.TitleTextColor), ColorOf(aa.Foreground));
          List<Rectangle> lines = WpfAppHost.VisualDescendants<Rectangle>(presets[i]).Where(r => r.Name.StartsWith("PresetLine", StringComparison.Ordinal)).ToList();
          Assert.Equal(3, lines.Count);
          Assert.All(lines, l => {
            Assert.Equal(C(p.TextColor), ColorOf(l.Fill));
            Assert.True(l.RadiusX > 0);
          });
        }
      });
    }

    [Fact]
    public void Each_picture_has_the_theme_name_as_tooltip_and_accessible_name() {
      WithPanel((window, w) => {
        List<Button> presets = Presets(window);
        for (int i = 0; i < presets.Count; i++) {
          Assert.Equal(ColorPresets.All[i].Name, presets[i].ToolTip);
          Assert.Equal(ColorPresets.All[i].Name, AutomationProperties.GetName(presets[i]));
        }
      });
    }

    [Fact]
    public void The_pictures_are_reached_with_Tab_once_and_moved_through_with_the_arrow_keys() {
      WithPanel((window, w) => {
        FrameworkElement list = Named<FrameworkElement>(window, "ListPresets");
        Assert.Equal(KeyboardNavigationMode.Once, KeyboardNavigation.GetTabNavigation(list));
        Assert.Equal(KeyboardNavigationMode.Cycle, KeyboardNavigation.GetDirectionalNavigation(list));
        Assert.All(Presets(window), b => {
          Assert.True(b.Focusable);
          Assert.True(b.IsTabStop);
          Assert.True(KeyboardNavigation.GetAcceptsReturn(b)); // Enter clicks it, like Space
        });
      });
    }

    [Fact]
    public void The_focus_ring_is_hidden_until_hover_or_focus_and_uses_the_accent_in_light_and_dark() {
      WithPanel((window, w) => {
        try {
          foreach (WindowThemeKind theme in new[] { WindowThemeKind.Light, WindowThemeKind.Dark }) {
            WindowThemes.Apply(theme);
            WpfAppHost.Pump();
            Color accent = ColorOf(window.FindResource("Theme.Accent"));
            foreach (Button b in Presets(window)) {
              Border ring = Part<Border>(b, "PresetRing");
              Assert.Equal(Visibility.Hidden, ring.Visibility);
              Assert.Equal(accent, ColorOf(ring.BorderBrush));
              Assert.Null(b.FocusVisualStyle); // the ring replaces the dotted focus rectangle
            }
            Assert.Equal(ColorOf(window.FindResource("Theme.Text")), ColorOf(Named<Label>(window, "LabelPresetsHeading").Foreground));
          }
        } finally {
          WindowThemes.Apply(WindowThemeKind.Light);
          WpfAppHost.Pump();
        }
      });
    }

    [Fact]
    public void Everything_still_fits_without_scrolling() {
      WithPanel((window, w) => {
        ScrollViewer scroll = Named<ScrollViewer>(window, "ScrollBody");
        Assert.Equal(0, scroll.ScrollableHeight, 1);
        FrameworkElement last = Presets(window)[^1];
        FrameworkElement footer = Named<FrameworkElement>(window, "Footer");
        Assert.True(last.TranslatePoint(new Point(0, last.ActualHeight), footer).Y <= 0, "The presets are hidden behind the footer.");
      });
    }

    // ---- "Preset: <name>" and the selected picture ----

    private static TextBlock PresetText(ColorsWindow window) {
      return Named<TextBlock>(window, "TextPreset");
    }

    private static Border Ring(Button preset) {
      return Part<Border>(preset, "PresetRing");
    }

    /// <summary>"Preset: name" shows, and only that picture has the ring, when <paramref name="index"/> is not -1.</summary>
    private static void AssertSelected(ColorsWindow window, int index) {
      TextBlock text = PresetText(window);
      List<Button> presets = Presets(window);
      if (index < 0) {
        Assert.NotEqual(Visibility.Visible, text.Visibility);
      } else {
        Assert.Equal(Visibility.Visible, text.Visibility);
        Assert.Equal("Preset: " + ColorPresets.All[index].Name, text.Text);
      }
      for (int i = 0; i < presets.Count; i++) {
        Assert.Equal(i == index ? Visibility.Visible : Visibility.Hidden, Ring(presets[i]).Visibility);
      }
    }

    [Fact]
    public void Preset_text_looks_like_Editing_and_sits_under_the_presets_on_the_left() {
      WithPanel((window, w) => {
        Click(Presets(window)[4]);
        TextBlock text = PresetText(window);
        TextBlock editing = Named<TextBlock>(window, "TextEditing");
        Assert.Equal(editing.Margin, text.Margin);
        Assert.Equal(ColorOf(editing.Foreground), ColorOf(text.Foreground));
        Assert.Equal(ColorOf(window.FindResource("Theme.TextSecondary")), ColorOf(text.Foreground));
        FrameworkElement body = Named<FrameworkElement>(window, "PanelBody");
        Button first = Presets(window)[0];
        Assert.Equal(first.TranslatePoint(new Point(0, 0), body).X, text.TranslatePoint(new Point(0, 0), body).X, 0.5);
        Assert.True(text.TranslatePoint(new Point(0, 0), body).Y >= first.TranslatePoint(new Point(0, first.ActualHeight), body).Y);
      });
    }

    [Fact]
    public void No_preset_is_shown_when_the_window_opens_on_colours_that_match_none() {
      WithPanel((window, w) => AssertSelected(window, -1));
    }

    [Fact]
    public void Clicking_a_preset_shows_its_name_and_selects_its_picture() {
      WithPanel((window, w) => {
        Click(Presets(window)[4]);
        AssertSelected(window, 4);
        Click(Presets(window)[2]);
        AssertSelected(window, 2);
      });
    }

    [Fact]
    public void Editing_a_colour_hides_the_preset_and_putting_it_back_shows_it_again() {
      WithPanel((window, w) => {
        Click(Presets(window)[4]);
        ColorPicker.PickerControlBase palette = Named<ColorPicker.PickerControlBase>(window, "PickerColor");
        palette.SelectedColor = Color.FromRgb(0x12, 0x34, 0x56);
        WpfAppHost.Pump();
        AssertSelected(window, -1);
        palette.SelectedColor = C(ColorPresets.All[4].BorderColor);
        WpfAppHost.Pump();
        AssertSelected(window, 4);
      });
    }

    [Fact]
    public void Default_colours_hides_the_preset_unless_the_defaults_match_one() {
      WithPanel((window, w) => {
        Click(Presets(window)[4]);
        Click(Named<Button>(window, "ButtonDefaultColours"));
        AssertSelected(window, -1);
        // Saved defaults that are a preset show it.
        Click(Presets(window)[6]);
        Click(Named<Button>(window, "ButtonSetAsDefault"));
        Click(Presets(window)[0]);
        Click(Named<Button>(window, "ButtonDefaultColours"));
        AssertSelected(window, 6);
      });
    }

    [Fact]
    public void The_window_opens_showing_the_preset_its_colours_match() {
      using (TempDb db = new TempDb()) {
        WpfAppHost.Run(() => {
          DesktopWindowViewModel w = new AppViewModel().AddWindow("TEST", 100, 100);
          ColorPreset dracula = ColorPresets.All[5];
          w.Appearance.BorderColor = dracula.BorderColor.ToLowerInvariant();
          w.Appearance.TitleBackgroundColor = dracula.TitleBackgroundColor;
          w.Appearance.TitleTextColor = dracula.TitleTextColor;
          w.Appearance.BackgroundColor = dracula.BackgroundColor;
          w.Appearance.TextColor = dracula.TextColor;
          ColorsWindow window = new ColorsWindow(w);
          Shown(window, () => AssertSelected(window, 5));
        });
      }
    }

    [Fact]
    public void A_note_shows_its_preset_too() {
      WithNote((window, note) => {
        AssertSelected(window, -1);
        Click(Presets(window)[7]);
        AssertSelected(window, 7);
        note.NoteColors.TextColor = "#FFFF0000";
        WpfAppHost.Pump();
        AssertSelected(window, -1);
      });
    }

    // ---- Panel ----

    [Fact]
    public void Clicking_a_preset_puts_all_five_colours_of_the_panel_on_screen_and_the_palette_follows() {
      WithPanel((window, w) => {
        ColorPreset nord = ColorPresets.All[4];
        Click(Presets(window)[4]);
        Assert.Equal(Colours(nord), Colours(w.Appearance));
        ColorPicker.PickerControlBase palette = Named<ColorPicker.PickerControlBase>(window, "PickerColor");
        Assert.Equal(C(nord.BorderColor), palette.SelectedColor); // Border stays selected
        Assert.Equal(C(nord.BorderColor), ColorOf(Named<Border>(window, "SwatchBorder").Background));
        Named<ListBox>(window, "ListColorTargets").SelectedIndex = 2;
        WpfAppHost.Pump();
        Assert.Equal(C(nord.TitleTextColor), palette.SelectedColor);
        // Another preset replaces all five again, and the palette shows the selected one.
        ColorPreset dracula = ColorPresets.All[5];
        Click(Presets(window)[5]);
        Assert.Equal(Colours(dracula), Colours(w.Appearance));
        Assert.Equal(C(dracula.TitleTextColor), palette.SelectedColor);
      });
    }

    [Fact]
    public void A_panel_preset_is_saved_only_with_Apply() {
      WithPanel((window, w) => {
        string[] before = Colours(w.Appearance);
        Click(Presets(window)[0]);
        DesktopWindow stored = DataStore.Windows.LoadAll().Single(r => r.ID == w.ID);
        Assert.Equal(before, new[] { stored.BorderColor, stored.TitleBackgroundColor, stored.TitleTextColor, stored.BackgroundColor, stored.TextColor });
        Click(Named<Button>(window, "ButtonApply"));
        stored = DataStore.Windows.LoadAll().Single(r => r.ID == w.ID);
        Assert.Equal(Colours(ColorPresets.All[0]), new[] { stored.BorderColor, stored.TitleBackgroundColor, stored.TitleTextColor, stored.BackgroundColor, stored.TextColor });
      });
    }

    [Fact]
    public void Closing_without_Apply_puts_the_panel_colours_back_after_a_preset() {
      WithPanel((window, w) => {
        string[] before = Colours(w.Appearance);
        Click(Presets(window)[7]);
        window.Close();
        WpfAppHost.Pump();
        Assert.Equal(before, Colours(w.Appearance));
      });
    }

    [Fact]
    public void Set_as_default_after_a_preset_saves_the_preset_as_the_panel_defaults() {
      WithPanel((window, w) => {
        Click(Presets(window)[3]);
        Click(Named<Button>(window, "ButtonSetAsDefault"));
        ColorSet saved = DataStore.DefaultColors.Get(ColorSetKind.Window);
        Assert.Equal(Colours(ColorPresets.All[3]), new[] { saved.BorderColor, saved.TitleBackgroundColor, saved.TitleTextColor, saved.BackgroundColor, saved.TextColor });
      });
    }

    // ---- Note ----

    [Fact]
    public void Clicking_a_preset_puts_all_five_colours_of_the_note_on_screen_and_Apply_saves_them() {
      WithNote((window, note) => {
        Assert.Equal(ColorPresets.All.Count, Presets(window).Count);
        ColorPreset mocha = ColorPresets.All[7];
        Click(Presets(window)[7]);
        Assert.Equal(Colours(mocha), Colours(note.NoteColors));
        Assert.Equal(C(mocha.BorderColor), Named<ColorPicker.PickerControlBase>(window, "PickerColor").SelectedColor);
        Assert.Equal("", DataStore.Shortcuts.GetAll().Single(s => s.ID == note.ID).NoteBorderColor); // not saved yet
        Click(Named<Button>(window, "ButtonApply"));
        Shortcut row = DataStore.Shortcuts.GetAll().Single(s => s.ID == note.ID);
        Assert.Equal(Colours(mocha), new[] { row.NoteBorderColor, row.NoteTitleBackgroundColor, row.NoteTitleTextColor, row.NoteBackgroundColor, row.NoteTextColor });
      });
    }

    [Fact]
    public void Closing_without_Apply_puts_the_note_colours_back_after_a_preset() {
      WithNote((window, note) => {
        string[] before = Colours(note.NoteColors);
        Click(Presets(window)[1]);
        window.Close();
        WpfAppHost.Pump();
        Assert.Equal(before, Colours(note.NoteColors));
      });
    }
  }
}
