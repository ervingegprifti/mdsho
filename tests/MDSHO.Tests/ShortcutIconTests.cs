using System;
using System.Collections.Generic;
using MDSHO.ViewModels;
using Xunit;

namespace MDSHO.Tests {

  /// <summary>
  /// What a shortcut points at (folder, file, web link, missing target, command, group), detected from its target without
  /// freezing the UI, and the icon glyph each kind shows. Colours are not involved: every shortcut icon uses the window
  /// text colour (see ShortcutIconColorViewTests).
  /// </summary>
  public class ShortcutIconTests : IDisposable {
    private readonly Func<string, bool> oldExists = ShortcutViewModel.DirectoryExists;
    private readonly Func<string, bool> oldFileExists = ShortcutViewModel.FileExists;
    private readonly Action<Action> oldRunner = ShortcutViewModel.BackgroundRunner;
    private readonly Func<string, bool> oldSlow = ShortcutViewModel.IsSlowPath;
    private readonly List<string> checkedPaths = new List<string>();
    private readonly HashSet<string> folders = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> files = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    private readonly List<Action> queued = new List<Action>();

    public ShortcutIconTests() {
      ShortcutViewModel.IsSlowPath = p => p.StartsWith(@"\\") || p.StartsWith("Z:") || p.StartsWith("R:");
      ShortcutViewModel.DirectoryExists = p => { checkedPaths.Add(p); return folders.Contains(p); };
      ShortcutViewModel.FileExists = p => { checkedPaths.Add(p); return files.Contains(p); };
      ShortcutViewModel.BackgroundRunner = a => queued.Add(a);
    }
    public void Dispose() {
      ShortcutViewModel.DirectoryExists = oldExists;
      ShortcutViewModel.FileExists = oldFileExists;
      ShortcutViewModel.BackgroundRunner = oldRunner;
      ShortcutViewModel.IsSlowPath = oldSlow;
    }

    private static ShortcutViewModel Make(string target, string ext = ".lnk", bool group = false, bool command = false) {
      return new ShortcutViewModel(null, null, 1, null, "n", target, ext, "g", group, false, null, false, null, 0, command, "cmd.exe", "/c");
    }

    // ---- Target kind ----

    [Fact]
    public void Folder_target_is_folder_kind() {
      folders.Add(@"C:\Work");
      ShortcutViewModel s = Make(@"C:\Work");
      Assert.Equal(ShortcutTargetKind.Folder, s.TargetKind);
    }

    [Fact]
    public void File_target_is_file_kind() {
      files.Add(@"C:\Work\a.txt");
      ShortcutViewModel s = Make(@"C:\Work\a.txt");
      Assert.Equal(ShortcutTargetKind.File, s.TargetKind);
    }

    [Fact]
    public void Target_that_is_neither_file_nor_folder_is_missing() {
      Assert.Equal(ShortcutTargetKind.Missing, Make(@"C:\nope\nothing").TargetKind);
    }

