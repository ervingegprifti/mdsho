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
    /// <summary>"Runs: " and the full command line, e.g. <c>Runs: cmd.exe /K ping {=1}</c>, then "In: " and the working folder on a second line when there is one.</summary>
    public string RunsText {
      get {
        string arguments = RunCommandLine.JoinLines(this.ProgramFileName, RunCommandLine.ApplyKeepOpen(this.ProgramFileName, this.Arguments, this.KeepWindowOpen));
        string text = "Runs: " + RunCommandLine.CommandLine(this.ProgramFileName, arguments);
        if (this.RunAsAdministrator) {
          text += " (as administrator)";
        }
        // The working folder, when there is one, on a second line.
        return string.IsNullOrEmpty(this.WorkingFolder) ? text : text + "\nIn: " + this.WorkingFolder;
      }
    }
  }

  /// <summary>The examples offered by the Examples button of the Run Command window, grouped by program.</summary>
  public static class RunCommandExamples {
    private const string Cmd = "Command Prompt (cmd)";
    private const string PowerShell = "PowerShell";
    private const string Explorer = "Explorer";
    private const string Git = "Git";
    private const string Docker = "Docker";
    private const string Other = "Other";

    /// <summary>All examples, in display order (grouped by <see cref="RunCommandExample.Category"/>).</summary>
    public static IReadOnlyList<RunCommandExample> All { get; } = new List<RunCommandExample> {
      new RunCommandExample {
        Category = Cmd, Name = "Ping a computer", ProgramFileName = "cmd.exe", Arguments = "ping {=1}", KeepWindowOpen = true,
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
        Category = Cmd, Name = "Run a batch script with the group value", ProgramFileName = "cmd.exe", Arguments = "\"C:\\Scripts\\script.cmd\" {=1}", KeepWindowOpen = true,
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
        Category = PowerShell, Name = "Run a PowerShell script with the group value", ProgramFileName = "powershell.exe", Arguments = "-ExecutionPolicy Bypass -File \"C:\\Scripts\\script.ps1\" -Name {=1}", KeepWindowOpen = true,
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
        Category = Git, Name = "git clone https://...", ProgramFileName = "cmd.exe", WorkingFolder = "C:\\Repos", Arguments = "git clone https://github.com/MyName/MyRepo.git", KeepWindowOpen = true,
        Description = "Downloads a copy of a repository into a new folder; replace the address with the repository's."
      },
      new RunCommandExample {
        Category = Git, Name = "git remote", ProgramFileName = "cmd.exe", WorkingFolder = "C:\\Repos\\MyRepo", Arguments = "git remote", KeepWindowOpen = true,
        Description = "Lists the remote repositories this one is linked to."
      },
      new RunCommandExample {
        Category = Git, Name = "git remote show origin", ProgramFileName = "cmd.exe", WorkingFolder = "C:\\Repos\\MyRepo", Arguments = "git remote show origin", KeepWindowOpen = true,
        Description = "Shows the address of origin and which branches it tracks."
      },
      new RunCommandExample {
        Category = Git, Name = "git fetch", ProgramFileName = "cmd.exe", WorkingFolder = "C:\\Repos\\MyRepo", Arguments = "git fetch", KeepWindowOpen = true,
        Description = "Downloads the latest changes from the remote without changing your files."
      },
      new RunCommandExample {
        Category = Git, Name = "git checkout --track origin/BranchX", ProgramFileName = "cmd.exe", WorkingFolder = "C:\\Repos\\MyRepo", Arguments = "git checkout --track origin/BranchX", KeepWindowOpen = true,
        Description = "Creates a local branch that follows the remote branch BranchX and switches to it."
      },
      new RunCommandExample {
        Category = Git, Name = "git branch -l", ProgramFileName = "cmd.exe", WorkingFolder = "C:\\Repos\\MyRepo", Arguments = "git branch -l", KeepWindowOpen = true,
        Description = "Lists the local branches; the current one is marked with *."
      },
      new RunCommandExample {
        Category = Git, Name = "git checkout main", ProgramFileName = "cmd.exe", WorkingFolder = "C:\\Repos\\MyRepo", Arguments = "git checkout main", KeepWindowOpen = true,
        Description = "Switches to the main branch."
      },
      new RunCommandExample {
        Category = Git, Name = "git pull", ProgramFileName = "cmd.exe", WorkingFolder = "C:\\Repos\\MyRepo", Arguments = "git pull", KeepWindowOpen = true,
        Description = "Downloads the latest changes and merges them into the current branch."
      },
      new RunCommandExample {
        Category = Git, Name = "git status", ProgramFileName = "cmd.exe", WorkingFolder = "C:\\Repos\\MyRepo", Arguments = "git status", KeepWindowOpen = true,
        Description = "Shows which files are changed, staged or new."
      },
      new RunCommandExample {
        Category = Git, Name = "git diff", ProgramFileName = "cmd.exe", WorkingFolder = "C:\\Repos\\MyRepo", Arguments = "git diff", KeepWindowOpen = true,
        Description = "Shows the changes not staged yet, line by line."
      },
      new RunCommandExample {
        Category = Git, Name = "git log --oneline -20", ProgramFileName = "cmd.exe", WorkingFolder = "C:\\Repos\\MyRepo", Arguments = "git log --oneline -20", KeepWindowOpen = true,
        Description = "Shows the last 20 commits, one line each."
      },
      new RunCommandExample {
        Category = Git, Name = "git branch LocalWorkingBranch", ProgramFileName = "cmd.exe", WorkingFolder = "C:\\Repos\\MyRepo", Arguments = "git branch LocalWorkingBranch", KeepWindowOpen = true,
        Description = "Creates the branch LocalWorkingBranch without switching to it."
      },
      new RunCommandExample {
        Category = Git, Name = "git checkout LocalWorkingBranch", ProgramFileName = "cmd.exe", WorkingFolder = "C:\\Repos\\MyRepo", Arguments = "git checkout LocalWorkingBranch", KeepWindowOpen = true,
        Description = "Switches to the branch LocalWorkingBranch."
      },
      new RunCommandExample {
        Category = Git, Name = "git switch -c NewBranch", ProgramFileName = "cmd.exe", WorkingFolder = "C:\\Repos\\MyRepo", Arguments = "git switch -c NewBranch", KeepWindowOpen = true,
        Description = "Creates the branch NewBranch and switches to it."
      },
      new RunCommandExample {
        Category = Git, Name = "git stash", ProgramFileName = "cmd.exe", WorkingFolder = "C:\\Repos\\MyRepo", Arguments = "git stash", KeepWindowOpen = true,
        Description = "Puts your uncommitted changes aside, leaving the files as in the last commit."
      },
      new RunCommandExample {
        Category = Git, Name = "git stash pop", ProgramFileName = "cmd.exe", WorkingFolder = "C:\\Repos\\MyRepo", Arguments = "git stash pop", KeepWindowOpen = true,
        Description = "Brings back the changes last put aside with git stash."
      },
      new RunCommandExample {
        Category = Git, Name = "git branch -d LocalWorkingBranch", ProgramFileName = "cmd.exe", WorkingFolder = "C:\\Repos\\MyRepo", Arguments = "git branch -d LocalWorkingBranch", KeepWindowOpen = true,
        Description = "Deletes the branch LocalWorkingBranch, only if it's fully merged."
      },
      new RunCommandExample {
        Category = Git, Name = "git branch -D LocalWorkingBranch", ProgramFileName = "cmd.exe", WorkingFolder = "C:\\Repos\\MyRepo", Arguments = "git branch -D LocalWorkingBranch", KeepWindowOpen = true,
        Description = "Deletes the branch LocalWorkingBranch even if it has changes that aren't merged."
      },
      new RunCommandExample {
        Category = Docker, Name = "docker ps", ProgramFileName = "cmd.exe", Arguments = "docker ps", KeepWindowOpen = true,
        Description = "Lists the running containers."
      },
      new RunCommandExample {
        Category = Docker, Name = "docker ps -a", ProgramFileName = "cmd.exe", Arguments = "docker ps -a", KeepWindowOpen = true,
        Description = "Lists all containers, stopped ones too."
      },
      new RunCommandExample {
        Category = Docker, Name = "docker images", ProgramFileName = "cmd.exe", Arguments = "docker images", KeepWindowOpen = true,
        Description = "Lists the images on this PC."
      },
      new RunCommandExample {
        Category = Docker, Name = "docker start MyContainer", ProgramFileName = "cmd.exe", Arguments = "docker start MyContainer", KeepWindowOpen = true,
        Description = "Starts the stopped container MyContainer."
      },
      new RunCommandExample {
        Category = Docker, Name = "docker stop MyContainer", ProgramFileName = "cmd.exe", Arguments = "docker stop MyContainer", KeepWindowOpen = true,
        Description = "Stops the running container MyContainer."
      },
      new RunCommandExample {
        Category = Docker, Name = "docker logs MyContainer", ProgramFileName = "cmd.exe", Arguments = "docker logs MyContainer", KeepWindowOpen = true,
        Description = "Shows what the container MyContainer has written to its log."
      },
      new RunCommandExample {
        Category = Docker, Name = "docker compose up -d", ProgramFileName = "cmd.exe", WorkingFolder = "C:\\Repos\\MyRepo", Arguments = "docker compose up -d", KeepWindowOpen = true,
        Description = "Starts the services of the folder's compose file in the background."
      },
      new RunCommandExample {
        Category = Docker, Name = "docker compose down", ProgramFileName = "cmd.exe", WorkingFolder = "C:\\Repos\\MyRepo", Arguments = "docker compose down", KeepWindowOpen = true,
        Description = "Stops and removes the services started with docker compose up."
      },
      new RunCommandExample {
        Category = Other, Name = "Refresh shortcut thumbnails", ProgramFileName = "ie4uinit.exe", Arguments = "-show",
        Description = "Rebuilds the Windows icon cache, which fixes blank or wrong shortcut icons."
      }
    };
  }
}
