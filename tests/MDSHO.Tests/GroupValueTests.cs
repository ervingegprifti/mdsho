using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using MDSHO.Core;
using MDSHO.Data;
using MDSHO.ViewModels;
using Xunit;

namespace MDSHO.Tests {

  /// <summary>
  /// Group values: a group name holds values in braces (<c>Prod {SRV01} {user=admin}</c>) and run commands directly
  /// inside it use them as <c>{=1}</c>…<c>{=9}</c> or <c>{=name}</c>. Mistakes are MDSHO syntax errors.
  /// </summary>
  public class GroupValueTests {

    private static string[] Values(string groupName) {
      return GroupValue.Parse(groupName).Select(v => v.Value).ToArray();
    }

    // ---- Reading values from a group name ----

    [Theory]
    [InlineData("Prod {SRV01}", new[] { "SRV01" })]
    [InlineData("Prod {SRV01} {admin} {D$}", new[] { "SRV01", "admin", "D$" })]
    [InlineData("Prod {server=SRV01} {user=admin} {share=D$}", new[] { "SRV01", "admin", "D$" })]
    [InlineData("{A}{B}", new[] { "A", "B" })]
    [InlineData("Test { SRV 01 }", new[] { " SRV 01 " })]
    [InlineData("{a{b}}", new[] { "a{b" })]
    [InlineData("Prod {SRV01} {", new[] { "SRV01" })]
    public void Values_are_read_left_to_right_verbatim(string groupName, string[] expected) {
      Assert.Equal(expected, Values(groupName));
    }

    [Theory]
    [InlineData("Prod")]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("Test {}")]
    [InlineData("Test {SRV")]
    [InlineData("Test }SRV{")]
    public void Names_without_values(string groupName) {
      Assert.Empty(GroupValue.Parse(groupName));
    }

    [Fact]
    public void Empty_braces_are_not_a_value_and_do_not_count() {
      IReadOnlyList<GroupValueItem> values = GroupValue.Parse("{} {A} {} {B}");
      Assert.Equal(new[] { (1, "A"), (2, "B") }, values.Select(v => (v.Number, v.Value)).ToArray());
    }

    [Fact]
    public void Values_are_numbered_from_one() {
      Assert.Equal(new[] { 1, 2, 3 }, GroupValue.Parse("P {a} {b} {c}").Select(v => v.Number).ToArray());
    }

    [Fact]
    public void At_most_nine_values_count() {
      IReadOnlyList<GroupValueItem> values = GroupValue.Parse("{1}{2}{3}{4}{5}{6}{7}{8}{9}{10}{11}");
      Assert.Equal(9, values.Count);
      Assert.Equal("9", values[8].Value);
      Assert.Equal(9, GroupValue.MaxValues);
    }

    [Fact]
    public void A_value_can_have_a_name() {
      GroupValueItem item = Assert.Single(GroupValue.Parse("Prod {server=SRV01}"));
      Assert.Equal((1, "server", "SRV01"), (item.Number, item.Name, item.Value));
    }

    [Fact]
    public void Everything_after_the_first_equals_sign_is_the_value() {
      GroupValueItem item = Assert.Single(GroupValue.Parse("Search {query=a=b}"));
      Assert.Equal(("query", "a=b"), (item.Name, item.Value));
    }

    [Theory]
    [InlineData("{my server=x}", "my server=x")]
    [InlineData("{1st=x}", "1st=x")]
    [InlineData("{=x}", "=x")]
    [InlineData("{a-b=x}", "a-b=x")]
    [InlineData("{ user=x}", " user=x")]
    public void Text_before_the_equals_sign_that_is_not_a_valid_name_makes_a_plain_value(string groupName, string value) {
      GroupValueItem item = Assert.Single(GroupValue.Parse(groupName));
      Assert.Null(item.Name);
      Assert.Equal(value, item.Value);
    }

