using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Xml.Linq;
using MDSHO.Common;
using MDSHO.Core;
using MDSHO.Data;
using MDSHO.Helpers;
using MDSHO.ViewModels;
using Xunit;

namespace MDSHO.Tests {

  /// <summary>
  /// The font and text size buttons and menus in the title bar of the Note window, the snippet editor and the Comment
  /// window, and how the fonts load (shipped ones from embedded resources, Courier New when a file is missing).
  /// </summary>
  public class NoteFontViewTests {
    private static readonly ScreenRect TestArea = new ScreenRect(-20000, -20000, 15000, 15000);

    private sealed class Fixture {
      public AppViewModel App;
      public DesktopWindowViewModel Win;
      public ShortcutViewModel Note;
      public ShortcutViewModel Snippet;
      public ShortcutViewModel Command;
    }

    private static void WithButtons(Action<Fixture> body) {
      WpfAppHost.Run(() => {
        Action<ShortcutViewModel> oldNoteEditor = ShortcutViewModel.OpenNoteEditor;
        Action<ShortcutViewModel> oldSnippetEditor = ShortcutViewModel.OpenSnippetEditor;
        Action<TextEditWindow> oldPlace = TextEditWindow.PlaceNoteWindow;
        Func<IList<ScreenRect>> oldAreas = TextEditWindow.WorkAreas;
        Func<ModifierKeys> oldModifiers = TextEditWindow.KeyboardModifiers;
        ShortcutViewModel.OpenNoteEditor = s => { };
        ShortcutViewModel.OpenSnippetEditor = s => { };
        TextEditWindow.PlaceNoteWindow = w => {
          w.WindowStartupLocation = WindowStartupLocation.Manual;
          w.Left = -10000;
          w.Top = -10000;
        };
        TextEditWindow.WorkAreas = () => new List<ScreenRect> { TestArea };
        Fixture f = new Fixture();
        try {
          using (TempDb tempDb = new TempDb()) {
            f.App = new AppViewModel();
            f.Win = f.App.AddWindow("TEST", 0, 0);
            f.Win.NewNoteShortcutCommand.Execute(null);
            f.Note = f.Win.RootShortcuts.Last();
            f.Note.CancelEdit();
            f.Note.Name = "Checklist";
            f.Win.NewSnippetShortcutCommand.Execute(null);
            f.Snippet = f.Win.RootShortcuts.Last();
            f.Snippet.CancelEdit();
            f.Snippet.Name = "Address";
            f.Snippet.SnippetText = "Main Street 12";
            f.Command = ViewModelFactory.NewProcessCommandShortcut(f.App, null, f.Win);
            f.Win.RootShortcuts.Add(f.Command);
            f.Command.Name = "Ping";
            body(f);
          }
        } finally {
          foreach (TextEditWindow w in TextEditWindow.OpenNoteWindows.ToList()) {
            w.Close();
          }
          WpfAppHost.Pump();
          ShortcutViewModel.OpenNoteEditor = oldNoteEditor;
          ShortcutViewModel.OpenSnippetEditor = oldSnippetEditor;
          TextEditWindow.PlaceNoteWindow = oldPlace;
          TextEditWindow.WorkAreas = oldAreas;
          TextEditWindow.KeyboardModifiers = oldModifiers;
        }
      });
    }

    private static TextEditWindow OpenNote(ShortcutViewModel note) {
      TextEditWindow.ShowNoteFor(note);
      WpfAppHost.Pump();
      return TextEditWindow.OpenNoteWindows.Single(w => w.Shortcut == note);
    }

    private static void Shown(TextEditWindow window, Action check) {
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
        window.Close();
        WpfAppHost.Pump();
      }
    }

    private static T Named<T>(Window window, string name) where T : class {
      return Assert.IsAssignableFrom<T>(window.FindName(name));
    }

    private static TextBox Text(Window window) {
      return Named<TextBox>(window, "TextComment");
    }

    private static bool Wheel(TextEditWindow window, int delta) {
      TextEditWindow.KeyboardModifiers = () => ModifierKeys.Control;
      MouseWheelEventArgs e = new MouseWheelEventArgs(Mouse.PrimaryDevice, Environment.TickCount, delta) { RoutedEvent = UIElement.PreviewMouseWheelEvent };
      Text(window).RaiseEvent(e);
      WpfAppHost.Pump();
      return e.Handled;
    }

