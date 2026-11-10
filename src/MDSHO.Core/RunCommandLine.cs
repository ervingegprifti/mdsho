using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace MDSHO.Core {

  /// <summary>The program families a run command knows about: they decide how lines are joined and the "keep window open" switch.</summary>
  public enum RunCommandProgramKind {
    /// <summary>Command Prompt (cmd.exe): lines joined with <c> &amp; </c>, keep open with <c>/K</c> (otherwise <c>/C</c>).</summary>
    Cmd,
    /// <summary>Windows PowerShell or PowerShell 7 (powershell.exe, pwsh.exe): lines joined with <c>; </c>, keep open with <c>-NoExit</c>.</summary>
    PowerShell,
    /// <summary>Any other program: lines joined with a space, no keep-open switch.</summary>
    Other
  }

  /// <summary>A program offered in the Program list of the Run Command window.</summary>
  public sealed class RunCommandProgram {
    /// <summary>Creates a list entry.</summary>
    public RunCommandProgram(string fileName, string friendlyName) {
      this.FileName = fileName;
      this.FriendlyName = friendlyName;
    }
    /// <summary>The file name that is run, e.g. <c>cmd.exe</c>.</summary>
    public string FileName { get; }
    /// <summary>The plain-English name shown beside it, e.g. <c>Command Prompt</c>.</summary>
    public string FriendlyName { get; }
  }

  /// <summary>
  /// The rules for turning what the user typed in the Run Command window into what runs. The arguments are stored
  /// exactly as typed (line breaks included); at run time empty lines are skipped, each line is trimmed and the lines are
  /// joined in the way the program understands. The "keep window open" choice is not a database field: it is the
  /// leading <c>/K</c> or <c>/C</c> (cmd) or <c>-NoExit</c> (PowerShell) of the arguments.
  /// </summary>
  public static class RunCommandLine {
    /// <summary>The program a new run command starts with.</summary>
    public const string DefaultProgram = "cmd.exe";

    /// <summary>The programs offered in the Program list, in display order.</summary>
    public static IReadOnlyList<RunCommandProgram> KnownPrograms { get; } = new List<RunCommandProgram> {
      new RunCommandProgram("cmd.exe", "Command Prompt"),
      new RunCommandProgram("powershell.exe", "Windows PowerShell"),
      new RunCommandProgram("pwsh.exe", "PowerShell 7"),
      new RunCommandProgram("wt.exe", "Windows Terminal"),
      new RunCommandProgram("explorer.exe", "File Explorer"),
      new RunCommandProgram("notepad.exe", "Notepad")
    };

    /// <summary>Finds the program family from a file name, with or without a path, quotes or <c>.exe</c> (case-insensitive).</summary>
    public static RunCommandProgramKind GetProgramKind(string fileName) {
      string name = (fileName ?? "").Trim().Trim('"').Trim();
      if (name.Length == 0) {
        return RunCommandProgramKind.Other;
      }
      try {
        name = Path.GetFileName(name);
      } catch (ArgumentException) {
        return RunCommandProgramKind.Other;
      }
      if (name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) {
        name = name.Substring(0, name.Length - 4);
      }
      if (string.Equals(name, "cmd", StringComparison.OrdinalIgnoreCase)) {
        return RunCommandProgramKind.Cmd;
      }
      if (string.Equals(name, "powershell", StringComparison.OrdinalIgnoreCase) || string.Equals(name, "pwsh", StringComparison.OrdinalIgnoreCase)) {
        return RunCommandProgramKind.PowerShell;
      }
      return RunCommandProgramKind.Other;
    }

    /// <summary>True when the program has a "keep window open" switch (cmd and PowerShell).</summary>
    public static bool SupportsKeepOpen(string fileName) {
      return GetProgramKind(fileName) != RunCommandProgramKind.Other;
    }

    /// <summary>
    /// Joins the argument lines into one line: empty lines are skipped, each line is trimmed, and the lines are joined
    /// with <c> &amp; </c> for cmd, <c>; </c> for PowerShell and a space for anything else. A leading keep-open switch
    /// (<c>/K</c>, <c>/C</c>, <c>-NoExit</c>) is followed by a space, never by the separator, even when the first line
    /// was empty. Null stays null.
    /// </summary>
    public static string JoinLines(string fileName, string arguments) {
      if (arguments == null) {
        return null;
      }
      RunCommandProgramKind kind = GetProgramKind(fileName);
      string separator;
      switch (kind) {
        case RunCommandProgramKind.Cmd: separator = " & "; break;
        case RunCommandProgramKind.PowerShell: separator = "; "; break;
        default: separator = " "; break;
      }
      string switchText = TakeLeadingSwitch(kind, ref arguments);
      IEnumerable<string> lines = arguments.Split(new[] { "\r\n", "\n", "\r" }, StringSplitOptions.None)
        .Select(line => line.Trim())
        .Where(line => line.Length > 0);
      string joined = string.Join(separator, lines);
      if (switchText == null) {
        return joined;
      }
      return joined.Length == 0 ? switchText : switchText + " " + joined;
    }

    // The keep-open switch at the start of the arguments, as typed, and the arguments without it; null when there is none.
    private static string TakeLeadingSwitch(RunCommandProgramKind kind, ref string arguments) {
      string[] switches = kind == RunCommandProgramKind.Cmd ? new[] { "/K", "/C" }
        : kind == RunCommandProgramKind.PowerShell ? new[] { "-NoExit" }
        : new string[0];
      foreach (string switchText in switches) {
        if (TryTakeSwitch(arguments, switchText, out string rest)) {
          string typed = arguments.TrimStart().Substring(0, switchText.Length);
          arguments = rest;
          return typed;
        }
      }
      return null;
    }

    /// <summary>
    /// Reads the "keep window open" switch off the start of stored arguments and returns the arguments without it.
    /// cmd: a leading <c>/K</c> (ticked) or <c>/C</c> (not ticked); PowerShell: a leading <c>-NoExit</c> (ticked);
    /// both case-insensitive and only as a whole word. Other programs: not ticked, arguments unchanged.
    /// </summary>
    public static string SplitKeepOpen(string fileName, string arguments, out bool keepOpen) {
      keepOpen = false;
      if (arguments == null) {
        return "";
      }
      RunCommandProgramKind kind = GetProgramKind(fileName);
      if (kind == RunCommandProgramKind.Cmd) {
        if (TryTakeSwitch(arguments, "/K", out string rest)) {
          keepOpen = true;
          return rest;
        }
        if (TryTakeSwitch(arguments, "/C", out rest)) {
          return rest;
        }
      } else if (kind == RunCommandProgramKind.PowerShell) {
        if (TryTakeSwitch(arguments, "-NoExit", out string rest)) {
          keepOpen = true;
          return rest;
        }
      }
      return arguments;
    }

    // The switch must be the first word: followed by white space (a line break too) or the end.
    private static bool TryTakeSwitch(string arguments, string switchText, out string rest) {
      rest = null;
      string trimmed = arguments.TrimStart();
      if (!trimmed.StartsWith(switchText, StringComparison.OrdinalIgnoreCase)) {
        return false;
      }
      if (trimmed.Length > switchText.Length && !char.IsWhiteSpace(trimmed[switchText.Length])) {
        return false;
      }
      rest = trimmed.Substring(switchText.Length).TrimStart();
      return true;
    }

    /// <summary>
    /// Puts the "keep window open" switch back in front of the arguments for saving: cmd <c>/K </c> or <c>/C </c>,
    /// PowerShell <c>-NoExit </c> when ticked. Other programs get the arguments unchanged.
    /// </summary>
    public static string ApplyKeepOpen(string fileName, string arguments, bool keepOpen) {
      arguments = arguments ?? "";
      string switchText = null;
      switch (GetProgramKind(fileName)) {
        case RunCommandProgramKind.Cmd: switchText = keepOpen ? "/K" : "/C"; break;
        case RunCommandProgramKind.PowerShell: switchText = keepOpen ? "-NoExit" : null; break;
      }
      if (switchText == null) {
        return arguments;
      }
      return arguments.Length == 0 ? switchText : switchText + " " + arguments;
    }

    /// <summary>The program (in quotes when it contains a space) followed by the arguments; empty when both are empty.</summary>
    public static string CommandLine(string fileName, string arguments) {
      string program = (fileName ?? "").Trim();
      if (program.Contains(' ') && !program.StartsWith("\"")) {
        program = "\"" + program + "\"";
      }
      return (program + " " + (arguments ?? "")).Trim();
    }

    /// <summary>
    /// The "Will run as" text: the command line that runs (lines joined, <c>{}</c> filled with the group value), then
    /// "In folder: ..." when a working folder is set and "As administrator" when it runs elevated, each on its own line.
    /// </summary>
    /// <param name="fileName">The program.</param>
    /// <param name="storedArguments">The arguments as they are saved (switch included).</param>
    /// <param name="groupValue">The value of the parent group, or null.</param>
    /// <param name="workingFolder">The working folder, or empty.</param>
    /// <param name="runAsAdministrator">True when it runs elevated.</param>
    public static string Describe(string fileName, string storedArguments, string groupValue, string workingFolder, bool runAsAdministrator) {
      StringBuilder text = new StringBuilder(CommandLine(fileName, JoinLines(fileName, GroupValue.ResolveArguments(storedArguments ?? "", groupValue))));
      if (!string.IsNullOrWhiteSpace(workingFolder)) {
        text.Append(Environment.NewLine).Append("In folder: ").Append(GroupValue.ResolveArguments(workingFolder.Trim(), groupValue));
      }
      if (runAsAdministrator) {
        text.Append(Environment.NewLine).Append("As administrator");
      }
      return text.ToString();
    }
  }
}
