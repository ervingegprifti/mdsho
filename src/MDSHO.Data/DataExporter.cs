using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text.Json;
using MDSHO.Common;
using MDSHO.Core;

namespace MDSHO.Data {

  /// <summary>
  /// Exports all MDSHO data (windows, clones, shortcuts and the shortcut files) to one zip file:
  /// <c>manifest.json</c>, <c>data.json</c> and <c>shortcuts/&lt;guid&gt;&lt;ext&gt;</c>.
  /// </summary>
  public static class DataExporter {

    /// <summary>MDSHO-export-yyyy-MM-dd-HHmm.zip</summary>
    public static string DefaultFileName(DateTime localNow) {
      return "MDSHO-export-" + localNow.ToString("yyyy-MM-dd-HHmm", System.Globalization.CultureInfo.InvariantCulture) + ".zip";
    }

    /// <summary>The application version as Major.Minor.Build (1.1.0).</summary>
    internal static string AppVersion() {
      Version v = typeof(DataExporter).Assembly.GetName().Version;
      return v.Major + "." + v.Minor + "." + Math.Max(v.Build, 0);
    }

    /// <summary>
    /// Reads everything in one transaction scope (a consistent snapshot), writes the zip to a temporary file next to the
    /// target and moves it into place. An existing target is replaced. Missing shortcut files are listed in the manifest.
    /// </summary>
    public static ExportManifest Export(string zipPath) {
      AppLog.Information("Export started to {ZipPath}", zipPath);
      try {
        ExportManifest manifest = ExportCore(zipPath);
        AppLog.Information("Export finished: {Windows} windows, {Clones} clones, {Shortcuts} shortcuts, {Files} files ({MissingFiles} missing)",
          manifest.Counts.Windows, manifest.Counts.Clones, manifest.Counts.Shortcuts, manifest.Counts.Files, manifest.MissingFiles.Count);
        return manifest;
      } catch (Exception ex) {
        AppLog.Error(ex, "Export failed");
        throw;
      }
    }

    private static ExportManifest ExportCore(string zipPath) {
      DataFile data = new DataFile();
      using (ITransactionScope tx = DataStore.BeginTransaction()) {
        data.Windows = DataStore.Windows.GetAll();
        data.Clones = DataStore.Clones.GetAll();
        data.Shortcuts = DataStore.Shortcuts.GetAll();
        tx.Commit();
      }
      string shortcutsPath = Paths.GetShortcutsPath();
      ExportManifest manifest = new ExportManifest {
        Format = ExportManifest.FormatName,
        FormatVersion = ExportManifest.CurrentFormatVersion,
        AppVersion = AppVersion(),
        SchemaVersion = AppConstants.DatabaseVersion,
        ExportedAtUtc = DateTime.UtcNow
      };
      manifest.Counts.Windows = data.Windows.Count;
      manifest.Counts.Clones = data.Clones.Count;
      manifest.Counts.Shortcuts = data.Shortcuts.Count;
      string tempPath = zipPath + ".tmp";
      try {
        using (FileStream fs = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None))
        using (ZipArchive zip = new ZipArchive(fs, ZipArchiveMode.Create)) {
          HashSet<string> done = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
          foreach (Shortcut s in data.Shortcuts) {
            if (string.IsNullOrEmpty(s.Guid) || string.IsNullOrEmpty(s.Ext)) {
              continue;
            }
            string name = s.Guid + s.Ext;
            if (!done.Add(name)) {
              continue;
            }
            try {
              zip.CreateEntryFromFile(Path.Combine(shortcutsPath, name), PortabilityJson.FilesPrefix + name, CompressionLevel.Optimal);
              manifest.Counts.Files++;
            } catch (FileNotFoundException) {
              manifest.MissingFiles.Add(name);
            } catch (DirectoryNotFoundException) {
              manifest.MissingFiles.Add(name);
            }
          }
          WriteJson(zip, PortabilityJson.ManifestEntry, manifest);
          WriteJson(zip, PortabilityJson.DataEntry, data);
        }
        File.Move(tempPath, zipPath, overwrite: true);
      } catch {
        try { File.Delete(tempPath); } catch (IOException) { } catch (UnauthorizedAccessException) { }
        throw;
      }
      return manifest;
    }

    private static void WriteJson(ZipArchive zip, string entryName, object value) {
      ZipArchiveEntry entry = zip.CreateEntry(entryName, CompressionLevel.Optimal);
      using (Stream stream = entry.Open()) {
        JsonSerializer.Serialize(stream, value, value.GetType(), PortabilityJson.Options);
      }
    }
  }
}
