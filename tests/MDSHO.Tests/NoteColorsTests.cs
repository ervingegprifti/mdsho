using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using MDSHO.Core;
using MDSHO.Data;
using MDSHO.Helpers;
using MDSHO.ViewModels;
using Xunit;

namespace MDSHO.Tests {

  /// <summary>
  /// Note colours: the colours button in a Note window's title bar opens the Settings window as "Note colours" (the five
  /// colours and the palette, Default colours, Apply). The colours are saved with the note (exported, imported and copied
  /// with it), and the Note window uses them for its border, title bar, title text and buttons, background and text.
  /// </summary>
  public class NoteColorsTests {

    private sealed class Fixture {
      public AppViewModel App;
      public DesktopWindowViewModel Win;
      public ShortcutViewModel Note;
    }

    private static void WithNote(Action<Fixture> body) {
      WpfAppHost.Run(() => {
        Action<ShortcutViewModel> oldNoteEditor = ShortcutViewModel.OpenNoteEditor;
        ShortcutViewModel.OpenNoteEditor = s => { };
        try {
          using (TempDb db = new TempDb()) {
            Fixture f = new Fixture();
            f.App = new AppViewModel();
            f.Win = f.App.AddWindow("TEST", 0, 0);
            f.Win.NewNoteShortcutCommand.Execute(null);
            f.Note = f.Win.RootShortcuts.Last();
            f.Note.Name = "Checklist";
            body(f);
          }
        } finally {
          ShortcutViewModel.OpenNoteEditor = oldNoteEditor;
        }
      });
    }

    private static Shortcut Stored(long id) {
      return DataStore.Shortcuts.GetAll().Single(s => s.ID == id);
    }

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

    private static Color ColorOf(Brush brush) {
      return Assert.IsType<SolidColorBrush>(brush).Color;
    }

    private static Color Hex(string hex) {
      return (Color)ColorConverter.ConvertFromString(hex);
    }

    // ---- View model ----

    [Fact]
    public void A_new_note_has_the_default_colours_and_stores_them_as_empty() {
      WithNote(f => {
        NoteColors c = f.Note.NoteColors;
        Assert.Equal((NoteColors.DefaultBorder, NoteColors.DefaultTitleBackground, NoteColors.DefaultTitleText, NoteColors.DefaultBackground, NoteColors.DefaultText),
          (c.BorderColor, c.TitleBackgroundColor, c.TitleTextColor, c.BackgroundColor, c.TextColor));
        Shortcut row = Stored(f.Note.ID);
        Assert.Equal(("", "", "", "", ""), (row.NoteBorderColor, row.NoteTitleBackgroundColor, row.NoteTitleTextColor, row.NoteBackgroundColor, row.NoteTextColor));
      });
    }

    [Fact]
    public void Saved_colours_are_stored_and_a_colour_equal_to_the_default_is_stored_as_empty() {
      WithNote(f => {
        f.Note.NoteColors.TitleBackgroundColor = "#FF2F4F6F";
        f.Note.NoteColors.BackgroundColor = "#FFFFF8DC";
        f.Note.SaveNoteColors();
        Shortcut row = Stored(f.Note.ID);
        Assert.Equal("#FF2F4F6F", row.NoteTitleBackgroundColor);
        Assert.Equal("#FFFFF8DC", row.NoteBackgroundColor);
        Assert.Equal("", row.NoteBorderColor);
      });
    }

    [Fact]
    public void Default_colours_puts_all_five_back() {
      WithNote(f => {
        f.Note.NoteColors.TextColor = "#FFFF0000";
        f.Note.NoteColors.BorderColor = "#FF00FF00";
        f.Note.NoteColors.ResetToDefaults();
        Assert.Equal(NoteColors.DefaultText, f.Note.NoteColors.TextColor);
        Assert.Equal(NoteColors.DefaultBorder, f.Note.NoteColors.BorderColor);
      });
    }

    [Fact]
    public void Colours_load_back_and_are_copied_with_the_note() {
      WithNote(f => {
        f.Note.NoteColors.BackgroundColor = "#FFFFF8DC";
        f.Note.SaveNoteColors();
        ShortcutViewModel loaded = ViewModelFactory.CreateAppViewModel().Windows.SelectMany(w => w.RootShortcuts).Single(s => s.ID == f.Note.ID);
        Assert.Equal("#FFFFF8DC", loaded.NoteColors.BackgroundColor);
        f.Note.CopyShortcutCommand.Execute(null);
        f.Win.PasteShortcutCommand.Execute(null);
        ShortcutViewModel copy = f.Win.RootShortcuts.Last();
        Assert.NotEqual(f.Note.ID, copy.ID);
        Assert.Equal("#FFFFF8DC", copy.NoteColors.BackgroundColor);
        Assert.Equal("#FFFFF8DC", Stored(copy.ID).NoteBackgroundColor);
      });
    }

    // ---- Note colours window (the Settings window in note mode) ----

    [Fact]
    public void The_note_colours_window_shows_only_the_colours_and_has_Default_colours() {
      WithNote(f => {
        SettingsWindow window = new SettingsWindow(f.Note);
        Shown(window, () => {
          Assert.Equal("Note colours: Checklist", window.Title);
          Assert.Equal("Note colours", Named<Label>(window, "LabelAppearanceHeading").Content);
          Assert.Equal(Visibility.Collapsed, Named<FrameworkElement>(window, "PanelWindowOnly").Visibility);
          Assert.Equal(Visibility.Visible, Named<Button>(window, "ButtonDefaultColours").Visibility);
          Assert.Equal(f.Note.OwningWindow.Appearance.Icons.Brush, GlyphIcon.GlyphOf(window.Icon));
        });
      });
    }

