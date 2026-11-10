using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using MDSHO.Data;
using MDSHO.ViewModels;
using Xunit;

namespace MDSHO.Tests {

  /// <summary>
  /// The first icon of a shortcut as the real desktop window draws it. Its glyph shows the kind of target (folder, web link,
  /// file, run command, missing target, group) in the window's solid or outline set; its colour is always the window's text
  /// colour and follows it when it changes.
  /// </summary>
  public class ShortcutIconViewTests {

    private sealed class Ctx {
      public DesktopWindowViewModel Vm;
      public DesktopWindowView View;
      public Func<string, TextBlock> IconOf;
      public string DoomedFile;
    }

    /// <summary>
    /// Builds a real desktop window over a temp database with a folder, file, url (.url), web address (.lnk), missing target,
    /// a file that a test may delete, a command and a group, then runs <paramref name="check"/>.
    /// </summary>
    private static void WithWindow(Action<Ctx> check) {
      WpfAppHost.Run(() => {
        using (TempDb db = new TempDb()) {
          var window = DataStore.Windows.LoadAll()[0];
          // A text colour that is neither the old folder yellow nor the old file black, so the checks can tell them apart.
          DataStore.Windows.UpdateTextColor(window.ID, "#FF336699");
          string doomed = Path.Combine(db.Folder, "doomed.txt");
          File.WriteAllText(doomed, "x");
          DataStore.Shortcuts.Create(null, window.ID, "Folder", Path.GetTempPath().TrimEnd('\\'), ".lnk", "22222222222222222222222222222222");
          DataStore.Shortcuts.Create(null, window.ID, "File", Path.GetTempFileName(), ".lnk", "11111111111111111111111111111111");
          DataStore.Shortcuts.Create(null, window.ID, "Url", "https://example.com/", ".url", "33333333333333333333333333333333");
          DataStore.Shortcuts.Create(null, window.ID, "WebLnk", "https://example.org/", ".lnk", "44444444444444444444444444444444");
          DataStore.Shortcuts.Create(null, window.ID, "Missing", Path.Combine(db.Folder, "gone", "nothing.txt"), ".lnk", "55555555555555555555555555555555");
          DataStore.Shortcuts.Create(null, window.ID, "Doomed", doomed, ".lnk", "66666666666666666666666666666666");
          var command = DataStore.Shortcuts.CreateProcessCommand(null, window.ID);
          DataStore.Shortcuts.UpdateName(command.ID, "Command");
          DataStore.Shortcuts.UpdateProcessFileName(command.ID, "cmd.exe");
          var group = DataStore.Shortcuts.CreateGroup(null, window.ID);
          DataStore.Shortcuts.UpdateName(group.ID, "Group");

          AppViewModel appVm = ViewModelFactory.CreateAppViewModel();
          DesktopWindowViewModel windowVm = appVm.Windows[0];
          DesktopWindowView view = new DesktopWindowView(windowVm) { ShowActivated = false };
          try {
            view.Show();
            view.UpdateLayout();
            WpfAppHost.Pump();
            TreeView tree = (TreeView)view.FindName("TreeViewShortcuts");
            Func<string, TextBlock> iconOf = name => {
              WpfAppHost.Pump();
              TreeViewItem item = WpfAppHost.VisualDescendants<TreeViewItem>(tree)
                .Single(i => i.DataContext is ShortcutViewModel s && s.Name == name);
              // The first icon: the visible 16px glyph in column 0 that belongs to this shortcut (not to a child).
              return WpfAppHost.VisualDescendants<TextBlock>(item)
                .Single(t => t.DataContext == item.DataContext && Grid.GetColumn(t) == 0 && t.Visibility == Visibility.Visible && t.FontSize == 16);
            };
            check(new Ctx { Vm = windowVm, View = view, IconOf = iconOf, DoomedFile = doomed });
          } finally {
            view.Close();
            WpfAppHost.Pump();
          }
        }
      });
    }

    private static Color ColorOf(TextBlock icon) { return ((SolidColorBrush)icon.Foreground).Color; }
    private static string Glyph(int code) { return ((char)code).ToString(); }
    private static readonly string[] AllNames = { "Folder", "File", "Url", "WebLnk", "Missing", "Command", "Group" };

    // ---- Colour ----