    [Theory]
    [InlineData("{user=x}", "user")]
    [InlineData("{_x=1}", "_x")]
    [InlineData("{Server_2=1}", "Server_2")]
    public void Valid_names(string groupName, string name) {
      Assert.Equal(name, Assert.Single(GroupValue.Parse(groupName)).Name);
    }

    [Fact]
    public void A_named_value_can_be_empty() {
      GroupValueItem item = Assert.Single(GroupValue.Parse("{user=}"));
      Assert.Equal(("user", ""), (item.Name, item.Value));
    }

    [Fact]
    public void A_name_used_twice_counts_only_the_first_time() {
      IReadOnlyList<GroupValueItem> values = GroupValue.Parse("{user=a} {USER=b}");
      Assert.Equal("user", values[0].Name);
      Assert.Null(values[1].Name);
      Assert.Equal("a-b", GroupValue.Fill("{=User}-{=2}", values));
    }

    [Fact]
    public void Labels_and_tokens_for_the_values_line() {
      IReadOnlyList<GroupValueItem> values = GroupValue.Parse("Prod {SRV01} {user=admin}");
      Assert.Equal(("{=1} SRV01", "{=1}"), (values[0].Label, values[0].Token));
      Assert.Equal(("{=2} {=user} admin", "{=user}"), (values[1].Label, values[1].Token));
    }

    // ---- Filling the tokens ----

    [Theory]
    [InlineData("Prod {SRV01}", "/K ping {=1}", "/K ping SRV01")]
    [InlineData("Prod {SRV01}", "{=1} {=1} -n {=1}", "SRV01 SRV01 -n SRV01")]
    [InlineData("Prod {My Server}", "ping {=1}", "ping My Server")]
    [InlineData("Prod {SRV01} {admin} {D$}", @"net use \\{=1}\{=3} /user:{=2}", @"net use \\SRV01\D$ /user:admin")]
    [InlineData("Prod {server=SRV01} {user=admin}", "{=server} {=user} {=1} {=2}", "SRV01 admin SRV01 admin")]
    [InlineData("Prod {SRV01} {user=admin}", "{=2}={=user}", "admin=admin")]
    [InlineData("Prod {server=SRV01}", "{=SERVER} {=Server}", "SRV01 SRV01")]
    [InlineData("Search {query=a=b}", "find {=query}", "find a=b")]
    [InlineData("P {1}{2}{3}{4}{5}{6}{7}{8}{9}", "{=9}{=1}", "91")]
    [InlineData("P {V}", "{{=1}}", "{V}")]
    [InlineData("P {V}", @"\d{3} {0} HEAD@{1} {=1}", @"\d{3} {0} HEAD@{1} V")]
    public void Tokens_are_replaced_by_the_value_as_is(string groupName, string text, string expected) {
      IReadOnlyList<GroupValueItem> values = GroupValue.Parse(groupName);
      Assert.Null(GroupValue.FindError(text, values));
      Assert.Equal(expected, GroupValue.Fill(text, values));
    }

    [Fact]
    public void Old_token_is_plain_text_and_never_replaced() {
      Assert.Equal("ping {}", GroupValue.Fill("ping {}", GroupValue.Parse("P {V}")));
      Assert.Equal("ping {}", GroupValue.Fill("ping {}", GroupValue.Parse("P")));
    }

    [Fact]
    public void Fill_leaves_text_without_values_unchanged_and_keeps_null() {
      Assert.Equal("ping {=1}", GroupValue.Fill("ping {=1}", GroupValue.Parse("P")));
      Assert.Equal("ping {=1}", GroupValue.Fill("ping {=1}", null));
      Assert.Null(GroupValue.Fill(null, GroupValue.Parse("P {V}")));
    }

    [Fact]
    public void Fill_leaves_bad_tokens_as_they_are() {
      Assert.Equal("V {=2} {=x} {=}", GroupValue.Fill("{=1} {=2} {=x} {=}", GroupValue.Parse("P {V}")));
    }

    // ---- Syntax errors ----

