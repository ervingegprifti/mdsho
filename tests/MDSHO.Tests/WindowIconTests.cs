using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using MDSHO.Helpers;
using MDSHO.ViewModels;
using Xunit;

namespace MDSHO.Tests {

  /// <summary>
  /// Window icons (Settings, About, Run Command) and the About menu items use glyphs of the MDSHOFont icon font, drawn by
  /// <see cref="GlyphIcon"/>. The font's pack URI may not resolve in the test host, so the tests check which glyph was
  /// drawn rather than the pixels.
  /// </summary>
  public class WindowIconTests {

    private static readonly IconGlyphs Solid = new IconGlyphs(true);

    private static void OffScreen(Window window) {
      window.WindowStartupLocation = WindowStartupLocation.Manual;
      window.Left = -10000;
      window.Top = -10000;
      window.ShowActivated = false;
      window.ShowInTaskbar = false;
    }

    private static void Shown(Window window, Action check) {
      OffScreen(window);
      window.Show();
      try {
        WpfAppHost.Pump();
        check();
      } finally {
        window.Close();
        WpfAppHost.Pump();
      }
    }

    [Fact]
    public void Info_glyph_is_e92a_solid_and_e92b_outline() {
      Assert.Equal("", new IconGlyphs(true).Info);
      Assert.Equal("", new IconGlyphs(false).Info);
    }

    [Fact]
    public void Info_glyph_follows_the_solid_icons_setting() {
      IconGlyphs icons = new IconGlyphs(true);
      bool raised = false;
      icons.PropertyChanged += (s, e) => raised |= e.PropertyName == nameof(IconGlyphs.Info);
      icons.SolidIcons = false;
      Assert.True(raised);
    }

    [Fact]
    public void Glyph_icon_is_a_square_image_that_remembers_its_glyph() {
      WpfAppHost.Run(() => {
        ImageSource image = GlyphIcon.Create(Solid.Settings);
        Assert.IsType<DrawingImage>(image);
        Assert.True(image.IsFrozen);
        Assert.Equal(image.Width, image.Height, 3);
        Assert.True(image.Width > 0);
        Assert.Equal(Solid.Settings, GlyphIcon.GlyphOf(image));
        Assert.Null(GlyphIcon.GlyphOf(new DrawingImage()));
      });
    }

    [Fact]
    public void Glyph_bitmap_for_winforms_menus_has_the_asked_size_and_remembers_its_glyph() {
      WpfAppHost.Run(() => {
        using (System.Drawing.Bitmap bitmap = GlyphIcon.ToBitmap(Solid.Info, 16)) {
          Assert.Equal(16, bitmap.Width);
          Assert.Equal(16, bitmap.Height);
          Assert.Equal(Solid.Info, GlyphIcon.GlyphOf(bitmap));
        }
      });
    }

    [Theory]
    [InlineData(true, "")]
    [InlineData(false, "")]
    public void Settings_window_icon_is_the_gear_of_its_window_set(bool solid, string glyph) {
      using (TempDb db = new TempDb()) {
        WpfAppHost.Run(() => {
          DesktopWindowViewModel w = new AppViewModel().AddWindow("TEST", 100, 100);
          w.Appearance.SolidIcons = solid;
          SettingsWindow window = new SettingsWindow(w);
          Shown(window, () => Assert.Equal(glyph, GlyphIcon.GlyphOf(window.Icon)));
        });
      }
    }

    [Fact]
    public void Settings_window_icon_changes_when_solid_icons_is_toggled_in_it() {
      using (TempDb db = new TempDb()) {
        WpfAppHost.Run(() => {
          DesktopWindowViewModel w = new AppViewModel().AddWindow("TEST", 100, 100);
          w.Appearance.SolidIcons = true;
          SettingsWindow window = new SettingsWindow(w);
          Shown(window, () => {
            Assert.Equal("", GlyphIcon.GlyphOf(window.Icon));
            RadioButton outlined = WpfAppHost.VisualDescendants<RadioButton>(window).Single(r => (string)r.Content == "Outlined Icons");
            outlined.IsChecked = true;
            WpfAppHost.Pump();
            Assert.False(w.Appearance.SolidIcons);
            Assert.Equal("", GlyphIcon.GlyphOf(window.Icon));
            WpfAppHost.VisualDescendants<RadioButton>(window).Single(r => (string)r.Content == "Solid Icons").IsChecked = true;
            WpfAppHost.Pump();
            Assert.Equal("", GlyphIcon.GlyphOf(window.Icon));
          });
          // Once closed, the window no longer listens to the window's icon set.
          w.Appearance.SolidIcons = false;
          Assert.Equal("", GlyphIcon.GlyphOf(window.Icon));
        });
      }
    }

    [Theory]
    [InlineData(true, "")]
    [InlineData(false, "")]
    public void About_window_icon_is_the_info_glyph_of_the_set_it_is_given(bool solid, string glyph) {
      WpfAppHost.Run(() => {
        AboutWindow window = new AboutWindow();
        window.UseIcons(new IconGlyphs(solid));
        Shown(window, () => Assert.Equal(glyph, GlyphIcon.GlyphOf(window.Icon)));
      });
    }

    [Fact]
    public void About_window_icon_is_solid_until_it_is_given_a_set() {
      WpfAppHost.Run(() => {
        AboutWindow window = new AboutWindow();
        Shown(window, () => Assert.Equal("", GlyphIcon.GlyphOf(window.Icon)));
      });
    }