    [Fact]
    public void Folder_shortcut_icon_uses_the_window_text_colour() {
      WithWindow(c => {
        Assert.Equal(ShortcutTargetKind.Folder, ((ShortcutViewModel)c.IconOf("Folder").DataContext).TargetKind);
        Assert.Equal("#FF336699", c.Vm.Appearance.TextColor);
        Assert.Equal(Color.FromRgb(0x33, 0x66, 0x99), ColorOf(c.IconOf("Folder")));
      });
    }

    [Fact]
    public void File_shortcut_icon_uses_the_window_text_colour() {
      WithWindow(c => {
        Assert.Equal(ShortcutTargetKind.File, ((ShortcutViewModel)c.IconOf("File").DataContext).TargetKind);
        Assert.Equal("#FF336699", c.Vm.Appearance.TextColor);
        Assert.Equal(Color.FromRgb(0x33, 0x66, 0x99), ColorOf(c.IconOf("File")));
      });
    }

    [Fact]
    public void Icons_follow_a_text_colour_change() {
      WithWindow(c => {
        // The settings colour picker writes the new colour.
        Color picked = Color.FromRgb(0x12, 0x80, 0x34);
        c.Vm.Appearance.TextColor = "#FF128034";
        foreach (string name in AllNames) {
          Assert.Equal(picked, ColorOf(c.IconOf(name)));
        }
        // Another colour (e.g. an imported or reset appearance).
        Color replaced = Color.FromRgb(0xC0, 0x10, 0x90);
        c.Vm.Appearance.TextColor = "#FFC01090";
        foreach (string name in AllNames) {
          Assert.Equal(replaced, ColorOf(c.IconOf(name)));
        }
      });
    }

    [Fact]
    public void Run_command_icon_uses_the_window_text_colour() {
      WithWindow(c => {
        Assert.Equal(ShortcutTargetKind.Command, ((ShortcutViewModel)c.IconOf("Command").DataContext).TargetKind);
        Assert.Equal(Color.FromRgb(0x33, 0x66, 0x99), ColorOf(c.IconOf("Command")));
        c.Vm.Appearance.TextColor = "#FF8B0000";
        Assert.Equal(Colors.DarkRed, ColorOf(c.IconOf("Command")));
      });
    }

    // ---- Glyph per kind ----

    private static void AssertGlyphs(Ctx c, bool solid) {
      int s = solid ? 0 : 1;
      Assert.Equal(Glyph(0xE944 + s), c.IconOf("Folder").Text);
      Assert.Equal(Glyph(0xE946 + s), c.IconOf("Url").Text);
      Assert.Equal(Glyph(0xE946 + s), c.IconOf("WebLnk").Text);
      Assert.Equal(Glyph(0xE92C + s), c.IconOf("Missing").Text);   // warning
      Assert.Equal(Glyph(0xE916 + s), c.IconOf("File").Text);
      Assert.Equal(Glyph(0xE948 + s), c.IconOf("Command").Text);   // run
      Assert.Equal(Glyph(0xE914 + s), c.IconOf("Group").Text);
    }

    [Fact]
    public void Rendered_icon_glyph_shows_the_kind_of_target() {
      WithWindow(c => AssertGlyphs(c, c.Vm.Appearance.SolidIcons));
    }

    [Fact]
    public void Rendered_glyphs_switch_between_solid_and_outline_with_the_setting() {
      WithWindow(c => {
        c.Vm.Appearance.SolidIcons = true;
        AssertGlyphs(c, true);
        c.Vm.Appearance.SolidIcons = false;
        AssertGlyphs(c, false);
      });
    }

    [Fact]
    public void Target_deleted_after_load_shows_the_warning_glyph_when_the_window_is_activated() {
      WithWindow(c => {
        int s = c.Vm.Appearance.SolidIcons ? 0 : 1;
        Assert.Equal(Glyph(0xE916 + s), c.IconOf("Doomed").Text);
        File.Delete(c.DoomedFile);
        // Coming back to the window (e.g. from Explorer, where the file was deleted) rechecks the targets.
        typeof(Window).GetMethod("OnActivated", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(c.View, new object[] { EventArgs.Empty });
        Assert.Equal(Glyph(0xE92C + s), c.IconOf("Doomed").Text);
      });
    }
  }
}