    [Theory]
    [InlineData("ping {}")]
    [InlineData("{=1} {}")]
    [InlineData("{{}}")]
    public void Old_token_with_values_is_an_error(string text) {
      Assert.Equal("{} is no longer supported. Use {=1}.", GroupValue.FindError(text, GroupValue.Parse("P {V}")));
      Assert.Equal(GroupValue.OldTokenMessage, GroupValue.FindError(text, GroupValue.Parse("P {V}")));
    }

    [Theory]
    [InlineData("P")]
    [InlineData("P {}")]
    [InlineData(null)]
    public void Old_token_without_values_is_fine(string groupName) {
      IReadOnlyList<GroupValueItem> values = groupName == null ? null : GroupValue.Parse(groupName);
      Assert.Null(GroupValue.FindError("ping {} {0}", values));
    }

    [Theory]
    [InlineData("P {A}", "{=2}", 2)]
    [InlineData("P {A} {B} {C}", "ping {=4}", 4)]
    [InlineData("P {1}{2}{3}{4}{5}{6}{7}{8}", "{=9}", 9)]
    public void A_number_above_the_count_is_an_error(string groupName, string text, int number) {
      Assert.Equal($"The group has no value {number}.", GroupValue.FindError(text, GroupValue.Parse(groupName)));
    }

    [Fact]
    public void Numbers_count_every_value_named_or_not() {
      IReadOnlyList<GroupValueItem> values = GroupValue.Parse("Prod {SRV01} {user=admin}");
      Assert.Null(GroupValue.FindError("{=2}", values));
      Assert.Equal("admin admin", GroupValue.Fill("{=2} {=user}", values));
    }

    [Theory]
    [InlineData("P {SRV01}", "{=user}", "user")]
    [InlineData("P {server=SRV01}", "{=Usr}", "Usr")]
    [InlineData("P {my server=x}", "{=my}", "my")]
    public void An_unknown_name_is_an_error(string groupName, string text, string name) {
      Assert.Equal($"The group has no value named \"{name}\".", GroupValue.FindError(text, GroupValue.Parse(groupName)));
    }

    [Theory]
    [InlineData("P", "{=1}")]
    [InlineData("P {}", "ping {=user}")]
    [InlineData(null, "{=1}")]
    public void A_token_without_values_is_an_error(string groupName, string text) {
      IReadOnlyList<GroupValueItem> values = groupName == null ? null : GroupValue.Parse(groupName);
      Assert.Equal("This command uses group values, but its group has none.", GroupValue.FindError(text, values));
      Assert.Equal(GroupValue.NoValuesMessage, GroupValue.FindError(text, values));
    }

    [Theory]
    [InlineData("{=}", "{=}")]
    [InlineData("ping {= 1}", "{= 1}")]
    [InlineData("ping {=1 }", "{=1 }")]
    [InlineData("ping {=us er}", "{=us er}")]
    [InlineData("{=0}", "{=0}")]
    [InlineData("{=10}", "{=10}")]
    [InlineData("{=1a}", "{=1a}")]
    [InlineData("{=-1}", "{=-1}")]
    [InlineData("{=a-b}", "{=a-b}")]
    [InlineData("ping {=1", "{=1")]
    [InlineData("ping {=1 -n 3", "{=1")]
    [InlineData("ping {=user\r\nipconfig}", "{=user")]
    [InlineData("ping {=", "{=")]
    public void Malformed_tokens_are_an_error_that_quotes_them(string text, string token) {
      Assert.Equal($"\"{token}\" is not valid. Use {{=1}} or {{=name}}.", GroupValue.FindError(text, GroupValue.Parse("P {V} {user=u}")));
    }

    [Fact]
    public void A_malformed_token_without_values_says_it_is_not_valid() {
      Assert.Equal("\"{=}\" is not valid. Use {=1} or {=name}.", GroupValue.FindError("{=}", GroupValue.Parse("P")));
    }

