using System;
using MDSHO.Common;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using MDSHO.Core;
using MDSHO.Data;
using MDSHO.ViewModels;
using Xunit;

namespace MDSHO.Tests {

  /// <summary>
  /// Tray menu, window title menu and About window after the menu clean-up: no "MDSHO" in the labels, Rate / Share in the
  /// title menu and the About window (with icons), and "Browse app data" in both menus. Explorer is never really opened.
  /// </summary>
  public class AppMenuTests {

    /// <summary>Replaces the Explorer seam with a recorder for the duration of the test.</summary>
    private sealed class OpenedFolders : IDisposable {
      private readonly FakeAppServices services = new FakeAppServices();
      public List<string> Paths => services.OpenedFolders;
      public void Dispose() {
        services.Dispose();
      }
    }

    private static void OnStaThread(Action body) {
      Exception failure = null;
      Thread thread = new Thread(() => {
        try {
          body();
        } catch (Exception ex) {
          failure = ex;
        }
      });
      thread.SetApartmentState(ApartmentState.STA);
      thread.Start();
      thread.Join();
      if (failure != null) {
        throw new Exception("STA body failed: " + failure, failure);
      }
    }

    private static List<string> TrayTexts() {
      List<string> texts = null;
      OnStaThread(() => {
        using (System.Windows.Forms.ContextMenuStrip menu = App.CreateTrayMenu(null)) {
          texts = menu.Items.Cast<System.Windows.Forms.ToolStripItem>().Select(i => i.Text).ToList();
        }
      });
      return texts;
    }

    [Fact]
    public void Tray_menu_has_no_rate_share_and_no_product_name() {
      Assert.Equal(new[] {
        "New window", "Find MDSHO lost windows", "Find other lost windows", "Start with Windows", "Export data...", "Import data...", "Browse app data", "About", "Exit application"
      }, TrayTexts());
    }

    private sealed class FakeOtherWindows : IOtherWindowsService {
      public List<OtherWindow> Windows { get; } = new List<OtherWindow>();
      public List<IntPtr> Moved { get; } = new List<IntPtr>();
      public IList<OtherWindow> List() { return Windows; }
      public bool MoveTo(OtherWindow window, ScreenRect bounds) { Moved.Add(window.Handle); return true; }
    }

    [Fact]
    public void Tray_find_other_lost_windows_moves_the_lost_windows_and_says_how_many() {
      Action<string, string> oldShow = MDSHO.ViewModels.Dialogs.ShowMessage;
      List<string> shown = new List<string>();
      try {
        using (FakeAppServices services = new FakeAppServices()) {
          FakeOtherWindows windows = new FakeOtherWindows();
          windows.Windows.Add(new OtherWindow((IntPtr)11, "Lost", new ScreenRect(-32000, -32000, 800, 600), false));
          windows.Windows.Add(new OtherWindow((IntPtr)12, "Also lost", new ScreenRect(-32000, -32000, 640, 480), false));
          services.Services.OtherWindows = windows;
          MDSHO.ViewModels.Dialogs.ShowMessage = (message, caption) => shown.Add(caption + "|" + message);
          OnStaThread(() => {
            using (System.Windows.Forms.ContextMenuStrip menu = App.CreateTrayMenu(null)) {
              menu.Items.Cast<System.Windows.Forms.ToolStripItem>().Single(i => i.Text == "Find other lost windows").PerformClick();
            }
          });
          Assert.Equal(new[] { (IntPtr)11, (IntPtr)12 }, windows.Moved);
        }
      } finally {
        MDSHO.ViewModels.Dialogs.ShowMessage = oldShow;
      }
      Assert.Equal(new[] { "Find other lost windows|Moved 2 windows to this screen." }, shown);
    }

    [Fact]
    public void The_app_services_use_the_real_other_windows_service_by_default() {
      Assert.IsType<MDSHO.Infrastructure.OtherWindowsService>(MDSHO.Helpers.AppServices.Defaults().OtherWindows);
    }

    [Fact]
    public void Tray_browse_app_data_opens_the_folder_with_the_database_and_shortcuts() {
      using (TempDb db = new TempDb(false))
      using (OpenedFolders opened = new OpenedFolders()) {
        OnStaThread(() => {
          using (System.Windows.Forms.ContextMenuStrip menu = App.CreateTrayMenu(null)) {
            System.Windows.Forms.ToolStripItem browse = menu.Items.Cast<System.Windows.Forms.ToolStripItem>().Single(i => i.Text == "Browse app data");
            browse.PerformClick();
          }
        });
        Assert.Equal(new[] { db.Folder }, opened.Paths);
      }
    }

