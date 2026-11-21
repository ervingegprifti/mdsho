using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Xml.Linq;
using MDSHO.Data;
using MDSHO.Helpers;
using MDSHO.ViewModels;
using Xunit;
using WinForms = System.Windows.Forms;

namespace MDSHO.Tests {

  /// <summary>
  /// The window theme in the app: the Light and Dark dictionaries, swapping them at runtime, which windows follow them
  /// (MDSHO's own windows and menus, never panels or notes), the tray menu, and the "Window theme" section of Settings.
  /// </summary>
  public class WindowThemeAppTests {

    private static readonly XNamespace X = "http://schemas.microsoft.com/winfx/2006/xaml";

    // ---- Helpers ----

    /// <summary>Runs <paramref name="body"/> on the WPF thread and puts the Light theme back afterwards.</summary>
    private static void InApp(Action body) {
      WpfAppHost.Run(() => {
        try {
          body();
        } finally {
          WindowThemes.Apply(WindowThemeKind.Light);
          WpfAppHost.Pump();
        }
      });
    }

    private static T ShowOffScreen<T>(T window) where T : Window {
      window.WindowStartupLocation = WindowStartupLocation.Manual;
      window.Left = -10000;
      window.Top = -10000;
      window.ShowActivated = false;
      window.ShowInTaskbar = false;
      window.Show();
      WpfAppHost.Pump();
      return window;
    }

    private static T Named<T>(FrameworkElement root, string name) where T : class {
      return Assert.IsAssignableFrom<T>(root.FindName(name));
    }

    private static Color ColorOf(object brush) {
      return Assert.IsType<SolidColorBrush>(brush).Color;
    }

    private static string Hex(object brush) {
      return ColorOf(brush).ToString();
    }

    private static Dictionary<object, object> Entries(Uri source) {
      ResourceDictionary dictionary = new ResourceDictionary { Source = source };
      return dictionary.Keys.Cast<object>().ToDictionary(k => k, k => dictionary[k]);
    }

    private static double Luminance(Color c) {
      static double Channel(byte v) {
        double s = v / 255.0;
        return s <= 0.03928 ? s / 12.92 : Math.Pow((s + 0.055) / 1.055, 2.4);
      }
      return 0.2126 * Channel(c.R) + 0.7152 * Channel(c.G) + 0.0722 * Channel(c.B);
    }

    private static double Contrast(Color a, Color b) {
      double la = Luminance(a), lb = Luminance(b);
      return (Math.Max(la, lb) + 0.05) / (Math.Min(la, lb) + 0.05);
    }

    private static string AppFolder() {
      return Path.Combine(ThirdPartyNoticesTests.RepoRoot(), "src", "MDSHO.App");
    }

    /// <summary>MDSHO's own windows and controls without user colours: they follow the theme.</summary>
    public static readonly string[] ThemedXamlFiles = {
      "AboutWindow.xaml", "ColorsWindow.xaml", "MessageWindow.xaml", "RunCommandExamplesWindow.xaml",
      "RunCommandWindow.xaml", "SettingsWindow.xaml", "ReviewWindow.xaml", "Controls/DialogTitleBar.xaml", "App.xaml",
    };

    // ---- The dictionaries ----

    [Fact]
    public void Every_key_is_in_both_the_Light_and_the_Dark_dictionary_and_every_brush_is_a_plain_colour() {
      InApp(() => {
        Dictionary<object, object> light = Entries(WindowThemes.LightUri);
        Dictionary<object, object> dark = Entries(WindowThemes.DarkUri);
        List<string> lightBrushes = light.Where(e => e.Value is Brush).Select(e => e.Key.ToString()).OrderBy(k => k).ToList();
        List<string> darkBrushes = dark.Where(e => e.Value is Brush).Select(e => e.Key.ToString()).OrderBy(k => k).ToList();
        Assert.True(lightBrushes.Count >= 30, "Expected the theme brushes, found " + lightBrushes.Count);
        Assert.Equal(lightBrushes, darkBrushes);
        Assert.All(light.Values.OfType<Brush>().Concat(dark.Values.OfType<Brush>()), b => Assert.IsType<SolidColorBrush>(b));
        Assert.All(lightBrushes, k => Assert.StartsWith("Theme.", k));
      });
    }

