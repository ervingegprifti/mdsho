using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MDSHO.Common;
using MDSHO.Core;
using MDSHO.Data;

namespace MDSHO.ViewModels {

  /// <summary>
  /// Creates and deletes the shortcut files MDSHO keeps in its shortcuts folder: .lnk files (file/folder shortcuts) and
  /// .url files (Internet shortcuts). Each file is named after the shortcut's GUID. The files are written by
  /// <see cref="LinkWriter"/>, which the app sets at startup.
  /// </summary>
  public static class ShortcutFileService {

    /// <summary>Writes the .lnk and .url files. Set once by the composition root; tests may replace it.</summary>
    public static IShellLinkWriter LinkWriter { get; set; } = new NotConfiguredLinkWriter();

    /// <summary>
    /// Creates <c>shortcuts folder\&lt;fileName&gt;.lnk</c> pointing at the target. Does nothing when the file already exists.
    /// </summary>
    /// <param name="shortcutLnkFileName">File name without extension (the shortcut's GUID).</param>
    /// <param name="targetPath">The file or folder the shortcut points at.</param>
    public static void CreateLnkFile(string shortcutLnkFileName, string targetPath) {
      string shortcutsPath = Paths.GetShortcutsPath();
      string shortcutLnkFilePath = Path.Combine(shortcutsPath, shortcutLnkFileName + AppConstants.LnkExtension);

      if (File.Exists(shortcutLnkFilePath)) {
        // The shortcut is already created. No need to create the shortcut.
        return;
      }
      // Without a working directory unexpected results might happen: use the folder the .lnk is in.
      LinkWriter.CreateLnk(shortcutLnkFilePath, targetPath, shortcutsPath);
    }

    /// <summary>
    /// Creates <c>shortcuts folder\&lt;fileName&gt;.url</c> (an Internet shortcut) for the address. Does nothing when the file already exists.
    /// </summary>
    /// <param name="shortcutUrlFileName">File name without extension (the shortcut's GUID).</param>
    /// <param name="targetUrl">The web address.</param>
    public static void CreateUrlFile(string shortcutUrlFileName, string targetUrl) {
      string shortcutUrlFilePath = Paths.GetShortcutFilePath(shortcutUrlFileName, AppConstants.UrlExtension);

      if (File.Exists(shortcutUrlFilePath)) {
        // The shortcut is already created. No need to create the shortcut.
        return;
      }
      LinkWriter.CreateUrl(shortcutUrlFilePath, targetUrl);
    }

    /// <summary>
    /// Deletes the physical files of a shortcut. For a group the files of every descendant are deleted (the group itself has no file).
    /// </summary>
    /// <remarks>
    /// Runs after the delete is saved, so a file in use never fails it: that file is logged and left, and the next start
    /// deletes it (<see cref="DeleteUnusedFiles"/>).
    /// </remarks>
    public static void DeleteShortcutFiles(ShortcutViewModel shortcut) {
      if (shortcut.IsGroup) {
        foreach (ShortcutViewModel childShortcut in shortcut.Children) {
          DeleteQuietly(Paths.GetShortcutFilePath(childShortcut.Guid, childShortcut.Ext));
          DeleteShortcutFiles(childShortcut);
        }
      } else {
        DeleteQuietly(Paths.GetShortcutFilePath(shortcut.Guid, shortcut.Ext));
      }
    }

    /// <summary>
    /// Deletes the .lnk and .url files in the shortcuts folder that no button uses (left over when a file was in use at
    /// delete time). Does nothing when there are no buttons at all, since that can be a fresh start after a problem.
    /// A file in use is only logged. Returns how many files were deleted.
    /// </summary>
    public static int DeleteUnusedFiles() {
      string folder = Paths.GetShortcutsPath();
      List<Shortcut> shortcuts = DataStore.Shortcuts.GetAll();
      if (shortcuts.Count == 0 || !Directory.Exists(folder)) {
        return 0;
      }
      HashSet<string> used = new HashSet<string>(
        shortcuts.Where(s => !string.IsNullOrEmpty(s.Guid)).Select(s => s.Guid + s.Ext), StringComparer.OrdinalIgnoreCase);
      int deleted = 0;
      foreach (string file in Directory.GetFiles(folder)) {
        string ext = Path.GetExtension(file);
        bool isShortcutFile = ext.Equals(AppConstants.LnkExtension, StringComparison.OrdinalIgnoreCase)
          || ext.Equals(AppConstants.UrlExtension, StringComparison.OrdinalIgnoreCase);
        if (isShortcutFile && !used.Contains(Path.GetFileName(file)) && DeleteQuietly(file)) {
          deleted++;
        }
      }
      if (deleted > 0) {
        AppLog.Information("Deleted {Count} shortcut files that no button uses", deleted);
      }
      return deleted;
    }

    // Deletes a file when it exists. A file in use is only logged. Returns true when a file was deleted.
    private static bool DeleteQuietly(string path) {
      try {
        if (!File.Exists(path)) {
          return false;
        }
        File.Delete(path);
        return true;
      } catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException) {
        AppLog.Warning(ex, "A shortcut file could not be deleted: {File}", Path.GetFileName(path));
        return false;
      }
    }
  }

  /// <summary>Used until the app sets a real writer: fails loudly instead of silently writing nothing.</summary>
  internal sealed class NotConfiguredLinkWriter : IShellLinkWriter {
    public void CreateLnk(string lnkFilePath, string targetPath, string workingFolder) { throw new InvalidOperationException("ShortcutFileService.LinkWriter is not set."); }
    public void CreateUrl(string urlFilePath, string url) { throw new InvalidOperationException("ShortcutFileService.LinkWriter is not set."); }
  }

}
