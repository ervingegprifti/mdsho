using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using MDSHO.Common;
using MDSHO.Core;
using MDSHO.Data;
using MDSHO.ViewModels;
using Xunit;

namespace MDSHO.Tests {

  /// <summary>
  /// "Set as default" in the Colours window saves the colours on screen as the defaults: for panels from a panel's Colours
  /// window, for notes from a note's. Saved defaults are what Default colours puts back and what new panels and new notes
  /// start with; existing panels and notes keep their colours. Without saved defaults MDSHO's own colours are used.
  /// </summary>
  public class SavedDefaultColoursTests {

    private sealed class Fixture {
      public AppViewModel App;
      public DesktopWindowViewModel Win;
    }

    private static void WithApp(Action<Fixture> body) {
      WpfAppHost.Run(() => {
        Action<ShortcutViewModel> oldNoteEditor = ShortcutViewModel.OpenNoteEditor;
        ShortcutViewModel.OpenNoteEditor = s => { };
        try {
          using (TempDb db = new TempDb()) {
            Fixture f = new Fixture { App = new AppViewModel() };
            f.Win = f.App.AddWindow("TEST", 0, 0);
            body(f);
          }
        } finally {
          ShortcutViewModel.OpenNoteEditor = oldNoteEditor;
        }
      });
    }

    private static ShortcutViewModel NewNote(Fixture f) {
      f.Win.NewNoteShortcutCommand.Execute(null);
      return f.Win.RootShortcuts.Last();
    }

    private static readonly ColorSet PanelSet = new ColorSet {
      BorderColor = "#FF101010", TitleBackgroundColor = "#FF202020", TitleTextColor = "#FF303030", BackgroundColor = "#FF404040", TextColor = "#FF505050"
    };
    private static readonly ColorSet NoteSet = new ColorSet {
      BorderColor = "#FFA1A1A1", TitleBackgroundColor = "#FFA2A2A2", TitleTextColor = "#FFA3A3A3", BackgroundColor = "#FFA4A4A4", TextColor = "#FFA5A5A5"
    };

    private static string[] Of(ColorSet c) {
      return new[] { c.BorderColor, c.TitleBackgroundColor, c.TitleTextColor, c.BackgroundColor, c.TextColor };
    }
    private static string[] Of(DesktopWindowAppearance a) {
      return new[] { a.BorderColor, a.TitleBackgroundColor, a.TitleTextColor, a.BackgroundColor, a.TextColor };
    }
    private static string[] Of(NoteColors c) {
      return new[] { c.BorderColor, c.TitleBackgroundColor, c.TitleTextColor, c.BackgroundColor, c.TextColor };
    }
    private static void Paint(DesktopWindowAppearance a, ColorSet c) {
      a.BorderColor = c.BorderColor; a.TitleBackgroundColor = c.TitleBackgroundColor; a.TitleTextColor = c.TitleTextColor;
      a.BackgroundColor = c.BackgroundColor; a.TextColor = c.TextColor;
    }
    private static void Paint(NoteColors n, ColorSet c) {
      n.Load(c.BorderColor, c.TitleBackgroundColor, c.TitleTextColor, c.BackgroundColor, c.TextColor);
    }