    /// <summary>The colours the windows had before themes, by the brush that replaced them.</summary>
    public static readonly (string Key, string Color)[] PreviousColours = {
      ("Theme.WindowBackground", "#FFF4F3ED"),
      ("Theme.WindowBorder", "#FFB9B39F"),
      ("Theme.BarBackground", "#FFDFDACB"),
      ("Theme.ButtonBarBackground", "#FFDDDDDD"),
      ("Theme.Text", "#FF000000"),
      ("Theme.TextStrong", "#FF333333"),
      ("Theme.TextSecondary", "#FF5A5648"),
      ("Theme.TextMuted", "#FF555555"),
      ("Theme.TextDim", "#FF696969"),
      ("Theme.TextFaint", "#FF808080"),
      ("Theme.TitleButtonHover", "#1F000000"),
      ("Theme.TitleButtonPressed", "#33000000"),
      ("Theme.BarButtonHover", "#FFCDC7B4"),
      ("Theme.BarButtonPressed", "#FFBDB6A0"),
      ("Theme.InputBackground", "#FFFFFFFF"),
      ("Theme.InputText", "#FF000000"),
      ("Theme.InputBorder", "#FFB9B39F"),
      ("Theme.InputBorderHover", "#FF808080"),
      ("Theme.InputBorderFocus", "#FF424242"),
      ("Theme.ReadOnlyBackground", "#FFEAE7DC"),
      ("Theme.CardBackground", "#FFFFFFFF"),
      ("Theme.ItemHover", "#FFEAE7DC"),
      ("Theme.ItemSelected", "#FFDCEBFA"),
      ("Theme.Accent", "#FF1F6FC5"),
      ("Theme.SwatchBorder", "#FF8C8778"),
      ("Theme.PaletteBorder", "#FFC9C3B1"),
      ("Theme.Button", "#FF808080"),
      ("Theme.ButtonHover", "#FF696969"),
      ("Theme.ButtonPressed", "#FF555555"),
      ("Theme.ButtonText", "#FFFFFFFF"),
      ("Theme.SecondaryButton", "#FFA09A88"),
      ("Theme.LightButton", "#FFD3D3D3"),
      ("Theme.LightButtonText", "#FF000000"),
      ("Theme.LightButtonHover", "#FF808080"),
      ("Theme.LightButtonPressed", "#FF696969"),
      ("Theme.LightButtonActiveText", "#FFFFFFFF"),
      ("Theme.MenuIcon", "#FF424242"),
      ("Theme.MarkPanel", "#FFEEEEEE"),
      ("Theme.MarkBorder", "#FFD3D3D3"),
      ("Theme.ErrorText", "#FFC42B1C"),
      ("Theme.HintErrorText", "#FFFF0000"),
      ("Theme.ToolTipSecondaryText", "#FF666666"),
    };

    [Fact]
    public void Light_has_exactly_the_colours_the_windows_had_before() {
      InApp(() => {
        Dictionary<object, object> light = Entries(WindowThemes.LightUri);
        foreach ((string key, string color) in PreviousColours) {
          Assert.True(light.ContainsKey(key), key + " is missing");
          Assert.Equal(color, Hex(light[key]));
        }
        // Links stay the Windows link colour.
        Assert.Equal(SystemColors.HotTrackColor, ColorOf(light["Theme.Link"]));
        // Menus and controls keep the Windows look in Light: no styles of their own.
        ResourceDictionary dictionary = new ResourceDictionary { Source = WindowThemes.LightUri };
        Assert.Empty(dictionary.Values.OfType<Style>());
      });
    }

    [Fact]
    public void Dark_uses_the_approved_starting_palette() {
      InApp(() => {
        Dictionary<object, object> dark = Entries(WindowThemes.DarkUri);
        Assert.Equal("#FF202020", Hex(dark["Theme.WindowBackground"]));
        Assert.Equal("#FF171717", Hex(dark["Theme.BarBackground"]));
        Assert.Equal("#FF171717", Hex(dark["Theme.ButtonBarBackground"]));
        Assert.Equal("#FFECECEC", Hex(dark["Theme.Text"]));
        Assert.Equal("#FFA8A8A8", Hex(dark["Theme.TextSecondary"]));
        Assert.Equal("#FF2C2C2C", Hex(dark["Theme.InputBackground"]));
        Assert.Equal("#FF555555", Hex(dark["Theme.InputBorder"]));
        Assert.Equal("#FF555555", Hex(dark["Theme.WindowBorder"]));
        Assert.Equal("#FF333333", Hex(dark["Theme.SecondaryButton"]));
        Assert.Equal("#FF2F3D52", Hex(dark["Theme.MenuHighlight"]));
      });
    }

    [Fact]
    public void In_Dark_text_errors_and_focus_borders_stay_readable() {
      InApp(() => {
        Dictionary<object, object> dark = Entries(WindowThemes.DarkUri);
        Color window = ColorOf(dark["Theme.WindowBackground"]);
        Color bar = ColorOf(dark["Theme.BarBackground"]);
        Color input = ColorOf(dark["Theme.InputBackground"]);
        foreach (string text in new[] { "Theme.Text", "Theme.TextStrong", "Theme.TextSecondary", "Theme.TextMuted", "Theme.TextDim", "Theme.ErrorText", "Theme.HintErrorText", "Theme.Link" }) {
          Assert.True(Contrast(ColorOf(dark[text]), window) >= 4.5, text + " is hard to read on the window background");
        }
        Assert.True(Contrast(ColorOf(dark["Theme.ErrorText"]), bar) >= 4.5, "The footer error is hard to read");
        Assert.True(Contrast(ColorOf(dark["Theme.InputText"]), input) >= 4.5, "Input text is hard to read");
        Assert.True(Contrast(ColorOf(dark["Theme.InputBorderFocus"]), input) >= 3, "The focus border is hard to see");
        Assert.True(Contrast(ColorOf(dark["Theme.ButtonText"]), ColorOf(dark["Theme.Button"])) >= 4.5, "Button text is hard to read");
        Assert.True(Contrast(ColorOf(dark["Theme.MenuText"]), ColorOf(dark["Theme.MenuBackground"])) >= 4.5, "Menu text is hard to read");
        Assert.True(Contrast(ColorOf(dark["Theme.MenuText"]), ColorOf(dark["Theme.MenuHighlight"])) >= 4.5, "Highlighted menu text is hard to read");
        Assert.True(Contrast(ColorOf(dark["Theme.MenuDisabledText"]), ColorOf(dark["Theme.MenuBackground"])) >= 3, "Disabled menu text is hard to read");
      });
    }

