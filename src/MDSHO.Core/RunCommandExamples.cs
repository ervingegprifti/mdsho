using System.Collections.Generic;

namespace MDSHO.Core {

  /// <summary>One ready-made run command offered by the Examples popup of the Run Command window.</summary>
  public sealed class RunCommandExample {
    /// <summary>The group it is listed under, e.g. "PowerShell".</summary>
    public string Category { get; set; }
    /// <summary>The name shown in the list.</summary>
    public string Name { get; set; }
    /// <summary>One plain-English line saying what it does.</summary>
    public string Description { get; set; }
    /// <summary>The program it runs.</summary>
    public string ProgramFileName { get; set; }
    /// <summary>The working folder (empty = none).</summary>
    public string WorkingFolder { get; set; } = "";
    /// <summary>The arguments, without the keep-open switch.</summary>
    public string Arguments { get; set; }
    /// <summary>True when the window stays open after it runs.</summary>
    public bool KeepWindowOpen { get; set; }
    /// <summary>True when it runs as administrator.</summary>
    public bool RunAsAdministrator { get; set; }
    /// <summary>"Runs: " and the full command line, e.g. <c>Runs: cmd.exe /K ping {}</c>.</summary>
    public string RunsText {
      get {
        string arguments = RunCommandLine.JoinLines(this.ProgramFileName, RunCommandLine.ApplyKeepOpen(this.ProgramFileName, this.Arguments, this.KeepWindowOpen));
        string text = "Runs: " + RunCommandLine.CommandLine(this.ProgramFileName, arguments);
        return this.RunAsAdministrator ? text + " (as administrator)" : text;
      }
    }
  }

  /// <summary>The examples offered by the star button of the Run Command window, grouped by program.</summary>
  public static class RunCommandExamples {
    private const string Cmd = "Command Prompt (cmd)";
    private const string PowerShell = "PowerShell";
    private const string Explorer = "Explorer";
    private const string Other = "Other";

    /// <summary>All examples, in display order (grouped by <see cref="RunCommandExample.Category"/>).</summary>
    public static IReadOnlyList<RunCommandExample> All { get; } = new List<RunCommandExample> {
      new RunCommandExample {
        Category = Cmd, Name = "Ping a computer", ProgramFileName = "cmd.exe", Arguments = "ping {}", KeepWindowOpen = true,
        Description = "Checks whether a computer answers on the network; put it in a group like \"Test {SRV01}\" to fill in the name."
      },
      new RunCommandExample {
        Category = Cmd, Name = "Show network settings", ProgramFileName = "cmd.exe", Arguments = "ipconfig /all", KeepWindowOpen = true,
        Description = "Lists the IP addresses, DNS servers and other settings of every network adapter."
      },
      new RunCommandExample {
        Category = Cmd, Name = "Clear the DNS cache", ProgramFileName = "cmd.exe", Arguments = "ipconfig /flushdns", KeepWindowOpen = true, RunAsAdministrator = true,
        Description = "Forgets the remembered web site addresses, which can fix sites that do not load."
      },
      new RunCommandExample {
        Category = Cmd, Name = "Run a batch script (.cmd or .bat)", ProgramFileName = "cmd.exe", Arguments = "\"C:\\Scripts\\script.cmd\"", KeepWindowOpen = true,
        Description = "Runs a .cmd or .bat file; replace the path with your script's."
      },
      new RunCommandExample {
        Category = Cmd, Name = "Run a batch script with the group value", ProgramFileName = "cmd.exe", Arguments = "\"C:\\Scripts\\script.cmd\" {}", KeepWindowOpen = true,
        Description = "Passes the group value to the script as its first argument (%1)."
      },
      new RunCommandExample {
        Category = PowerShell, Name = "Find MDSHO in Microsoft Store", ProgramFileName = "powershell.exe", Arguments = "start ms-windows-store://pdp/?ProductId=9P8DNCD0PPG8",
        Description = "Opens the MDSHO page in the Microsoft Store app."
      },
      new RunCommandExample {
        Category = PowerShell, Name = "Review MDSHO in Microsoft Store", ProgramFileName = "powershell.exe", Arguments = "start ms-windows-store://review/?ProductId=9P8DNCD0PPG8",
        Description = "Opens the Microsoft Store app where you can rate and review MDSHO."
      },
      new RunCommandExample {
        Category = PowerShell, Name = "List stopped services", ProgramFileName = "powershell.exe", Arguments = "Get-Service | Where-Object Status -eq 'Stopped'", KeepWindowOpen = true,
        Description = "Shows the Windows services that are not running."
      },
      new RunCommandExample {
        Category = PowerShell, Name = "Run a PowerShell script (.ps1)", ProgramFileName = "powershell.exe", Arguments = "-ExecutionPolicy Bypass -File \"C:\\Scripts\\script.ps1\"", KeepWindowOpen = true,
        Description = "Runs a .ps1 file without changing the PC's script policy; replace the path with your script's, and add its parameters after the path."
      },
      new RunCommandExample {
        Category = PowerShell, Name = "Run a PowerShell script with the group value", ProgramFileName = "powershell.exe", Arguments = "-ExecutionPolicy Bypass -File \"C:\\Scripts\\script.ps1\" -Name {}", KeepWindowOpen = true,
        Description = "Passes the group value to the script, e.g. in a group named \"Prod {SRV01}\" it runs the script with -Name SRV01."
      },
      new RunCommandExample {
        Category = PowerShell, Name = "Run a PowerShell 7 script (.ps1)", ProgramFileName = "pwsh.exe", Arguments = "-ExecutionPolicy Bypass -File \"C:\\Scripts\\script.ps1\"", KeepWindowOpen = true,
        Description = "Same as above with PowerShell 7 (pwsh), which must be installed."
      },
      new RunCommandExample {
        Category = Explorer, Name = "Open the Startup folder", ProgramFileName = "explorer.exe", Arguments = "shell:startup",
        Description = "Opens the folder of programs that start when you sign in to Windows."
      },
      new RunCommandExample {
        Category = Explorer, Name = "Open the Apps folder", ProgramFileName = "explorer.exe", Arguments = "shell:appsfolder",
        Description = "Opens a folder with every installed app, ready to start or pin."
      },
      new RunCommandExample {
        Category = Explorer, Name = "Open Microsoft Store", ProgramFileName = "explorer.exe", Arguments = "ms-windows-store:",
        Description = "Opens the Microsoft Store app."
      },
      new RunCommandExample {
        Category = Other, Name = "Refresh shortcut thumbnails", ProgramFileName = "ie4uinit.exe", Arguments = "-show",
        Description = "Rebuilds the Windows icon cache, which fixes blank or wrong shortcut icons."
      }
    };
  }
}
