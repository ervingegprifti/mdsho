using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MDSHO.Common;
using MDSHO.Core;

namespace MDSHO.Data {

  /// <summary>
  /// Exports all MDSHO data (windows, clones, shortcuts, note window places, default colours, the portable settings and
  /// the shortcut files) to one zip file:
  /// <c>manifest.json</c>, <c>data.json</c> and <c>shortcuts/&lt;guid&gt;&lt;ext&gt;</c>.
  /// </summary>
  public static class DataExporter {

    /// <summary>MDSHO-export-PC-yyyy-MM-dd-HHmm.zip, with the PC name cleaned like in backup names.</summary>
    public static string DefaultFileName(string machineName, DateTime localNow) {
      return "MDSHO-export-" + MDSHO.Common.FileNames.SafePcName(machineName) + "-" + localNow.ToString("yyyy-MM-dd-HHmm", System.Globalization.CultureInfo.InvariantCulture) + ".zip";
    }

    /// <summary>The application version as Major.Minor.Build (1.1.0).</summary>
    public static string AppVersion() {
      Version v = typeof(DataExporter).Assembly.GetName().Version;
      return v.Major + "." + v.Minor + "." + Math.Max(v.Build, 0);
    }

    /// <summary>
    /// Reads everything in one transaction scope (a consistent snapshot), writes the zip to a temporary file next to the
    /// target and moves it into place. An existing target is replaced. Missing shortcut files are listed in the manifest.
    /// </summary>
    public static ExportManifest Export(string zipPath) {
      return Export(zipPath, new ExportOptions());
    }

    /// <summary>Like <see cref="Export(string)"/>, with a PC name in the manifest, or reading a database not yet upgraded.</summary>
    public static ExportManifest Export(string zipPath, ExportOptions options) {
      AppLog.Information("Export started to {ZipPath}", zipPath);
      try {
        ExportManifest manifest = ExportCore(zipPath, options ?? new ExportOptions());
        AppLog.Information("Export finished: {Windows} windows, {Clones} clones, {Shortcuts} shortcuts, {NoteWindows} note windows, {Files} files ({MissingFiles} missing)",
          manifest.Counts.Windows, manifest.Counts.Clones, manifest.Counts.Shortcuts, manifest.Counts.NoteWindows, manifest.Counts.Files, manifest.MissingFiles.Count);
        return manifest;
      } catch (Exception ex) {
        AppLog.Error(ex, "Export failed");
        throw;
      }
    }

    /// <summary>
    /// A SHA-256 of what an export would hold (data.json and the shortcut files), without the export time. The same data
    /// always gives the same hash, so backups can tell when something changed.
    /// </summary>
    public static string DataHash() {
      DataFile data = ReadData(false);
      string shortcutsPath = Paths.GetShortcutsPath();
      using (IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256)) {
        hash.AppendData(SerializeData(data));
        foreach (string name in FileNames(data)) {
          hash.AppendData(Encoding.UTF8.GetBytes("\n" + name + "\n"));
          string path = Path.Combine(shortcutsPath, name);
          hash.AppendData(File.Exists(path) ? File.ReadAllBytes(path) : Encoding.UTF8.GetBytes("missing"));
        }
        return Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
      }
    }

    private static ExportManifest ExportCore(string zipPath, ExportOptions options) {
      DataFile data = ReadData(options.BeforeUpgrade);
      string shortcutsPath = Paths.GetShortcutsPath();
      ExportManifest manifest = new ExportManifest {
        Format = ExportManifest.FormatName,
        FormatVersion = ExportManifest.CurrentFormatVersion,
        AppVersion = AppVersion(),
        SchemaVersion = options.BeforeUpgrade ? SchemaManager.ReadUserVersion() : AppConstants.DatabaseVersion,
        ExportedAtUtc = DateTime.UtcNow,
        PcName = string.IsNullOrEmpty(options.PcName) ? null : options.PcName
      };
      manifest.Counts.Windows = data.Windows.Count;
      manifest.Counts.Clones = data.Clones.Count;
      manifest.Counts.Shortcuts = data.Shortcuts.Count;
      manifest.Counts.NoteWindows = data.NoteWindows.Count;
      string tempPath = zipPath + ".tmp";
      try {
        using (FileStream fs = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None))
        using (ZipArchive zip = new ZipArchive(fs, ZipArchiveMode.Create)) {
          foreach (string name in FileNames(data)) {
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
          ZipArchiveEntry dataEntry = zip.CreateEntry(PortabilityJson.DataEntry, CompressionLevel.Optimal);
          using (Stream stream = dataEntry.Open()) {
            stream.Write(SerializeData(data));
          }
        }
        File.Move(tempPath, zipPath, overwrite: true);
      } catch {
        try { File.Delete(tempPath); } catch (IOException) { } catch (UnauthorizedAccessException) { }
        throw;
      }
      return manifest;
    }

    // One consistent snapshot. Before an upgrade the tables may lack newer columns: see OldSchemaReader.
    private static DataFile ReadData(bool beforeUpgrade) {
      DataFile data = new DataFile();
      using (ITransactionScope tx = DataStore.BeginTransaction()) {
        if (beforeUpgrade) {
          data.Windows = OldSchemaReader.Windows();
          data.Clones = OldSchemaReader.Clones();
          data.Shortcuts = OldSchemaReader.Shortcuts();
          data.NoteWindows = OldSchemaReader.NoteWindows();
          data.DefaultColors = OldSchemaReader.DefaultColors();
          data.Settings = OldSchemaReader.Settings();
          data.History = OldSchemaReader.History();
        } else {
          data.Windows = DataStore.Windows.GetAll();
          data.Clones = DataStore.Clones.GetAll();
          data.Shortcuts = DataStore.Shortcuts.GetAll();
          data.NoteWindows = DataStore.NoteWindows.GetAll();
          data.DefaultColors = PortableSettings.ReadDefaultColors();
          data.Settings = PortableSettings.Read();
          data.History = DataStore.History.GetAll();
        }
        tx.Commit();
      }
      return data;
    }

    // The shortcut file names, each once, in the order of the shortcuts.
    private static List<string> FileNames(DataFile data) {
      List<string> names = new List<string>();
      HashSet<string> done = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
      foreach (Shortcut s in data.Shortcuts) {
        if (string.IsNullOrEmpty(s.Guid) || string.IsNullOrEmpty(s.Ext)) {
          continue;
        }
        string name = s.Guid + s.Ext;
        if (done.Add(name)) {
          names.Add(name);
        }
      }
      return names;
    }

    private static byte[] SerializeData(DataFile data) {
      return JsonSerializer.SerializeToUtf8Bytes(data, PortabilityJson.Options);
    }

    private static void WriteJson(ZipArchive zip, string entryName, object value) {
      ZipArchiveEntry entry = zip.CreateEntry(entryName, CompressionLevel.Optimal);
      using (Stream stream = entry.Open()) {
        JsonSerializer.Serialize(stream, value, value.GetType(), PortabilityJson.Options);
      }
    }
  }
}
