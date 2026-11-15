using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using MDSHO.Helpers;
using MDSHO.ViewModels;
using Xunit;

namespace MDSHO.Tests {

  /// <summary>
  /// The Settings layout: one filled button style on the pages, a "..." folder button, a slim Apply bar, even spacing,
  /// page titles that line up with the list, short hints under their own control, and a height that never changes.
  /// </summary>
  public class SettingsTidyTests {

    private static readonly string[] PageButtons = { "ButtonBackupFolderChange", "ButtonBackupFolderOpen", "ButtonBackUpNow", "ButtonShortcutDefault", "ButtonShortcutOff" };
    private static readonly string[] Pages = { "Appearance", "General", "Keyboard", "Screens", "Backups" };

    private sealed class Setup : IDisposable {
      public readonly TempDb Db = new TempDb();
      public readonly FakeAppServices Services = new FakeAppServices();
      public readonly BackupService Backups;

      public Setup() {
        string profile = Path.Combine(Db.Folder, "Profile");
        Backups = new BackupService(new FakeClock(), "HOMEPC", profile, "1.12.2", new ScreenSetupTests.FakeTimer(), a => a());
        Services.Services.Backups = Backups;
      }

      public void Dispose() {
        Services.Dispose();
        Db.Dispose();
      }
    }

    private static SettingsWindow Open(string page = "Backups") {
      DesktopWindowViewModel w = new AppViewModel().AddWindow("TEST", 100, 100);
      SettingsWindow settings = new SettingsWindow(w) {
        WindowStartupLocation = WindowStartupLocation.Manual,
        Left = -10000,
        Top = -10000,
        ShowActivated = false,
        ShowInTaskbar = false
      };
      settings.Show();
      WpfAppHost.Pump();
      Choose(settings, page);
      return settings;
    }

    private static void Choose(Window window, string page) {
      ((ListBox)window.FindName("ListSettingsPages")).Items.Cast<ListBoxItem>().Single(i => (string)i.Content == page).IsSelected = true;
      WpfAppHost.Pump();
    }

    private static void WithSettings(Action<SettingsWindow> check, string page = "Backups") {
      WpfAppHost.Run(() => {
        SettingsWindow settings = Open(page);
        try {
          check(settings);
        } finally {
          settings.Close();
          WindowThemes.Apply(WindowThemeKind.Light);
          WpfAppHost.Pump();
        }
      });
    }

    private static T Named<T>(FrameworkElement root, string name) where T : class {
      return Assert.IsAssignableFrom<T>(root.FindName(name));
    }

    private static string Hex(object brush) {
      return Assert.IsType<SolidColorBrush>(brush).Color.ToString();
    }

    private static Point TopLeft(FrameworkElement element, Visual relativeTo) {
      return element.TranslatePoint(new Point(0, 0), (UIElement)relativeTo);
    }

    [Fact]
    public void The_page_buttons_share_one_filled_style_26_px_high_in_the_button_colour() {
      using (new Setup()) {
        WithSettings(window => {
          Style style = Assert.IsType<Style>(window.FindResource("StyleSettingsButton"));
          Assert.Same(window.FindResource("StyleDarkButton"), style.BasedOn);
          foreach (WindowThemeKind kind in new[] { WindowThemeKind.Light, WindowThemeKind.Dark }) {
            WindowThemes.Apply(kind);
            WpfAppHost.Pump();
            foreach (string name in PageButtons) {
              Button button = Named<Button>(window, name);
              Assert.Same(style, button.Style);
              Assert.Equal(DependencyProperty.UnsetValue, button.ReadLocalValue(Control.BackgroundProperty));
              Assert.Equal(Hex(WindowThemes.ActiveDictionary["Theme.Button"]), Hex(button.Background));
              Assert.Equal(26, button.Height);
            }
          }
          Choose(window, "Backups");
          foreach (string name in new[] { "ButtonBackupFolderChange", "ButtonBackupFolderOpen", "ButtonBackUpNow" }) {
            Assert.Equal(26, Named<Button>(window, name).ActualHeight, 0.5);
          }
          // Apply keeps its own look.
          Assert.Same(window.FindResource("StyleDarkButton"), Named<Button>(window, "ButtonApply").Style);
        });
      }
    }