    private static readonly string[] MdshoPanel = {
      AppConstants.DefaultWindowBorderColor, AppConstants.DefaultWindowTitleBackgroundColor, AppConstants.DefaultWindowTitleTextColor,
      AppConstants.DefaultWindowBackgroundColor, AppConstants.DefaultWindowTextColor
    };
    private static readonly string[] MdshoNote = {
      NoteColors.DefaultBorder, NoteColors.DefaultTitleBackground, NoteColors.DefaultTitleText, NoteColors.DefaultBackground, NoteColors.DefaultText
    };

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
        if (window.IsVisible) { window.Close(); }
        WpfAppHost.Pump();
      }
    }

    private static Button Named(Window window, string name) {
      return Assert.IsAssignableFrom<Button>(window.FindName(name));
    }

    private static void Click(Button button) {
      button.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent, button));
      WpfAppHost.Pump();
    }

    // ---- Without saved defaults: MDSHO's colours, as before ----

    [Fact]
    public void Without_saved_defaults_Default_colours_and_new_ones_use_MDSHOs_colours() {
      WithApp(f => {
        Assert.Equal(MdshoPanel, Of(f.Win.Appearance));
        Paint(f.Win.Appearance, PanelSet);
        f.Win.Appearance.ResetColorsToDefaults();
        Assert.Equal(MdshoPanel, Of(f.Win.Appearance));
        ShortcutViewModel note = NewNote(f);
        Assert.Equal(MdshoNote, Of(note.NoteColors));
        Paint(note.NoteColors, NoteSet);
        note.NoteColors.ResetToDefaults();
        Assert.Equal(MdshoNote, Of(note.NoteColors));
      });
    }

    // ---- Saved defaults ----

    [Fact]
    public void Saved_panel_defaults_are_what_Default_colours_puts_back_and_what_a_new_panel_starts_with() {
      WithApp(f => {
        Paint(f.Win.Appearance, PanelSet);
        f.Win.Appearance.SaveColorsAsDefaults();
        Assert.Equal(Of(PanelSet), Of(DataStore.DefaultColors.Get(ColorSetKind.Window)));
        Assert.Null(DataStore.DefaultColors.Get(ColorSetKind.Note));
        f.Win.Appearance.BorderColor = "#FFFFFFFF";
        f.Win.Appearance.ResetColorsToDefaults();
        Assert.Equal(Of(PanelSet), Of(f.Win.Appearance));
        DesktopWindowViewModel second = f.App.AddWindow("TEST2", 10, 10);
        Assert.Equal(Of(PanelSet), Of(second.Appearance));
        DesktopWindow stored = DataStore.Windows.GetAll().Single(w => w.ID == second.ID);
        Assert.Equal(Of(PanelSet), new[] { stored.BorderColor, stored.TitleBackgroundColor, stored.TitleTextColor, stored.BackgroundColor, stored.TextColor });
      });
    }

    [Fact]
    public void Saved_note_defaults_are_what_Default_colours_puts_back_and_what_a_new_note_starts_with() {
      WithApp(f => {
        ShortcutViewModel first = NewNote(f);
        Paint(first.NoteColors, NoteSet);
        first.NoteColors.SaveAsDefaults();
        Assert.Equal(Of(NoteSet), Of(DataStore.DefaultColors.Get(ColorSetKind.Note)));
        Assert.Null(DataStore.DefaultColors.Get(ColorSetKind.Window));
        first.NoteColors.BorderColor = "#FFFFFFFF";
        first.NoteColors.ResetToDefaults();
        Assert.Equal(Of(NoteSet), Of(first.NoteColors));
        ShortcutViewModel second = NewNote(f);
        Assert.Equal(Of(NoteSet), Of(second.NoteColors));
        // Saved with the new note, so it keeps them when the app starts again.
        Shortcut stored = DataStore.Shortcuts.GetAll().Single(s => s.ID == second.ID);
        Assert.Equal(Of(NoteSet), new[] { stored.NoteBorderColor, stored.NoteTitleBackgroundColor, stored.NoteTitleTextColor, stored.NoteBackgroundColor, stored.NoteTextColor });
      });
    }

    [Fact]
    public void Existing_panels_and_notes_keep_their_colours_when_defaults_are_saved() {
      WithApp(f => {
        ShortcutViewModel oldNote = NewNote(f);
        string[] panelBefore = Of(f.Win.Appearance);
        DataStore.DefaultColors.Save(ColorSetKind.Window, PanelSet);
        DataStore.DefaultColors.Save(ColorSetKind.Note, NoteSet);
        Assert.Equal(panelBefore, Of(f.Win.Appearance));
        Assert.Equal(MdshoNote, Of(oldNote.NoteColors));
        // Also after loading everything again, as at the next start.
        AppViewModel reloaded = ViewModelFactory.CreateAppViewModel();
        DesktopWindowViewModel win = reloaded.Windows.Single(w => w.ID == f.Win.ID);
        Assert.Equal(panelBefore, Of(win.Appearance));
        ShortcutViewModel note = win.RootShortcuts.Single(s => s.ID == oldNote.ID);
        Assert.Equal(MdshoNote, Of(note.NoteColors));
      });
    }

    // ---- The Colours window's Set as default button ----

    [Fact]
    public void Set_as_default_sits_between_Default_colours_and_Apply_in_both_kinds_of_Colours_window() {
      WithApp(f => {
        foreach (Window window in new Window[] { new ColorsWindow(f.Win), new ColorsWindow(NewNote(f)) }) {
          Shown(window, () => {
            Button defaults = Named(window, "ButtonDefaultColours");
            Button setDefault = Named(window, "ButtonSetAsDefault");
            Button apply = Named(window, "ButtonApply");
            Assert.Equal("Set as default", setDefault.Content);
            Assert.Equal(Visibility.Visible, setDefault.Visibility);
            double x(Button b) => b.TranslatePoint(new Point(0, 0), window).X;
            Assert.True(x(defaults) < x(setDefault) && x(setDefault) < x(apply), "Expected Default colours, Set as default, Apply from left to right.");
            Assert.False(string.IsNullOrEmpty(setDefault.ToolTip as string));
          });
        }
      });
    }

    [Fact]
    public void Set_as_default_in_a_panels_Colours_window_saves_the_panel_defaults_at_once_and_stays_open() {
      WithApp(f => {
        ColorsWindow window = new ColorsWindow(f.Win);
        Shown(window, () => {
          Paint(f.Win.Appearance, PanelSet);
          Click(Named(window, "ButtonSetAsDefault"));
          Assert.True(window.IsVisible);
          Assert.Equal(Of(PanelSet), Of(DataStore.DefaultColors.Get(ColorSetKind.Window)));
          Assert.Null(DataStore.DefaultColors.Get(ColorSetKind.Note));
        });
        // Closing without Apply still puts the panel's own colours back; the saved defaults stay.
        Assert.Equal(MdshoPanel, Of(f.Win.Appearance));
        Assert.Equal(Of(PanelSet), Of(DataStore.DefaultColors.Get(ColorSetKind.Window)));
      });
    }

    [Fact]
    public void Set_as_default_in_a_notes_Colours_window_saves_the_note_defaults() {
      WithApp(f => {
        ShortcutViewModel note = NewNote(f);
        ColorsWindow window = new ColorsWindow(note);
        Shown(window, () => {
          Paint(note.NoteColors, NoteSet);
          Click(Named(window, "ButtonSetAsDefault"));
          Assert.True(window.IsVisible);
          Assert.Equal(Of(NoteSet), Of(DataStore.DefaultColors.Get(ColorSetKind.Note)));
          Assert.Null(DataStore.DefaultColors.Get(ColorSetKind.Window));
          // Default colours now puts back the saved note defaults.
          note.NoteColors.TextColor = "#FFFFFFFF";
          Click(Named(window, "ButtonDefaultColours"));
          Assert.Equal(Of(NoteSet), Of(note.NoteColors));
        });
      });
    }
  }
}
