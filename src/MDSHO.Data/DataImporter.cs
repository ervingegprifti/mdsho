using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using MDSHO.Common;
using MDSHO.Core;

namespace MDSHO.Data {

  /// <summary>
  /// Imports an export file made by <see cref="DataExporter"/>. The file is fully validated before anything is touched;
  /// all database writes happen in one transaction; the shortcut files are staged in a temporary folder and swapped into
  /// place just before the commit, in a way that is undone when the import fails.
  /// </summary>
  public static class DataImporter {
    /// <summary>The largest accepted total uncompressed size of the file's entries (and of any single entry).</summary>
    public static long MaxTotalBytes { get; set; } = 200L * 1024 * 1024;

    /// <summary>How many before-import backups are kept (the newest ones).</summary>
    public static int KeepImportBackups { get; set; } = 5;

    private static readonly Regex SafeGuid = new Regex("^[A-Za-z0-9-]{0,64}$", RegexOptions.Compiled);
    // A file type that is safe in a file name (no path); SafeExt then allows only the types MDSHO makes.
    private static readonly Regex SafeFileType = new Regex(@"^(\.[A-Za-z0-9]{1,16})?$", RegexOptions.Compiled);
    // MDSHO only makes .lnk and .url files (or none, for groups, notes and run commands). Any other type, such as .exe or
    // .bat, would run when the button is clicked, so it is never imported.
    private static readonly Regex SafeExt = new Regex(@"^(\.lnk|\.url)?$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex Colour = new Regex("^#[0-9A-Fa-f]{8}$", RegexOptions.Compiled);

    /// <summary>Validates the file and returns its manifest (the counts are the real ones found in data.json).</summary>
    public static ExportManifest Inspect(string zipPath) {
      using (ImportPackage package = ImportPackage.Load(zipPath)) {
        return package.Manifest;
      }
    }

    /// <summary>
    /// Imports the file. Replace first saves the current data to the import backups folder (<see cref="ImportResult.BackupPath"/>);
    /// only the newest <see cref="KeepImportBackups"/> are kept there.
    /// Throws <see cref="DataPortabilityException"/> for an invalid file (nothing is changed); any other exception means
    /// the import failed and was rolled back.
    /// </summary>
    public static ImportResult Import(string zipPath, ImportMode mode) {
      AppLog.Information("Import started from {ZipPath} ({Mode})", zipPath, mode);
      try {
        return ImportCore(zipPath, mode);
      } catch (DataPortabilityException ex) {
        AppLog.Warning(ex, "Import failed: the file was rejected and nothing was changed");
        throw;
      } catch (Exception ex) {
        AppLog.Error(ex, "Import failed and was rolled back");
        throw;
      }
    }

    private static ImportResult ImportCore(string zipPath, ImportMode mode) {
      using (ImportPackage package = ImportPackage.Load(zipPath)) {
        ImportResult result = new ImportResult();
        if (mode == ImportMode.Replace) {
          result.BackupPath = BackupCurrentData();
          AppLog.Information("Previous data saved to {BackupPath} before the import", result.BackupPath);
        }
        string shortcutsPath = Path.GetFullPath(Paths.GetShortcutsPath()).TrimEnd(Path.DirectorySeparatorChar);
        string parent = Path.GetDirectoryName(shortcutsPath);
        string stamp = Guid.NewGuid().ToString("N");
        string stagePath = Path.Combine(parent, "ImportTemp-" + stamp);
        string oldPath = Path.Combine(parent, "ImportOld-" + stamp);
        ShortcutFileSwap swap = null;
        try {
          // 1. Stage the files under their final names.
          Dictionary<long, string> guidMap = MapGuids(package.Data.Shortcuts, mode);
          Directory.CreateDirectory(stagePath);
          List<string> staged = package.StageFiles(stagePath, guidMap);
          // 2. All database writes in one transaction. The shortcut files are put in place before the commit, in a
          // way that can be undone: if anything fails, the database rolls back and the files go back as they were.
          using (ITransactionScope tx = DataStore.BeginTransaction()) {
            if (mode == ImportMode.Replace) {
              DeleteAllData();
              // The places remembered per screen setup would move the imported windows back after the restart.
              DataStore.ScreenSetups.DeleteAllPlaces();
            }
            WriteData(package.Data, mode, guidMap);
            // Settings and default colours only replace; Add as new merges into this PC, which keeps its own.
            if (mode == ImportMode.Replace) {
              PortableSettings.Apply(package.Data.Settings, package.Data.DefaultColors);
            }
            swap = mode == ImportMode.Replace
              ? ShortcutFileSwap.SwapFolders(shortcutsPath, stagePath, oldPath)
              : ShortcutFileSwap.AddFiles(shortcutsPath, staged);
            tx.Commit();
          }
          swap = null;
        } catch {
          // Not committed: put the old shortcut files back.
          swap?.Undo();
          throw;
        } finally {
          DeleteFolderQuietly(stagePath);
        }
        // Committed: the old shortcut files are no longer needed. A folder that can't be deleted now is only logged.
        DeleteFolderQuietly(oldPath);
        AppLog.Information("Import finished: {Windows} windows, {Clones} clones, {Shortcuts} shortcuts, {NoteWindows} note windows",
          package.Manifest.Counts.Windows, package.Manifest.Counts.Clones, package.Manifest.Counts.Shortcuts, package.Manifest.Counts.NoteWindows);
        return result;
      }
    }

    /// <summary>
    /// The final GUID of every imported shortcut, by its ID in the file. Replace keeps the GUIDs, Add as new windows gives
    /// new ones. Shortcuts that share a GUID in the file would share one shortcut file: the first (lowest ID) is mapped as
    /// usual, every other one gets a new GUID (and so its own copy of the file).
    /// </summary>
    private static Dictionary<long, string> MapGuids(IEnumerable<Shortcut> shortcuts, ImportMode mode) {
      Dictionary<long, string> map = new Dictionary<long, string>();
      HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
      foreach (Shortcut s in shortcuts.OrderBy(x => x.ID)) {
        if (string.IsNullOrEmpty(s.Guid)) {
          continue;
        }
        bool duplicate = !seen.Add(s.Guid);
        map[s.ID] = mode == ImportMode.Replace && !duplicate ? s.Guid : Identifiers.NewGuid();
      }
      return map;
    }

    private static string BackupCurrentData() {
      string folder = Paths.GetImportBackupsPath();
      string stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss", System.Globalization.CultureInfo.InvariantCulture);
      string path = Path.Combine(folder, "MDSHO-before-import-" + stamp + ".zip");
      for (int i = 2; File.Exists(path); i++) {
        path = Path.Combine(folder, "MDSHO-before-import-" + stamp + "-" + i + ".zip");
      }
      DataExporter.Export(path);
      DeleteOldImportBackups(folder, path);
      return path;
    }

    // Keeps only the newest KeepImportBackups before-import backups (each can be large). Other files are left alone, and
    // a backup that can't be deleted is only logged.
    private static void DeleteOldImportBackups(string folder, string newest) {
      IEnumerable<FileInfo> old = new DirectoryInfo(folder).GetFiles("MDSHO-before-import-*.zip")
        .Where(f => !string.Equals(f.FullName, Path.GetFullPath(newest), StringComparison.OrdinalIgnoreCase))
        .OrderByDescending(f => f.LastWriteTimeUtc)
        .Skip(Math.Max(0, KeepImportBackups - 1));
      foreach (FileInfo file in old) {
        try {
          file.Delete();
        } catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException) {
          AppLog.Warning(ex, "An old import backup could not be deleted: {Path}", file.FullName);
        }
      }
    }

