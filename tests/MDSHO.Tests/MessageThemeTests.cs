using System;
using System.IO;
using System.Windows.Controls;
using System.Windows.Media;
using MDSHO.Common;
using MDSHO.Helpers;
using MDSHO.ViewModels;
using Xunit;

namespace MDSHO.Tests {

  /// <summary>
  /// Every MDSHO dialog follows the Window theme (Same as Windows, Light or Dark), also one that is already open, and also
  /// the ones shown at startup before the data (where the choice is saved) can be read: those follow Windows' app mode.
  /// </summary>
  public class MessageThemeTests {

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

    private static Color ColorOf(object brush) { return ((SolidColorBrush)brush).Color; }

    private static Color ThemeColor(string key) { return ColorOf(WindowThemes.ActiveDictionary[key]); }

    [Fact]
    public void A_dialog_opened_in_dark_uses_the_dark_colours() {
      InApp(() => {
        WindowThemes.Apply(WindowThemeKind.Dark);
        MessageWindow window = new MessageWindow("T", "Heading.", "Text.", MessageIcon.Warning, MessageButtons.YesNo, MessageResult.No);
        MessageWindowTests.Shown(window, () => {
          Assert.True(WindowThemes.GetIsThemed(window));
          Color text = ThemeColor("Theme.Text");
          Assert.Equal(text, ColorOf(((TextBlock)window.FindName("TextHeading")).Foreground));
          Assert.Equal(text, ColorOf(((TextBlock)window.FindName("TextMessage")).Foreground));
          Assert.Equal(text, ColorOf(((TextBlock)window.FindName("TextIcon")).Foreground));
          Assert.Equal(ThemeColor("Theme.WindowBackground"), ColorOf(window.Background));
          Assert.Equal(ThemeColor("Theme.ButtonBarBackground"), ColorOf(((Border)window.FindName("ButtonBar")).Background));
          Assert.NotEqual(Colors.Black, text);
        });
      });
    }

    [Fact]
    public void An_open_dialog_follows_a_theme_change() {
      InApp(() => {
        MessageWindow window = new MessageWindow("T", "Heading.", "", MessageIcon.Info, MessageButtons.OK, MessageResult.None);
        MessageWindowTests.Shown(window, () => {
          TextBlock heading = (TextBlock)window.FindName("TextHeading");
          Assert.Equal(Colors.Black, ColorOf(heading.Foreground));
          WindowThemes.Apply(WindowThemeKind.Dark);
          WpfAppHost.Pump();
          Assert.Equal(ThemeColor("Theme.Text"), ColorOf(heading.Foreground));
          Assert.Equal(ThemeColor("Theme.WindowBackground"), ColorOf(window.Background));
        });
      });
    }

    [Fact]
    public void The_error_windows_follow_the_theme_too() {
      InApp(() => {
        WindowThemes.Apply(WindowThemeKind.Dark);
        foreach (MessageWindow window in new MessageWindow[] {
            new ErrorWindow(new InvalidOperationException("x")), new CommandErrorWindow("T", "E.", "D."), new PasteMessageWindow("T", "H.", "X.") }) {
          MessageWindowTests.Shown(window, () =>
            Assert.Equal(ThemeColor("Theme.Text"), ColorOf(((TextBlock)window.FindName("TextHeading")).Foreground)));
        }
      });
    }

    [Theory]
    [InlineData(true, WindowThemeKind.Light)]
    [InlineData(false, WindowThemeKind.Dark)]
    public void Before_the_data_opens_dialogs_follow_windows_app_mode(bool windowsLight, WindowThemeKind expected) {
      InApp(() => {
        App.ApplyThemeBeforeData(new FakeWindowsAppMode { Light = windowsLight });
        Assert.Equal(expected, WindowThemes.Current);
        MessageWindow window = new MessageWindow("MDSHO already running", "Running.", "", MessageIcon.Info, MessageButtons.OK, MessageResult.None);
        MessageWindowTests.Shown(window, () =>
          Assert.Equal(ThemeColor("Theme.Text"), ColorOf(((TextBlock)window.FindName("TextHeading")).Foreground)));
      });
    }

    [Fact]
    public void Startup_applies_that_theme_before_the_first_dialog() {
      string app = File.ReadAllText(Path.Combine(ThirdPartyNoticesTests.RepoRoot(), "src", "MDSHO.App", "App.xaml.cs"));
      int early = app.IndexOf("ApplyThemeBeforeData(new Infrastructure.WindowsAppMode())", StringComparison.Ordinal);
      Assert.True(early > 0, "OnStartup does not apply the theme before the data opens.");
      Assert.True(early < app.IndexOf("AppMessages.AlreadyRunning();", StringComparison.Ordinal));
      Assert.True(early < app.IndexOf("if (!DatabaseUpdate())", StringComparison.Ordinal));
    }
  }
}
