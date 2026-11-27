using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;

namespace MDSHO.Data {

  /// <summary>The kinds of sync location. A sync is one kind everywhere, never mixed.</summary>
  public static class SyncKinds {
    /// <summary>A folder all PCs reach (a cloud drive app's folder, a network drive).</summary>
    public const string Folder = "Folder";
    /// <summary>Google Drive, through its API.</summary>
    public const string GoogleDrive = "GoogleDrive";
    /// <summary>OneDrive, through Microsoft Graph.</summary>
    public const string OneDrive = "OneDrive";

    /// <summary>Every kind.</summary>
    public static readonly IReadOnlyList<string> All = new[] { Folder, GoogleDrive, OneDrive };

    /// <summary>The name shown to the user.</summary>
    public static string DisplayName(string kind) {
      switch (kind) {
        case GoogleDrive: return "Google Drive";
        case OneDrive: return "OneDrive";
        default: return "Folder";
      }
    }

    /// <summary>A known kind, or Folder.</summary>
    public static string Parse(string text) {
      return All.FirstOrDefault(k => string.Equals(k, text, StringComparison.OrdinalIgnoreCase)) ?? Folder;
    }
  }

  /// <summary>A file in a sync location: its name and when it was last written (UTC).</summary>
  public sealed record SyncStoreFile(string Name, DateTime ModifiedUtc);

  /// <summary>A sync location can't be reached (network down, signed out, drive missing): the round changes nothing.</summary>
  public class SyncStoreException : IOException {
    /// <summary>Creates the exception.</summary>
    public SyncStoreException(string message, Exception inner = null) : base(message, inner) { }
  }

  /// <summary>
  /// The file work of sync, so the engine is the same for a folder, Google Drive and OneDrive. Paths inside a PC's place
  /// are like "index.json" or "items/&lt;name&gt;.json". Methods throw <see cref="IOException"/> (often
  /// <see cref="SyncStoreException"/>) when the location can't be reached; a missing file is null, not an exception.
  /// </summary>
  public interface ISyncStore {
    /// <summary>The kind (see <see cref="SyncKinds"/>).</summary>
    string Kind { get; }
    /// <summary>True when the location is there now (a folder that exists, a cloud that answers).</summary>
    bool IsReachable();
    /// <summary>The device ids that have a place here.</summary>
    IReadOnlyList<string> Devices();
    /// <summary>A file at the root of the location, or null.</summary>
    byte[] ReadRoot(string name);
    /// <summary>Writes a root file only when it is not there yet; false when another PC made it first.</summary>
    bool CreateRoot(string name, byte[] content);
    /// <summary>A file of a PC, or null.</summary>
    byte[] Read(string device, string path);
    /// <summary>Writes a file of this PC (a new item or blob file; a reader checks its hash, so a half write is never used).</summary>
    void Write(string device, string path, byte[] content);
    /// <summary>Replaces this PC's index.json, keeping the one before as index.prev.json.</summary>
    void ReplaceIndex(string device, byte[] content);
    /// <summary>The files in a folder of a PC ("items" or "blobs").</summary>
    IReadOnlyList<SyncStoreFile> List(string device, string folder);
    /// <summary>Deletes a file of this PC; a file that is gone already is fine.</summary>
    void Delete(string device, string path);
  }

  /// <summary>A sync folder on disk: what 2.0 did, unchanged.</summary>
  public sealed class FolderSyncStore : ISyncStore {
    private const string TempName = "index.tmp";

    /// <summary>The store of a sync folder.</summary>
    public FolderSyncStore(string root) {
      Root = root ?? "";
    }

    /// <summary>The sync folder.</summary>
    public string Root { get; }

    /// <inheritdoc/>
    public string Kind => SyncKinds.Folder;

    /// <inheritdoc/>
    public bool IsReachable() => !string.IsNullOrWhiteSpace(Root) && Directory.Exists(Root);

    /// <inheritdoc/>
    public IReadOnlyList<string> Devices() {
      return Directory.GetDirectories(Root).Select(Path.GetFileName)
        .Where(n => !n.StartsWith(".", StringComparison.Ordinal)).OrderBy(n => n, StringComparer.Ordinal).ToList();
    }

