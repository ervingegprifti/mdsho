using System.Linq;
using MDSHO.Core;
using Xunit;

namespace MDSHO.Tests {

  /// <summary>
  /// The pure logic behind the Run Command window: which program family a file name is, how several argument lines are
  /// joined, the "keep window open" switch that lives in the arguments, the preview text and the examples list.
  /// </summary>
  public class RunCommandLineTests {

    // ---- Program family ----

    [Theory]
    [InlineData("cmd", RunCommandProgramKind.Cmd)]
    [InlineData("cmd.exe", RunCommandProgramKind.Cmd)]
    [InlineData("CMD.EXE", RunCommandProgramKind.Cmd)]
    [InlineData(@"C:\Windows\System32\cmd.exe", RunCommandProgramKind.Cmd)]
    [InlineData("\"C:\\Windows\\System32\\cmd.exe\"", RunCommandProgramKind.Cmd)]
    [InlineData("  cmd.exe  ", RunCommandProgramKind.Cmd)]
    [InlineData("powershell", RunCommandProgramKind.PowerShell)]
    [InlineData("PowerShell.exe", RunCommandProgramKind.PowerShell)]
    [InlineData("pwsh", RunCommandProgramKind.PowerShell)]
    [InlineData(@"C:\Program Files\PowerShell\7\pwsh.exe", RunCommandProgramKind.PowerShell)]
    [InlineData("explorer.exe", RunCommandProgramKind.Other)]
    [InlineData("notepad", RunCommandProgramKind.Other)]
    [InlineData("cmdx.exe", RunCommandProgramKind.Other)]
    [InlineData("", RunCommandProgramKind.Other)]
    [InlineData(null, RunCommandProgramKind.Other)]
    public void Program_kind_is_found_from_the_file_name(string fileName, RunCommandProgramKind expected) {
      Assert.Equal(expected, RunCommandLine.GetProgramKind(fileName));
    }

    // ---- Joining lines ----

    [Theory]
    [InlineData("cmd.exe", "ping a\r\nping b", "ping a & ping b")]
    [InlineData("cmd", "ping a\nping b\nping c", "ping a & ping b & ping c")]
    [InlineData("powershell.exe", "Get-Service\r\nGet-Process", "Get-Service; Get-Process")]
    [InlineData("pwsh", "a\nb", "a; b")]
    [InlineData("explorer.exe", "shell:startup\r\n/select", "shell:startup /select")]
    [InlineData("cmd.exe", "  ping a  \r\n\r\n   \r\n  ping b ", "ping a & ping b")]
    [InlineData("cmd.exe", "/K ping localhost", "/K ping localhost")]
    [InlineData("cmd.exe", "", "")]
    [InlineData("cmd.exe", "\r\n  \r\n", "")]
    [InlineData("cmd.exe", "a\rb", "a & b")]
    public void Lines_are_trimmed_empty_ones_skipped_and_joined_by_program(string fileName, string arguments, string expected) {
      Assert.Equal(expected, RunCommandLine.JoinLines(fileName, arguments));
    }

    [Fact]
    public void Joining_null_arguments_gives_null() {
      Assert.Null(RunCommandLine.JoinLines("cmd.exe", null));
    }

    // ---- Keep window open (the switch lives in the arguments) ----

    [Theory]
    [InlineData("cmd.exe", "/K ping localhost", true, "ping localhost")]
    [InlineData("cmd.exe", "/k ping localhost", true, "ping localhost")]
    [InlineData("cmd.exe", "/C ping localhost", false, "ping localhost")]
    [InlineData("cmd.exe", "/c ping localhost", false, "ping localhost")]
    [InlineData("cmd.exe", "  /K   ping localhost", true, "ping localhost")]
    [InlineData("cmd.exe", "/K", true, "")]
    [InlineData("cmd.exe", "/K\r\nping a\r\nping b", true, "ping a\r\nping b")]
    [InlineData("cmd.exe", "ping localhost", false, "ping localhost")]
    [InlineData("cmd.exe", "/Kping", false, "/Kping")]
    [InlineData("cmd.exe", "echo /K", false, "echo /K")]
    [InlineData("powershell.exe", "-NoExit Get-Service", true, "Get-Service")]
    [InlineData("pwsh.exe", "-noexit Get-Service", true, "Get-Service")]
    [InlineData("powershell.exe", "Get-Service", false, "Get-Service")]
    [InlineData("powershell.exe", "-NoExitX", false, "-NoExitX")]
    [InlineData("explorer.exe", "/K shell:startup", false, "/K shell:startup")]
    [InlineData("notepad.exe", "-NoExit x", false, "-NoExit x")]
    public void Keep_open_switch_is_read_off_the_start_and_hidden(string fileName, string arguments, bool keepOpen, string rest) {
      Assert.Equal(rest, RunCommandLine.SplitKeepOpen(fileName, arguments, out bool actual));
      Assert.Equal(keepOpen, actual);
    }

