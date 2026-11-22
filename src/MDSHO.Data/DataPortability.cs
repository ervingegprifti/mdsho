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

  /// <summary>
  /// An import failed and was rolled back, but the old shortcut files could not be put back in the shortcuts folder
  /// (the message is meant for the user and says where they are).
  /// </summary>
  public sealed class ImportFilesNotRestoredException : Exception {
    /// <summary>The folder that holds the old shortcut files, or null.</summary>
    public string OldFilesFolder { get; }

    /// <summary>Creates the error for the folder that holds the old shortcut files (or null).</summary>
    public ImportFilesNotRestoredException(string oldFilesFolder, string shortcutsFolder, Exception inner) : base(MessageFor(oldFilesFolder, shortcutsFolder), inner) {
      OldFilesFolder = oldFilesFolder;
    }

    private static string MessageFor(string folder, string shortcutsFolder) {
      return folder == null
        ? "The data could not be imported. Your data was not changed, but some shortcut files could not be put back. Some buttons may show a missing icon."
        : "The data could not be imported. Your data was not changed, but the shortcut files could not be put back. They are in:\n" + folder +
          "\n\nClose MDSHO and move them back to:\n" + shortcutsFolder;
    }
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
    /// <summary>Number of note window places (0 in format 1 files, which have none).</summary>
    public int NoteWindows { get; set; }
  }

  /// <summary>The content of manifest.json.</summary>
  public sealed class ExportManifest {
    /// <summary>Fixed name that identifies an MDSHO export file.</summary>
    public const string FormatName = "MDSHO-export";
    /// <summary>The export format version this build writes. 2 added the note window places, 3 the default colours and settings.</summary>
    public const int CurrentFormatVersion = 3;
    /// <summary>The oldest export format version this build still reads.</summary>
    public const int OldestFormatVersion = 1;

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
    /// <summary>The PC that made the file (automatic backups only). Optional: older files and Export data leave it out.</summary>
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public string PcName { get; set; }
  }

  /// <summary>Options of an export.</summary>
  public sealed class ExportOptions {
    /// <summary>The PC name written to the manifest; null leaves it out.</summary>
    public string PcName { get; set; }
    /// <summary>The database still has an older version: read it as it is, and write that version to the manifest.</summary>
    public bool BeforeUpgrade { get; set; }
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
    /// <summary>The note window places (format 2); null in format 1 files.</summary>
    public List<NoteWindowPlace> NoteWindows { get; set; }
    /// <summary>The default colours by ColorSetKind name, null for a kind never set (format 3); null in older files.</summary>
    public Dictionary<string, ColorSet> DefaultColors { get; set; }
    /// <summary>The PortableSettings values, null for one never set (format 3); null in older files.</summary>
    public Dictionary<string, string> Settings { get; set; }
    /// <summary>
    /// The text history rows (database version 24); null in older files, whose shortcuts carry their two earlier copies
    /// as textPrevious1/2 and commentPrevious1/2 instead (see <see cref="PortabilityJson.LegacyHistory"/>).
    /// </summary>
    public List<HistoryEntry> History { get; set; }
  }

  /// <summary>
  /// The settings an export carries and a Replace import applies: Window theme, the Bring panels to front shortcut,
  /// Remember places for each screen setup and how many earlier texts the text history keeps. Settings tied to this PC
  /// (backups, review prompt, last Settings page) stay out.
  /// </summary>
  public static class PortableSettings {
    /// <summary>Their AppSettings keys (the same as the SettingKey constants of the view models).</summary>
    public static readonly string[] Keys = { "WindowTheme", "BringToFrontShortcut", "RememberScreenSetupPlaces", TextHistorySetting.SettingKey };

    /// <summary>The current value of each key; null when never saved.</summary>
    internal static Dictionary<string, string> Read() {
      Dictionary<string, string> values = new Dictionary<string, string>();
      foreach (string key in Keys) {
        values[key] = DataStore.AppSettings.Get(key);
      }
      return values;
    }

    /// <summary>Both kinds of default colours; null for a kind never set.</summary>
    internal static Dictionary<string, ColorSet> ReadDefaultColors() {
      Dictionary<string, ColorSet> colors = new Dictionary<string, ColorSet>();
      foreach (ColorSetKind kind in Enum.GetValues<ColorSetKind>()) {
        colors[kind.ToString()] = DataStore.DefaultColors.Get(kind);
      }
      return colors;
    }

    /// <summary>
    /// Sets what the file has: a value is saved, a null is removed. A key or kind missing from the file (and a file
    /// without these parts at all) keeps the value on this PC. Other keys in the file are ignored.
    /// </summary>
    internal static void Apply(Dictionary<string, string> settings, Dictionary<string, ColorSet> defaultColors) {
      if (settings != null) {
        foreach (string key in Keys) {
          if (settings.TryGetValue(key, out string value)) {
            if (value == null) {
              DataStore.AppSettings.Delete(key);
            } else {
              DataStore.AppSettings.Set(key, value);
            }
          }
        }
      }
      if (defaultColors != null) {
        foreach (ColorSetKind kind in Enum.GetValues<ColorSetKind>()) {
          if (defaultColors.TryGetValue(kind.ToString(), out ColorSet colors)) {
            if (colors == null) {
              DataStore.DefaultColors.Delete(kind);
            } else {
              DataStore.DefaultColors.Save(kind, colors);
            }
          }
        }
      }
    }
  }

  internal static class PortabilityJson {
    public const string ManifestEntry = "manifest.json";
    public const string DataEntry = "data.json";
    public const string FilesPrefix = "shortcuts/";

    public static readonly JsonSerializerOptions Options = Create();

    /// <summary>The JSON names of the two earlier copies files from database versions 22 and 23 carry on each shortcut.</summary>
    internal static readonly string[] LegacyHistoryNames = { "textPrevious1", "textPrevious1Time", "textPrevious2", "textPrevious2Time",
      "commentPrevious1", "commentPrevious1Time", "commentPrevious2", "commentPrevious2Time" };

    /// <summary>
    /// The earlier copies read from such a file, per shortcut, in the order of <see cref="LegacyHistoryNames"/> (never
    /// written). The importer turns them into History rows.
    /// </summary>
    internal static readonly System.Runtime.CompilerServices.ConditionalWeakTable<Shortcut, string[]> LegacyHistory =
      new System.Runtime.CompilerServices.ConditionalWeakTable<Shortcut, string[]>();

    /// <summary>The History rows for one shortcut of an older file: the older copy first; empty copies are skipped.</summary>
    internal static IEnumerable<HistoryEntry> LegacyHistoryOf(Shortcut shortcut) {
      if (!LegacyHistory.TryGetValue(shortcut, out string[] v)) {
        yield break;
      }
      foreach ((string field, int i) in new[] { (HistoryFields.Text, 2), (HistoryFields.Text, 0), (HistoryFields.Comment, 6), (HistoryFields.Comment, 4) }) {
        if (!string.IsNullOrEmpty(v[i])) {
          yield return new HistoryEntry { ShortcutID = shortcut.ID, Field = field, Text = v[i], KeptAt = v[i + 1] ?? "" };
        }
      }
    }

    private static JsonSerializerOptions Create() {
      DefaultJsonTypeInfoResolver resolver = new DefaultJsonTypeInfoResolver();
      // The children lists are not columns: the tree is rebuilt from the parent ids.
      resolver.Modifiers.Add(info => {
        // Exports made before database version 6 call the shortcut comment "notes": read it into Comment (a "comment"
        // in the same object wins), but never write it.
        if (info.Type == typeof(Shortcut)) {
          JsonPropertyInfo legacyNotes = info.CreateJsonPropertyInfo(typeof(string), "notes");
          legacyNotes.Set = (obj, value) => {
            Shortcut shortcut = (Shortcut)obj;
            if (string.IsNullOrEmpty(shortcut.Comment)) {
              shortcut.Comment = (string)value;
            }
          };
          info.Properties.Add(legacyNotes);
          // Database versions 22 and 23 kept two earlier copies on the shortcut: read them aside, never write them.
          for (int i = 0; i < LegacyHistoryNames.Length; i++) {
            int index = i;
            JsonPropertyInfo legacy = info.CreateJsonPropertyInfo(typeof(string), LegacyHistoryNames[i]);
            legacy.Set = (obj, value) => LegacyHistory.GetValue((Shortcut)obj, s => new string[LegacyHistoryNames.Length])[index] = (string)value;
            info.Properties.Add(legacy);
          }
        }
        // A history row is linked by its shortcut's ID; its own row ID is not written.
        if (info.Type == typeof(HistoryEntry)) {
          for (int i = info.Properties.Count - 1; i >= 0; i--) {
            if (string.Equals(info.Properties[i].Name, "id", StringComparison.OrdinalIgnoreCase)) {
              info.Properties.RemoveAt(i);
            }
          }
        }
        // A note window place is linked by its note's ID; its own row ID is not written.
        if (info.Type == typeof(NoteWindowPlace)) {
          for (int i = info.Properties.Count - 1; i >= 0; i--) {
            if (string.Equals(info.Properties[i].Name, "id", StringComparison.OrdinalIgnoreCase)) {
              info.Properties.RemoveAt(i);
            }
          }
        }
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