    private string PathOf(string device, string path) => Path.Combine(Root, device, path.Replace('/', Path.DirectorySeparatorChar));

    private static byte[] ReadFile(string path) {
      try {
        return File.Exists(path) ? File.ReadAllBytes(path) : null;
      } catch (FileNotFoundException) {
        return null;
      } catch (DirectoryNotFoundException) {
        return null;
      }
    }

    /// <inheritdoc/>
    public byte[] ReadRoot(string name) => ReadFile(Path.Combine(Root, name));

    /// <inheritdoc/>
    public bool CreateRoot(string name, byte[] content) {
      try {
        using (FileStream f = new FileStream(Path.Combine(Root, name), FileMode.CreateNew, FileAccess.Write)) {
          f.Write(content, 0, content.Length);
        }
        return true;
      } catch (IOException) when (File.Exists(Path.Combine(Root, name))) {
        return false;
      }
    }

    /// <inheritdoc/>
    public byte[] Read(string device, string path) => ReadFile(PathOf(device, path));

    /// <inheritdoc/>
    public void Write(string device, string path, byte[] content) {
      string target = PathOf(device, path);
      Directory.CreateDirectory(Path.GetDirectoryName(target));
      string tmp = target + ".tmp";
      File.WriteAllBytes(tmp, content);
      File.Move(tmp, target, overwrite: true);
    }

    /// <inheritdoc/>
    public void ReplaceIndex(string device, byte[] content) {
      string folder = Path.Combine(Root, device);
      Directory.CreateDirectory(folder);
      string tmp = Path.Combine(folder, TempName);
      string current = Path.Combine(folder, SyncFolder.IndexName);
      string previous = Path.Combine(folder, SyncFolder.PreviousName);
      File.WriteAllBytes(tmp, content);
      if (File.Exists(current)) {
        File.Move(current, previous, overwrite: true);
      }
      File.Move(tmp, current, overwrite: true);
    }

    /// <inheritdoc/>
    public IReadOnlyList<SyncStoreFile> List(string device, string folder) {
      string path = Path.Combine(Root, device, folder);
      if (!Directory.Exists(path)) {
        return Array.Empty<SyncStoreFile>();
      }
      return Directory.GetFiles(path).Select(f => new SyncStoreFile(Path.GetFileName(f), File.GetLastWriteTimeUtc(f))).ToList();
    }

    /// <inheritdoc/>
    public void Delete(string device, string path) {
      string target = PathOf(device, path);
      if (File.Exists(target)) {
        File.Delete(target);
      }
    }
  }

  /// <summary>
  /// sync.json at the root of a sync location: which kind of sync uses it (and the format). The first PC writes it; a PC
  /// set to another kind finds it and reads and writes nothing there.
  /// </summary>
  public static class SyncMarker {
    /// <summary>The file name.</summary>
    public const string FileName = "sync.json";
    /// <summary>The format written.</summary>
    public const int Format = 1;

    /// <summary>The kind in the marker, null when there is none, or "" when it can't be read.</summary>
    public static string ReadKind(ISyncStore store) {
      byte[] bytes = store.ReadRoot(FileName);
      if (bytes == null) {
        return null;
      }
      try {
        using (JsonDocument doc = JsonDocument.Parse(bytes)) {
          return doc.RootElement.TryGetProperty("kind", out JsonElement k) && k.ValueKind == JsonValueKind.String ? k.GetString() ?? "" : "";
        }
      } catch (JsonException) {
        return "";
      }
    }

    /// <summary>
    /// The kind of sync that uses the location: this store's kind when it is free (the marker is written then), or the
    /// other kind found there.
    /// </summary>
    public static string Claim(ISyncStore store) {
      string kind = ReadKind(store);
      if (kind == null) {
        string text = "{\n  \"format\": " + Format + ",\n  \"kind\": \"" + store.Kind + "\"\n}\n";
        if (store.CreateRoot(FileName, Encoding.UTF8.GetBytes(text))) {
          return store.Kind;
        }
        kind = ReadKind(store);
      }
      // A marker that can't be read (half copied?) is read again next round; until then nothing is done.
      return string.IsNullOrEmpty(kind) ? "" : kind;
    }
  }
}
