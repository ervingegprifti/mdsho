using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using MDSHO.Core;

namespace MDSHO.Data {

  /// <summary>How an import treats the data already in MDSHO.</summary>
  public enum ImportMode {
    /// <summary>Replaces all current windows, clones, shortcuts and shortcut files (IDs are kept).</summary>
    Replace,
    /// <summary>Adds the imported windows next to the existing ones with new IDs and new shortcut file names.</summary>
    AddAsNew
  }

  /// <summary>An export file that cannot be imported (the message is meant for the user).</summary>
  public sealed class DataPortabilityException : Exception {
    /// <summary>Creates an error whose message is meant to be shown to the user as it is.</summary>
    public DataPortabilityException(string message) : base(message) { }
    /// <summary>Creates an error whose message is meant to be shown to the user as it is.</summary>
    public DataPortabilityException(string message, Exception inner) : base(message, inner) { }
  }

  /// <summary>How many rows and files an export contains.</summary>
  public sealed class ExportCounts {
    /// <summary>Number of windows.</summary>
    public int Windows { get; set; }
    /// <summary>Number of clones.</summary>
    public int Clones { get; set; }
    /// <summary>Number of shortcuts.</summary>
    public int Shortcuts { get; set; }
    /// <summary>Number of shortcut files in the zip.</summary>
    public int Files { get; set; }
  }

  /// <summary>The content of manifest.json.</summary>
  public sealed class ExportManifest {
    /// <summary>Fixed name that identifies an MDSHO export file.</summary>
    public const string FormatName = "MDSHO-export";
    /// <summary>The export format version this build writes and reads.</summary>
    public const int CurrentFormatVersion = 1;

    /// <summary>Identifies the file as an MDSHO export.</summary>
    public string Format { get; set; }
    /// <summary>Version of the file layout.</summary>
    public int FormatVersion { get; set; }
    /// <summary>MDSHO version that wrote the file.</summary>
    public string AppVersion { get; set; }
    /// <summary>Database version of the exported data.</summary>
    public int SchemaVersion { get; set; }
    /// <summary>When the export was made (UTC).</summary>
    public DateTime ExportedAtUtc { get; set; }
    /// <summary>What the file contains.</summary>
    public ExportCounts Counts { get; set; } = new ExportCounts();
    /// <summary>Names of the shortcut files that did not exist at export time (not an error).</summary>
    public List<string> MissingFiles { get; set; } = new List<string>();
  }

  /// <summary>What an import did.</summary>
  public sealed class ImportResult {
    /// <summary>The automatic export of the previous data made before a Replace (null for AddAsNew).</summary>
    public string BackupPath { get; set; }
  }

  /// <summary>The content of data.json.</summary>
  internal sealed class DataFile {
    public List<DesktopWindow> Windows { get; set; }
    public List<DesktopWindowClone> Clones { get; set; }
    public List<Shortcut> Shortcuts { get; set; }
  }

  internal static class PortabilityJson {
    public const string ManifestEntry = "manifest.json";
    public const string DataEntry = "data.json";
    public const string FilesPrefix = "shortcuts/";

    public static readonly JsonSerializerOptions Options = Create();

    private static JsonSerializerOptions Create() {
      DefaultJsonTypeInfoResolver resolver = new DefaultJsonTypeInfoResolver();
      // The children lists are not columns: the tree is rebuilt from the parent ids.
      resolver.Modifiers.Add(info => {
        if (info.Type == typeof(DesktopWindow) || info.Type == typeof(DesktopWindowClone) || info.Type == typeof(Shortcut)) {
          for (int i = info.Properties.Count - 1; i >= 0; i--) {
            string name = info.Properties[i].Name;
            if (string.Equals(name, "shortcuts", StringComparison.OrdinalIgnoreCase) || string.Equals(name, "rootShortcuts", StringComparison.OrdinalIgnoreCase)) {
              info.Properties.RemoveAt(i);
            }
          }
        }
      });
      return new JsonSerializerOptions {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
        TypeInfoResolver = resolver
      };
    }
  }
}