    // ---- Swapping ----

    private static List<Uri> ThemeSources() {
      return Application.Current.Resources.MergedDictionaries
        .Where(d => d.Source != null && (d.Source.OriginalString.EndsWith("Light.xaml") || d.Source.OriginalString.EndsWith("Dark.xaml")))
        .Select(d => d.Source).ToList();
    }

    [Fact]
    public void The_app_starts_in_Light_and_Apply_swaps_the_dictionary_in_the_app_resources() {
      InApp(() => {
        Assert.Equal(WindowThemeKind.Light, WindowThemes.Current);
        Assert.EndsWith("Themes/Light.xaml", Assert.Single(ThemeSources()).OriginalString);
        Assert.Equal("#FFF4F3ED", Hex(Application.Current.FindResource("Theme.WindowBackground")));

        int applied = 0;
        Action count = () => applied++;
        WindowThemes.Applied += count;
        try {
          WindowThemes.Apply(WindowThemeKind.Dark);
          Assert.Equal(WindowThemeKind.Dark, WindowThemes.Current);
          Assert.EndsWith("Themes/Dark.xaml", Assert.Single(ThemeSources()).OriginalString);
          Assert.Equal("#FF202020", Hex(Application.Current.FindResource("Theme.WindowBackground")));
          Assert.Same(WindowThemes.ActiveDictionary, Application.Current.Resources.MergedDictionaries.Single(d => d.Source == WindowThemes.ActiveDictionary.Source));
          // Menus get the dark menu styles.
          Assert.True(WindowThemes.ActiveDictionary.Contains(typeof(ContextMenu)));
          Assert.True(WindowThemes.ActiveDictionary.Contains(typeof(MenuItem)));

          WindowThemes.Apply(WindowThemeKind.Light);
          Assert.EndsWith("Themes/Light.xaml", Assert.Single(ThemeSources()).OriginalString);
          Assert.False(WindowThemes.ActiveDictionary.Contains(typeof(MenuItem)));
          Assert.Equal(2, applied);
        } finally {
          WindowThemes.Applied -= count;
        }
      });
    }

    [Fact]
    public void Following_a_setting_applies_it_at_once_and_again_whenever_it_changes() {
      using (TempDb db = new TempDb()) {
        InApp(() => {
          FakeWindowsAppMode windows = new FakeWindowsAppMode { Light = false };
          WindowThemeSetting setting = new WindowThemeSetting(windows);
          try {
            WindowThemes.Follow(setting);
            Assert.Equal(WindowThemeKind.Dark, WindowThemes.Current);
            windows.Switch(light: true);
            WpfAppHost.Pump();
            Assert.Equal(WindowThemeKind.Light, WindowThemes.Current);
            setting.SetChoice(WindowThemeChoice.Dark);
            WpfAppHost.Pump();
            Assert.Equal(WindowThemeKind.Dark, WindowThemes.Current);
          } finally {
            WindowThemes.Follow(null);
          }
          // No longer followed.
          setting.SetChoice(WindowThemeChoice.Light);
          WindowThemes.Apply(WindowThemeKind.Dark);
          setting.SetChoice(WindowThemeChoice.SameAsWindows);
          WpfAppHost.Pump();
          Assert.Equal(WindowThemeKind.Dark, WindowThemes.Current);
        });
      }
    }

    [Fact]
    public void An_open_window_changes_at_once() {
      using (TempDb db = new TempDb()) {
        InApp(() => {
          DesktopWindowViewModel w = new AppViewModel().AddWindow("TEST", 100, 100);
          SettingsWindow settings = ShowOffScreen(new SettingsWindow(w));
          try {
            Border titleBar = Assert.IsType<Border>(Named<UserControl>(settings, "TitleBar").Content);
            TextBlock title = Named<TextBlock>((FrameworkElement)settings.FindName("TitleBar"), "TextTitle");
            Border footer = Named<Border>(settings, "Footer");
            Assert.Equal("#FFF4F3ED", Hex(settings.Background));
            Assert.Equal("#FFB9B39F", Hex(settings.BorderBrush));
            Assert.Equal("#FFDFDACB", Hex(titleBar.Background));
            Assert.Equal("#FF333333", Hex(title.Foreground));
            Assert.Equal("#FFDFDACB", Hex(footer.Background));

            WindowThemes.Apply(WindowThemeKind.Dark);
            WpfAppHost.Pump();
            Assert.Equal("#FF202020", Hex(settings.Background));
            Assert.Equal("#FF555555", Hex(settings.BorderBrush));
            Assert.Equal("#FF171717", Hex(titleBar.Background));
            Assert.Equal("#FFECECEC", Hex(title.Foreground));
            Assert.Equal("#FF171717", Hex(footer.Background));
            // Text with no colour of its own (labels, check boxes) is light too.
            Label category = WpfAppHost.VisualDescendants<Label>(settings).First();
            Assert.Equal("#FFECECEC", Hex(category.Foreground));
            Assert.Equal("#FFECECEC", Hex(Named<CheckBox>(settings, "CheckSingleClickToOpen").Foreground));

            WindowThemes.Apply(WindowThemeKind.Light);
            WpfAppHost.Pump();
            Assert.Equal("#FFF4F3ED", Hex(settings.Background));
            Assert.Equal("#FF333333", Hex(title.Foreground));
          } finally {
            settings.Close();
          }
        });
      }
    }