    [Fact]
    public void Split_of_null_arguments_gives_empty_and_not_ticked() {
      Assert.Equal("", RunCommandLine.SplitKeepOpen("cmd.exe", null, out bool keepOpen));
      Assert.False(keepOpen);
    }

    [Theory]
    [InlineData("cmd.exe", true)]
    [InlineData("pwsh", true)]
    [InlineData("powershell.exe", true)]
    [InlineData("explorer.exe", false)]
    [InlineData("", false)]
    public void Only_cmd_and_powershell_support_keep_open(string fileName, bool expected) {
      Assert.Equal(expected, RunCommandLine.SupportsKeepOpen(fileName));
    }

    [Theory]
    [InlineData("cmd.exe", "ping localhost", true, "/K ping localhost")]
    [InlineData("cmd.exe", "ping localhost", false, "/C ping localhost")]
    [InlineData("cmd.exe", "", true, "/K")]
    [InlineData("cmd.exe", "", false, "/C")]
    [InlineData("cmd.exe", "ping a\r\nping b", true, "/K ping a\r\nping b")]
    [InlineData("powershell.exe", "Get-Service", true, "-NoExit Get-Service")]
    [InlineData("pwsh", "Get-Service", false, "Get-Service")]
    [InlineData("powershell.exe", "", true, "-NoExit")]
    [InlineData("explorer.exe", "shell:startup", true, "shell:startup")]
    [InlineData("explorer.exe", "shell:startup", false, "shell:startup")]
    public void Keep_open_switch_is_put_back_on_save(string fileName, string arguments, bool keepOpen, string expected) {
      Assert.Equal(expected, RunCommandLine.ApplyKeepOpen(fileName, arguments, keepOpen));
    }

    [Theory]
    [InlineData("cmd.exe", "/K ping localhost")]
    [InlineData("cmd.exe", "/C ipconfig /all")]
    [InlineData("powershell.exe", "-NoExit Get-Service")]
    [InlineData("powershell.exe", "start ms-windows-store:")]
    [InlineData("explorer.exe", "shell:startup")]
    [InlineData("ie4uinit.exe", "-show")]
    [InlineData("cmd.exe", "/K ping a\r\nping b")]
    public void Existing_commands_round_trip_unchanged(string fileName, string stored) {
      string rest = RunCommandLine.SplitKeepOpen(fileName, stored, out bool keepOpen);
      Assert.Equal(stored, RunCommandLine.ApplyKeepOpen(fileName, rest, keepOpen));
    }

    // ---- Command line and preview ----

    [Theory]
    [InlineData("cmd.exe", "/K ping a", "cmd.exe /K ping a")]
    [InlineData(@"C:\Program Files\App\app.exe", "-x", "\"C:\\Program Files\\App\\app.exe\" -x")]
    [InlineData("\"C:\\Program Files\\App\\app.exe\"", "-x", "\"C:\\Program Files\\App\\app.exe\" -x")]
    [InlineData("notepad.exe", "", "notepad.exe")]
    [InlineData("notepad.exe", null, "notepad.exe")]
    public void Command_line_quotes_a_program_with_spaces(string fileName, string arguments, string expected) {
      Assert.Equal(expected, RunCommandLine.CommandLine(fileName, arguments));
    }