    [Theory]
    [InlineData("{=5} {}", "The group has no value 5.")]
    [InlineData("{} {=5}", "{} is no longer supported. Use {=1}.")]
    [InlineData("{=x y} {=5}", "\"{=x y}\" is not valid. Use {=1} or {=name}.")]
    [InlineData("{=1} {=nope} {=5}", "The group has no value named \"nope\".")]
    public void The_first_error_from_the_left_is_reported(string text, string expected) {
      Assert.Equal(expected, GroupValue.FindError(text, GroupValue.Parse("P {V}")));
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("ping localhost")]
    [InlineData("echo { = } {a=b} @{x=1}")]
    public void Text_without_tokens_is_fine(string text) {
      Assert.Null(GroupValue.FindError(text, GroupValue.Parse("P {V}")));
      Assert.Null(GroupValue.FindError(text, null));
    }

    [Fact]
    public void Arguments_are_checked_before_the_working_folder() {
      IReadOnlyList<GroupValueItem> values = GroupValue.Parse("P {V}");
      Assert.Equal("The group has no value 2.", GroupValue.FindError("{=2}", @"C:\{=x}", values));
      Assert.Equal("The group has no value named \"x\".", GroupValue.FindError("{=1}", @"C:\{=x}", values));
      Assert.Null(GroupValue.FindError("{=1}", @"C:\{=1}", values));
      Assert.Null(GroupValue.FindError(null, null, values));
    }