    private static void DeleteAllData() {
      foreach (DesktopWindow w in DataStore.Windows.GetAll()) {
        DataStore.Windows.Delete(w.ID); // clones and shortcuts go with it (cascade)
      }
      foreach (Shortcut s in DataStore.Shortcuts.GetAll()) {
        DataStore.Shortcuts.Delete(s.ID); // orphans, if any (a no-op when already deleted)
      }
    }

    private static void WriteData(DataFile data, ImportMode mode, Dictionary<long, string> guidMap) {
      bool keepIds = mode == ImportMode.Replace;
      Dictionary<long, long> windowIds = new Dictionary<long, long>();
      foreach (DesktopWindow w in data.Windows.OrderBy(x => x.ID)) {
        long oldId = w.ID;
        if (!keepIds) { w.ID = 0; }
        DataStore.Windows.Add(w);
        windowIds[oldId] = w.ID;
      }
      foreach (DesktopWindowClone c in data.Clones.OrderBy(x => x.ID)) {
        if (!keepIds) { c.ID = 0; }
        c.ParentWindowID = windowIds[c.ParentWindowID];
        DataStore.Clones.Add(c);
      }
      // Parents before children (foreign keys); the stored Position values keep the order.
      ILookup<long, Shortcut> children = data.Shortcuts.Where(s => s.ParentShortcutID != null).ToLookup(s => s.ParentShortcutID.Value);
      Dictionary<long, long> shortcutIds = new Dictionary<long, long>();
      Queue<Shortcut> queue = new Queue<Shortcut>(data.Shortcuts.Where(s => s.ParentShortcutID == null).OrderBy(s => s.ParentWindowID).ThenBy(s => s.Position).ThenBy(s => s.ID));
      while (queue.Count > 0) {
        Shortcut s = queue.Dequeue();
        long oldId = s.ID;
        foreach (Shortcut child in children[oldId].OrderBy(x => x.Position).ThenBy(x => x.ID)) {
          queue.Enqueue(child);
        }
        if (s.ParentWindowID != null) { s.ParentWindowID = windowIds[s.ParentWindowID.Value]; }
        if (s.ParentShortcutID != null) { s.ParentShortcutID = shortcutIds[s.ParentShortcutID.Value]; }
        if (!keepIds) { s.ID = 0; }
        if (!string.IsNullOrEmpty(s.Guid)) { s.Guid = guidMap[oldId]; }
        DataStore.Shortcuts.Add(s);
        shortcutIds[oldId] = s.ID;
      }
      // Each history row goes with its button's new ID, oldest first; then every button keeps at most the setting.
      foreach (HistoryEntry row in data.History) {
        row.ID = 0;
        row.ShortcutID = shortcutIds[row.ShortcutID];
        DataStore.History.Add(row);
      }
      DataStore.History.TrimAll(TextHistorySetting.Keep);
      // Each note window place goes with its note's new ID; a place of a note not in the file is skipped.
      foreach (NoteWindowPlace place in data.NoteWindows) {
        if (shortcutIds.TryGetValue(place.ShortcutID, out long noteId)) {
          place.ID = 0;
          place.ShortcutID = noteId;
          DataStore.NoteWindows.Save(place);
        }
      }
    }