    [Fact]
    public void Hover_and_press_change_the_page_buttons_colour() {
      using (new Setup()) {
        WithSettings(window => {
          ControlTemplate template = Named<Button>(window, "ButtonBackUpNow").Template;
          Assert.Contains(template.Triggers.OfType<Trigger>(), t => t.Property == UIElement.IsMouseOverProperty);
          Assert.Contains(template.Triggers.OfType<Trigger>(), t => t.Property == System.Windows.Controls.Primitives.ButtonBase.IsPressedProperty);
        });
      }
    }

    [Fact]
    public void No_beige_secondary_buttons_are_left() {
      string xaml = File.ReadAllText(Path.Combine(ThirdPartyNoticesTests.RepoRoot(), "src", "MDSHO.App", "SettingsWindow.xaml"));
      Assert.DoesNotContain("Theme.SecondaryButton", xaml);
    }

    [Fact]
    public void Change_is_a_small_square_dots_button_named_Change_folder() {
      using (new Setup()) {
        WithSettings(window => {
          Button change = Named<Button>(window, "ButtonBackupFolderChange");
          Assert.Equal("...", change.Content);
          Assert.Equal("Change folder", change.ToolTip);
          Assert.Equal("Change folder", AutomationProperties.GetName(change));
          Assert.Equal(32, change.ActualWidth, 0.5);
        });
      }
    }

    [Fact]
    public void The_Apply_bar_is_like_Run_commands_bar() {
      // The same markup: a Footer border on Theme.BarBackground holding FooterContent, and no line above it.
      string root = ThirdPartyNoticesTests.RepoRoot();
      string settingsXaml = File.ReadAllText(Path.Combine(root, "src", "MDSHO.App", "SettingsWindow.xaml"));
      string runXaml = File.ReadAllText(Path.Combine(root, "src", "MDSHO.App", "RunCommandWindow.xaml"));
      System.Text.RegularExpressions.Regex footerTag = new System.Text.RegularExpressions.Regex("<Border x:Name=\"Footer\"[^>]*>");
      string settingsFooter = footerTag.Match(settingsXaml).Value;
      string runFooter = footerTag.Match(runXaml).Value;
      Assert.Contains("Background=\"{DynamicResource Theme.BarBackground}\"", runFooter);
      Assert.Contains("Background=\"{DynamicResource Theme.BarBackground}\"", settingsFooter);
      Assert.DoesNotContain("BorderThickness", settingsFooter);
      // The same margin rule: every outer margin of the page and of the bar is the button height, set in code.
      string runCode = File.ReadAllText(Path.Combine(root, "src", "MDSHO.App", "RunCommandWindow.xaml.cs"));
      string settingsCode = File.ReadAllText(Path.Combine(root, "src", "MDSHO.App", "SettingsWindow.xaml.cs"));
      Assert.Contains("this.ButtonSave.SizeChanged", runCode);
      Assert.Contains("this.FooterContent.Margin = margin;", runCode);
      Assert.Contains("this.ButtonApply.SizeChanged", settingsCode);
      Assert.Contains("this.FooterContent.Margin = margin;", settingsCode);
      using (new Setup()) {
        WithSettings(window => {
          foreach (WindowThemeKind kind in new[] { WindowThemeKind.Light, WindowThemeKind.Dark }) {
            WindowThemes.Apply(kind);
            WpfAppHost.Pump();
            Border footer = Named<Border>(window, "Footer");
            Assert.Equal(Hex(WindowThemes.ActiveDictionary["Theme.BarBackground"]), Hex(footer.Background));
            Assert.Equal(new Thickness(0), footer.BorderThickness);
          }
          Button apply = Named<Button>(window, "ButtonApply");
          double h = apply.ActualHeight;
          Assert.True(h > 10);
          Assert.Equal(new Thickness(22, 5, 22, 5), apply.Padding);
          Assert.Same(window.FindResource("StyleDarkButton"), apply.Style);
          Assert.Equal(HorizontalAlignment.Right, apply.HorizontalAlignment);
          Assert.Equal(new Thickness(h), Named<FrameworkElement>(window, "FooterContent").Margin);
          Assert.Equal(new Thickness(h), Named<FrameworkElement>(window, "PanelBody").Margin);
          Border bar = Named<Border>(window, "Footer");
          Assert.Equal(h + h + h, bar.ActualHeight, 0.5);
        });
      }
    }

