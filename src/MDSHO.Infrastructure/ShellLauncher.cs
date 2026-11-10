using System;
using System.Diagnostics;
using MDSHO.Common;

namespace MDSHO.Infrastructure {

  /// <summary>Starts processes, web links, Explorer folders and programs through Windows.</summary>
  public sealed class ShellLauncher : IShellLauncher {
    private readonly Action<ProcessStartInfo> start;

    /// <summary>Starts real processes.</summary>
    public ShellLauncher() : this(psi => { using (Process.Start(psi)) { } }) { }

    /// <summary>Starts processes through <paramref name="start"/> (tests pass a recorder).</summary>
    public ShellLauncher(Action<ProcessStartInfo> start) {
      this.start = start;
    }

    /// <summary>Starts a process exactly as described.</summary>
    public void Start(ProcessStartInfo startInfo) {
      start(startInfo);
    }

    /// <summary>Opens a web address in the default browser.</summary>
    public void OpenUrl(string url) {
      start(new ProcessStartInfo { FileName = url, UseShellExecute = true });
    }

    /// <summary>Opens a folder in Explorer.</summary>
    public void OpenFolder(string path) {
      Run("explorer.exe", "\"" + path + "\"");
    }

    /// <summary>Starts a program with arguments through the shell.</summary>
    public void Run(string fileName, string arguments) {
      start(new ProcessStartInfo(fileName, arguments) { UseShellExecute = true });
    }
  }
}