    private static int ColumnOf(UIElement e) => Grid.GetColumn(e);

    private static List<MenuItem> FontItems(ContextMenu menu) => menu.Items.OfType<MenuItem>().Where(i => i.IsEnabled).ToList();

    private static void Click(MenuItem item) {
      item.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent, item));
      WpfAppHost.Pump();
    }

    // ---- The font button ----

    [Fact]
    public void The_note_window_has_a_font_button_left_of_the_colours_wrap_pin_and_close() {
      WithButtons(f => {
        TextEditWindow window = OpenNote(f.Note);
        Button font = Named<Button>(window, "ButtonFont");
        Assert.Equal(Visibility.Visible, font.Visibility);
        Assert.Equal("Font", font.ToolTip);
        Assert.Equal("Font", System.Windows.Automation.AutomationProperties.GetName(font));
        Assert.Equal(f.Win.Appearance.Icons.Font, font.Content);
        int column = ColumnOf(font);
        foreach (string other in new[] { "ButtonColors", "ButtonWrap", "ButtonPin", "ButtonClose" }) {
          Assert.True(column < ColumnOf(Named<Button>(window, other)), "Font should be left of " + other);
        }
        Assert.True(WindowChromeHit(font));
      });
    }

    private static bool WindowChromeHit(Button b) => System.Windows.Shell.WindowChrome.GetIsHitTestVisibleInChrome(b);

    [Fact]
    public void The_font_button_shows_the_font_glyph_in_the_icon_font_and_follows_the_icon_set() {
      WithButtons(f => {
        TextEditWindow window = OpenNote(f.Note);
        Button font = Named<Button>(window, "ButtonFont");
        Assert.Contains("MDSHOFont", font.FontFamily.Source);
        f.Win.Appearance.Icons.SolidIcons = true;
        WpfAppHost.Pump();
        Assert.Equal("\uE95C", font.Content);
        f.Win.Appearance.Icons.SolidIcons = false;
        WpfAppHost.Pump();
        Assert.Equal("\uE95D", font.Content);
      });
    }

    [Fact]
    public void The_snippet_editor_font_button_shows_the_font_glyph() {
      WithButtons(f => {
        TextEditWindow window = TextEditWindow.ForSnippet(f.Snippet);
        Shown(window, () => Assert.Equal(f.Win.Appearance.Icons.Font, Named<Button>(window, "ButtonFont").Content));
      });
    }

    [Fact]
    public void The_font_button_follows_the_note_title_text_colour() {
      WithButtons(f => {
        TextEditWindow window = OpenNote(f.Note);
        Button font = Named<Button>(window, "ButtonFont");
        Button close = Named<Button>(window, "ButtonClose");
        Assert.Equal(((SolidColorBrush)close.Foreground).Color, ((SolidColorBrush)font.Foreground).Color);
        Assert.NotNull(System.Windows.Data.BindingOperations.GetBinding(font, Control.ForegroundProperty));
      });
    }

    [Fact]
    public void The_snippet_editor_has_the_font_button_left_of_wrap_ok_and_close() {
      WithButtons(f => {
        TextEditWindow window = TextEditWindow.ForSnippet(f.Snippet);
        Shown(window, () => {
          Button font = Named<Button>(window, "ButtonFont");
          Assert.Equal(Visibility.Visible, font.Visibility);
          Assert.Equal("Font", font.ToolTip);
          foreach (string other in new[] { "ButtonWrap", "ButtonClose" }) {
            Assert.True(ColumnOf(font) < ColumnOf(Named<Button>(window, other)), "Font should be left of " + other);
          }
        });
      });
    }

    [Fact]
    public void A_comment_window_has_the_font_button_and_starts_in_courier_new_at_12_pt() {
      WithButtons(f => {
        TextEditWindow window = TextEditWindow.ForComment(f.Command);
        Shown(window, () => {
          Button font = Named<Button>(window, "ButtonFont");
          Assert.Equal(Visibility.Visible, font.Visibility);
          Assert.Equal(f.Win.Appearance.Icons.Font, font.Content);
          Assert.Equal("Courier New", Text(window).FontFamily.Source);
          Assert.Equal(16, Text(window).FontSize, 6);
          foreach (string other in new[] { "ButtonWrap", "ButtonClose" }) {
            Assert.True(ColumnOf(font) < ColumnOf(Named<Button>(window, other)), "Font should be left of " + other);
          }
        });
      });
    }

    [Fact]
    public void A_comment_keeps_its_own_font_and_size_apart_from_the_text_font() {
      WithButtons(f => {
        TextEditWindow window = TextEditWindow.ForComment(f.Command);
        Shown(window, () => {
          Click(FontItems(window.BuildFontMenu()).Single(i => (string)i.Header == "Consolas"));
          Click(SizeItem(window.BuildSizeMenu(), "18 pt"));
          Assert.Equal("Consolas", Text(window).FontFamily.Source);
          Assert.Equal(24, Text(window).FontSize, 6);
          Assert.Equal(new[] { "Consolas" }, FontItems(window.BuildFontMenu()).Where(i => i.IsChecked).Select(i => (string)i.Header));
        });
        Shortcut row = DataStore.Shortcuts.Get(f.Command.ID);
        Assert.Equal(("Consolas", 18L, "Courier New"), (row.CommentFont, row.CommentFontSize, row.TextFont));
        TextEditWindow again = TextEditWindow.ForComment(f.Command);
        Shown(again, () => {
          Assert.Equal("Consolas", Text(again).FontFamily.Source);
          Assert.Equal(24, Text(again).FontSize, 6);
          Assert.Equal("18 pt", Named<Button>(again, "ButtonSize").Content);
        });
      });
    }

    [Fact]
    public void Ctrl_wheel_in_a_comment_saves_its_size() {
      WithButtons(f => {
        TextEditWindow window = TextEditWindow.ForComment(f.Command);
        Shown(window, () => {
          Assert.True(Wheel(window, 120));
          Assert.Equal("14 pt", Named<Button>(window, "ButtonSize").Content);
        });
        Assert.Equal(14, DataStore.Shortcuts.Get(f.Command.ID).CommentFontSize);
      });
    }

    // ---- The text uses the note's or snippet's font ----

    [Fact]
    public void A_note_opens_in_courier_new_by_default() {
      WithButtons(f => {
        Assert.Equal("Courier New", Text(OpenNote(f.Note)).FontFamily.Source);
      });
    }

    [Fact]
    public void A_note_opens_in_its_own_font() {
      WithButtons(f => {
        f.Note.SaveTextFont("Consolas");
        Assert.Equal("Consolas", Text(OpenNote(f.Note)).FontFamily.Source);
      });
    }

    [Fact]
    public void A_snippet_opens_in_its_own_font() {
      WithButtons(f => {
        f.Snippet.SaveTextFont("Arial");
        TextEditWindow window = TextEditWindow.ForSnippet(f.Snippet);
        Shown(window, () => Assert.Equal("Arial", Text(window).FontFamily.Source));
      });
    }

    [Fact]
    public void A_shipped_font_whose_file_is_missing_shows_courier_new() {
      WithButtons(f => {
        Func<string, bool> old = NoteFontFamilies.ResourceExists;
        NoteFontFamilies.ResourceExists = file => false;
        try {
          f.Note.SaveTextFont("JetBrains Mono");
          Assert.Equal("Courier New", Text(OpenNote(f.Note)).FontFamily.Source);
        } finally {
          NoteFontFamilies.ResourceExists = old;
        }
      });
    }

    // ---- The font menu ----

    [Fact]
    public void The_menu_has_two_labelled_groups_with_each_name_in_its_own_font() {
      WithButtons(f => {
        TextEditWindow window = OpenNote(f.Note);
        ContextMenu menu = window.BuildFontMenu();
        List<object> items = menu.Items.Cast<object>().ToList();
        List<string> shape = items.Select(i => i is Separator ? "-" : i is MenuItem m ? (m.IsEnabled ? "" : "label:") + m.Header : "?").ToList();
        Assert.Equal(new[] {
          "label:From Windows", "Courier New", "Consolas", "Arial", "Verdana", "Calibri", "-",
          "label:Comes with MDSHO", "JetBrains Mono", "Source Code Pro", "IBM Plex Mono", "Latin Modern Roman", "Latin Modern Mono" }, shape);
        foreach (MenuItem item in FontItems(menu)) {
          Assert.Equal(NoteFontFamilies.Resolve((string)item.Header).Source, item.FontFamily.Source);
        }
        Assert.Equal("Consolas", FontItems(menu).Single(i => (string)i.Header == "Consolas").FontFamily.Source);
        foreach (MenuItem label in menu.Items.OfType<MenuItem>().Where(i => !i.IsEnabled)) {
          Assert.True(label.FontSize < menu.Items.OfType<MenuItem>().First(i => i.IsEnabled).FontSize || label.FontSize <= 11, "Labels are small");
        }
      });
    }

    [Fact]
    public void The_current_font_has_the_check_mark() {
      WithButtons(f => {
        f.Note.SaveTextFont("IBM Plex Mono");
        ContextMenu menu = OpenNote(f.Note).BuildFontMenu();
        Assert.Equal(new[] { "IBM Plex Mono" }, FontItems(menu).Where(i => i.IsChecked).Select(i => (string)i.Header));
      });
    }

    [Fact]
    public void The_font_button_opens_the_menu_below_it() {
      WithButtons(f => {
        TextEditWindow window = OpenNote(f.Note);
        Button font = Named<Button>(window, "ButtonFont");
        font.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent, font));
        WpfAppHost.Pump();
        ContextMenu menu = window.FontMenu;
        Assert.NotNull(menu);
        try {
          Assert.True(menu.IsOpen);
          Assert.Same(font, menu.PlacementTarget);
          Assert.Equal(PlacementMode.Bottom, menu.Placement);
        } finally {
          menu.IsOpen = false;
          WpfAppHost.Pump();
        }
      });
    }

    [Fact]
    public void Picking_a_font_applies_it_at_once_saves_it_and_moves_the_check_mark() {
      WithButtons(f => {
        TextEditWindow window = OpenNote(f.Note);
        Click(FontItems(window.BuildFontMenu()).Single(i => (string)i.Header == "Arial"));
        Assert.Equal("Arial", Text(window).FontFamily.Source);
        Assert.Equal("Arial", f.Note.TextFont);
        Assert.Equal("Arial", DataStore.Shortcuts.Get(f.Note.ID).TextFont);
        Assert.Equal(new[] { "Arial" }, FontItems(window.BuildFontMenu()).Where(i => i.IsChecked).Select(i => (string)i.Header));
        window.Close();
        WpfAppHost.Pump();
        Assert.Equal("Arial", Text(OpenNote(f.Note)).FontFamily.Source);
      });
    }

    [Fact]
    public void Picking_a_font_in_the_snippet_editor_saves_it_and_is_not_a_change_to_the_value() {
      WithButtons(f => {
        TextEditWindow window = TextEditWindow.ForSnippet(f.Snippet);
        Shown(window, () => {
          Click(FontItems(window.BuildFontMenu()).Single(i => (string)i.Header == "Consolas"));
          Assert.Equal("Consolas", Text(window).FontFamily.Source);
        });
        Assert.False(window.Saved);
        Assert.Equal("Consolas", DataStore.Shortcuts.Get(f.Snippet.ID).TextFont);
        Assert.Equal("Main Street 12", DataStore.Shortcuts.Get(f.Snippet.ID).SnippetText);
      });
    }

    [Fact]
    public void Text_typed_in_another_font_stays_plain_text() {
      WithButtons(f => {
        TextEditWindow window = OpenNote(f.Note);
        Click(FontItems(window.BuildFontMenu()).Single(i => (string)i.Header == "Consolas"));
        TextBox text = Text(window);
        text.Text = "line one\r\nline two";
        Assert.IsType<TextBox>(text);
        Assert.True(window.SaveNoteNow());
        Assert.Equal("line one\r\nline two", DataStore.Shortcuts.Get(f.Note.ID).NoteText);
      });
    }

    // ---- The text size button ----

    private static List<string> SizeShape(ContextMenu menu) {
      return menu.Items.Cast<object>().Select(i => i is Separator ? "-" : i is MenuItem m ? (m.IsEnabled ? "" : "label:") + m.Header : "?").ToList();
    }

    private static List<MenuItem> SizeItems(ContextMenu menu) => menu.Items.OfType<MenuItem>().Where(i => i.IsEnabled).ToList();

    private static MenuItem SizeItem(ContextMenu menu, string header) => SizeItems(menu).Single(i => (string)i.Header == header);

    private static List<string> Ticked(ContextMenu menu) => SizeItems(menu).Where(i => i.IsChecked).Select(i => (string)i.Header).ToList();

    [Fact]
    public void The_note_window_has_a_text_size_button_just_left_of_the_font_button() {
      WithButtons(f => {
        TextEditWindow window = OpenNote(f.Note);
        Button size = Named<Button>(window, "ButtonSize");
        Assert.Equal(Visibility.Visible, size.Visibility);
        Assert.Equal("Text size", size.ToolTip);
        Assert.Equal("Text size", System.Windows.Automation.AutomationProperties.GetName(size));
        Assert.Equal("12 pt", size.Content);
        Assert.Equal(ColumnOf(Named<Button>(window, "ButtonFont")) - 1, ColumnOf(size));
        Assert.True(WindowChromeHit(size));
        Assert.NotNull(System.Windows.Data.BindingOperations.GetBinding(size, Control.ForegroundProperty));
      });
    }

    [Fact]
    public void The_snippet_editor_has_the_text_size_button_too() {
      WithButtons(f => {
        TextEditWindow window = TextEditWindow.ForSnippet(f.Snippet);
        Shown(window, () => {
          Button size = Named<Button>(window, "ButtonSize");
          Assert.Equal(Visibility.Visible, size.Visibility);
          Assert.Equal("12 pt", size.Content);
          Assert.Equal(16, Text(window).FontSize, 6);
          Assert.Equal(ColumnOf(Named<Button>(window, "ButtonFont")) - 1, ColumnOf(size));
        });
      });
    }

    [Fact]
    public void The_snippet_editor_has_the_comment_window_s_smallest_width() {
      WithButtons(f => {
        TextEditWindow window = TextEditWindow.ForSnippet(f.Snippet);
        Shown(window, () => {
          Assert.Equal(TextEditWindow.TextMinWidth, window.MinWidth);
          Assert.True(window.ActualWidth >= TextEditWindow.TextMinWidth);
        });
      });
    }

    [Fact]
    public void A_comment_window_has_the_text_size_button_just_left_of_the_font_button() {
      WithButtons(f => {
        TextEditWindow window = TextEditWindow.ForComment(f.Command);
        Shown(window, () => {
          Button size = Named<Button>(window, "ButtonSize");
          Assert.Equal(Visibility.Visible, size.Visibility);
          Assert.Equal("12 pt", size.Content);
          Assert.Equal(ColumnOf(Named<Button>(window, "ButtonFont")) - 1, ColumnOf(size));
        });
      });
    }

    [Fact]
    public void The_size_menu_has_a_header_the_presets_and_the_default() {
      WithButtons(f => {
        ContextMenu menu = OpenNote(f.Note).BuildSizeMenu();
        Assert.Equal(new[] {
          "label:Text size", "8 pt", "9 pt", "10 pt", "11 pt", "12 pt", "14 pt", "16 pt", "18 pt", "20 pt", "22 pt", "24 pt",
          "26 pt", "28 pt", "36 pt", "48 pt", "72 pt", "-",
          "Default (12 pt)" }, SizeShape(menu));
        Assert.Equal(new[] { "12 pt" }, Ticked(menu));
      });
    }

    [Fact]
    public void The_size_button_opens_the_menu_below_it() {
      WithButtons(f => {
        TextEditWindow window = OpenNote(f.Note);
        Button size = Named<Button>(window, "ButtonSize");
        size.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent, size));
        WpfAppHost.Pump();
        ContextMenu menu = window.SizeMenu;
        Assert.NotNull(menu);
        try {
          Assert.True(menu.IsOpen);
          Assert.Same(size, menu.PlacementTarget);
          Assert.Equal(PlacementMode.Bottom, menu.Placement);
        } finally {
          menu.IsOpen = false;
          WpfAppHost.Pump();
        }
      });
    }

    [Fact]
    public void Picking_a_size_applies_it_at_once_saves_it_and_moves_the_tick() {
      WithButtons(f => {
        TextEditWindow window = OpenNote(f.Note);
        Click(SizeItem(window.BuildSizeMenu(), "18 pt"));
        Assert.Equal(24, Text(window).FontSize, 6);
        Assert.Equal("18 pt", Named<Button>(window, "ButtonSize").Content);
        Assert.Equal(18, DataStore.NoteWindows.Get(f.Note.ID).FontSize);
        Assert.Equal(new[] { "18 pt" }, Ticked(window.BuildSizeMenu()));
        window.Close();
        WpfAppHost.Pump();
        TextEditWindow again = OpenNote(f.Note);
        Assert.Equal(24, Text(again).FontSize, 6);
        Assert.Equal("18 pt", Named<Button>(again, "ButtonSize").Content);
      });
    }

    [Fact]
    public void Default_goes_back_to_12_pt_and_saves_it() {
      WithButtons(f => {
        TextEditWindow window = OpenNote(f.Note);
        Click(SizeItem(window.BuildSizeMenu(), "36 pt"));
        Click(SizeItem(window.BuildSizeMenu(), "Default (12 pt)"));
        Assert.Equal(16, Text(window).FontSize, 6);
        Assert.Equal("12 pt", Named<Button>(window, "ButtonSize").Content);
        Assert.Equal(12, DataStore.NoteWindows.Get(f.Note.ID).FontSize);
      });
    }

    [Fact]
    public void Ctrl_wheel_updates_the_button_in_a_note() {
      WithButtons(f => {
        TextEditWindow window = OpenNote(f.Note);
        Assert.True(Wheel(window, 120));
        Assert.Equal(NoteFontSizeTests.Dip(14), Text(window).FontSize, 6);
        Assert.Equal("14 pt", Named<Button>(window, "ButtonSize").Content);
        Assert.Equal(new[] { "14 pt" }, Ticked(window.BuildSizeMenu()));
      });
    }

    [Fact]
    public void A_saved_size_that_is_not_a_preset_shows_on_the_button_with_no_tick() {
      WithButtons(f => {
        DataStore.NoteWindows.Save(new NoteWindowPlace(f.Note.ID, -9200, -9100, 720, 610, NoteWindowPlace.Normal, isOpen: true) { FontSize = 17 });
        TextEditWindow window = OpenNote(f.Note);
        Assert.Equal(NoteFontSizeTests.Dip(17), Text(window).FontSize, 6);
        Assert.Equal("17 pt", Named<Button>(window, "ButtonSize").Content);
        Assert.Empty(Ticked(window.BuildSizeMenu()));
        Wheel(window, 120);
        Assert.Equal("18 pt", Named<Button>(window, "ButtonSize").Content);
        Assert.Equal(new[] { "18 pt" }, Ticked(window.BuildSizeMenu()));
      });
    }

    [Fact]
    public void The_wheel_lands_on_menu_sizes_only() {
      WithButtons(f => {
        TextEditWindow window = OpenNote(f.Note);
        for (int i = 0; i < 12; i++) {
          Wheel(window, i < 6 ? 120 : -120);
          Assert.Single(Ticked(window.BuildSizeMenu()));
        }
      });
    }

    [Fact]
    public void The_wheel_still_stops_at_the_limits_and_the_button_follows() {
      WithButtons(f => {
        TextEditWindow window = OpenNote(f.Note);
        for (int i = 0; i < 40; i++) { Wheel(window, 120); }
        Assert.Equal(96, Text(window).FontSize, 6);
        Assert.Equal("72 pt", Named<Button>(window, "ButtonSize").Content);
        for (int i = 0; i < 40; i++) { Wheel(window, -120); }
        Assert.Equal(NoteFontSizeTests.Dip(8), Text(window).FontSize, 6);
        Assert.Equal("8 pt", Named<Button>(window, "ButtonSize").Content);
      });
    }

    [Fact]
    public void The_snippet_editor_applies_a_picked_size() {
      WithButtons(f => {
        TextEditWindow window = TextEditWindow.ForSnippet(f.Snippet);
        Shown(window, () => {
          Click(SizeItem(window.BuildSizeMenu(), "9 pt"));
          Assert.Equal(12, Text(window).FontSize, 6);
          Assert.Equal("9 pt", Named<Button>(window, "ButtonSize").Content);
          Wheel(window, -120);
          Assert.Equal("8 pt", Named<Button>(window, "ButtonSize").Content);
        });
      });
    }

    [Fact]
    public void The_size_button_follows_the_note_title_text_colour() {
      WithButtons(f => {
        TextEditWindow window = OpenNote(f.Note);
        Button size = Named<Button>(window, "ButtonSize");
        Button close = Named<Button>(window, "ButtonClose");
        Assert.Equal(((SolidColorBrush)close.Foreground).Color, ((SolidColorBrush)size.Foreground).Color);
      });
    }

    // ---- No size label ----

    [Fact]
    public void No_window_has_the_centred_size_label_any_more() {
      WithButtons(f => {
        TextEditWindow comment = TextEditWindow.ForComment(f.Command);
        Shown(comment, () => {
          Wheel(comment, 120);
          Assert.Null(comment.FindName("TextSizeLabel"));
        });
        Assert.Null(OpenNote(f.Note).FindName("TextSizeLabel"));
      });
    }

    // ---- Loading the fonts ----

    [Fact]
    public void Windows_fonts_load_by_name() {
      WpfAppHost.Run(() => {
        Assert.Equal("Courier New", NoteFontFamilies.Resolve("Courier New").Source);
        Assert.Equal("Consolas", NoteFontFamilies.Resolve("Consolas").Source);
        Assert.Equal("Arial", NoteFontFamilies.Resolve("Arial").Source);
        Assert.Equal("Courier New", NoteFontFamilies.Resolve("Not a font").Source);
      });
    }

    [Fact]
    public void Each_shipped_font_has_a_regular_file_under_assets_fonts() {
      foreach (string name in NoteFonts.ComesWithMdsho) {
        string file = NoteFontFamilies.FileOf(name);
        Assert.False(string.IsNullOrEmpty(file), name);
        Assert.Matches(@"^[A-Za-z0-9\-]+\.(ttf|otf)$", file);
        Assert.Contains("regular", file, StringComparison.OrdinalIgnoreCase);
      }
      Assert.Equal(5, NoteFonts.ComesWithMdsho.Select(NoteFontFamilies.FileOf).Distinct().Count());
      Assert.Null(NoteFontFamilies.FileOf("Courier New"));
    }

    [Fact]
    public void A_shipped_font_loads_from_the_embedded_fonts_folder_when_its_file_is_there() {
      WpfAppHost.Run(() => {
        Func<string, bool> old = NoteFontFamilies.ResourceExists;
        try {
          NoteFontFamilies.ResourceExists = file => true;
          FontFamily family = NoteFontFamilies.Resolve("IBM Plex Mono");
          Assert.Equal("pack://application:,,,/MDSHO;component/Assets/Fonts/", family.BaseUri.ToString());
          Assert.EndsWith("#" + NoteFontFamilies.FamilyInFileOf("IBM Plex Mono"), family.Source);
          NoteFontFamilies.ResourceExists = file => false;
          Assert.Equal("Courier New", NoteFontFamilies.Resolve("IBM Plex Mono").Source);
        } finally {
          NoteFontFamilies.ResourceExists = old;
        }
      });
    }

    [Fact]
    public void The_embedded_resource_check_finds_the_icon_font_and_not_a_missing_file() {
      Assert.True(NoteFontFamilies.EmbeddedFontFileExists("MDSHOFont.ttf"));
      Assert.False(NoteFontFamilies.EmbeddedFontFileExists("No-Such-Font-Regular.ttf"));
    }

    [Fact]
    public void The_app_project_embeds_every_ttf_and_otf_in_assets_fonts() {
      XDocument project = XDocument.Load(Path.Combine(ThirdPartyNoticesTests.RepoRoot(), "src", "MDSHO.App", "MDSHO.App.csproj"));
      List<string> resources = project.Descendants("Resource").Select(e => (string)e.Attribute("Include")).ToList();
      Assert.Contains(@"Assets\Fonts\*.ttf", resources);
      Assert.Contains(@"Assets\Fonts\*.otf", resources);
    }
  }
}
