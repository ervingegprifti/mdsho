using System;
using System.Collections.Generic;
using System.IO;

namespace MDSHO.Common {

  /// <summary>
  /// The sync lab, for developers: <c>--profile &lt;name&gt;</c> runs a copy of MDSHO with its own data folder
  /// (<c>%LOCALAPPDATA%\MDSHO-lab\&lt;name&gt;</c>), single-instance mutex, device name and title, so several copies can
  /// sync with each other on one PC without touching the real data. <c>--sync-every &lt;seconds&gt;</c> shortens the sync
  /// intervals and <c>--sync-folder &lt;path&gt;</c> turns sync on to that folder at start; both only with a profile. The
  /// MSIX package ignores every lab option (see tools/sync-lab.ps1 and docs/building.md).
  /// </summary>
  public sealed class LabOptions {
    /// <summary>The folder under %LOCALAPPDATA% that holds every lab profile and the lab sync folder.</summary>
    public const string LabFolderName = "MDSHO-lab";
    /// <summary>The longest profile name.</summary>
    public const int MaxNameLength = 32;

    /// <summary>A normal run.</summary>
    public static readonly LabOptions None = new LabOptions(null, null, null);

    /// <summary>The options this run started with (the app sets them first thing at start).</summary>
    public static LabOptions Current { get; set; } = None;

    private LabOptions(string profile, TimeSpan? syncEvery, string syncFolder) {
      Profile = profile;
      SyncEvery = syncEvery;
      SyncFolder = syncFolder;
    }

    /// <summary>The profile name, or null for a normal run.</summary>
    public string Profile { get; }
    /// <summary>The shorter sync check and read interval, or null.</summary>
    public TimeSpan? SyncEvery { get; }
    /// <summary>The sync folder to turn sync on to at start, or null.</summary>
    public string SyncFolder { get; }
    /// <summary>True when this run is a lab profile.</summary>
    public bool IsLab => Profile != null;

    /// <summary>The lab root: %LOCALAPPDATA%\MDSHO-lab.</summary>
    public static string LabRoot => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), LabFolderName);

    /// <summary>This profile's data folder, or null for a normal run.</summary>
    public string DataFolder => IsLab ? Path.Combine(LabRoot, Profile) : null;

    /// <summary>Shown after window titles and in the tray tooltip: " [name]", or "" for a normal run.</summary>
    public string TitleSuffix => IsLab ? " [" + Profile + "]" : "";

    /// <summary>The single-instance mutex name: the normal one, plus the profile for a lab run.</summary>
    public string MutexName(string normal) => IsLab ? normal + "-lab-" + Profile : normal;

    /// <summary>The options as command line arguments, to start the same profile again ("" for a normal run).</summary>
    public string CommandLine {
      get {
        if (!IsLab) {
          return "";
        }
        List<string> parts = new List<string> { "--profile", Profile };
        if (SyncEvery != null) {
          parts.Add("--sync-every");
          parts.Add(((long)SyncEvery.Value.TotalSeconds).ToString(System.Globalization.CultureInfo.InvariantCulture));
        }
        if (SyncFolder != null) {
          parts.Add("--sync-folder");
          parts.Add(SyncFolder);
        }
        return string.Join(" ", parts.ConvertAll(Quote));
      }
    }

    /// <summary>True for 1 to <see cref="MaxNameLength"/> ASCII letters, digits and dashes.</summary>
    public static bool IsValidName(string name) {
      if (string.IsNullOrEmpty(name) || name.Length > MaxNameLength) {
        return false;
      }
      foreach (char c in name) {
        if (!((c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9') || c == '-')) {
          return false;
        }
      }
      return true;
    }

    // One command line argument, quoted when it has a space or quote (a path never ends in a backslash here).
    private static string Quote(string arg) {
      return arg.IndexOfAny(new[] { ' ', '\t', '"' }) < 0 ? arg : "\"" + arg.Replace("\"", "\\\"") + "\"";
    }

    /// <summary>Reads the lab options; a packaged run (the MSIX) and invalid values give a normal run.</summary>
    public static LabOptions Parse(IReadOnlyList<string> args, bool isPackaged) {
      if (isPackaged || args == null) {
        return None;
      }
      string profile = null, every = null, folder = null;
      for (int i = 0; i + 1 < args.Count; i++) {
        string name = args[i];
        if (string.Equals(name, "--profile", StringComparison.OrdinalIgnoreCase)) {
          profile = args[++i];
        } else if (string.Equals(name, "--sync-every", StringComparison.OrdinalIgnoreCase)) {
          every = args[++i];
        } else if (string.Equals(name, "--sync-folder", StringComparison.OrdinalIgnoreCase)) {
          folder = args[++i];
        }
      }
      if (!IsValidName(profile)) {
        return None;
      }
      TimeSpan? syncEvery = null;
      if (int.TryParse(every, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out int seconds) && seconds > 0) {
        syncEvery = TimeSpan.FromSeconds(seconds);
      }
      return new LabOptions(profile, syncEvery, string.IsNullOrWhiteSpace(folder) ? null : folder);
    }
  }
}