    [Fact]
    public void Controls_in_themed_windows_get_dark_styles_only_in_Dark() {
      using (TempDb db = new TempDb()) {
        InApp(() => {
          ShortcutViewModel command = RunCommandWindowTests.NewCommand();
          RunCommandWindow window = ShowOffScreen(new RunCommandWindow(new RunCommandViewModel(command, false)));
          try {
            TextBox folder = Named<TextBox>(window, "TextWorkingFolder");
            ComboBox program = Named<ComboBox>(window, "ComboProgram");
            CheckBox keepOpen = Named<CheckBox>(window, "CheckKeepOpen");
            Button browse = Named<Button>(window, "ButtonBrowseFolder");
            Assert.True(WindowThemes.GetIsThemed(window));
            // Light: the Windows look, as before.
            Assert.DoesNotContain(window.Resources.MergedDictionaries, d => d.Source == WindowThemes.DarkControlsUri);
            Assert.Null(keepOpen.Style);
            Assert.Null(program.Style);
            Assert.Equal(Colors.White, ColorOf(folder.Background));

            WindowThemes.Apply(WindowThemeKind.Dark);
            WpfAppHost.Pump();
            Assert.Single(window.Resources.MergedDictionaries, d => d.Source == WindowThemes.DarkControlsUri);
            Assert.Equal("#FF2C2C2C", Hex(folder.Background));
            Assert.Equal("#FFECECEC", Hex(folder.Foreground));
            Assert.NotNull(keepOpen.Style);
            Assert.NotNull(program.Style);
            Assert.NotNull(browse.Style);
            Assert.Equal("#FFECECEC", Hex(keepOpen.Foreground));
            Assert.Equal("#FFECECEC", Hex(browse.Foreground));
            // Every text box, also one with a style of its own (the arguments, with their error border).
            foreach (TextBox box in WpfAppHost.VisualDescendants<TextBox>(window).Where(b => b.Name != "PART_EditableTextBox")) {
              Assert.True(Luminance(ColorOf(box.Background)) < 0.05, box.Name + " has a light background in Dark");
              Assert.True(Luminance(ColorOf(box.Foreground)) > 0.5, box.Name + " has dark text in Dark");
              Assert.True(Luminance(ColorOf(box.BorderBrush)) < 0.3, box.Name + " has a light border in Dark");
            }

            WindowThemes.Apply(WindowThemeKind.Light);
            WpfAppHost.Pump();
            Assert.DoesNotContain(window.Resources.MergedDictionaries, d => d.Source == WindowThemes.DarkControlsUri);
            Assert.Null(keepOpen.Style);
            Assert.Equal(Colors.White, ColorOf(folder.Background));
          } finally {
            window.Close();
          }
        });
      }
    }

    [Fact]
    public void A_window_opened_while_Dark_is_dark_from_the_start() {
      InApp(() => {
        WindowThemes.Apply(WindowThemeKind.Dark);
        ErrorWindow window = ShowOffScreen(new ErrorWindow(new InvalidOperationException("Test error")));
        try {
          Assert.Equal("#FF202020", Hex(window.Background));
          Assert.Equal("#FF171717", Hex(Named<Border>(window, "ButtonBar").Background));
          Assert.Single(window.Resources.MergedDictionaries, d => d.Source == WindowThemes.DarkControlsUri);
        } finally {
          window.Close();
        }
      });
    }

    [Fact]
    public void Every_link_in_About_has_the_theme_link_colour() {
      InApp(() => {
        foreach (WindowThemeKind kind in new[] { WindowThemeKind.Light, WindowThemeKind.Dark }) {
          WindowThemes.Apply(kind);
          AboutWindow window = ShowOffScreen(new AboutWindow());
          try {
            List<System.Windows.Documents.Hyperlink> links = WpfAppHost.VisualDescendants<TextBlock>(window)
              .SelectMany(t => t.Inlines.OfType<System.Windows.Documents.Hyperlink>()).ToList();
            links.AddRange(new[] { "HyperlinkProject", "HyperlinkRate", "HyperlinkShare", "HyperlinkSupport" }.Select(n => Named<System.Windows.Documents.Hyperlink>(window, n)));
            Assert.True(links.Count > 20, "Expected the links of the third-party list too");
            Color expected = ColorOf(Application.Current.FindResource("Theme.Link"));
            Assert.All(links, l => Assert.Equal(expected, ColorOf(l.Foreground)));
          } finally {
            window.Close();
          }
        }
      });
    }

