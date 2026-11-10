using System;
using System.IO;
using System.Runtime.InteropServices;
using MDSHO.Common;

// Windows Script Host (WScript.Shell) is used late-bound via 'dynamic' instead of a COMReference
// to IWshRuntimeLibrary, because COMReference is not supported by the .NET (dotnet CLI) MSBuild.

namespace MDSHO.Infrastructure {

  /// <summary>Writes .lnk (file or folder) and .url (Internet) shortcut files.</summary>
  public sealed class ShellLinkWriter : IShellLinkWriter {

    /// <summary>Writes a .lnk file pointing at the target, opening in a normal window, with the given working folder.</summary>
    public void CreateLnk(string lnkFilePath, string targetPath, string workingFolder) {
      // The COM interface IShellLink requires admin access which end users may not have; the late-bound Windows Script Host
      // object below works without it. (https://stackoverflow.com/questions/2934420)
      Type wshShellType = Type.GetTypeFromProgID("WScript.Shell", throwOnError: true);
      dynamic wshShell = Activator.CreateInstance(wshShellType);
      dynamic wshShortcut = null;
      try {
        // CreateShortcut only creates the object in memory; nothing exists on disk until Save is called.
        wshShortcut = wshShell.CreateShortcut(lnkFilePath);
        wshShortcut.TargetPath = targetPath;
        // WindowStyle 1 = activate and display the window (restore it if it is minimised or maximised).
        wshShortcut.WindowStyle = 1;
        // Without a working directory unexpected results might happen.
        wshShortcut.WorkingDirectory = workingFolder;
        wshShortcut.Save();
      } finally {
        if (wshShortcut != null) Marshal.FinalReleaseComObject(wshShortcut);
        Marshal.FinalReleaseComObject(wshShell);
      }
    }

    /// <summary>Writes a .url (Internet shortcut) file for the address.</summary>
    public void CreateUrl(string urlFilePath, string url) {
      using (StreamWriter writer = new StreamWriter(urlFilePath)) {
        writer.WriteLine("[InternetShortcut]");
        writer.WriteLine("URL=" + url);
        writer.WriteLine("IDList=");
        writer.WriteLine("HotKey="); // 0
        writer.WriteLine("IconFile="); // .ico file
        writer.WriteLine("IconIndex="); // 0
        writer.Flush();
      }
    }
  }
}