    [Theory]
    [InlineData(true, "")]
    [InlineData(false, "")]
    public void Run_command_window_icon_is_the_run_glyph_of_its_window_set(bool solid, string glyph) {
      using (TempDb db = new TempDb()) {
        WpfAppHost.Run(() => {
          AppViewModel app = new AppViewModel();
          DesktopWindowViewModel w = app.AddWindow("TEST", 0, 0);
          w.Appearance.SolidIcons = solid;
          ShortcutViewModel command = ViewModelFactory.NewProcessCommandShortcut(app, null, w);
          w.RootShortcuts.Add(command);
          RunCommandWindow window = new RunCommandWindow(new RunCommandViewModel(command, false));
          Shown(window, () => Assert.Equal(glyph, GlyphIcon.GlyphOf(window.Icon)));
        });
      }
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Tray_and_about_from_the_tray_follow_the_first_window(bool solid) {
      using (TempDb db = new TempDb()) {
        WpfAppHost.Run(() => {
          AppViewModel app = new AppViewModel();
          DesktopWindowViewModel first = app.AddWindow("FIRST", 0, 0);
          DesktopWindowViewModel second = app.AddWindow("SECOND", 0, 0);
          first.Appearance.SolidIcons = solid;
          second.Appearance.SolidIcons = !solid;
          Assert.Same(first.Appearance.Icons, App.FirstWindowIcons(app));
        });
      }
    }

    [Fact]
    public void First_window_icons_are_solid_when_there_is_no_window() {
      Assert.True(App.FirstWindowIcons(null).SolidIcons);
      Assert.True(App.FirstWindowIcons(new AppViewModel()).SolidIcons);
    }

    [Fact]
    public void Title_menu_about_item_has_the_info_icon_like_the_other_items() {
      using (TempDb db = new TempDb()) {
        WpfAppHost.Run(() => {
          AppViewModel app = ViewModelFactory.CreateAppViewModel();
          DesktopWindowViewModel w = app.Windows[0];
          DesktopWindowView view = new DesktopWindowView(w);
          Shown(view, () => {
            ContextMenu menu = ((FrameworkElement)view.FindName("GridHeader")).ContextMenu;
            menu.DataContext = view.DataContext; // what opening the menu does
            WpfAppHost.Pump();
            MenuItem about = menu.Items.OfType<MenuItem>().Single(i => (string)i.Header == "About");
            MenuItem rate = menu.Items.OfType<MenuItem>().Single(i => (string)i.Header == "Rate...");
            TextBlock icon = Assert.IsType<TextBlock>(about.Icon);
            // A closed menu never resolves its bindings, so check the binding like the other title menu icons
            // (AppMenuTests.Title_menu_items_have_their_font_icons); it follows the solid/outline setting like them.
            BindingExpression binding = BindingOperations.GetBindingExpression(icon, TextBlock.TextProperty);
            Assert.NotNull(binding);
            Assert.Equal("Appearance.Icons.Info", binding.ParentBinding.Path.Path);
            Assert.Same(((TextBlock)rate.Icon).Style, icon.Style);
            Assert.Equal(Application.Current.FindResource("StyleMenuItemFontIcon"), icon.Style);
          });
        });
      }
    }

    [Theory]
    [InlineData(true, "")]
    [InlineData(false, "")]
    public void Tray_menu_about_item_has_the_info_icon_of_the_first_window_set(bool solid, string glyph) {
      WpfAppHost.Run(() => {
        IconGlyphs icons = new IconGlyphs(solid);
        using (System.Windows.Forms.ContextMenuStrip menu = App.CreateTrayMenu(null, () => icons)) {
          System.Windows.Forms.ToolStripItem about = menu.Items.Cast<System.Windows.Forms.ToolStripItem>().Single(i => i.Text == "About");
          Assert.NotNull(about.Image);
          Assert.Equal(glyph, GlyphIcon.GlyphOf(about.Image));
          // The first window's setting may change while MDSHO runs: the icon is drawn again each time the menu opens.
          icons.SolidIcons = !solid;
          App.UpdateTrayIcons(menu, icons);
          Assert.Equal(icons.Info, GlyphIcon.GlyphOf(about.Image));
        }
      });
    }
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Tray_menu_items_have_the_icons_of_the_first_window_set(bool solid) {
      WpfAppHost.Run(() => {
        IconGlyphs icons = new IconGlyphs(solid);
        using (System.Windows.Forms.ContextMenuStrip menu = App.CreateTrayMenu(null, () => icons)) {
          System.Collections.Generic.Dictionary<string, Func<IconGlyphs, string>> expected = new System.Collections.Generic.Dictionary<string, Func<IconGlyphs, string>> {
            ["New Window"] = g => g.Plus,
            ["Find Windows"] = g => g.Desktop,
            ["Export data..."] = g => g.Send,
            ["Import data..."] = g => g.Database,
            ["Browse app data"] = g => g.Folder,
            ["About"] = g => g.Info,
            ["Exit Application"] = g => g.Times,
          };
          System.Windows.Forms.ToolStripItem Item(string text) => menu.Items.Cast<System.Windows.Forms.ToolStripItem>().Single(i => i.Text == text);
          foreach (var pair in expected) {
            Assert.Equal(pair.Value(icons), GlyphIcon.GlyphOf(Item(pair.Key).Image));
          }
          // Windows draws the check mark where the icon goes, so Start with Windows keeps only its check mark.
          Assert.Null(Item("Start with Windows").Image);
          icons.SolidIcons = !solid;
          App.UpdateTrayIcons(menu, icons);
          foreach (var pair in expected) {
            Assert.Equal(pair.Value(icons), GlyphIcon.GlyphOf(Item(pair.Key).Image));
          }
        }
      });
    }

    [Fact]
    public void Desktop_glyph_is_e92e_solid_and_e92f_outline() {
      Assert.Equal("", new IconGlyphs(true).Desktop);
      Assert.Equal("", new IconGlyphs(false).Desktop);
    }
  }
}