    [Fact]
    public void Every_themed_window_is_marked_and_panels_and_notes_are_not() {
      using (TempDb db = new TempDb()) {
        InApp(() => {
          DesktopWindowViewModel w = new AppViewModel().AddWindow("TEST", 100, 100);
          RunCommandViewModel run = new RunCommandViewModel(RunCommandWindowTests.NewCommand(), false);
          Window[] themed = {
            new SettingsWindow(w), new ColorsWindow(w), new RunCommandWindow(run), new RunCommandExamplesWindow(run), new AboutWindow(),
            new ErrorWindow(new InvalidOperationException("Test error")), new CommandErrorWindow("Title", "Error", "Detail"),
            new ReviewWindow(new ReviewPrompt(new FakeClock(), new ScreenSetupTests.FakeTimer(), () => false, p => { }, new Common.StoreActions(null), false)),
          };
          foreach (Window window in themed) {
            Assert.True(WindowThemes.GetIsThemed(window), window.GetType().Name + " does not follow the theme");
          }
          Assert.False(WindowThemes.GetIsThemed(new DesktopWindowView(w)));
        });
      }
    }

    [Fact]
    public void Panels_and_notes_keep_their_own_colours_in_Dark() {
      using (TempDb db = new TempDb()) {
        InApp(() => {
          DesktopWindowViewModel w = new AppViewModel().AddWindow("TEST", 100, 100);
          DesktopWindowView panel = ShowOffScreen(new DesktopWindowView(w));
          Action<ShortcutViewModel> oldNoteEditor = ShortcutViewModel.OpenNoteEditor;
          ShortcutViewModel.OpenNoteEditor = s => { };
          try {
            w.NewNoteShortcutCommand.Execute(null);
          } finally {
            ShortcutViewModel.OpenNoteEditor = oldNoteEditor;
          }
          TextEditWindow note = ShowOffScreen(TextEditWindow.ForNote(w.RootShortcuts.Last()));
          try {
            Border header = Named<Border>(panel, "BorderHeaderRoundedCorners");
            string headerBefore = Hex(header.Background), borderBefore = Hex(header.BorderBrush);
            string noteBefore = Hex(note.Background), noteBodyBefore = Hex(Named<Grid>(note, "Body").Background);
            string noteTitleBefore = Hex(Named<Border>(note, "TitleBar").Background);
            Assert.False(WindowThemes.GetIsThemed(note));

            WindowThemes.Apply(WindowThemeKind.Dark);
            WpfAppHost.Pump();
            Assert.Equal(headerBefore, Hex(header.Background));
            Assert.Equal(borderBefore, Hex(header.BorderBrush));
            Assert.Equal(noteBefore, Hex(note.Background));
            Assert.Equal(noteBodyBefore, Hex(Named<Grid>(note, "Body").Background));
            Assert.Equal(noteTitleBefore, Hex(Named<Border>(note, "TitleBar").Background));
            Assert.DoesNotContain(panel.Resources.MergedDictionaries, d => d.Source == WindowThemes.DarkControlsUri);
            Assert.DoesNotContain(note.Resources.MergedDictionaries, d => d.Source == WindowThemes.DarkControlsUri);
          } finally {
            note.Close();
            panel.Close();
          }
        });
      }
    }

    [Fact]
    public void The_Colours_window_frame_follows_the_theme_but_the_swatches_and_palette_keep_their_real_colours() {
      using (TempDb db = new TempDb()) {
        InApp(() => {
          DesktopWindowViewModel w = new AppViewModel().AddWindow("TEST", 100, 100);
          ColorsWindow window = ShowOffScreen(new ColorsWindow(w));
          try {
            string swatch = Hex(Named<Border>(window, "SwatchTitleBackground").Background);
            string hue = Hex(Named<Border>(window, "SquareHue").Background);
            WindowThemes.Apply(WindowThemeKind.Dark);
            WpfAppHost.Pump();
            Assert.Equal("#FF202020", Hex(window.Background));
            Assert.Equal("#FF2C2C2C", Hex(Named<Border>(window, "PanelPalette").Background));
            Assert.Equal(swatch, Hex(Named<Border>(window, "SwatchTitleBackground").Background));
            Assert.Equal(hue, Hex(Named<Border>(window, "SquareHue").Background));
          } finally {
            window.Close();
          }
        });
      }
    }

    // ---- No hard-coded colours left ----

    private static readonly HashSet<string> NamedColours = new HashSet<string>(
      typeof(Colors).GetProperties(BindingFlags.Public | BindingFlags.Static).Select(p => p.Name).Where(n => n != "Transparent"),
      StringComparer.OrdinalIgnoreCase);

    private static bool IsColourLiteral(string value) {
      string v = value.Trim();
      return (v.StartsWith("#") && v.Length >= 4 && v.Skip(1).All(Uri.IsHexDigit)) || NamedColours.Contains(v);
    }

    // The real colours of the Colours window's palette (the saturation square, the hue rainbow and its knob), and the
    // tooltips' error colour (tooltips keep the Windows look).
    private static readonly string[] AllowedNames = { "SquareSaturationValue", "GridHue", "StylePaletteSlider", "ErrorBrush" };

    private static bool Allowed(XElement element) {
      return element.AncestorsAndSelf().Any(e =>
        AllowedNames.Contains((string)e.Attribute(X + "Name")) || AllowedNames.Contains((string)e.Attribute(X + "Key")));
    }

    private static IEnumerable<string> Literals(IEnumerable<XElement> elements, Func<XElement, bool> allowed) {
      foreach (XElement element in elements) {
        if (allowed(element)) {
          continue;
        }
        foreach (XAttribute attribute in element.Attributes().Where(a => a.Name.Namespace == XNamespace.None && IsColourLiteral(a.Value))) {
          yield return element.Name.LocalName + " " + attribute.Name.LocalName + "=\"" + attribute.Value + "\" (line " + ((System.Xml.IXmlLineInfo)attribute).LineNumber + ")";
        }
      }
    }