    [Fact]
    public void Trailing_separator_is_folder_when_it_exists_and_missing_otherwise() {
      folders.Add(@"C:\There\");
      Assert.Equal(ShortcutTargetKind.Folder, Make(@"C:\There\").TargetKind);
      Assert.Equal(ShortcutTargetKind.Missing, Make(@"C:\Nowhere\").TargetKind);
    }

    [Fact]
    public void Environment_variables_are_expanded_before_checking() {
      string expanded = Environment.ExpandEnvironmentVariables(@"%USERPROFILE%\Docs");
      folders.Add(expanded);
      Assert.Equal(ShortcutTargetKind.Folder, Make(@"%USERPROFILE%\Docs").TargetKind);
      Assert.Contains(expanded, checkedPaths);
    }

    [Fact]
    public void Command_shortcut_is_command_kind_without_check() {
      ShortcutViewModel s = Make("whatever", command: true);
      Assert.Equal(ShortcutTargetKind.Command, s.TargetKind);
      Assert.Empty(checkedPaths);
    }

    [Fact]
    public void Group_is_group_kind() {
      ShortcutViewModel s = Make(null, group: true);
      Assert.Equal(ShortcutTargetKind.Group, s.TargetKind);
      Assert.Empty(checkedPaths);
    }

    [Fact]
    public void Url_shortcut_is_web_link_without_check() {
      ShortcutViewModel s = Make("http://x.com", ".url");
      Assert.Equal(ShortcutTargetKind.WebLink, s.TargetKind);
      Assert.Empty(checkedPaths);
    }

    [Theory]
    [InlineData("https://example.com/page")]
    [InlineData("http://intranet/start")]
    [InlineData("ftp://files.example.com/")]
    public void Lnk_with_a_web_address_target_is_web_link_without_check(string url) {
      Assert.Equal(ShortcutTargetKind.WebLink, Make(url).TargetKind);
      Assert.Empty(checkedPaths);
    }

    [Fact]
    public void Target_change_updates_kind_and_raises_PropertyChanged() {
      folders.Add(@"C:\Work");
      files.Add(@"C:\file.txt");
      ShortcutViewModel s = Make(@"C:\file.txt");
      var raised = new List<string>();
      s.PropertyChanged += (o, e) => raised.Add(e.PropertyName);
      s.Target = @"C:\Work";
      Assert.Equal(ShortcutTargetKind.Folder, s.TargetKind);
      Assert.Contains(nameof(ShortcutViewModel.TargetKind), raised);
    }

    // ---- Target deleted (or restored) after loading ----

    [Fact]
    public void Refresh_finds_a_target_deleted_after_load() {
      folders.Add(@"C:\Work");
      ShortcutViewModel s = Make(@"C:\Work");
      Assert.Equal(ShortcutTargetKind.Folder, s.TargetKind);
      var raised = new List<string>();
      s.PropertyChanged += (o, e) => raised.Add(e.PropertyName);
      folders.Remove(@"C:\Work");
      s.RefreshTargetKind();
      Assert.Equal(ShortcutTargetKind.Missing, s.TargetKind);
      Assert.Contains(nameof(ShortcutViewModel.TargetKind), raised);
      // ...and back when it is restored.
      folders.Add(@"C:\Work");
      s.RefreshTargetKind();
      Assert.Equal(ShortcutTargetKind.Folder, s.TargetKind);
    }

    [Fact]
    public void Refresh_does_not_raise_PropertyChanged_when_nothing_changed() {
      files.Add(@"C:\a.txt");
      ShortcutViewModel s = Make(@"C:\a.txt");
      var raised = new List<string>();
      s.PropertyChanged += (o, e) => raised.Add(e.PropertyName);
      s.RefreshTargetKind();
      Assert.DoesNotContain(nameof(ShortcutViewModel.TargetKind), raised);
    }

    [Fact]
    public void Running_a_shortcut_rechecks_its_target() {
      files.Add(@"C:\a.txt");
      ShortcutViewModel s = Make(@"C:\a.txt");
      s.StartProcessAction = (shortcut, admin) => { };
      files.Remove(@"C:\a.txt");
      s.RunShortcut(false);
      Assert.Equal(ShortcutTargetKind.Missing, s.TargetKind);
    }

    [Fact]
    public void Refresh_does_not_queue_a_second_slow_check_while_one_is_running() {
      ShortcutViewModel s = Make(@"\\srv\share\dir");
      Assert.Single(queued);
      s.RefreshTargetKind();
      s.RefreshTargetKind();
      Assert.Single(queued);
      queued[0]();
      s.RefreshTargetKind();
      Assert.Equal(2, queued.Count);
    }

    // ---- Slow paths (network, removable, optical) ----

    [Fact]
    public void Unc_path_is_not_checked_on_calling_thread_and_updates_when_done() {
      folders.Add(@"\\srv\share\dir");
      ShortcutViewModel s = Make(@"\\srv\share\dir");
      Assert.Empty(checkedPaths);
      Assert.Equal(ShortcutTargetKind.File, s.TargetKind);
      var raised = new List<string>();
      s.PropertyChanged += (o, e) => raised.Add(e.PropertyName);
      Assert.Single(queued);
      queued[0]();
      Assert.Equal(ShortcutTargetKind.Folder, s.TargetKind);
      Assert.Contains(nameof(ShortcutViewModel.TargetKind), raised);
    }

    [Fact]
    public void Missing_unc_target_shows_as_file_until_the_background_check_says_missing() {
      ShortcutViewModel s = Make(@"\\srv\share\gone.txt");
      Assert.Empty(checkedPaths);
      Assert.Equal(ShortcutTargetKind.File, s.TargetKind);
      queued[0]();
      Assert.Equal(ShortcutTargetKind.Missing, s.TargetKind);
    }

    [Theory]
    [InlineData(@"Z:\Share\dir")]   // mapped network drive
    [InlineData(@"R:\media")]       // removable / CD drive
    public void Slow_drive_path_is_checked_in_background(string path) {
      folders.Add(path);
      ShortcutViewModel s = Make(path);
      Assert.Empty(checkedPaths);
      Assert.Equal(ShortcutTargetKind.File, s.TargetKind);
      Assert.Single(queued);
      queued[0]();
      Assert.Equal(ShortcutTargetKind.Folder, s.TargetKind);
    }

    [Fact]
    public void Fixed_local_path_is_checked_synchronously() {
      folders.Add(@"C:\Work");
      Make(@"C:\Work");
      Assert.Contains(@"C:\Work", checkedPaths);
      Assert.Empty(queued);
    }

    [Fact]
    public void Default_IsSlowPath_flags_unc_and_treats_errors_as_slow() {
      Func<string, bool> real = oldSlow;
      Assert.True(real(@"\\srv\share\x"));
      Assert.True(real("|:\\bad"));
    }

    [Fact]
    public void Stale_unc_result_is_ignored_after_target_changes() {
      folders.Add(@"\\srv\share\dir");
      files.Add(@"C:\file.txt");
      ShortcutViewModel s = Make(@"\\srv\share\dir");
      s.Target = @"C:\file.txt";
      queued[0]();
      Assert.Equal(ShortcutTargetKind.File, s.TargetKind);
    }

    // ---- Glyph per kind (MDSHOFont v1.14) ----

    [Theory]
    [InlineData(ShortcutTargetKind.Folder, true, 0xE944)]
    [InlineData(ShortcutTargetKind.Folder, false, 0xE945)]
    [InlineData(ShortcutTargetKind.WebLink, true, 0xE946)]
    [InlineData(ShortcutTargetKind.WebLink, false, 0xE947)]
    [InlineData(ShortcutTargetKind.Missing, true, 0xE92C)]   // warning_s
    [InlineData(ShortcutTargetKind.Missing, false, 0xE92D)]  // warning_o
    [InlineData(ShortcutTargetKind.File, true, 0xE916)]      // shortcut_s, as before
    [InlineData(ShortcutTargetKind.File, false, 0xE917)]
    [InlineData(ShortcutTargetKind.Command, true, 0xE948)]   // run_s
    [InlineData(ShortcutTargetKind.Command, false, 0xE949)]  // run_o
    [InlineData(ShortcutTargetKind.Group, true, 0xE914)]     // shortcuts_s, as before
    [InlineData(ShortcutTargetKind.Group, false, 0xE915)]
    public void Each_kind_has_its_glyph_in_the_solid_and_outline_set(ShortcutTargetKind kind, bool solid, int code) {
      Assert.Equal(((char)code).ToString(), new IconGlyphs(solid).ForKind(kind));
    }

    [Fact]
    public void New_glyph_properties_follow_the_solid_icons_setting() {
      IconGlyphs icons = new IconGlyphs(true);
      Assert.Equal("", icons.Folder);
      Assert.Equal("", icons.Globe);
      Assert.Equal("", icons.Warning);
      Assert.Equal("", icons.Run);
      Assert.Equal("", icons.Wizard);
      var raised = new List<string>();
      icons.PropertyChanged += (o, e) => raised.Add(e.PropertyName);
      icons.SolidIcons = false;
      Assert.Equal("", icons.Folder);
      Assert.Equal("", icons.Globe);
      Assert.Equal("", icons.Warning);
      Assert.Equal("", icons.Run);
      Assert.Equal("", icons.Wizard);
      Assert.Contains(nameof(IconGlyphs.Folder), raised);
      Assert.Contains(nameof(IconGlyphs.Globe), raised);
      Assert.Contains(nameof(IconGlyphs.Warning), raised);
      Assert.Contains(nameof(IconGlyphs.Run), raised);
      Assert.Contains(nameof(IconGlyphs.Wizard), raised);
    }

    /// <summary>Font v1.14 dropped the unknown glyph; U+E948/U+E949 are now the run icon, so the property must be gone.</summary>
    [Fact]
    public void Unknown_glyph_is_gone_because_font_v1_14_reuses_its_codes_for_run() {
      Assert.Null(typeof(IconGlyphs).GetProperty("Unknown"));
    }
  }
}
