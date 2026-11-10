using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using MDSHO.Core;
using MDSHO.Data;
using MDSHO.ViewModels;
using Xunit;

namespace MDSHO.Tests {

  /// <summary>
  /// Running a command (joined lines, admin, working folder) and the view model of the Run Command window. No real
  /// process is ever started: every start goes through the <see cref="ShortcutViewModel.ProcessStarter"/> seam, which
  /// only records here.
  /// </summary>
  public class RunCommandViewModelTests : IDisposable {
    private readonly TempDb db = new TempDb();
    private readonly Action<ProcessStartInfo> oldStarter = ShortcutViewModel.ProcessStarter;
    private readonly Action<ShortcutViewModel, bool> oldEditor = ShortcutViewModel.OpenRunCommandEditor;
    private readonly List<ProcessStartInfo> started = new List<ProcessStartInfo>();
    private readonly List<(ShortcutViewModel Shortcut, bool IsNew)> opened = new List<(ShortcutViewModel, bool)>();
    private readonly List<string> confirms = new List<string>();
    private bool confirmAnswer = true;
    private readonly AppViewModel app = new AppViewModel();
    private readonly DesktopWindowViewModel win;

    public RunCommandViewModelTests() {
      ShortcutViewModel.ProcessStarter = psi => started.Add(psi);
      ShortcutViewModel.OpenRunCommandEditor = (s, isNew) => opened.Add((s, isNew));
      Dialogs.Confirm = (message, caption) => { confirms.Add(message); return confirmAnswer; };
      win = app.AddWindow("TEST", 0, 0);
    }
    public void Dispose() {
      ShortcutViewModel.ProcessStarter = oldStarter;
      ShortcutViewModel.OpenRunCommandEditor = oldEditor;
      db.Dispose();
    }