    [Fact]
    public void Themed_windows_have_no_hard_coded_colours() {
      List<string> problems = new List<string>();
      foreach (string file in ThemedXamlFiles) {
        XDocument doc = XDocument.Load(Path.Combine(AppFolder(), file), LoadOptions.SetLineInfo);
        problems.AddRange(Literals(doc.Descendants(), Allowed).Select(p => file + ": " + p));
      }
      Assert.True(problems.Count == 0, "Hard-coded colours:" + Environment.NewLine + string.Join(Environment.NewLine, problems));
    }

    [Fact]
    public void Themed_windows_follow_the_theme_and_use_the_theme_brushes() {
      foreach (string file in ThemedXamlFiles.Where(f => f.EndsWith("Window.xaml"))) {
        XDocument doc = XDocument.Load(Path.Combine(AppFolder(), file));
        XElement root = doc.Root;
        Assert.True(root.Attributes().Any(a => a.Name.LocalName == "WindowThemes.IsThemed" && a.Value == "True"), file + " is not marked as themed");
        Assert.Equal("{DynamicResource Theme.WindowBackground}", (string)root.Attribute("Background"));
        Assert.Equal("{DynamicResource Theme.WindowBorder}", (string)root.Attribute("BorderBrush"));
        Assert.Equal("{DynamicResource Theme.Text}", (string)root.Attribute("Foreground"));
        // Theme brushes are looked up live, so open windows change at once.
        Assert.DoesNotContain("{StaticResource Theme.", File.ReadAllText(Path.Combine(AppFolder(), file)));
      }
    }

    [Fact]
    public void Panel_context_menus_use_theme_brushes_except_for_the_mark_colours() {
      XDocument doc = XDocument.Load(Path.Combine(AppFolder(), "DesktopWindowView.xaml"), LoadOptions.SetLineInfo);
      IEnumerable<XElement> menus = doc.Descendants().Where(e => e.Name.LocalName == "ContextMenu").SelectMany(m => m.DescendantsAndSelf());
      // A mark item shows its own colour (its Tag).
      Func<XElement, bool> markColour = e => e.Ancestors().Any(a => a.Name.LocalName == "MenuItem" && IsColourLiteral((string)a.Attribute("Tag") ?? ""))
        || (e.Name.LocalName == "MenuItem" && IsColourLiteral((string)e.Attribute("Tag") ?? ""));
      List<string> problems = Literals(menus, markColour).ToList();
      Assert.True(problems.Count == 0, "Hard-coded colours in panel menus:" + Environment.NewLine + string.Join(Environment.NewLine, problems));
    }

    // ---- The tray menu ----

    // The most opaque pixel of a glyph icon: the glyph's own colour (premultiplied edges are darker).
    private static Color FirstInkPixel(System.Drawing.Image image) {
      System.Drawing.Bitmap bitmap = (System.Drawing.Bitmap)image;
      System.Drawing.Color best = System.Drawing.Color.Transparent;
      for (int y = 0; y < bitmap.Height; y++) {
        for (int x = 0; x < bitmap.Width; x++) {
          System.Drawing.Color c = bitmap.GetPixel(x, y);
          if (c.A > best.A) {
            best = c;
          }
        }
      }
      if (best.A < 20) {
        throw new InvalidOperationException("The icon is empty: " + bitmap.Width + "x" + bitmap.Height + " max alpha " + best.A + " " + bitmap.PixelFormat);
      }
      return Color.FromArgb(255, best.R, best.G, best.B);
    }

    [Fact]
    public void The_tray_menu_is_dark_in_Dark_with_light_text_and_icons_and_back_to_the_Windows_look_in_Light() {
      using (TempDb db = new TempDb())
      using (FakeAppServices services = new FakeAppServices()) {
        InApp(() => {
          using (WinForms.ContextMenuStrip menu = App.CreateTrayMenu(null)) {
            WinForms.ToolStripItem newWindow = menu.Items.Cast<WinForms.ToolStripItem>().Single(i => i.Text == "New window");
            WinForms.ToolStripMenuItem setups = (WinForms.ToolStripMenuItem)menu.Items[1];
            Assert.Equal(WinForms.ToolStripRenderMode.ManagerRenderMode, menu.RenderMode);
            Assert.True(Luminance(FirstInkPixel(newWindow.Image)) < 0.2, "Light: dark icons");

            WindowThemes.Apply(WindowThemeKind.Dark);
            Assert.IsType<DarkTrayMenuRenderer>(menu.Renderer);
            Assert.True(newWindow.ForeColor.GetBrightness() > 0.8f, "Dark: light text");
            Assert.True(setups.DropDownItems[0].ForeColor.GetBrightness() > 0.8f, "Dark: light text in the submenu");
            Assert.IsType<DarkTrayMenuRenderer>(setups.DropDown.Renderer);
            Assert.True(Luminance(FirstInkPixel(newWindow.Image)) > 0.5, "Dark: light icons");

            WindowThemes.Apply(WindowThemeKind.Light);
            Assert.Equal(WinForms.ToolStripRenderMode.ManagerRenderMode, menu.RenderMode);
            Assert.Equal(WinForms.Control.DefaultForeColor, newWindow.ForeColor);
            Assert.True(Luminance(FirstInkPixel(newWindow.Image)) < 0.2, "Light again: dark icons");
          }
        });
      }
    }