    /// <summary>Builds a real window off screen over a temp database and gives its title (header) context menu.</summary>
    private static void WithTitleMenu(Action<ContextMenu, DesktopWindowView> check) {
      WpfAppHost.Run(() => {
        using (TempDb db = new TempDb()) {
          AppViewModel appVm = ViewModelFactory.CreateAppViewModel();
          DesktopWindowViewModel windowVm = appVm.Windows[0];
          DesktopWindowView view = new DesktopWindowView(windowVm) { ShowActivated = false, ShowInTaskbar = false };
          view.Left = -10000;
          view.Top = -10000;
          try {
            view.Show();
            view.Left = -10000;
            view.Top = -10000;
            view.UpdateLayout();
            WpfAppHost.Pump();
            ContextMenu menu = ((FrameworkElement)view.FindName("GridHeader")).ContextMenu;
            Assert.NotNull(menu);
            check(menu, view);
          } finally {
            view.Close();
            WpfAppHost.Pump();
          }
        }
      });
    }

    private static string Describe(object item) {
      if (item is Separator) {
        return "-";
      }
      return (string)((MenuItem)item).Header;
    }

    [Fact]
    public void Title_menu_tail_has_browse_rate_share_and_about() {
      WithTitleMenu((menu, view) => {
        List<string> items = menu.Items.Cast<object>().Where(i => !(i is FrameworkElement f) || f.Visibility != Visibility.Collapsed || i is Separator).Select(Describe).ToList();
        int export = items.IndexOf("Export data...");
        Assert.True(export > 0, "Export data... missing: " + string.Join(" | ", items));
        Assert.Equal(new[] { "-", "Export data...", "Import data...", "Browse app data", "-", "Rate...", "Share...", "About", "Exit application" },
          items.Skip(export - 1).ToArray());
        Assert.DoesNotContain(items, text => text.Contains("MDSHO"));
      });
    }

    /// <summary>
    /// With only one window (not a clone) both "Close this clone" and "Delete window" are hidden; the separators around
    /// them must not then show up as two lines next to each other.
    /// </summary>
    [Fact]
    public void Title_menu_with_one_window_has_no_doubled_separator() {
      WithTitleMenu((menu, view) => {
        menu.DataContext = view.DataContext; // what opening the menu does: its items bind to the window
        WpfAppHost.Pump();
        view.UpdateHeaderMenuItems();
        List<string> visible = menu.Items.Cast<object>()
          .Where(i => !(i is FrameworkElement f) || f.Visibility != Visibility.Collapsed)
          .Select(Describe).ToList();
        Assert.DoesNotContain("Delete window", visible);
        Assert.DoesNotContain("Close this clone", visible);
        for (int i = 1; i < visible.Count; i++) {
          Assert.False(visible[i] == "-" && visible[i - 1] == "-", "Two separators in a row: " + string.Join(" | ", visible));
        }
      });
    }

    [Fact]
    public void Footer_button_styles_are_gone() {
      Assert.Null(typeof(DesktopWindowViewModel).GetProperty("FooterMostRightButtonCornerRadius"));
      WithTitleMenu((menu, view) => {
        Assert.Null(view.TryFindResource("StyleFooterButton"));
        Assert.Null(view.TryFindResource("StyleFooterMostRightButton"));
        Assert.Null(view.TryFindResource("StyleFooterMostLeftButton"));
        Assert.Null(view.TryFindResource("StyleFooterButtonPRO"));
      });
    }

    private static MenuItem Item(ContextMenu menu, string name) {
      return menu.Items.OfType<MenuItem>().Single(i => i.Name == name);
    }

    [Theory]
    [InlineData("MenuItemHeaderBrowseAppData", "Appearance.Icons.Folder")]
    [InlineData("MenuItemHeaderRate", "Appearance.Icons.Star")]
    [InlineData("MenuItemHeaderShare", "Appearance.Icons.Share")]
    [InlineData("MenuItemHeaderAboutWindow", "Appearance.Icons.Info")]
    [InlineData("MenuItemHeaderExitApplication", "Appearance.Icons.Times")]
    public void Title_menu_items_have_their_font_icons(string name, string iconPath) {
      WithTitleMenu((menu, view) => {
        TextBlock icon = Assert.IsType<TextBlock>(Item(menu, name).Icon);
        BindingExpression binding = BindingOperations.GetBindingExpression(icon, TextBlock.TextProperty);
        Assert.NotNull(binding);
        Assert.Equal(iconPath, binding.ParentBinding.Path.Path);
        Assert.Equal(Application.Current.FindResource("StyleMenuItemFontIcon"), icon.Style);
      });
    }

    [Fact]
    public void Title_menu_browse_app_data_opens_the_folder_with_the_database_and_shortcuts() {
      using (OpenedFolders opened = new OpenedFolders()) {
        string expected = null;
        WithTitleMenu((menu, view) => {
          expected = System.IO.Path.GetDirectoryName(MDSHO.Common.Paths.GetAppDataLocalDatabaseFilePath());
          Item(menu, "MenuItemHeaderBrowseAppData").RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
        });
        Assert.Equal(new[] { expected }, opened.Paths);
      }
    }