    private ShortcutViewModel Group(string name) {
      ShortcutViewModel g = ViewModelFactory.NewGroupShortcut(app, null, win);
      win.RootShortcuts.Add(g);
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
    private Shortcut Row(ShortcutViewModel s) { return DataStore.Shortcuts.Get(s.ID); }

    // ---- Running a saved command ----

    [Fact]
    public void Run_joins_the_argument_lines_by_program() {
      Command(null, "cmd.exe", "/K ping a\r\nping b").Run();
      Command(null, "powershell.exe", "Get-Service\r\n\r\nGet-Process").Run();
      Command(null, "explorer.exe", "shell:startup\n").Run();
      Assert.Equal(new[] { "/K ping a & ping b", "Get-Service; Get-Process", "shell:startup" }, started.Select(p => p.Arguments).ToArray());
    }

    [Fact]
    public void Run_fills_the_group_value_before_joining() {
      Command(Group("Test {SRV01}"), "cmd.exe", "/K ping {}\r\ntracert {}").Run();
      Assert.Equal("/K ping SRV01 & tracert SRV01", Assert.Single(started).Arguments);
    }

    [Fact]
    public void Stored_admin_flag_runs_elevated() {
      ShortcutViewModel cmd = Command(null, "cmd.exe", "/K ipconfig /flushdns");
      cmd.ProcessRunAsAdmin = true;
      cmd.Run();
      ProcessStartInfo psi = Assert.Single(started);
      Assert.True(psi.UseShellExecute);
      Assert.Equal("runas", psi.Verb);
    }

    [Fact]
    public void Not_admin_runs_without_verb() {
      Command(null, "cmd.exe", "/K x").Run();
      Assert.Equal("", Assert.Single(started).Verb);
    }

    [Fact]
    public void Working_folder_is_used_with_the_group_value_only_when_set() {
      ShortcutViewModel cmd = Command(Group("T {SRV01}"), "cmd.exe", "/K dir");
      cmd.Run();
      cmd.ProcessWorkingFolder = @"C:\Logs\{}";
      cmd.Run();
      Assert.Equal("", started[0].WorkingDirectory);
      Assert.Equal(@"C:\Logs\SRV01", started[1].WorkingDirectory);
    }

    [Fact]
    public void Declined_UAC_prompt_is_ignored_silently() {
      ShortcutViewModel.ProcessStarter = psi => throw new Win32Exception(1223);
      ShortcutViewModel cmd = Command(null, "cmd.exe", "/K x");
      cmd.ProcessRunAsAdmin = true;
      cmd.Run(); // no exception, no message box (a message box would block the test)
    }

    [Fact]
    public void Admin_flag_and_folder_are_saved_at_once_and_load_back() {
      ShortcutViewModel cmd = Command(null, "cmd.exe", "/K x");
      cmd.ProcessRunAsAdmin = true;
      cmd.ProcessWorkingFolder = @"C:\Work";
      Assert.Equal((1L, @"C:\Work"), (Row(cmd).ProcessRunAsAdmin, Row(cmd).ProcessWorkingFolder));
      ShortcutViewModel loaded = ViewModelFactory.ShortcutToViewModel(app, null, Row(cmd));
      Assert.True(loaded.ProcessRunAsAdmin);
      Assert.Equal(@"C:\Work", loaded.ProcessWorkingFolder);
      // A new command starts without the admin flag and folder.
      ShortcutViewModel fresh = Command(Group("G"), "a", "b");
      Assert.Equal((0L, ""), (Row(fresh).ProcessRunAsAdmin, Row(fresh).ProcessWorkingFolder));
    }

    [Fact]
    public void Copy_and_paste_keeps_admin_and_folder() {
      ShortcutViewModel cmd = Command(null, "cmd.exe", "/K x");
      cmd.ProcessRunAsAdmin = true;
      cmd.ProcessWorkingFolder = @"C:\Work";
      ShortcutViewModel copy = ViewModelFactory.CopyShortcut(cmd, null, win);
      Assert.True(copy.ProcessRunAsAdmin);
      Assert.Equal(@"C:\Work", copy.ProcessWorkingFolder);
      Assert.Equal((1L, @"C:\Work"), (Row(copy).ProcessRunAsAdmin, Row(copy).ProcessWorkingFolder));
    }

    [Fact]
    public void Tooltip_and_resolved_arguments_use_the_joined_form() {
      ShortcutViewModel cmd = Command(Group("T {SRV01}"), "cmd.exe", "/K ping {}\r\nipconfig");
      Assert.Equal("/K ping SRV01 & ipconfig", cmd.ResolvedProcessArguments);
      Assert.Equal("cmd.exe /K ping SRV01 & ipconfig", cmd.ToolTipText);
    }

    [Fact]
    public void Parent_group_value_is_exposed() {
      Assert.Equal("SRV01", Command(Group("T {SRV01}"), "cmd.exe", "").ParentGroupValue);
      Assert.Null(Command(null, "cmd.exe", "").ParentGroupValue);
    }

    // ---- Opening the window ----

    [Fact]
    public void New_command_from_a_window_opens_the_editor_as_new() {
      win.NewProcessCommandShortcutCommand.Execute(null);
      var o = Assert.Single(opened);
      Assert.True(o.IsNew);
      Assert.Contains(o.Shortcut, win.RootShortcuts);
    }

    [Fact]
    public void New_command_in_a_group_opens_the_editor_as_new() {
      ShortcutViewModel g = Group("G");
      g.NewProcessCommandShortcutCommand.Execute(null);
      var o = Assert.Single(opened);
      Assert.True(o.IsNew);
      Assert.Contains(o.Shortcut, g.Children);
    }

    [Fact]
    public void Set_Run_Command_opens_the_editor_for_the_existing_command() {
      ShortcutViewModel cmd = Command(null, "cmd.exe", "/K x");
      cmd.SetRunCommandCommand.Execute(null);
      var o = Assert.Single(opened);
      Assert.Same(cmd, o.Shortcut);
      Assert.False(o.IsNew);
    }

    [Fact]
    public void Sample_commands_are_gone() {
      Assert.Null(typeof(ShortcutViewModel).GetProperty("SampleCommandsCommand"));
      Assert.Null(typeof(DesktopWindowViewModel).GetProperty("SampleCommandsCommand"));
      Assert.Null(typeof(ViewModelFactory).GetMethod("SampleCommands"));
    }

    // ---- The window's view model: loading ----

    [Fact]
    public void Cmd_K_is_hidden_and_ticked() {
      RunCommandViewModel vm = new RunCommandViewModel(Command(null, "cmd.exe", "/K ping localhost"), false);
      Assert.Equal("cmd.exe", vm.Program);
      Assert.Equal("ping localhost", vm.Arguments);
      Assert.True(vm.KeepWindowOpen);
      Assert.True(vm.IsKeepOpenEnabled);
    }

    [Fact]
    public void Cmd_C_or_no_switch_is_unticked() {
      Assert.False(new RunCommandViewModel(Command(null, "cmd.exe", "/C ping a"), false).KeepWindowOpen);
      RunCommandViewModel none = new RunCommandViewModel(Command(null, "cmd", "ping a"), false);
      Assert.False(none.KeepWindowOpen);
      Assert.Equal("ping a", none.Arguments);
    }

    [Fact]
    public void PowerShell_NoExit_is_hidden_and_ticked() {
      RunCommandViewModel vm = new RunCommandViewModel(Command(null, "powershell.exe", "-NoExit Get-Service"), false);
      Assert.True(vm.KeepWindowOpen);
      Assert.Equal("Get-Service", vm.Arguments);
    }

    [Fact]
    public void Other_programs_disable_the_checkbox_and_keep_the_arguments() {
      RunCommandViewModel vm = new RunCommandViewModel(Command(null, "explorer.exe", "/K shell:startup"), false);
      Assert.False(vm.IsKeepOpenEnabled);
      Assert.Equal("/K shell:startup", vm.Arguments);
    }

    [Fact]
    public void Admin_and_folder_are_loaded() {
      ShortcutViewModel cmd = Command(null, "cmd.exe", "/K x");
      cmd.ProcessRunAsAdmin = true;
      cmd.ProcessWorkingFolder = @"C:\Work";
      RunCommandViewModel vm = new RunCommandViewModel(cmd, false);
      Assert.True(vm.RunAsAdministrator);
      Assert.Equal(@"C:\Work", vm.WorkingFolder);
    }

    [Fact]
    public void New_command_defaults_to_cmd_with_keep_open_ticked() {
      ShortcutViewModel cmd = ViewModelFactory.NewProcessCommandShortcut(app, null, win);
      RunCommandViewModel vm = new RunCommandViewModel(cmd, true);
      Assert.Equal("cmd.exe", vm.Program);
      Assert.True(vm.KeepWindowOpen);
      Assert.False(vm.RunAsAdministrator);
      Assert.Equal("", vm.Arguments);
      Assert.Equal("", vm.WorkingFolder);
    }

    [Fact]
    public void Changing_the_program_enables_or_disables_keep_open() {
      RunCommandViewModel vm = new RunCommandViewModel(Command(null, "cmd.exe", "/K x"), false);
      List<string> raised = new List<string>();
      vm.PropertyChanged += (s, e) => raised.Add(e.PropertyName);
      vm.Program = "notepad.exe";
      Assert.False(vm.IsKeepOpenEnabled);
      Assert.Contains(nameof(RunCommandViewModel.IsKeepOpenEnabled), raised);
      vm.Program = "pwsh";
      Assert.True(vm.IsKeepOpenEnabled);
    }

    // ---- Preview ----

    [Fact]
    public void Preview_is_live_and_shows_the_final_command_line() {
      ShortcutViewModel cmd = Command(Group("T {SRV01}"), "cmd.exe", "/K ping {}");
      RunCommandViewModel vm = new RunCommandViewModel(cmd, false);
      Assert.Equal("cmd.exe /K ping SRV01", vm.Preview);
      List<string> raised = new List<string>();
      vm.PropertyChanged += (s, e) => raised.Add(e.PropertyName);

      vm.Arguments = "ping {}\r\nipconfig";
      Assert.Contains(nameof(RunCommandViewModel.Preview), raised);
      Assert.StartsWith("cmd.exe /K ping SRV01 & ipconfig", vm.Preview);

      vm.KeepWindowOpen = false;
      Assert.StartsWith("cmd.exe /C ping SRV01 & ipconfig", vm.Preview);

      vm.RunAsAdministrator = true;
      vm.WorkingFolder = @"C:\{}";
      Assert.Contains("As administrator", vm.Preview);
      Assert.Contains(@"In folder: C:\SRV01", vm.Preview);
    }

    [Fact]
    public void Programs_list_is_the_known_programs() {
      RunCommandViewModel vm = new RunCommandViewModel(Command(null, "cmd.exe", ""), false);
      Assert.Equal(RunCommandLine.KnownPrograms.Select(p => p.FileName), vm.Programs.Select(p => p.FileName));
    }

    // ---- Save and Cancel ----

    [Fact]
    public void Save_writes_everything_with_the_switch_put_back() {
      ShortcutViewModel cmd = Command(null, "cmd.exe", "/K ping localhost");
      RunCommandViewModel vm = new RunCommandViewModel(cmd, false);
      vm.Arguments = "ping a\r\nping b";
      vm.KeepWindowOpen = false;
      vm.RunAsAdministrator = true;
      vm.WorkingFolder = @"C:\Work";
      vm.Save();
      Assert.Equal("cmd.exe", cmd.ProcessFileName);
      Assert.Equal("/C ping a\r\nping b", cmd.ProcessArguments);
      Assert.True(cmd.ProcessRunAsAdmin);
      Assert.Equal(@"C:\Work", cmd.ProcessWorkingFolder);
      Shortcut row = Row(cmd);
      Assert.Equal(("cmd.exe", "/C ping a\r\nping b", 1L, @"C:\Work"), (row.ProcessFileName, row.ProcessArguments, row.ProcessRunAsAdmin, row.ProcessWorkingFolder));
    }

    [Theory]
    [InlineData("cmd.exe", "/K ping localhost")]
    [InlineData("cmd.exe", "/C ipconfig")]
    [InlineData("powershell.exe", "-NoExit Get-Service")]
    [InlineData("powershell.exe", "start ms-windows-store:")]
    [InlineData("explorer.exe", "shell:startup")]
    public void Opening_and_saving_without_changes_keeps_the_command(string fileName, string arguments) {
      ShortcutViewModel cmd = Command(null, fileName, arguments);
      new RunCommandViewModel(cmd, false).Save();
      Assert.Equal(arguments, Row(cmd).ProcessArguments);
      Assert.Equal(fileName, Row(cmd).ProcessFileName);
    }

    [Fact]
    public void Save_for_powershell_adds_NoExit_only_when_ticked() {
      ShortcutViewModel cmd = Command(null, "powershell.exe", "Get-Service");
      RunCommandViewModel vm = new RunCommandViewModel(cmd, false);
      vm.KeepWindowOpen = true;
      vm.Save();
      Assert.Equal("-NoExit Get-Service", Row(cmd).ProcessArguments);
    }

    [Fact]
    public void New_command_saved_as_is_becomes_cmd_K() {
      ShortcutViewModel cmd = ViewModelFactory.NewProcessCommandShortcut(app, null, win);
      RunCommandViewModel vm = new RunCommandViewModel(cmd, true);
      vm.Arguments = "ping localhost";
      vm.Save();
      Assert.Equal(("cmd.exe", "/K ping localhost"), (Row(cmd).ProcessFileName, Row(cmd).ProcessArguments));
    }

    [Fact]
    public void Nothing_is_saved_until_Save() {
      ShortcutViewModel cmd = Command(null, "cmd.exe", "/K ping localhost");
      RunCommandViewModel vm = new RunCommandViewModel(cmd, false);
      vm.Program = "notepad.exe";
      vm.Arguments = "x";
      vm.RunAsAdministrator = true;
      vm.WorkingFolder = @"C:\W";
      Assert.Equal(("cmd.exe", "/K ping localhost", false, ""), (cmd.ProcessFileName, cmd.ProcessArguments, cmd.ProcessRunAsAdmin, cmd.ProcessWorkingFolder));
      Assert.Equal(("cmd.exe", "/K ping localhost", 0L, ""), (Row(cmd).ProcessFileName, Row(cmd).ProcessArguments, Row(cmd).ProcessRunAsAdmin, Row(cmd).ProcessWorkingFolder));
    }

    // ---- Test run ----

    [Fact]
    public void Test_run_starts_what_is_on_screen_with_the_group_value_without_saving() {
      ShortcutViewModel cmd = Command(Group("T {SRV01}"), "cmd.exe", "/K ping localhost");
      RunCommandViewModel vm = new RunCommandViewModel(cmd, false);
      vm.Arguments = "ping {}\r\ntracert {}";
      vm.KeepWindowOpen = false;
      vm.WorkingFolder = @"C:\{}";
      vm.RunAsAdministrator = true;
      vm.TestRunCommand.Execute(null);
      ProcessStartInfo psi = Assert.Single(started);
      Assert.Equal("cmd.exe", psi.FileName);
      Assert.Equal("/C ping SRV01 & tracert SRV01", psi.Arguments);
      Assert.Equal(@"C:\SRV01", psi.WorkingDirectory);
      Assert.Equal("runas", psi.Verb);
      Assert.True(psi.UseShellExecute);
      Assert.Equal("/K ping localhost", Row(cmd).ProcessArguments);
    }

    [Fact]
    public void Test_run_with_declined_UAC_is_silent() {
      ShortcutViewModel.ProcessStarter = psi => throw new Win32Exception(1223);
      RunCommandViewModel vm = new RunCommandViewModel(Command(null, "cmd.exe", "/K x"), false);
      vm.RunAsAdministrator = true;
      vm.TestRun();
    }

    // ---- Examples ----

    [Fact]
    public void Using_an_example_fills_every_field() {
      RunCommandViewModel vm = new RunCommandViewModel(ViewModelFactory.NewProcessCommandShortcut(app, null, win), true);
      RunCommandExample dns = RunCommandExamples.All.Single(e => e.Name == "Clear the DNS cache");
      Assert.True(vm.UseExample(dns));
      Assert.Empty(confirms);
      Assert.Equal(("cmd.exe", "", "ipconfig /flushdns", true, true), (vm.Program, vm.WorkingFolder, vm.Arguments, vm.KeepWindowOpen, vm.RunAsAdministrator));

      RunCommandExample store = RunCommandExamples.All.Single(e => e.Name == "Open Microsoft Store");
      confirmAnswer = true;
      Assert.True(vm.UseExample(store));
      Assert.Equal(("explorer.exe", "ms-windows-store:", false, false), (vm.Program, vm.Arguments, vm.KeepWindowOpen, vm.RunAsAdministrator));
    }

    [Theory]
    [InlineData("Run a PowerShell script with the group value", "powershell.exe", "-NoExit -ExecutionPolicy Bypass -File \"C:\\Scripts\\script.ps1\" -Name SRV01")]
    [InlineData("Run a batch script with the group value", "cmd.exe", "/K \"C:\\Scripts\\script.cmd\" SRV01")]
    [InlineData("Run a PowerShell script (.ps1)", "powershell.exe", "-NoExit -ExecutionPolicy Bypass -File \"C:\\Scripts\\script.ps1\"")]
    [InlineData("Run a PowerShell 7 script (.ps1)", "pwsh.exe", "-NoExit -ExecutionPolicy Bypass -File \"C:\\Scripts\\script.ps1\"")]
    [InlineData("Run a batch script (.cmd or .bat)", "cmd.exe", "/K \"C:\\Scripts\\script.cmd\"")]
    public void Script_template_in_a_group_fills_in_the_group_value(string name, string program, string expectedArguments) {
      ShortcutViewModel command = Command(Group("Prod {SRV01}"), "cmd.exe", "");
      RunCommandViewModel vm = new RunCommandViewModel(command, true);
      Assert.True(vm.UseExample(Assert.Single(RunCommandExamples.All, e => e.Name == name)));
      // The "Will run as" preview and the real start both go through GroupValue.ResolveArguments.
      Assert.Equal(program + " " + expectedArguments, vm.Preview);
      vm.TestRun();
      ProcessStartInfo psi = Assert.Single(started);
      Assert.Equal(program, psi.FileName);
      Assert.Equal(expectedArguments, psi.Arguments);
      Assert.True(string.IsNullOrEmpty(psi.Verb));
      // Saved and run from the panel, the shortcut gets the same command line.
      vm.Save();
      started.Clear();
      command.Run();
      Assert.Equal(expectedArguments, Assert.Single(started).Arguments);
    }

    [Fact]
    public void Using_an_example_over_typed_text_asks_first() {
      RunCommandViewModel vm = new RunCommandViewModel(Command(null, "cmd.exe", "/K my own"), false);
      RunCommandExample ping = RunCommandExamples.All.Single(e => e.Name == "Ping a computer");
      confirmAnswer = false;
      Assert.False(vm.UseExample(ping));
      Assert.Equal("Replace what you've typed?", Assert.Single(confirms));
      Assert.Equal("my own", vm.Arguments);
      confirmAnswer = true;
      Assert.True(vm.UseExample(ping));
      Assert.Equal("ping {}", vm.Arguments);
    }

    [Fact]
    public void Typed_folder_also_counts_as_typed() {
      RunCommandViewModel vm = new RunCommandViewModel(ViewModelFactory.NewProcessCommandShortcut(app, null, win), true);
      vm.WorkingFolder = @"C:\W";
      confirmAnswer = false;
      Assert.False(vm.UseExample(RunCommandExamples.All[0]));
      Assert.Single(confirms);
    }

    [Fact]
    public void Icons_follow_the_window_solid_icons_setting() {
      RunCommandViewModel vm = new RunCommandViewModel(Command(null, "cmd.exe", ""), false);
      Assert.Same(win.Appearance.Icons, vm.Icons);
    }
  }
}