    // ---- The Settings window ----

    private sealed class ThemeSetup : IDisposable {
      private readonly WindowThemeSetting old = AppServices.Current.WindowTheme;
      public readonly FakeWindowsAppMode Windows = new FakeWindowsAppMode();
      public readonly WindowThemeSetting Setting;

      public ThemeSetup() {
        Setting = new WindowThemeSetting(Windows);
        AppServices.Current.WindowTheme = Setting;
        WpfAppHost.Run(() => WindowThemes.Follow(Setting));
      }

      public void Dispose() {
        WpfAppHost.Run(() => {
          WindowThemes.Follow(null);
          WindowThemes.Apply(WindowThemeKind.Light);
        });
        AppServices.Current.WindowTheme = old;
      }
    }

    private static void WithSettings(Action<SettingsWindow> check) {
      InApp(() => {
        DesktopWindowViewModel w = new AppViewModel().AddWindow("TEST", 100, 100);
        SettingsWindow settings = ShowOffScreen(new SettingsWindow(w));
        try {
          check(settings);
        } finally {
          settings.Close();
          WpfAppHost.Pump();
        }
      });
    }

    private static void Click(Button button) {
      button.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent, button));
      WpfAppHost.Pump();
    }

    [Fact]
    public void Settings_has_a_Window_theme_section_on_the_Appearance_page_with_Same_as_Windows_chosen_and_the_hint() {
      using (TempDb db = new TempDb())
      using (ThemeSetup t = new ThemeSetup()) {
        WithSettings(window => {
          List<Label> labels = WpfAppHost.VisualDescendants<Label>(window).ToList();
          Assert.Equal(new[] { "Icons appearance", "Window theme", "Other", "Text history", "Keyboard shortcut", "Screen setups", "Backups" }, labels.Select(l => l.Content as string).ToArray());
          Assert.Equal(window.FindResource("StyleSettingsCategory"), labels.Last().Style);
          RadioButton system = Named<RadioButton>(window, "RadioThemeSameAsWindows");
          RadioButton light = Named<RadioButton>(window, "RadioThemeLight");
          RadioButton dark = Named<RadioButton>(window, "RadioThemeDark");
          Assert.True(Named<FrameworkElement>(window, "PageAppearance").IsAncestorOf(dark));
          Assert.Equal(new object[] { "Same as Windows", "Light", "Dark" }, new[] { system.Content, light.Content, dark.Content });
          Assert.True(system.IsChecked);
          Assert.False(light.IsChecked);
          Assert.False(dark.IsChecked);
          TextBlock hint = Named<TextBlock>(window, "TextWindowThemeHint");
          Assert.Equal("For MDSHO's own windows and menus, on this PC. Panels and notes keep their own colours.", hint.Text);
          TextBlock screenHint = Named<TextBlock>(window, "TextScreenSetupsHint");
          Assert.Equal(TextWrapping.Wrap, hint.TextWrapping);
          Assert.Equal(screenHint.Margin, hint.Margin);
          Assert.Equal(screenHint.Foreground.ToString(), hint.Foreground.ToString());
          // Choosing a theme does not change the icon radio buttons.
          dark.IsChecked = true;
          WpfAppHost.Pump();
          Assert.False(system.IsChecked);
          Assert.Single(WpfAppHost.VisualDescendants<RadioButton>(window).Take(2), r => r.IsChecked == true);
        });
      }
    }

    [Fact]
    public void Choosing_Dark_shows_it_at_once_and_Apply_saves_it() {
      using (TempDb db = new TempDb())
      using (ThemeSetup t = new ThemeSetup()) {
        WithSettings(window => {
          Named<RadioButton>(window, "RadioThemeDark").IsChecked = true;
          WpfAppHost.Pump();
          Assert.Equal(WindowThemeKind.Dark, WindowThemes.Current); // before Apply
          Assert.Null(DataStore.AppSettings.Get(WindowThemeSetting.SettingKey));
          Named<RadioButton>(window, "RadioThemeLight").IsChecked = true;
          WpfAppHost.Pump();
          Assert.Equal(WindowThemeKind.Light, WindowThemes.Current);
          Named<RadioButton>(window, "RadioThemeDark").IsChecked = true;
          Click(Named<Button>(window, "ButtonApply"));
          Assert.False(window.IsVisible);
          Assert.Equal(WindowThemeKind.Dark, WindowThemes.Current);
        });
        Assert.Equal(WindowThemeChoice.Dark, t.Setting.Choice);
        Assert.Equal("Dark", DataStore.AppSettings.Get(WindowThemeSetting.SettingKey));
        Assert.Equal(WindowThemeKind.Dark, t.Setting.Current);
      }
    }

    [Fact]
    public void Closing_Settings_without_Apply_goes_back_to_the_saved_theme() {
      using (TempDb db = new TempDb())
      using (ThemeSetup t = new ThemeSetup()) {
        WithSettings(window => {
          Named<RadioButton>(window, "RadioThemeDark").IsChecked = true;
          WpfAppHost.Pump();
          Assert.Equal(WindowThemeKind.Dark, WindowThemes.Current);
          window.Close();
          WpfAppHost.Pump();
          Assert.Equal(WindowThemeKind.Light, WindowThemes.Current);
        });
        Assert.Equal(WindowThemeChoice.SameAsWindows, t.Setting.Choice);
        Assert.Null(DataStore.AppSettings.Get(WindowThemeSetting.SettingKey));
      }
    }

    // ---- The Comment window ----

    private static ShortcutViewModel NewCommand(DesktopWindowViewModel w) {
      ShortcutViewModel command = ViewModelFactory.NewProcessCommandShortcut(w.AppViewModel, null, w);
      w.RootShortcuts.Add(command);
      command.Name = "Ping";
      return command;
    }

    [Fact]
    public void The_Comment_window_follows_the_theme_but_the_Note_window_does_not() {
      using (TempDb db = new TempDb()) {
        InApp(() => {
          DesktopWindowViewModel w = new AppViewModel().AddWindow("TEST", 100, 100);
          TextEditWindow comment = ShowOffScreen(TextEditWindow.ForComment(NewCommand(w)));
          try {
            Assert.True(WindowThemes.GetIsThemed(comment));
            TextBox text = Named<TextBox>(comment, "TextComment");
            Action<string> check = theme => {
              ResourceDictionary d = WindowThemes.ActiveDictionary;
              string Key(string key) => Hex(d[key]);
              Assert.Equal(Key("Theme.WindowBackground"), Hex(comment.Background));
              Assert.Equal(Key("Theme.WindowBorder"), Hex(comment.BorderBrush));
              Assert.Equal(Key("Theme.BarBackground"), Hex(Named<Border>(comment, "TitleBar").Background));
              Assert.Equal(Key("Theme.TextStrong"), Hex(Named<TextBlock>(comment, "TextTitle").Foreground));
              Assert.Equal(Key("Theme.TextStrong"), Hex(Named<TextBlock>(comment, "TextTitleIcon").Foreground));
              Assert.Equal(Key("Theme.TextStrong"), Hex(Named<Button>(comment, "ButtonClose").Foreground));
              Assert.Equal(Key("Theme.WindowBackground"), Hex(Named<Grid>(comment, "Body").Background));
              Assert.Equal(Key("Theme.WindowBackground"), Hex(text.Background));
              Assert.Equal(Key("Theme.Text"), Hex(text.Foreground));
              Assert.Equal(Key("Theme.Text"), Hex(text.CaretBrush));
              Assert.Equal(Key("Theme.TitleButtonHover"), Hex(comment.FindResource("Theme.TitleButtonHover")));
              Assert.Equal(Key("Theme.TitleButtonPressed"), Hex(comment.FindResource("Theme.TitleButtonPressed")));
            };
            // Light is exactly the look the Comment window always had.
            Assert.Equal("#FFF4F3ED", Hex(comment.Background));
            Assert.Equal("#FFDFDACB", Hex(Named<Border>(comment, "TitleBar").Background));
            Assert.Equal("#FF333333", Hex(Named<TextBlock>(comment, "TextTitle").Foreground));
            check("Light");
            WindowThemes.Apply(WindowThemeKind.Dark);
            WpfAppHost.Pump();
            Assert.Equal("#FF202020", Hex(comment.Background));
            check("Dark");
          } finally {
            comment.Close();
          }
        });
      }
    }

    [Fact]
    public void The_Note_window_keeps_black_shades_on_its_title_bar_buttons_in_Dark() {
      using (TempDb db = new TempDb()) {
        InApp(() => {
          DesktopWindowViewModel w = new AppViewModel().AddWindow("TEST", 100, 100);
          Action<ShortcutViewModel> oldNoteEditor = ShortcutViewModel.OpenNoteEditor;
          ShortcutViewModel.OpenNoteEditor = s => { };
          try {
            w.NewNoteShortcutCommand.Execute(null);
          } finally {
            ShortcutViewModel.OpenNoteEditor = oldNoteEditor;
          }
          TextEditWindow note = ShowOffScreen(TextEditWindow.ForNote(w.RootShortcuts.Last()));
          try {
            WindowThemes.Apply(WindowThemeKind.Dark);
            WpfAppHost.Pump();
            Assert.Equal("#1F000000", Hex(note.FindResource("Theme.TitleButtonHover")));
            Assert.Equal("#33000000", Hex(note.FindResource("Theme.TitleButtonPressed")));
          } finally {
            note.Close();
          }
        });
      }
    }

    [Fact]
    public void The_radio_buttons_show_the_saved_choice_and_Same_as_Windows_follows_Windows_after_Apply() {
      using (TempDb db = new TempDb())
      using (ThemeSetup t = new ThemeSetup()) {
        t.Setting.SetChoice(WindowThemeChoice.Light);
        t.Windows.Light = false;
        WithSettings(window => {
          Assert.True(Named<RadioButton>(window, "RadioThemeLight").IsChecked);
          Named<RadioButton>(window, "RadioThemeSameAsWindows").IsChecked = true;
          Click(Named<Button>(window, "ButtonApply"));
          Assert.Equal(WindowThemeKind.Dark, WindowThemes.Current);
        });
        Assert.Equal("SameAsWindows", DataStore.AppSettings.Get(WindowThemeSetting.SettingKey));
      }
    }
  }
}