    [Fact]
    public void Pages_have_even_padding_and_titles_line_up_with_the_first_list_item() {
      using (new Setup()) {
        WithSettings(window => {
          FrameworkElement body = Named<FrameworkElement>(window, "PanelBody");
          double h = Named<Button>(window, "ButtonApply").ActualHeight;
          Assert.Equal(new Thickness(h), body.Margin);
          // The list starts where the page does, so its first item lines up with the page title.
          Assert.Equal(h, ((ListBox)window.FindName("ListSettingsPages")).Padding.Top, 0.5);
          ListBoxItem first = ((ListBox)window.FindName("ListSettingsPages")).Items.Cast<ListBoxItem>().First();
          foreach (string page in Pages) {
            Choose(window, page);
            Label title = WpfAppHost.VisualDescendants<Label>(Named<FrameworkElement>(window, "Page" + page)).First();
            Assert.Equal(15, title.FontSize);
            Assert.Equal(FontWeights.SemiBold, title.FontWeight);
            Assert.Equal(Hex(WindowThemes.ActiveDictionary["Theme.TextStrong"]), Hex(title.Foreground));
            Assert.Equal(TopLeft(first, window).Y, TopLeft(title, window).Y, 1.0);
          }
        });
      }
    }

    [Fact]
    public void Groups_are_16_px_apart_and_hints_are_small_and_4_px_under_their_control() {
      using (new Setup()) {
        WithSettings(window => {
          // The second heading on Appearance starts a new group.
          Label theme = WpfAppHost.VisualDescendants<Label>(Named<FrameworkElement>(window, "PageAppearance")).ElementAt(1);
          Assert.Equal("Window theme", theme.Content);
          Assert.Equal(16, theme.Margin.Top);
          foreach (string hint in new[] { "TextWindowThemeHint", "TextShortcutHint", "TextScreenSetupsHint", "TextBackupOnHint", "TextBackupFolderHint", "TextBackupRestoreHint" }) {
            TextBlock text = Named<TextBlock>(window, hint);
            Assert.Equal(12, text.FontSize);
            Assert.Equal(4, text.Margin.Top);
            Assert.Equal(TextWrapping.Wrap, text.TextWrapping);
          }
          Assert.Equal(Hex(WindowThemes.ActiveDictionary["Theme.TextSecondary"]), Hex(Named<TextBlock>(window, "TextBackupFolderLabel").Foreground));
          Assert.Equal("Folder", Named<TextBlock>(window, "TextBackupFolderLabel").Text);
        });
      }
    }