    [Fact]
    public void Preview_joins_lines_fills_the_group_value_and_shows_folder_and_admin() {
      string preview = RunCommandLine.Describe("cmd.exe", "/K ping {}\r\nipconfig", "SRV01", @"C:\Work\{}", true);
      string[] lines = preview.Split('\n').Select(l => l.TrimEnd('\r')).ToArray();
      Assert.Equal("cmd.exe /K ping SRV01 & ipconfig", lines[0]);
      Assert.Contains(@"In folder: C:\Work\SRV01", lines);
      Assert.Contains("As administrator", lines);
    }

    [Fact]
    public void Preview_without_folder_admin_or_value_is_only_the_command_line() {
      Assert.Equal("cmd.exe /C ping {}", RunCommandLine.Describe("cmd.exe", "/C ping {}", null, "", false));
    }

    [Fact]
    public void Known_programs_list_is_in_the_agreed_order_with_friendly_names() {
      Assert.Equal(
        new[] { "cmd.exe", "powershell.exe", "pwsh.exe", "wt.exe", "explorer.exe", "notepad.exe" },
        RunCommandLine.KnownPrograms.Select(p => p.FileName).ToArray());
      Assert.Equal(
        new[] { "Command Prompt", "Windows PowerShell", "PowerShell 7", "Windows Terminal", "File Explorer", "Notepad" },
        RunCommandLine.KnownPrograms.Select(p => p.FriendlyName).ToArray());
      Assert.Equal("cmd.exe", RunCommandLine.DefaultProgram);
    }

    // ---- Examples ----

    [Fact]
    public void Examples_are_grouped_in_the_agreed_order() {
      Assert.Equal(
        new[] { "Command Prompt (cmd)", "PowerShell", "Explorer", "Other" },
        RunCommandExamples.All.Select(e => e.Category).Distinct().ToArray());
      Assert.Equal(15, RunCommandExamples.All.Count);
    }

    [Fact]
    public void Examples_of_one_category_sit_together_in_the_agreed_order() {
      Assert.Equal(
        new[] {
          "Ping a computer", "Show network settings", "Clear the DNS cache",
          "Run a batch script (.cmd or .bat)", "Run a batch script with the group value",
          "Find MDSHO in Microsoft Store", "Review MDSHO in Microsoft Store", "List stopped services",
          "Run a PowerShell script (.ps1)", "Run a PowerShell script with the group value", "Run a PowerShell 7 script (.ps1)",
          "Open the Startup folder", "Open the Apps folder", "Open Microsoft Store",
          "Refresh shortcut thumbnails"
        },
        RunCommandExamples.All.Select(e => e.Name).ToArray());
      // Each category is one unbroken run, so the grouped list shows every entry under its own header.
      string[] categories = RunCommandExamples.All.Select(e => e.Category).ToArray();
      int runs = categories.Where((c, i) => i == 0 || c != categories[i - 1]).Count();
      Assert.Equal(categories.Distinct().Count(), runs);
    }

    [Theory]
    [InlineData("Ping a computer", "cmd.exe", "ping {}", true, false)]
    [InlineData("Show network settings", "cmd.exe", "ipconfig /all", true, false)]
    [InlineData("Clear the DNS cache", "cmd.exe", "ipconfig /flushdns", true, true)]
    [InlineData("Find MDSHO in Microsoft Store", "powershell.exe", "start ms-windows-store://pdp/?ProductId=9P8DNCD0PPG8", false, false)]
    [InlineData("Review MDSHO in Microsoft Store", "powershell.exe", "start ms-windows-store://review/?ProductId=9P8DNCD0PPG8", false, false)]
    [InlineData("List stopped services", "powershell.exe", "Get-Service | Where-Object Status -eq 'Stopped'", true, false)]
    [InlineData("Open the Startup folder", "explorer.exe", "shell:startup", false, false)]
    [InlineData("Open the Apps folder", "explorer.exe", "shell:appsfolder", false, false)]
    [InlineData("Open Microsoft Store", "explorer.exe", "ms-windows-store:", false, false)]
    [InlineData("Refresh shortcut thumbnails", "ie4uinit.exe", "-show", false, false)]
    [InlineData("Run a PowerShell script (.ps1)", "powershell.exe", "-ExecutionPolicy Bypass -File \"C:\\Scripts\\script.ps1\"", true, false)]
    [InlineData("Run a PowerShell script with the group value", "powershell.exe", "-ExecutionPolicy Bypass -File \"C:\\Scripts\\script.ps1\" -Name {}", true, false)]
    [InlineData("Run a PowerShell 7 script (.ps1)", "pwsh.exe", "-ExecutionPolicy Bypass -File \"C:\\Scripts\\script.ps1\"", true, false)]
    [InlineData("Run a batch script (.cmd or .bat)", "cmd.exe", "\"C:\\Scripts\\script.cmd\"", true, false)]
    [InlineData("Run a batch script with the group value", "cmd.exe", "\"C:\\Scripts\\script.cmd\" {}", true, false)]
    public void Each_example_has_the_agreed_values(string name, string program, string arguments, bool keepOpen, bool admin) {
      RunCommandExample e = Assert.Single(RunCommandExamples.All, x => x.Name == name);
      Assert.Equal(program, e.ProgramFileName);
      Assert.Equal(arguments, e.Arguments);
      Assert.Equal(keepOpen, e.KeepWindowOpen);
      Assert.Equal(admin, e.RunAsAdministrator);
      Assert.Equal("", e.WorkingFolder);
      Assert.False(string.IsNullOrWhiteSpace(e.Description));
      Assert.DoesNotContain("\n", e.Description);
    }

