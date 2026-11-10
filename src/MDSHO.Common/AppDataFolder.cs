using System;
using System.IO;

namespace MDSHO.Common {

  /// <summary>
  /// Finds the folder that really holds the database and the shortcuts, for "Browse app data".<br />
  /// When MDSHO runs as a package, Windows may silently redirect writes to %LOCALAPPDATA%\MDSHO into the package's
  /// private LocalCache\Local\MDSHO folder (unless the real folder existed before the install). Explorer runs outside the
  /// package and would show the real (possibly empty) folder, so the redirected folder is returned when it exists.
  /// </summary>
  public static class AppDataFolder {

    /// <summary>Decides which folder to open.</summary>
    /// <param name="hasPackageIdentity">True when the app runs as a package.</param>
    /// <param name="packageLocalCacheFolder">The package's LocalCache folder; only called when packaged. Null or an exception = not available.</param>
    /// <param name="directoryExists">Existence check (Directory.Exists in the app).</param>
    /// <param name="databaseFilePath">The database file path the app uses.</param>
    /// <param name="shortcutsFolder">The shortcuts folder the app uses.</param>
    /// <returns>The redirected package folder when packaged and it exists, otherwise the folder that contains both the database and the shortcuts folder.</returns>
    public static string Locate(bool hasPackageIdentity, Func<string> packageLocalCacheFolder, Func<string, bool> directoryExists, string databaseFilePath, string shortcutsFolder) {
      if (hasPackageIdentity) {
        string cache;
        try {
          cache = packageLocalCacheFolder();
        } catch (Exception) {
          cache = null;
        }
        if (!string.IsNullOrEmpty(cache)) {
          string redirected = Path.Combine(cache, "Local", AppConstants.ApplicationFolderName);
          if (directoryExists(redirected)) {
            return redirected;
          }
        }
      }
      return CommonFolder(Path.GetDirectoryName(databaseFilePath), shortcutsFolder);
    }

    /// <summary>The deepest folder that contains both folders (case-insensitive, whole folder names only).</summary>
    public static string CommonFolder(string first, string second) {
      string[] a = Trim(first).Split(Path.DirectorySeparatorChar);
      string[] b = Trim(second).Split(Path.DirectorySeparatorChar);
      int count = 0;
      while (count < a.Length && count < b.Length && string.Equals(a[count], b[count], StringComparison.OrdinalIgnoreCase)) {
        count++;
      }
      if (count == 0) {
        return Trim(first);
      }
      string common = string.Join(Path.DirectorySeparatorChar.ToString(), a, 0, count);
      // A drive root keeps its separator ("X:" is the current folder on drive X, "X:\" is the root).
      if (common.EndsWith(Path.VolumeSeparatorChar.ToString(), StringComparison.Ordinal)) {
        common += Path.DirectorySeparatorChar;
      }
      return common;
    }

    private static string Trim(string folder) {
      return folder.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar).TrimEnd(Path.DirectorySeparatorChar);
    }
  }
}