    [Fact]
    public void Backups_hints_are_short_and_under_their_own_control() {
      using (new Setup()) {
        WithSettings(window => {
          Assert.Equal("Only when something changed.", Named<TextBlock>(window, "TextBackupOnHint").Text);
          Assert.Equal("Pick a OneDrive or Google Drive folder to keep a copy in the cloud.", Named<TextBlock>(window, "TextBackupFolderHint").Text);
          Assert.Equal("Restore one with Import data.", Named<TextBlock>(window, "TextBackupRestoreHint").Text);
          Assert.Null(window.FindName("TextBackupHint"));
          double Top(string name) => TopLeft(Named<FrameworkElement>(window, name), window).Y;
          double Bottom(string name) => Top(name) + Named<FrameworkElement>(window, name).ActualHeight;
          Assert.Equal(Bottom("CheckBackupOn") + 4, Top("TextBackupOnHint"), 1.0);
          Assert.Equal(Bottom("TextBackupFolder") + 4, Top("TextBackupFolderHint"), 1.5);
          Assert.True(Top("TextBackupFolderHint") < Top("ComboBackupKeep"));
          // The bottom, top to bottom: keep count, Last backup, the hint, then Back up now on its own line.
          Assert.True(Top("ComboBackupKeep") < Top("TextLastBackup"));
          Assert.True(Top("TextLastBackup") < Top("TextBackupRestoreHint"));
          Assert.True(Top("TextBackupRestoreHint") < Top("ButtonBackUpNow"));
          Assert.Equal(Bottom("TextLastBackup") + 4, Top("TextBackupRestoreHint"), 1.0);
          Assert.Equal(Bottom("TextBackupRestoreHint") + 16, Top("ButtonBackUpNow"), 1.0);
          Assert.Equal(Bottom("TextBackupFolderHint") + 16, Top("PanelBackupKeep"), 1.0);
          Assert.Equal(Bottom("PanelBackupKeep") + 16, Top("TextLastBackup"), 1.0);
          // The dropdown first, then its text.
          ComboBox keep = Named<ComboBox>(window, "ComboBackupKeep");
          TextBlock keepText = Named<TextBlock>(window, "TextBackupKeep");
          Assert.Equal("latest backups of this PC to keep", keepText.Text);
          Assert.Equal("Latest backups of this PC to keep", AutomationProperties.GetName(keep));
          Assert.Equal(TopLeft(keep, window).X + keep.ActualWidth + 6, TopLeft(keepText, window).X, 1.0);
          double left = TopLeft(Named<FrameworkElement>(window, "CheckBackupOn"), window).X;
          Assert.Equal(left, TopLeft(keep, window).X, 1.0);
          Assert.Equal(left, TopLeft(Named<FrameworkElement>(window, "TextLastBackup"), window).X, 1.0);
          Assert.Equal(left, TopLeft(Named<FrameworkElement>(window, "ButtonBackUpNow"), window).X, 1.0);
          Assert.Equal(Hex(WindowThemes.ActiveDictionary["Theme.Text"]), Hex(Named<TextBlock>(window, "TextLastBackup").Foreground));
          Assert.False(Named<TextBlock>(window, "TextBackupError").IsVisible);
          Assert.Null(window.FindName("TextKeepTheLast"));
        });
      }
    }

    [Fact]
    public void The_red_line_goes_between_Last_backup_and_the_hint_and_the_window_keeps_its_height() {
      double plain = 0;
      using (new Setup()) {
        WithSettings(window => plain = window.ActualHeight);
      }
      using (Setup s = new Setup()) {
        string blocker = Path.Combine(s.Db.Folder, "a rather long folder name for a test", "blocker.txt");
        Directory.CreateDirectory(Path.GetDirectoryName(blocker));
        File.WriteAllText(blocker, "x");
        s.Backups.Save(true, Path.Combine(blocker, "mdsho-backups"), 10);
        s.Backups.RunIfDue();
        Assert.NotNull(s.Backups.LastErrorText);
        WithSettings(window => {
          TextBlock error = Named<TextBlock>(window, "TextBackupError");
          Assert.True(error.IsVisible);
          Assert.Equal(12, error.FontSize);
          double Top(string name) => TopLeft(Named<FrameworkElement>(window, name), window).Y;
          double Bottom(string name) => Top(name) + Named<FrameworkElement>(window, name).ActualHeight;
          Assert.Equal(Bottom("TextLastBackup") + 4, Top("TextBackupError"), 1.0);
          Assert.True(Named<TextBlock>(window, "TextBackupRestoreHint").IsVisible);
          Assert.Equal(Bottom("TextBackupError") + 4, Top("TextBackupRestoreHint"), 1.0);
          Assert.Equal(Bottom("TextBackupRestoreHint") + 16, Top("ButtonBackUpNow"), 1.0);
          // Two lines at most, with the full text in the tooltip.
          Assert.True(error.ActualHeight <= 33, "The red line is " + error.ActualHeight + " px high.");
          Assert.Equal(error.Text, error.ToolTip);
          Assert.Equal(plain, window.ActualHeight, 0.5);
          foreach (string page in Pages) {
            Choose(window, page);
            Assert.Equal(plain, window.ActualHeight, 0.5);
          }
        });
      }
    }

    [Fact]
    public void The_window_stays_compact() {
      // 481 px: the Backups page is the tallest, with Back up now on its own line and two lines always kept for the red
      // line, inside the Apply-height margins and bar of the other windows. This catches it growing further.
      using (new Setup()) {
        WithSettings(window => Assert.True(window.ActualHeight <= 483, "Settings is " + window.ActualHeight + " px high."));
      }
    }
  }
}