    [Fact]
    public void Example_runs_text_shows_the_full_command_line() {
      RunCommandExample ping = RunCommandExamples.All.Single(x => x.Name == "Ping a computer");
      Assert.Equal("Runs: cmd.exe /K ping {}", ping.RunsText);
      RunCommandExample dns = RunCommandExamples.All.Single(x => x.Name == "Clear the DNS cache");
      Assert.Equal("Runs: cmd.exe /K ipconfig /flushdns (as administrator)", dns.RunsText);
      RunCommandExample store = RunCommandExamples.All.Single(x => x.Name == "Open Microsoft Store");
      Assert.Equal("Runs: explorer.exe ms-windows-store:", store.RunsText);
    }

    [Theory]
    [InlineData("Run a PowerShell script (.ps1)", "PowerShell", "Runs: powershell.exe -NoExit -ExecutionPolicy Bypass -File \"C:\\Scripts\\script.ps1\"")]
    [InlineData("Run a PowerShell script with the group value", "PowerShell", "Runs: powershell.exe -NoExit -ExecutionPolicy Bypass -File \"C:\\Scripts\\script.ps1\" -Name {}")]
    [InlineData("Run a PowerShell 7 script (.ps1)", "PowerShell", "Runs: pwsh.exe -NoExit -ExecutionPolicy Bypass -File \"C:\\Scripts\\script.ps1\"")]
    [InlineData("Run a batch script (.cmd or .bat)", "Command Prompt (cmd)", "Runs: cmd.exe /K \"C:\\Scripts\\script.cmd\"")]
    [InlineData("Run a batch script with the group value", "Command Prompt (cmd)", "Runs: cmd.exe /K \"C:\\Scripts\\script.cmd\" {}")]
    public void Script_template_runs_text_shows_the_full_command_line(string name, string category, string expected) {
      RunCommandExample e = Assert.Single(RunCommandExamples.All, x => x.Name == name);
      Assert.Equal(category, e.Category);
      Assert.Equal(expected, e.RunsText);
    }

    [Theory]
    [InlineData("Run a PowerShell script (.ps1)", "Runs a .ps1 file without changing the PC's script policy; replace the path with your script's, and add its parameters after the path.")]
    [InlineData("Run a PowerShell script with the group value", "Passes the group value to the script, e.g. in a group named \"Prod {SRV01}\" it runs the script with -Name SRV01.")]
    [InlineData("Run a PowerShell 7 script (.ps1)", "Same as above with PowerShell 7 (pwsh), which must be installed.")]
    [InlineData("Run a batch script (.cmd or .bat)", "Runs a .cmd or .bat file; replace the path with your script's.")]
    [InlineData("Run a batch script with the group value", "Passes the group value to the script as its first argument (%1).")]
    public void Script_template_has_the_agreed_description(string name, string description) {
      Assert.Equal(description, Assert.Single(RunCommandExamples.All, x => x.Name == name).Description);
    }
  }
}