    [Theory]
    [InlineData("ping {=1}", true)]
    [InlineData("ping {=", true)]
    [InlineData("ping {}", false)]
    [InlineData("ping {0} { =1}", false)]
    [InlineData(null, false)]
    public void Uses_values_finds_any_token(string text, bool expected) {
      Assert.Equal(expected, GroupValue.UsesValues(text));
    }
  }

  /// <summary>Run commands use the values of their direct parent group when they are started and in their tooltip.</summary>
  public class GroupValueViewModelTests : IDisposable {
    private readonly TempDb db = new TempDb();
    private readonly Action<ProcessStartInfo> oldStarter = ShortcutViewModel.ProcessStarter;
    private readonly Action<string, string, string> oldShowError = Dialogs.ShowError;
    private readonly List<ProcessStartInfo> started = new List<ProcessStartInfo>();
    private readonly List<(string Caption, string Error, string Detail)> errors = new List<(string, string, string)>();
    private readonly AppViewModel app = new AppViewModel();
    private readonly DesktopWindowViewModel win;

    public GroupValueViewModelTests() {
      ShortcutViewModel.ProcessStarter = psi => started.Add(psi);
      Dialogs.ShowError = (caption, error, detail) => errors.Add((caption, error, detail));
      win = app.AddWindow("TEST", 0, 0);
    }
    public void Dispose() {
      ShortcutViewModel.ProcessStarter = oldStarter;
      Dialogs.ShowError = oldShowError;
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
      s.Name = "Ping";
      return s;
    }

    [Fact]
    public void Started_process_gets_numbered_and_named_values() {
      ShortcutViewModel cmd = Command(Group("Prod {SRV01} {user=admin}"), "cmd.exe", "/K ping {=1} -u {=user} {=2}");
      cmd.Run();
      ProcessStartInfo psi = Assert.Single(started);
      Assert.Equal("cmd.exe", psi.FileName);
      Assert.Equal("/K ping SRV01 -u admin admin", psi.Arguments);
      Assert.True(psi.UseShellExecute);
      Assert.Equal("", psi.Verb);
      Assert.Empty(errors);
    }

    [Fact]
    public void Working_folder_gets_the_values_too() {
      ShortcutViewModel cmd = Command(Group("Prod {share=D$}"), "cmd.exe", "/K dir");
      cmd.ProcessWorkingFolder = @"\\srv\{=share}";
      cmd.Run();
      Assert.Equal(@"\\srv\D$", Assert.Single(started).WorkingDirectory);
    }

    [Fact]
    public void Run_as_administrator_also_gets_the_values() {
      ShortcutViewModel cmd = Command(Group("Test {SRV01}"), "cmd.exe", "/K ping {=1}");
      cmd.ProcessRunAsAdmin = true;
      cmd.Run();
      ProcessStartInfo psi = Assert.Single(started);
      Assert.Equal("/K ping SRV01", psi.Arguments);
      Assert.Equal("runas", psi.Verb);
    }

    [Fact]
    public void Renaming_the_group_retargets_the_next_run() {
      ShortcutViewModel group = Group("Test {SRV01}");
      ShortcutViewModel cmd = Command(group, "cmd.exe", "/K ping {=1}");
      cmd.Run();
      group.Name = "Test {SRV02}";
      cmd.Run();
      Assert.Equal(new[] { "/K ping SRV01", "/K ping SRV02" }, started.ConvertAll(p => p.Arguments));
    }

    [Fact]
    public void Group_name_is_kept_exactly_as_typed() {
      ShortcutViewModel group = Group("Test {server=SRV01}");
      Assert.Equal("Test {server=SRV01}", group.Name);
      Assert.Equal("Test {server=SRV01}", DataStore.Shortcuts.GetAll().Find(s => s.ID == group.ID).Name);
    }

    [Fact]
    public void Old_token_in_a_group_with_values_does_not_run_and_shows_the_error() {
      ShortcutViewModel cmd = Command(Group("Test {SRV01}"), "cmd.exe", "/K ping {}");
      cmd.Run();
      Assert.Empty(started);
      var shown = Assert.Single(errors);
      Assert.Equal("Ping", shown.Caption);
      Assert.Equal("{} is no longer supported. Use {=1}.", shown.Error);
      Assert.Equal("The command did not run. Right-click it and choose Edit run command... to fix it.", shown.Detail);
      Assert.Equal(shown.Detail, ShortcutViewModel.RunCommandNotRunText);
    }

    [Fact]
    public void Run_as_administrator_with_an_error_does_not_run() {
      ShortcutViewModel cmd = Command(Group("Test {SRV01}"), "cmd.exe", "/K ping {=3}");
      cmd.RunShortcut(true);
      Assert.Empty(started);
      Assert.Equal("The group has no value 3.", Assert.Single(errors).Error);
    }

    [Fact]
    public void An_error_in_the_working_folder_does_not_run() {
      ShortcutViewModel cmd = Command(Group("Test {SRV01}"), "cmd.exe", "/K dir");
      cmd.ProcessWorkingFolder = @"C:\{=user}";
      cmd.Run();
      Assert.Empty(started);
      Assert.Equal("The group has no value named \"user\".", Assert.Single(errors).Error);
    }

    [Fact]
    public void Old_token_at_window_root_or_in_a_group_without_values_runs_unchanged() {
      Command(null, "cmd.exe", "/K ping {}").Run();
      Command(Group("Test {}"), "cmd.exe", "/K ping {}").Run();
      Assert.Equal(new[] { "/K ping {}", "/K ping {}" }, started.ConvertAll(p => p.Arguments));
      Assert.Empty(errors);
    }

    [Fact]
    public void A_token_at_window_root_does_not_run() {
      Command(null, "cmd.exe", "/K ping {=1}").Run();
      Assert.Empty(started);
      Assert.Equal("This command uses group values, but its group has none.", Assert.Single(errors).Error);
    }

    [Fact]
    public void Only_the_direct_parent_group_counts() {
      ShortcutViewModel outer = Group("Outer {SRV01}");
      ShortcutViewModel inner = Group("Inner", outer);
      Command(inner, "cmd.exe", "/K ping {=1}").Run();
      Assert.Empty(started);
      Assert.Equal(GroupValue.NoValuesMessage, Assert.Single(errors).Error);
    }

    [Fact]
    public void Stored_arguments_keep_the_token() {
      ShortcutViewModel cmd = Command(Group("Test {SRV01}"), "cmd.exe", "/K ping {=1}");
      cmd.Run();
      Assert.Equal("/K ping {=1}", cmd.ProcessArguments);
      Assert.Equal("/K ping {=1}", DataStore.Shortcuts.GetAll().Find(s => s.ID == cmd.ID).ProcessArguments);
    }

    // ---- Tooltip text and error ----

    [Fact]
    public void Tooltip_shows_the_command_line_that_will_run() {
      ShortcutViewModel cmd = Command(Group("Test {SRV01} {user=admin}"), "cmd.exe", "/K ping {=1} {=user}");
      Assert.Equal("cmd.exe /K ping SRV01 admin", cmd.ToolTipText);
      Assert.Null(cmd.RunCommandError);
      Assert.False(cmd.HasRunCommandError);
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
      ShortcutViewModel cmd = Command(group, "cmd.exe", "/K ping {=1}");
      List<string> raised = new List<string>();
      cmd.PropertyChanged += (s, e) => raised.Add(e.PropertyName);

      group.Name = "Test {SRV02}";
      Assert.Contains(nameof(ShortcutViewModel.ToolTipText), raised);
      Assert.Equal("cmd.exe /K ping SRV02", cmd.ToolTipText);

      raised.Clear();
      cmd.ProcessArguments = "/K tracert {=1}";
      Assert.Contains(nameof(ShortcutViewModel.ToolTipText), raised);
      Assert.Equal("cmd.exe /K tracert SRV02", cmd.ToolTipText);

      raised.Clear();
      cmd.ProcessFileName = "powershell.exe";
      Assert.Contains(nameof(ShortcutViewModel.ToolTipText), raised);
      Assert.Equal("powershell.exe /K tracert SRV02", cmd.ToolTipText);
    }

    [Fact]
    public void Renaming_the_group_away_from_its_values_shows_the_error() {
      ShortcutViewModel group = Group("Test {SRV01}");
      ShortcutViewModel cmd = Command(group, "cmd.exe", "/K ping {=1}");
      List<string> raised = new List<string>();
      cmd.PropertyChanged += (s, e) => raised.Add(e.PropertyName);
      group.Name = "Test";
      Assert.Contains(nameof(ShortcutViewModel.RunCommandError), raised);
      Assert.Contains(nameof(ShortcutViewModel.HasRunCommandError), raised);
      Assert.Equal(GroupValue.NoValuesMessage, cmd.RunCommandError);
      Assert.True(cmd.HasRunCommandError);
      // The command line is shown as stored.
      Assert.Equal("cmd.exe /K ping {=1}", cmd.ToolTipText);
      Assert.True(cmd.HasToolTip);
    }

    [Fact]
    public void Working_folder_changes_update_the_error() {
      ShortcutViewModel cmd = Command(Group("Test {SRV01}"), "cmd.exe", "/K dir");
      List<string> raised = new List<string>();
      cmd.PropertyChanged += (s, e) => raised.Add(e.PropertyName);
      cmd.ProcessWorkingFolder = @"C:\{}";
      Assert.Contains(nameof(ShortcutViewModel.RunCommandError), raised);
      Assert.Equal(GroupValue.OldTokenMessage, cmd.RunCommandError);
    }

    [Fact]
    public void Tooltip_follows_a_move_to_another_group() {
      ShortcutViewModel a = Group("A {SRV01}");
      ShortcutViewModel b = Group("B {SRV02}");
      ShortcutViewModel cmd = Command(a, "cmd.exe", "ping {=1}");
      List<string> raised = new List<string>();
      cmd.PropertyChanged += (s, e) => raised.Add(e.PropertyName);
      cmd.SetParentInMemory(b, null);
      Assert.Contains(nameof(ShortcutViewModel.ToolTipText), raised);
      Assert.Contains(nameof(ShortcutViewModel.RunCommandError), raised);
      Assert.Equal("cmd.exe ping SRV02", cmd.ToolTipText);
    }

    [Fact]
    public void Groups_and_other_buttons_have_no_run_command_error() {
      ShortcutViewModel group = Group("Test {SRV01} {=1}");
      Assert.Equal(group.Target, group.ToolTipText);
      Assert.Null(group.RunCommandError);
    }
  }
}
