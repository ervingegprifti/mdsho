using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Navigation;
using MDSHO.Common;
using MDSHO.Converters;
using MDSHO.Helpers;
using MDSHO.ViewModels;
using Xunit;

namespace MDSHO.Tests {

  /// <summary>
  /// The helpers that used to be copied in several places and now live in one: package identity, shell launching
  /// (links, Explorer, restart), brush transparency, centring a new window and the path of a shortcut's file.
  /// </summary>
  public class SharedHelpersTests : IDisposable {
    private readonly FakeAppServices services = new FakeAppServices();
    private List<ProcessStartInfo> started => services.Started;

    public void Dispose() {
      services.Dispose();
    }

    #region SHELL LAUNCHER
    [Fact]
    public void Browse_app_data_opens_Explorer_through_the_shell_launcher() {
      using (TempDb db = new TempDb(create: false)) {
        AppServices.Current.BrowseAppData();
        ProcessStartInfo psi = Assert.Single(started);
        Assert.Equal("explorer.exe", psi.FileName);
        Assert.Equal("\"" + db.Folder + "\"", psi.Arguments);
      }
    }

    [Fact]
    public void Store_actions_open_the_browser_through_the_shell_launcher() {
      new StoreActionsHost(AppServices.Current.Clipboard, AppServices.Current.Shell, AppServices.Current.UriLauncher).OpenInBrowser("https://example.org/");
      ProcessStartInfo psi = Assert.Single(started);
      Assert.Equal("https://example.org/", psi.FileName);
      Assert.True(psi.UseShellExecute);
    }

    [Fact]
    public void About_window_links_open_through_the_shell_launcher() {
      WpfAppHost.Run(() => {
        AboutWindow window = new AboutWindow {
          WindowStartupLocation = WindowStartupLocation.Manual,
          Left = -10000,
          Top = -10000,
          ShowActivated = false,
          ShowInTaskbar = false
        };
        window.Show();
        try {
          WpfAppHost.Pump();
          Hyperlink link = (Hyperlink)window.FindName("HyperlinkProject");
          RequestNavigateEventArgs args = new RequestNavigateEventArgs(link.NavigateUri, null) { RoutedEvent = Hyperlink.RequestNavigateEvent };
          link.RaiseEvent(args);
          Assert.True(args.Handled);
        } finally {
          window.Close();
        }
      });
      ProcessStartInfo psi = Assert.Single(started);
      Assert.Equal(new Uri(AppConstants.ProjectUrl).AbsoluteUri, psi.FileName);
      Assert.True(psi.UseShellExecute);
    }
    #endregion SHELL LAUNCHER

    #region BRUSH TRANSPARENCY
    [Fact]
    public void WithAlpha_keeps_the_colour_and_replaces_the_alpha() {
      SolidColorBrush brush = new SolidColorBrush(Color.FromArgb(0xFF, 0x12, 0x34, 0x56));
      SolidColorBrush result = brush.WithAlpha(60);
      Assert.Equal(Color.FromArgb(60, 0x12, 0x34, 0x56), result.Color);
      Assert.NotSame(brush, result);
      Assert.Equal(0xFF, brush.Color.A);
    }

    [Fact]
    public void Shortcut_hover_and_selected_backgrounds_are_the_text_colour_made_transparent() {
      string text = "#FFAABBCC";
      SolidColorBrush hover = (SolidColorBrush)new HOverShortcutBackgroundConverter().Convert(text, typeof(Brush), null, CultureInfo.InvariantCulture);
      Assert.Equal(Color.FromArgb(60, 0xAA, 0xBB, 0xCC), hover.Color);
      Assert.Null(new HOverShortcutBackgroundConverter().Convert(null, typeof(Brush), null, CultureInfo.InvariantCulture));

      ShortcutBackgroundConverter background = new ShortcutBackgroundConverter();
      SolidColorBrush selected = (SolidColorBrush)background.Convert(new object[] { text, true }, typeof(Brush), null, CultureInfo.InvariantCulture);
      Assert.Equal(Color.FromArgb(40, 0xAA, 0xBB, 0xCC), selected.Color);
      SolidColorBrush notSelected = (SolidColorBrush)background.Convert(new object[] { text, false }, typeof(Brush), null, CultureInfo.InvariantCulture);
      Assert.Equal(Colors.Transparent, notSelected.Color);
      Assert.Null(background.Convert(new object[] { text }, typeof(Brush), null, CultureInfo.InvariantCulture));
    }
    #endregion BRUSH TRANSPARENCY

    #region NEW WINDOW POSITION
    [Fact]
    public void A_new_window_is_centred_on_the_screen_at_its_initial_size() {
      // Secondary 3000x1800 physical at 150% (= 2000x1200 DIPs), left of the primary.
      ScreenInfo screen = new ScreenInfo("\\\\.\\DISPLAY2", new ScreenRect(-3000, 0, 3000, 1800), new ScreenRect(-3000, 0, 3000, 1740), 1.5, false);
      ScreenPoint expected = ScreenGeometry.CenterOnScreen(screen, AppConstants.InitialWindowWidth, AppConstants.InitialWindowHeight);
      Assert.Equal(expected, ScreenGeometry.CenterNewWindow(screen));
    }
    #endregion NEW WINDOW POSITION

    #region SHORTCUT FILE PATH
    [Fact]
    public void Shortcut_file_path_is_the_guid_and_extension_in_the_shortcuts_folder() {
      using (TempDb db = new TempDb(create: false)) {
        string path = Paths.GetShortcutFilePath("0123abcd", ".lnk");
        Assert.Equal(Path.Combine(Paths.ShortcutsPathOverride, "0123abcd.lnk"), path);
        Assert.True(Directory.Exists(Paths.ShortcutsPathOverride));
      }
    }
    #endregion SHORTCUT FILE PATH

    #region NEW SHORTCUT PARENT CHECK
    [Fact]
    public void New_shortcuts_need_exactly_one_parent() {
      using (TempDb db = new TempDb()) {
        AppViewModel app = new AppViewModel();
        DesktopWindowViewModel win = app.AddWindow("TEST", 0, 0);
        ShortcutViewModel group = ViewModelFactory.NewGroupShortcut(app, null, win);
        win.RootShortcuts.Add(group);
        List<Action<ShortcutViewModel, DesktopWindowViewModel>> creators = new List<Action<ShortcutViewModel, DesktopWindowViewModel>> {
          (p, w) => ViewModelFactory.NewGroupShortcut(app, p, w),
          (p, w) => ViewModelFactory.NewShortcut(app, p, w, "n", "t", ".lnk", Identifiers.NewGuid()),
          (p, w) => ViewModelFactory.NewProcessCommandShortcut(app, p, w),
          (p, w) => ViewModelFactory.CreateShortcutCopy(group, p, w),
        };
        long before = db.Scalar("SELECT COUNT(*) FROM Shortcuts");
        foreach (Action<ShortcutViewModel, DesktopWindowViewModel> create in creators) {
          Exception none = Assert.Throws<Exception>(() => create(null, null));
          Assert.Equal("Both parentShortcut and parentWindow cannot be null.", none.Message);
          Exception both = Assert.Throws<Exception>(() => create(group, win));
          Assert.Equal("Both parentShortcut and parentWindow cannot be NOT null.", both.Message);
        }
        Assert.Equal(before, db.Scalar("SELECT COUNT(*) FROM Shortcuts"));
      }
    }
    #endregion NEW SHORTCUT PARENT CHECK
  }
}
