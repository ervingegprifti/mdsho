using System;
using System.Collections.Generic;
using System.Diagnostics;
using MDSHO.Core;
using MDSHO.Data;
using MDSHO.ViewModels;
using Xunit;

namespace MDSHO.Tests {

  /// <summary>
  /// A group whose name holds one value in braces (e.g. <c>Test {SRV01}</c>) acts as a small profile: run commands directly
  /// inside it get that value in place of every <c>{}</c> in their arguments.
  /// </summary>
  public class GroupValueTests {

    // ---- Reading the value from a group name ----

    [Theory]
    [InlineData("Test {SRV01}", "SRV01")]
    [InlineData("{SRV01}", "SRV01")]
    [InlineData("{SRV01} Test", "SRV01")]
    [InlineData("Test {My Server}", "My Server")]
    [InlineData("A {first} B {second}", "first")]
    [InlineData("Nested {a{b}}", "a{b")]
    [InlineData("Spaces { x }", " x ")]
    [InlineData("Close first } then {X}", "X")]
    public void Value_is_the_text_inside_the_first_brace_pair(string groupName, string expected) {
      Assert.True(GroupValue.TryParse(groupName, out string value));
      Assert.Equal(expected, value);
    }

    [Theory]
    [InlineData("Test")]
    [InlineData("Test {}")]
    [InlineData("{} then {X}")]
    [InlineData("Test {SRV")]
    [InlineData("Test SRV}")]
    [InlineData("")]
    [InlineData(null)]
    public void No_value_without_a_non_empty_first_brace_pair(string groupName) {
      Assert.False(GroupValue.TryParse(groupName, out string value));
      Assert.Null(value);
    }

    // ---- Putting the value into the arguments ----

    [Theory]
    [InlineData("/K ping {}", "SRV01", "/K ping SRV01")]
    [InlineData("{} {} -n {}", "SRV01", "SRV01 SRV01 -n SRV01")]
    [InlineData("ping {}", "My Server", "ping My Server")]
    [InlineData("no token here", "SRV01", "no token here")]
    [InlineData("{ } and {x} stay", "SRV01", "{ } and {x} stay")]
    [InlineData("{{}}", "V", "{V}")]
    [InlineData("", "SRV01", "")]
    public void Every_empty_brace_token_is_replaced_verbatim(string arguments, string value, string expected) {
      Assert.Equal(expected, GroupValue.ResolveArguments(arguments, value));
    }

    [Theory]
    [InlineData("ping {}")]
    [InlineData("")]
    [InlineData(null)]
    public void Without_a_value_the_arguments_stay_unchanged(string arguments) {
      Assert.Equal(arguments, GroupValue.ResolveArguments(arguments, null));
    }

    [Fact]
    public void Null_arguments_stay_null_even_with_a_value() {
      Assert.Null(GroupValue.ResolveArguments(null, "SRV01"));
    }
  }

  /// <summary>Run commands use the value of their direct parent group when they are started and in their tooltip.</summary>
  public class GroupValueViewModelTests : IDisposable {
    private readonly TempDb db = new TempDb();
    private readonly Action<ProcessStartInfo> oldStarter = ShortcutViewModel.ProcessStarter;
    private readonly List<ProcessStartInfo> started = new List<ProcessStartInfo>();
    private readonly AppViewModel app = new AppViewModel();
    private readonly DesktopWindowViewModel win;

    public GroupValueViewModelTests() {
      ShortcutViewModel.ProcessStarter = psi => started.Add(psi);
      win = app.AddWindow("TEST", 0, 0);
    }
    public void Dispose() {
      ShortcutViewModel.ProcessStarter = oldStarter;
      db.Dispose();
    }

    private ShortcutViewModel Group(string name, ShortcutViewModel parent = null) {
      ShortcutViewModel g = ViewModelFactory.NewGroupShortcut(app, parent, parent == null ? win : null);
      (parent == null ? win.RootShortcuts : parent.Children).Add(g);
      g.Name = name;
      return g;
    }
    private ShortcutViewModel Command(ShortcutViewModel parent, string fileName, string arguments) {
      ShortcutViewModel s = ViewModelFactory.NewProcessCommandShortcut(app, parent, parent == null ? win : null);
      (parent == null ? win.RootShortcuts : parent.Children).Add(s);
      s.ProcessFileName = fileName;
      s.ProcessArguments = arguments;
      return s;
    }

    [Fact]
    public void Started_process_gets_the_group_value_in_its_arguments() {
      ShortcutViewModel cmd = Command(Group("Test {SRV01}"), "cmd.exe", "/K ping {} -n {}");
      cmd.Run();
      ProcessStartInfo psi = Assert.Single(started);
      Assert.Equal("cmd.exe", psi.FileName);
      Assert.Equal("/K ping SRV01 -n SRV01", psi.Arguments);
      Assert.True(psi.UseShellExecute);
      Assert.Equal("", psi.Verb);
    }

    [Fact]
    public void Run_as_administrator_also_gets_the_group_value() {
      ShortcutViewModel cmd = Command(Group("Test {SRV01}"), "cmd.exe", "/K ping {}");
      cmd.ProcessRunAsAdmin = true;
      cmd.Run();
      ProcessStartInfo psi = Assert.Single(started);
      Assert.Equal("/K ping SRV01", psi.Arguments);
      Assert.Equal("runas", psi.Verb);
    }

