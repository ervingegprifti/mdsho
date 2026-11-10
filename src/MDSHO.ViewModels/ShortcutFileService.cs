using System;
using System.IO;
using MDSHO.Common;

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
    public static void DeleteShortcutFiles(ShortcutViewModel shortcut) {
      if (shortcut.IsGroup) {
        foreach (ShortcutViewModel childShortcut in shortcut.Children) {
          string shortcutFilePath = Paths.GetShortcutFilePath(childShortcut.Guid, childShortcut.Ext);
          if (File.Exists(shortcutFilePath)) {
            File.Delete(shortcutFilePath);
          }
          DeleteShortcutFiles(childShortcut);
        }
      } else {
        string shortcutFilePath = Paths.GetShortcutFilePath(shortcut.Guid, shortcut.Ext);
        if (File.Exists(shortcutFilePath)) {
          File.Delete(shortcutFilePath);
        }
      }
    }
  }

  /// <summary>Used until the app sets a real writer: fails loudly instead of silently writing nothing.</summary>
  internal sealed class NotConfiguredLinkWriter : IShellLinkWriter {
    public void CreateLnk(string lnkFilePath, string targetPath, string workingFolder) { throw new InvalidOperationException("ShortcutFileService.LinkWriter is not set."); }
    public void CreateUrl(string urlFilePath, string url) { throw new InvalidOperationException("ShortcutFileService.LinkWriter is not set."); }
  }

}