    /// <summary>Shows the About window off screen, runs <paramref name="check"/> on it, then closes it.</summary>
    private static void WithAboutWindow(Action<AboutWindow> check) {
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
          check(window);
        } finally {
          window.Close();
        }
      });
    }

    private static string TextOf(Hyperlink link) {
      return new TextRange(link.ContentStart, link.ContentEnd).Text.Trim();
    }

    [Fact]
    public void About_links_are_rate_and_share_with_solid_icons() {
      WithAboutWindow(window => {
        IconGlyphs solid = new IconGlyphs(true);
        Assert.Equal("Rate", TextOf((Hyperlink)window.FindName("HyperlinkRate")));
        Assert.Equal("Share", TextOf((Hyperlink)window.FindName("HyperlinkShare")));
        TextBlock rateIcon = (TextBlock)window.FindName("TextBlockRateIcon");
        TextBlock shareIcon = (TextBlock)window.FindName("TextBlockShareIcon");
        Assert.Equal(solid.Star, rateIcon.Text);
        Assert.Equal(solid.Share, shareIcon.Text);
        FontFamily iconFont = (FontFamily)Application.Current.FindResource("IconFont");
        Assert.Equal(iconFont, rateIcon.FontFamily);
        Assert.Equal(iconFont, shareIcon.FontFamily);
        // Icon in front of its link: the icon's left edge is left of the link's label.
        DependencyObject rateLabel = (Hyperlink)window.FindName("HyperlinkRate");
        while (rateLabel != null && rateLabel is not Label) {
          rateLabel = LogicalTreeHelper.GetParent(rateLabel);
        }
        Assert.NotNull(rateLabel);
        Assert.True(rateIcon.TranslatePoint(new Point(0, 0), (UIElement)rateLabel).X < 0, "The Rate icon should be in front of the Rate link.");
      });
    }
    /// <summary>
    /// Saves about-rate-share.png (About window, top part) and title-menu.png (the title menu, laid out without opening a
    /// popup). Does nothing unless the MDSHO_SCREENSHOT_DIR environment variable names an existing folder.
    /// </summary>
    [Fact]
    public void Render_pictures_when_asked() {
      string folder = Environment.GetEnvironmentVariable("MDSHO_SCREENSHOT_DIR");
      if (string.IsNullOrEmpty(folder) || !Directory.Exists(folder)) { return; }
      object oldFont = null;
      WpfAppHost.Run(() => {
        // Outside MDSHO.exe the icon font's application pack URI finds nothing; point it at the MDSHO assembly for the pictures only.
        oldFont = Application.Current.Resources["IconFont"];
        Application.Current.Resources["IconFont"] = new FontFamily(new Uri("pack://application:,,,/MDSHO;component/"), "./Assets/Fonts/#MDSHOFont");
      });
      try {
        WithAboutWindow(window => {
          FrameworkElement top = (FrameworkElement)((Hyperlink)window.FindName("HyperlinkRate")).Parent;
          while (top != null && !(top is StackPanel panel && panel.Background == Brushes.White)) {
            top = (FrameworkElement)(LogicalTreeHelper.GetParent(top) ?? VisualTreeHelper.GetParent(top));
          }
          Save(top ?? (FrameworkElement)VisualTreeHelper.GetChild(window, 0), Path.Combine(folder, "about-rate-share.png"));
        });
        WithTitleMenu((menu, view) => {
          menu.DataContext = view.DataContext;
          try {
            // The menu style captured the font when App.xaml loaded; give the icons the picture font directly (this view is thrown away).
            foreach (TextBlock icon in menu.Items.OfType<MenuItem>().Select(i => i.Icon).OfType<TextBlock>()) {
              icon.FontFamily = (FontFamily)Application.Current.Resources["IconFont"];
            }
            menu.ApplyTemplate();
            menu.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            menu.Arrange(new Rect(menu.DesiredSize));
            menu.UpdateLayout();
            WpfAppHost.Pump();
            Save(menu, Path.Combine(folder, "title-menu.png"));
          } finally {
            menu.ClearValue(FrameworkElement.DataContextProperty);
          }
        });
      } finally {
        WpfAppHost.Run(() => Application.Current.Resources["IconFont"] = oldFont);
      }
    }

    private static void Save(FrameworkElement element, string path) {
      element.UpdateLayout();
      double width = element.ActualWidth;
      double height = element.ActualHeight;
      RenderTargetBitmap bitmap = new RenderTargetBitmap((int)Math.Ceiling(width), (int)Math.Ceiling(height), 96, 96, PixelFormats.Pbgra32);
      DrawingVisual visual = new DrawingVisual();
      using (DrawingContext dc = visual.RenderOpen()) {
        dc.DrawRectangle(Brushes.White, null, new Rect(0, 0, width, height));
        dc.DrawRectangle(new VisualBrush(element), null, new Rect(0, 0, width, height));
      }
      bitmap.Render(visual);
      PngBitmapEncoder encoder = new PngBitmapEncoder();
      encoder.Frames.Add(BitmapFrame.Create(bitmap));
      using (FileStream fs = File.Create(path)) { encoder.Save(fs); }
    }
  }
}