    [Fact]
    public void The_panel_settings_window_has_no_Default_colours() {
      WithNote(f => {
        SettingsWindow window = new SettingsWindow(f.Win);
        Shown(window, () => {
          Assert.Equal(Visibility.Visible, Named<FrameworkElement>(window, "PanelWindowOnly").Visibility);
          Assert.Equal(Visibility.Collapsed, Named<Button>(window, "ButtonDefaultColours").Visibility);
          Assert.Equal("Window appearance", Named<Label>(window, "LabelAppearanceHeading").Content);
        });
      });
    }

    [Fact]
    public void Apply_saves_the_note_colours() {
      WithNote(f => {
        SettingsWindow window = new SettingsWindow(f.Note);
        Shown(window, () => {
          f.Note.NoteColors.TitleBackgroundColor = "#FF2F4F6F";
          Click(Named<Button>(window, "ButtonApply"));
        });
        Assert.Equal("#FF2F4F6F", f.Note.NoteColors.TitleBackgroundColor);
        Assert.Equal("#FF2F4F6F", Stored(f.Note.ID).NoteTitleBackgroundColor);
      });
    }

    [Fact]
    public void Closing_without_Apply_puts_the_old_note_colours_back() {
      WithNote(f => {
        SettingsWindow window = new SettingsWindow(f.Note);
        Shown(window, () => {
          f.Note.NoteColors.TitleBackgroundColor = "#FF2F4F6F";
        });
        Assert.Equal(NoteColors.DefaultTitleBackground, f.Note.NoteColors.TitleBackgroundColor);
        Assert.Equal("", Stored(f.Note.ID).NoteTitleBackgroundColor);
      });
    }

    [Fact]
    public void Default_colours_button_resets_and_Apply_saves_the_defaults() {
      WithNote(f => {
        f.Note.NoteColors.BackgroundColor = "#FFFFF8DC";
        f.Note.SaveNoteColors();
        SettingsWindow window = new SettingsWindow(f.Note);
        Shown(window, () => {
          Click(Named<Button>(window, "ButtonDefaultColours"));
          Assert.Equal(NoteColors.DefaultBackground, f.Note.NoteColors.BackgroundColor);
          Click(Named<Button>(window, "ButtonApply"));
        });
        Assert.Equal("", Stored(f.Note.ID).NoteBackgroundColor);
      });
    }

    // ---- Note window ----

    [Fact]
    public void The_note_window_has_a_colours_button_before_the_pin() {
      WithNote(f => {
        TextEditWindow window = TextEditWindow.ForNote(f.Note);
        Shown(window, () => {
          Button colours = Named<Button>(window, "ButtonColors");
          Button pin = Named<Button>(window, "ButtonPin");
          Assert.Equal(Visibility.Visible, colours.Visibility);
          Assert.Equal("Note colours", colours.ToolTip);
          Assert.Equal(f.Note.OwningWindow.Appearance.Icons.Brush, colours.Content);
          FrameworkElement bar = Named<FrameworkElement>(window, "TitleBar");
          Assert.True(colours.TranslatePoint(new Point(0, 0), bar).X < pin.TranslatePoint(new Point(0, 0), bar).X);
        });
      });
    }

    [Fact]
    public void The_comment_window_has_no_colours_button() {
      WithNote(f => {
        TextEditWindow window = TextEditWindow.ForComment(f.Win.RootShortcuts.First(s => !s.IsNote || true));
        Shown(window, () => {
          Assert.Equal(Visibility.Collapsed, Named<Button>(window, "ButtonColors").Visibility);
        });
      });
    }

    [Fact]
    public void The_note_window_uses_the_note_colours_and_follows_changes_live() {
      WithNote(f => {
        TextEditWindow window = TextEditWindow.ForNote(f.Note);
        Shown(window, () => {
          f.Note.NoteColors.BorderColor = "#FF112233";
          f.Note.NoteColors.TitleBackgroundColor = "#FF2F4F6F";
          f.Note.NoteColors.TitleTextColor = "#FFFFFFFF";
          f.Note.NoteColors.BackgroundColor = "#FFFFF8DC";
          f.Note.NoteColors.TextColor = "#FF1F2F3F";
          WpfAppHost.Pump();
          Assert.Equal(Hex("#FF112233"), ColorOf(window.BorderBrush));
          Assert.Equal(Hex("#FF2F4F6F"), ColorOf(Named<Border>(window, "TitleBar").Background));
          Assert.Equal(Hex("#FFFFFFFF"), ColorOf(Named<TextBlock>(window, "TextTitle").Foreground));
          Assert.Equal(Hex("#FFFFFFFF"), ColorOf(Named<Button>(window, "ButtonClose").Foreground));
          TextBox editor = Named<TextBox>(window, "TextComment");
          Assert.Equal(Hex("#FFFFF8DC"), ColorOf(editor.Background));
          Assert.Equal(Hex("#FF1F2F3F"), ColorOf(editor.Foreground));
        });
      });
    }
  }
}
