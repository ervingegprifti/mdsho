using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace MDSHO.Common {

  /// <summary>
  /// The folders a portable path can start with, by token (for example %DESKTOP%), and where each one is on a PC.
  /// A folder this PC doesn't have is left out.
  /// </summary>
  public sealed class KnownFolders {
    /// <summary>The user's profile folder.</summary>
    public const string UserProfile = "%USERPROFILE%";
    /// <summary>The Desktop folder.</summary>
    public const string Desktop = "%DESKTOP%";
    /// <summary>The Documents folder.</summary>
    public const string Documents = "%DOCUMENTS%";
    /// <summary>The Downloads folder.</summary>
    public const string Downloads = "%DOWNLOADS%";
    /// <summary>The Pictures folder.</summary>
    public const string Pictures = "%PICTURES%";
    /// <summary>The Music folder.</summary>
    public const string Music = "%MUSIC%";
    /// <summary>The Videos folder.</summary>
    public const string Videos = "%VIDEOS%";
    /// <summary>The OneDrive folder.</summary>
    public const string OneDrive = "%ONEDRIVE%";
    /// <summary>C:\Program Files.</summary>
    public const string ProgramFiles = "%PROGRAMFILES%";
    /// <summary>C:\Program Files (x86).</summary>
    public const string ProgramFilesX86 = "%PROGRAMFILES(X86)%";
    /// <summary>The Windows folder.</summary>
    public const string Windows = "%WINDIR%";

    /// <summary>Every token, in no particular order.</summary>
    public static readonly string[] Tokens = { UserProfile, Desktop, Documents, Downloads, Pictures, Music, Videos, OneDrive, ProgramFiles, ProgramFilesX86, Windows };

    private readonly Dictionary<string, string> paths = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    /// <summary>The folders by token; unknown tokens and empty paths are ignored. Paths lose a trailing backslash.</summary>
    public KnownFolders(IDictionary<string, string> paths) {
      foreach (KeyValuePair<string, string> pair in paths) {
        if (Tokens.Contains(pair.Key, StringComparer.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(pair.Value)) {
          this.paths[pair.Key] = pair.Value.TrimEnd('\\', '/');
        }
      }
    }

    /// <summary>The folders of this PC and user.</summary>
    public static KnownFolders Current() {
      string profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
      return new KnownFolders(new Dictionary<string, string> {
        [UserProfile] = profile,
        [Desktop] = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
        [Documents] = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
        // .NET has no Downloads folder; Windows keeps it in the profile unless the user moved it.
        [Downloads] = string.IsNullOrEmpty(profile) ? "" : Path.Combine(profile, "Downloads"),
        [Pictures] = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures),
        [Music] = Environment.GetFolderPath(Environment.SpecialFolder.MyMusic),
        [Videos] = Environment.GetFolderPath(Environment.SpecialFolder.MyVideos),
        [OneDrive] = Environment.GetEnvironmentVariable("OneDrive") ?? "",
        [ProgramFiles] = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
        [ProgramFilesX86] = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
        [Windows] = Environment.GetFolderPath(Environment.SpecialFolder.Windows),
      });
    }

    /// <summary>The folder of a token on this PC, or null when it has none.</summary>
    public string PathOf(string token) {
      return token != null && this.paths.TryGetValue(token, out string path) ? path : null;
    }

    /// <summary>The token and folder pairs, the longest folder first (the most specific match wins).</summary>
    internal IEnumerable<KeyValuePair<string, string>> LongestFirst() {
      return this.paths.OrderByDescending(p => p.Value.Length);
    }
  }

  /// <summary>
  /// Turns absolute paths into portable ones that start with a known folder token (%DESKTOP%\a.lnk), and back on another
  /// PC. Pure: the folders are given. Anything else (web addresses, network paths, other drives) is left as it is.
  /// </summary>
  public static class PortablePaths {

    /// <summary>The path with its most specific known folder replaced by the token; unchanged when none matches.</summary>
    public static string ToPortable(string path, KnownFolders folders) {
      if (string.IsNullOrEmpty(path) || path.StartsWith("%", StringComparison.Ordinal)) {
        return path;
      }
      foreach (KeyValuePair<string, string> folder in folders.LongestFirst()) {
        if (StartsWithFolder(path, folder.Value)) {
          return folder.Key + path.Substring(folder.Value.Length);
        }
      }
      return path;
    }

    /// <summary>The portable path with its token replaced by that folder on this PC; unchanged when there is none.</summary>
    public static string FromPortable(string portable, KnownFolders folders) {
      if (string.IsNullOrEmpty(portable) || !portable.StartsWith("%", StringComparison.Ordinal)) {
        return portable;
      }
      int end = portable.IndexOf('%', 1);
      if (end < 0) {
        return portable;
      }
      string token = portable.Substring(0, end + 1);
      string rest = portable.Substring(end + 1);
      if (rest.Length > 0 && rest[0] != '\\' && rest[0] != '/') {
        return portable;
      }
      string folder = folders.PathOf(token);
      return folder == null ? portable : folder + rest;
    }

    // True when the path is the folder or inside it (case-insensitive, on a folder boundary).
    private static bool StartsWithFolder(string path, string folder) {
      if (!path.StartsWith(folder, StringComparison.OrdinalIgnoreCase)) {
        return false;
      }
      return path.Length == folder.Length || path[folder.Length] == '\\' || path[folder.Length] == '/';
    }
  }
}