    // Deletes a folder of the import when it exists. Never throws: a leftover folder is only logged.
    private static void DeleteFolderQuietly(string path) {
      try {
        if (Directory.Exists(path)) {
          Directory.Delete(path, true);
        }
      } catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException) {
        AppLog.Warning(ex, "A folder of the import could not be deleted: {Path}", path);
      }
    }

    /// <summary>
    /// Puts the imported shortcut files in place before the commit, and can undo it. Replace renames the shortcuts folder
    /// aside and the staged folder in its place (no file is deleted until the commit is done). Add as new moves the new
    /// files in, and Undo moves them back out.
    /// </summary>
    private sealed class ShortcutFileSwap {
      private string shortcutsPath, stagePath, oldPath;
      private bool oldMoved, stageMoved;
      private readonly List<(string from, string to)> moved = new List<(string from, string to)>();

      public static ShortcutFileSwap SwapFolders(string shortcutsPath, string stagePath, string oldPath) {
        ShortcutFileSwap swap = new ShortcutFileSwap { shortcutsPath = shortcutsPath, stagePath = stagePath, oldPath = oldPath };
        try {
          if (Directory.Exists(shortcutsPath)) {
            // Fails when another program has a shortcut file open: then nothing has changed yet.
            Directory.Move(shortcutsPath, oldPath);
            swap.oldMoved = true;
          }
          Directory.Move(stagePath, shortcutsPath);
          swap.stageMoved = true;
        } catch {
          swap.Undo();
          throw;
        }
        return swap;
      }

      public static ShortcutFileSwap AddFiles(string shortcutsPath, IEnumerable<string> staged) {
        ShortcutFileSwap swap = new ShortcutFileSwap();
        try {
          Directory.CreateDirectory(shortcutsPath);
          foreach (string file in staged) {
            string target = Path.Combine(shortcutsPath, Path.GetFileName(file));
            File.Move(file, target);
            swap.moved.Add((file, target));
          }
        } catch {
          swap.Undo();
          throw;
        }
        return swap;
      }