    [Fact]
    public void Renaming_the_group_retargets_the_next_run() {
      ShortcutViewModel group = Group("Test {SRV01}");
      ShortcutViewModel cmd = Command(group, "cmd.exe", "/K ping {}");
      cmd.Run();
      group.Name = "Test {SRV02}";
      cmd.Run();
      Assert.Equal(new[] { "/K ping SRV01", "/K ping SRV02" }, started.ConvertAll(p => p.Arguments));
    }

    [Fact]
    public void Group_name_is_kept_exactly_as_typed() {
      ShortcutViewModel group = Group("Test {SRV01}");
      Assert.Equal("Test {SRV01}", group.Name);
      Assert.Equal("Test {SRV01}", DataStore.Shortcuts.GetAll().Find(s => s.ID == group.ID).Name);
    }

    [Fact]
    public void Command_at_window_root_runs_its_arguments_unchanged() {
      ShortcutViewModel cmd = Command(null, "cmd.exe", "/K ping {}");
      cmd.Run();
      Assert.Equal("/K ping {}", Assert.Single(started).Arguments);
    }

    [Fact]
    public void Group_without_a_value_runs_the_arguments_unchanged() {
      ShortcutViewModel cmd = Command(Group("Test {}"), "cmd.exe", "/K ping {}");
      cmd.Run();
      Assert.Equal("/K ping {}", Assert.Single(started).Arguments);
    }

    [Fact]
    public void Only_the_direct_parent_group_counts() {
      ShortcutViewModel outer = Group("Outer {SRV01}");
      ShortcutViewModel inner = Group("Inner", outer);
      ShortcutViewModel cmd = Command(inner, "cmd.exe", "/K ping {}");
      cmd.Run();
      Assert.Equal("/K ping {}", Assert.Single(started).Arguments);
    }

    [Fact]
    public void Stored_arguments_keep_the_token() {
      ShortcutViewModel cmd = Command(Group("Test {SRV01}"), "cmd.exe", "/K ping {}");
      cmd.Run();
      Assert.Equal("/K ping {}", cmd.ProcessArguments);
      Assert.Equal("/K ping {}", DataStore.Shortcuts.GetAll().Find(s => s.ID == cmd.ID).ProcessArguments);
    }

    // ---- Tooltip text ----

    [Fact]
    public void Tooltip_shows_the_command_line_that_will_run() {
      ShortcutViewModel cmd = Command(Group("Test {SRV01}"), "cmd.exe", "/K ping {}");
      Assert.Equal("cmd.exe /K ping SRV01", cmd.ToolTipText);
    }

    [Fact]
    public void Tooltip_quotes_a_program_path_with_spaces_and_omits_empty_arguments() {
      ShortcutViewModel cmd = Command(null, @"C:\Program Files\App\app.exe", "");
      Assert.Equal("\"C:\\Program Files\\App\\app.exe\"", cmd.ToolTipText);
      cmd.ProcessArguments = "-x {}";
      Assert.Equal("\"C:\\Program Files\\App\\app.exe\" -x {}", cmd.ToolTipText);
    }

    [Fact]
    public void Tooltip_updates_when_the_group_is_renamed_or_the_command_changes() {
      ShortcutViewModel group = Group("Test {SRV01}");
      ShortcutViewModel cmd = Command(group, "cmd.exe", "/K ping {}");
      List<string> raised = new List<string>();
      cmd.PropertyChanged += (s, e) => raised.Add(e.PropertyName);

      group.Name = "Test {SRV02}";
      Assert.Contains(nameof(ShortcutViewModel.ToolTipText), raised);
      Assert.Equal("cmd.exe /K ping SRV02", cmd.ToolTipText);

      raised.Clear();
      cmd.ProcessArguments = "/K tracert {}";
      Assert.Contains(nameof(ShortcutViewModel.ToolTipText), raised);
      Assert.Equal("cmd.exe /K tracert SRV02", cmd.ToolTipText);

      raised.Clear();
      cmd.ProcessFileName = "powershell.exe";
      Assert.Contains(nameof(ShortcutViewModel.ToolTipText), raised);
      Assert.Equal("powershell.exe /K tracert SRV02", cmd.ToolTipText);
    }

    [Fact]
    public void Tooltip_follows_a_move_to_another_group() {
      ShortcutViewModel a = Group("A {SRV01}");
      ShortcutViewModel b = Group("B {SRV02}");
      ShortcutViewModel cmd = Command(a, "cmd.exe", "ping {}");
      List<string> raised = new List<string>();
      cmd.PropertyChanged += (s, e) => raised.Add(e.PropertyName);
      cmd.SetParentInMemory(b, null);
      Assert.Contains(nameof(ShortcutViewModel.ToolTipText), raised);
      Assert.Equal("cmd.exe ping SRV02", cmd.ToolTipText);
    }

    [Fact]
    public void Non_command_tooltip_is_still_the_target() {
      ShortcutViewModel group = Group("Test {SRV01}");
      Assert.Equal(group.Target, group.ToolTipText);
    }
  }
}
