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
  /// The font button and menu in the title bar of the Note window and the snippet editor, the size label in points,
  /// and how the fonts load (shipped ones from embedded resources, Courier New when a file is missing).
  /// </summary>
  public class NoteFontViewTests {
    private static readonly ScreenRect TestArea = new ScreenRect(-20000, -20000, 15000, 15000);

    /// <summary>A timer the test fires by hand.</summary>
    internal sealed class FakeTimer : IOneShotTimer {
      public TimeSpan? Delay;
      public Action Elapsed;
      public int Starts;
      public void Start(TimeSpan delay, Action elapsed) { Delay = delay; Elapsed = elapsed; Starts++; }
      public void Stop() { Delay = null; Elapsed = null; }
      public void Fire() { Action a = Elapsed; Delay = null; Elapsed = null; a?.Invoke(); }
    }

    private sealed class Fixture {
      public AppViewModel App;
      public DesktopWindowViewModel Win;
      public ShortcutViewModel Note;
      public ShortcutViewModel Snippet;
      public ShortcutViewModel Command;
      public FakeTimer Timer = new FakeTimer();
    }

    private static void WithButtons(Action<Fixture> body) {
      WpfAppHost.Run(() => {
        Action<ShortcutViewModel> oldNoteEditor = ShortcutViewModel.OpenNoteEditor;
        Action<ShortcutViewModel> oldSnippetEditor = ShortcutViewModel.OpenSnippetEditor;
        Action<TextEditWindow> oldPlace = TextEditWindow.PlaceNoteWindow;
        Func<IList<ScreenRect>> oldAreas = TextEditWindow.WorkAreas;
        Func<ModifierKeys> oldModifiers = TextEditWindow.KeyboardModifiers;
        Func<IOneShotTimer> oldTimer = TextEditWindow.NewSizeLabelTimer;
        ShortcutViewModel.OpenNoteEditor = s => { };
        ShortcutViewModel.OpenSnippetEditor = s => { };
        TextEditWindow.PlaceNoteWindow = w => {
          w.WindowStartupLocation = WindowStartupLocation.Manual;
          w.Left = -10000;
          w.Top = -10000;
        };
        TextEditWindow.WorkAreas = () => new List<ScreenRect> { TestArea };
        Fixture f = new Fixture();
        TextEditWindow.NewSizeLabelTimer = () => f.Timer;
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
          TextEditWindow.NewSizeLabelTimer = oldTimer;
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
          foreach (string other in new[] { "ButtonWrap", "ButtonOk", "ButtonClose" }) {
            Assert.True(ColumnOf(font) < ColumnOf(Named<Button>(window, other)), "Font should be left of " + other);
          }
        });
      });
    }

    [Fact]
    public void A_comment_window_has_no_font_button_and_keeps_its_font() {
      WithButtons(f => {
        TextEditWindow window = TextEditWindow.ForComment(f.Command);
        double before = 0;
        Shown(window, () => {
          Assert.NotEqual(Visibility.Visible, Named<Button>(window, "ButtonFont").Visibility);
          Assert.NotEqual("Courier New", Text(window).FontFamily.Source);
          before = Text(window).FontSize;
        });
        Assert.Equal(12, before);
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
          "label:From Windows", "Courier New", "Consolas", "Arial", "-",
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
    public void Picking_a_font_in_the_snippet_editor_saves_it_even_without_ok() {
      WithButtons(f => {
        TextEditWindow window = TextEditWindow.ForSnippet(f.Snippet);
        Shown(window, () => {
          Click(FontItems(window.BuildFontMenu()).Single(i => (string)i.Header == "Consolas"));
          Assert.Equal("Consolas", Text(window).FontFamily.Source);
          Assert.False(Named<Button>(window, "ButtonOk").IsEnabled);
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

    // ---- The size label ----

    [Fact]
    public void The_size_label_is_hidden_until_the_size_changes() {
      WithButtons(f => {
        TextEditWindow window = OpenNote(f.Note);
        Assert.False(window.IsSizeLabelShown);
        Assert.Equal(0, Named<TextBlock>(window, "TextSizeLabel").Opacity);
      });
    }

    [Fact]
    public void Ctrl_wheel_shows_the_size_in_points_in_the_title_bar() {
      WithButtons(f => {
        TextEditWindow window = OpenNote(f.Note);
        Assert.True(Wheel(window, 120));
        TextBlock label = Named<TextBlock>(window, "TextSizeLabel");
        Assert.True(window.IsSizeLabelShown);
        Assert.Equal("10.5 pt", label.Text);
        Assert.Equal(1, label.Opacity);
        Wheel(window, 120);
        Assert.Equal("12 pt", label.Text);
        Assert.Same(Named<Border>(window, "TitleBar"), FindAncestor<Border>(label, b => b.Name == "TitleBar"));
      });
    }

    [Fact]
    public void The_label_fades_two_seconds_after_the_last_change() {
      WithButtons(f => {
        TextEditWindow window = OpenNote(f.Note);
        Wheel(window, 120);
        Assert.Equal(TimeSpan.FromSeconds(2), f.Timer.Delay);
        Assert.Equal(TimeSpan.FromSeconds(2), TextEditWindow.SizeLabelDelay);
        Wheel(window, -120);
        Assert.Equal(2, f.Timer.Starts);
        Assert.True(window.IsSizeLabelShown);
        f.Timer.Fire();
        WpfAppHost.Pump();
        Assert.False(window.IsSizeLabelShown);
      });
    }

    [Fact]
    public void A_wheel_turn_at_the_limit_changes_nothing_and_shows_no_label() {
      WithButtons(f => {
        TextEditWindow window = OpenNote(f.Note);
        for (int i = 0; i < 40; i++) { Wheel(window, 120); }
        f.Timer.Fire();
        WpfAppHost.Pump();
        int starts = f.Timer.Starts;
        Wheel(window, 120);
        Assert.Equal(starts, f.Timer.Starts);
        Assert.False(window.IsSizeLabelShown);
      });
    }

    [Fact]
    public void The_label_never_takes_clicks_from_the_title_bar() {
      WithButtons(f => {
        TextEditWindow window = OpenNote(f.Note);
        Wheel(window, 120);
        TextBlock label = Named<TextBlock>(window, "TextSizeLabel");
        Assert.False(label.IsHitTestVisible);
        Assert.False(System.Windows.Shell.WindowChrome.GetIsHitTestVisibleInChrome(label));
        Border bar = Named<Border>(window, "TitleBar");
        Point middle = label.TranslatePoint(new Point(label.ActualWidth / 2, label.ActualHeight / 2), bar);
        // What the mouse would click there (input hit testing skips elements that aren't hit test visible).
        DependencyObject hit = bar.InputHitTest(middle) as DependencyObject;
        Assert.NotNull(hit);
        Assert.False(IsInside(hit, label), "The label must let clicks through");
      });
    }

    [Fact]
    public void The_snippet_editor_shows_the_size_label_too() {
      WithButtons(f => {
        TextEditWindow window = TextEditWindow.ForSnippet(f.Snippet);
        Shown(window, () => {
          Wheel(window, -120);
          Assert.True(window.IsSizeLabelShown);
          Assert.Equal("7.5 pt", Named<TextBlock>(window, "TextSizeLabel").Text);
        });
      });
    }

    private static bool IsInside(DependencyObject child, DependencyObject parent) {
      for (DependencyObject d = child; d != null; d = VisualTreeHelper.GetParent(d)) {
        if (ReferenceEquals(d, parent)) { return true; }
      }
      return false;
    }

    private static T FindAncestor<T>(DependencyObject child, Func<T, bool> match) where T : DependencyObject {
      for (DependencyObject d = VisualTreeHelper.GetParent(child); d != null; d = VisualTreeHelper.GetParent(d)) {
        if (d is T t && match(t)) { return t; }
      }
      return null;
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
          Assert.Equal("pack://application:,,,/Assets/Fonts/", family.BaseUri.ToString());
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