      /// <summary>Puts the files back as they were. If that fails too, says where the old files are.</summary>
      public void Undo() {
        try {
          for (int i = this.moved.Count - 1; i >= 0; i--) {
            File.Move(this.moved[i].to, this.moved[i].from);
          }
          this.moved.Clear();
          if (this.stageMoved) {
            Directory.Move(this.shortcutsPath, this.stagePath);
            this.stageMoved = false;
          }
          if (this.oldMoved) {
            Directory.Move(this.oldPath, this.shortcutsPath);
            this.oldMoved = false;
          }
        } catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException) {
          AppLog.Error(ex, "The shortcut files could not be put back after a failed import");
          throw new ImportFilesNotRestoredException(this.oldMoved ? this.oldPath : null, this.shortcutsPath, ex);
        }
      }
    }

    /// <summary>An opened, validated export file.</summary>
    private sealed class ImportPackage : IDisposable {
      private ZipArchive zip;
      public ExportManifest Manifest { get; private set; }
      public DataFile Data { get; private set; }

      public void Dispose() {
        if (zip != null) { zip.Dispose(); zip = null; }
      }

      public static ImportPackage Load(string zipPath) {
        if (string.IsNullOrEmpty(zipPath) || !File.Exists(zipPath)) {
          throw new DataPortabilityException("The file was not found: " + zipPath);
        }
        ImportPackage package = new ImportPackage();
        try {
          package.Open(zipPath);
          return package;
        } catch {
          package.Dispose();
          throw;
        }
      }

      private void Open(string zipPath) {
        try {
          zip = ZipFile.OpenRead(zipPath);
        } catch (InvalidDataException) {
          throw new DataPortabilityException("The file is not a valid MDSHO export (it is not a zip file).");
        }
        long total = 0;
        foreach (ZipArchiveEntry entry in zip.Entries) {
          if (!IsSafeEntryName(entry.FullName)) {
            throw new DataPortabilityException("The file contains an unsafe path (\"" + entry.FullName + "\") and was not imported.");
          }
          total += entry.Length;
          if (entry.Length > MaxTotalBytes || total > MaxTotalBytes) {
            throw TooLarge();
          }
        }
        Manifest = ReadManifest();
        Data = ReadData();
        Validate();
        Manifest.Counts.Windows = Data.Windows.Count;
        Manifest.Counts.Clones = Data.Clones.Count;
        Manifest.Counts.Shortcuts = Data.Shortcuts.Count;
        Manifest.Counts.NoteWindows = Data.NoteWindows.Count;
      }

      private static DataPortabilityException TooLarge() {
        return new DataPortabilityException("The file is too large to import (the limit is " + (MaxTotalBytes / (1024 * 1024)) + " MB).");
      }

      private static bool IsSafeEntryName(string name) {
        if (string.IsNullOrEmpty(name) || name.Contains(':') || name.Any(char.IsControl)) {
          return false;
        }
        if (name[0] == '/' || name[0] == '\\') {
          return false;
        }
        return !name.Split('/', '\\').Any(part => part == "..");
      }

      private string ReadEntryText(string entryName, string missingMessage) {
        ZipArchiveEntry entry = zip.GetEntry(entryName);
        if (entry == null) {
          throw new DataPortabilityException(missingMessage);
        }
        using (Stream stream = entry.Open())
        using (MemoryStream ms = new MemoryStream()) {
          CopyBounded(stream, ms, MaxTotalBytes);
          return new UTF8Encoding(false, false).GetString(ms.ToArray());
        }
      }

      /// <summary>Copies at most the limit (the declared entry length cannot be trusted).</summary>
      private static long CopyBounded(Stream from, Stream to, long limit) {
        byte[] buffer = new byte[81920];
        long copied = 0;
        int read;
        while ((read = from.Read(buffer, 0, buffer.Length)) > 0) {
          copied += read;
          if (copied > limit) {
            throw TooLarge();
          }
          to.Write(buffer, 0, read);
        }
        return copied;
      }

      private ExportManifest ReadManifest() {
        string text = ReadEntryText(PortabilityJson.ManifestEntry, "The file is not an MDSHO export (manifest.json is missing).");
        ExportManifest m;
        try {
          m = JsonSerializer.Deserialize<ExportManifest>(text, PortabilityJson.Options);
        } catch (JsonException ex) {
          throw new DataPortabilityException("manifest.json is not valid: " + ex.Message, ex);
        }
        if (m == null || m.Format != ExportManifest.FormatName) {
          throw new DataPortabilityException("The file is not an MDSHO export (unknown format).");
        }
        if (m.FormatVersion < ExportManifest.OldestFormatVersion || m.FormatVersion > ExportManifest.CurrentFormatVersion) {
          throw new DataPortabilityException("This export file uses formatVersion " + m.FormatVersion + ", which this version of MDSHO does not support.");
        }
        if (m.SchemaVersion > AppConstants.DatabaseVersion) {
          throw new DataPortabilityException("This file was exported by a newer version of MDSHO (database version " + m.SchemaVersion + ", this version supports up to " + AppConstants.DatabaseVersion + "). Please update MDSHO and try again.");
        }
        if (m.Counts == null) { m.Counts = new ExportCounts(); }
        if (m.MissingFiles == null) { m.MissingFiles = new List<string>(); }
        return m;
      }

      private DataFile ReadData() {
        string text = ReadEntryText(PortabilityJson.DataEntry, "The file is incomplete (data.json is missing).");
        DataFile d;
        try {
          d = JsonSerializer.Deserialize<DataFile>(text, PortabilityJson.Options);
        } catch (JsonException ex) {
          throw new DataPortabilityException("data.json is not valid: " + ex.Message, ex);
        }
        if (d == null || d.Windows == null || d.Clones == null || d.Shortcuts == null) {
          throw new DataPortabilityException("data.json is not valid: windows, clones and shortcuts are required.");
        }
        // Format 1 files have no note window places: the notes get default windows.
        d.NoteWindows = d.NoteWindows ?? new List<NoteWindowPlace>();
        // Files from before database version 24 have no History rows: their shortcuts' earlier copies become rows.
        d.History = d.History ?? d.Shortcuts.Where(s => s != null).SelectMany(PortabilityJson.LegacyHistoryOf).ToList();
        // Data from database version 20 or older has its text sizes in DIPs: convert them like the database upgrade.
        if (Manifest.SchemaVersion < NoteWindowPlace.FontSizeInPointsFromVersion) {
          foreach (NoteWindowPlace place in d.NoteWindows.Where(p => p != null)) {
            place.FontSize = NoteWindowPlace.FontSizeFromDips(place.FontSize);
          }
        }
        return d;
      }

      private void Validate() {
        foreach (object row in Data.Windows.Cast<object>().Concat(Data.Clones).Concat(Data.Shortcuts).Concat(Data.NoteWindows).Concat(Data.History)) {
          if (row == null) {
            throw new DataPortabilityException("data.json is not valid: it contains an empty record.");
          }
          foreach (PropertyInfo p in row.GetType().GetProperties().Where(p => p.PropertyType == typeof(string))) {
            if (p.GetValue(row) == null) {
              throw new DataPortabilityException("data.json is not valid: a " + row.GetType().Name + " record has no value for " + p.Name + ".");
            }
          }
        }
        Dictionary<long, DesktopWindow> windows = UniqueById(Data.Windows, w => w.ID, "window");
        UniqueById(Data.Clones, c => c.ID, "clone");
        Dictionary<long, Shortcut> shortcuts = UniqueById(Data.Shortcuts, s => s.ID, "shortcut");
        foreach (DesktopWindowClone c in Data.Clones) {
          if (!windows.ContainsKey(c.ParentWindowID)) {
            throw new DataPortabilityException("Clone " + c.ID + " belongs to window " + c.ParentWindowID + ", which is not in the file.");
          }
        }
        foreach (Shortcut s in Data.Shortcuts) {
          if (s.ParentShortcutID == null && s.ParentWindowID == null) {
            throw new DataPortabilityException("Shortcut " + s.ID + " has no parent window or group.");
          }
          if (s.ParentShortcutID != null && s.ParentWindowID != null) {
            throw new DataPortabilityException("Shortcut " + s.ID + " has two parents (a window and a group).");
          }
          if (s.ParentWindowID != null && !windows.ContainsKey(s.ParentWindowID.Value)) {
            throw new DataPortabilityException("Shortcut " + s.ID + " has parent window " + s.ParentWindowID + ", which is not in the file.");
          }
          if (s.ParentShortcutID != null && !shortcuts.ContainsKey(s.ParentShortcutID.Value)) {
            throw new DataPortabilityException("Shortcut " + s.ID + " has parent shortcut " + s.ParentShortcutID + ", which is not in the file.");
          }
          if (!SafeGuid.IsMatch(s.Guid) || !SafeFileType.IsMatch(s.Ext)) {
            throw new DataPortabilityException("Shortcut " + s.ID + " has an unsafe file name and was not imported.");
          }
          if (!SafeExt.IsMatch(s.Ext)) {
            throw new DataPortabilityException("Shortcut " + s.ID + " has the file type \"" + s.Ext + "\". MDSHO imports only .lnk and .url files, so this file was not imported.");
          }
        }
        foreach (HistoryEntry h in Data.History) {
          if (!HistoryFields.IsKnown(h.Field)) {
            throw new DataPortabilityException("data.json is not valid: a text history row has the field \"" + h.Field + "\" (it must be Text or Comment).");
          }
          if (!shortcuts.ContainsKey(h.ShortcutID)) {
            throw new DataPortabilityException("A text history row belongs to shortcut " + h.ShortcutID + ", which is not in the file.");
          }
        }
        if (Data.DefaultColors != null) {
          foreach (KeyValuePair<string, ColorSet> pair in Data.DefaultColors.Where(p => p.Value != null)) {
            ColorSet c = pair.Value;
            if (new[] { c.BorderColor, c.TitleBackgroundColor, c.TitleTextColor, c.BackgroundColor, c.TextColor }.Any(v => v == null || !Colour.IsMatch(v))) {
              throw new DataPortabilityException("data.json is not valid: a default colour of \"" + pair.Key + "\" is not a colour (#AARRGGBB).");
            }
          }
        }
        foreach (Shortcut s in Data.Shortcuts) {
          HashSet<long> seen = new HashSet<long>();
          for (Shortcut cur = s; cur.ParentShortcutID != null; cur = shortcuts[cur.ParentShortcutID.Value]) {
            if (!seen.Add(cur.ID)) {
              throw new DataPortabilityException("Shortcut " + s.ID + " is part of a cycle (a group inside itself).");
            }
          }
        }
      }

      private static Dictionary<long, T> UniqueById<T>(List<T> rows, Func<T, long> id, string what) {
        Dictionary<long, T> map = new Dictionary<long, T>();
        foreach (T row in rows) {
          long value = id(row);
          if (value <= 0) {
            throw new DataPortabilityException("data.json is not valid: a " + what + " has an invalid ID (" + value + ").");
          }
          if (!map.TryAdd(value, row)) {
            throw new DataPortabilityException("data.json is not valid: duplicate " + what + " ID " + value + ".");
          }
        }
        return map;
      }

      /// <summary>Copies the shortcut files of the package into the staging folder under their final (mapped) names.
      /// Each shortcut gets its own file, so shortcuts that shared a GUID in the file each get a copy.</summary>
      public List<string> StageFiles(string stagePath, Dictionary<long, string> guidMap) {
        List<string> staged = new List<string>();
        long total = 0;
        HashSet<string> done = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (Shortcut s in Data.Shortcuts) {
          if (string.IsNullOrEmpty(s.Guid) || string.IsNullOrEmpty(s.Ext) || !done.Add(guidMap[s.ID] + s.Ext)) {
            continue;
          }
          ZipArchiveEntry entry = zip.GetEntry(PortabilityJson.FilesPrefix + s.Guid + s.Ext);
          if (entry == null) {
            continue; // missing at export time: stays missing
          }
          string target = Path.Combine(stagePath, guidMap[s.ID] + s.Ext);
          using (Stream from = entry.Open())
          using (FileStream to = new FileStream(target, FileMode.CreateNew, FileAccess.Write)) {
            total += CopyBounded(from, to, MaxTotalBytes - total);
          }
          staged.Add(target);
        }
        return staged;
      }
    }
  }
}
