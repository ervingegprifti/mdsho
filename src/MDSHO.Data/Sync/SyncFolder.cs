using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MDSHO.Data {

  /// <summary>One item in a PC's index: the file with its content and its change record.</summary>
  public sealed class SyncIndexItem {
    /// <summary>The SHA-256 of the item file (its name in items).</summary>
    public string Hash { get; set; } = "";
    /// <summary>When it was changed: UTC milliseconds.</summary>
    public long ChangedAt { get; set; }
    /// <summary>The device id of the PC that changed it.</summary>
    public string ChangedBy { get; set; } = "";
    /// <summary>The write of this index in which the entry last changed.</summary>
    public long Seq { get; set; }
    /// <summary>When the version it was changed from was made (0 when not known).</summary>
    public long BaseAt { get; set; }
    /// <summary>Who made the version it was changed from.</summary>
    public string BaseBy { get; set; } = "";
  }

  /// <summary>One delete in a PC's index.</summary>
  public sealed class SyncIndexDelete {
    /// <summary>What it was (see MDSHO.Core.DeletedItemKinds).</summary>
    public string Kind { get; set; } = "";
    /// <summary>When it was deleted: UTC milliseconds.</summary>
    public long DeletedAt { get; set; }
    /// <summary>The device id of the PC that deleted it.</summary>
    public string DeletedBy { get; set; } = "";
    /// <summary>The write of this index in which the entry last changed.</summary>
    public long Seq { get; set; }
  }

  /// <summary>
  /// A PC's index.json: what its items are, what it deleted and how far it has merged the other PCs. <see cref="Check"/>
  /// is the SHA-256 of the rest, so a half-written or broken file is never used.
  /// </summary>
  public sealed class SyncIndex {
    /// <summary>The format this MDSHO writes. A PC with a newer one is not read.</summary>
    public const int CurrentFormat = 1;

    /// <summary>The format of the folder files.</summary>
    public int Format { get; set; } = CurrentFormat;
    /// <summary>The device id of the PC (also its folder name).</summary>
    public string Device { get; set; } = "";
    /// <summary>The PC's name.</summary>
    public string Name { get; set; } = "";
    /// <summary>The MDSHO version that wrote it.</summary>
    public string AppVersion { get; set; } = "";
    /// <summary>Raised by one on each write.</summary>
    public long Seq { get; set; }
    /// <summary>When it was written: UTC milliseconds.</summary>
    public long WrittenAt { get; set; }
    /// <summary>The items by SyncId.</summary>
    public SortedDictionary<string, SyncIndexItem> Items { get; set; } = new SortedDictionary<string, SyncIndexItem>(StringComparer.Ordinal);
    /// <summary>The deletes by SyncId.</summary>
    public SortedDictionary<string, SyncIndexDelete> Deleted { get; set; } = new SortedDictionary<string, SyncIndexDelete>(StringComparer.Ordinal);
    /// <summary>The blob files the items use.</summary>
    public SortedSet<string> Blobs { get; set; } = new SortedSet<string>(StringComparer.Ordinal);
    /// <summary>The last seq of each other PC that this PC merged.</summary>
    public SortedDictionary<string, long> Seen { get; set; } = new SortedDictionary<string, long>(StringComparer.Ordinal);
    /// <summary>The SHA-256 of the index written with an empty check.</summary>
    public string Check { get; set; } = "";
  }

  /// <summary>How reading a PC's index went.</summary>
  public enum SyncIndexState {
    /// <summary>index.json was read.</summary>
    Ok,
    /// <summary>index.json was bad (half written?), index.prev.json was read.</summary>
    FromPrevious,
    /// <summary>Neither could be read: wait for the next round.</summary>
    Unreadable,
    /// <summary>The PC has written no index yet.</summary>
    Missing,
    /// <summary>The PC has a newer MDSHO: its files are not read.</summary>
    NewerFormat
  }

  /// <summary>The result of reading a PC's index. For a newer format only the name and version are read.</summary>
  public sealed class SyncIndexRead {
    /// <summary>How it went.</summary>
    public SyncIndexState State { get; set; }
    /// <summary>The index, or null.</summary>
    public SyncIndex Index { get; set; }
    /// <summary>The PC's name, when it could be read.</summary>
    public string Name { get; set; }
    /// <summary>The PC's MDSHO version, when it could be read.</summary>
    public string AppVersion { get; set; }
  }

  /// <summary>
  /// The files of the sync folder. Each PC writes only in its own subfolder, named by its device id: index.json,
  /// index.prev.json, items/&lt;sha256&gt;.json and blobs/&lt;sha256&gt;.bin. Item and blob files never change: they are
  /// named by the hash of their content, and a file whose hash does not match is still being copied.
  /// </summary>
  public static class SyncFolder {
    /// <summary>The index of a PC.</summary>
    public const string IndexName = "index.json";
    /// <summary>The index before the last write.</summary>
    public const string PreviousName = "index.prev.json";
    /// <summary>The folder of a PC's item files.</summary>
    public const string ItemsName = "items";
    /// <summary>The folder of a PC's shortcut files.</summary>
    public const string BlobsName = "blobs";
    /// <summary>Files no index uses are deleted when they are this old.</summary>
    public static readonly TimeSpan KeepUnused = TimeSpan.FromDays(7);

    internal static readonly JsonSerializerOptions Json = new JsonSerializerOptions {
      PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
      PropertyNameCaseInsensitive = true,
      WriteIndented = true,
      DefaultIgnoreCondition = JsonIgnoreCondition.Never
    };

    /// <summary>The folder of a PC: its device id inside the sync folder.</summary>
    public static string DeviceFolder(string root, string deviceId) {
      return Path.Combine(root, deviceId);
    }

    /// <summary>The device ids of the other PCs: the other subfolders.</summary>
    public static IReadOnlyList<string> OtherDevices(string root, string myId) => OtherDevices(new FolderSyncStore(root), myId);

    /// <summary>The device ids of the other PCs in a sync location.</summary>
    public static IReadOnlyList<string> OtherDevices(ISyncStore store, string myId) {
      return store.Devices().Where(n => !string.Equals(n, myId, StringComparison.OrdinalIgnoreCase)).OrderBy(n => n, StringComparer.Ordinal).ToList();
    }

    /// <summary>The SHA-256 of the bytes as lower-case hex.</summary>
    public static string Hash(byte[] bytes) {
      return Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    }

    #region INDEX
    // A PC's folder path as a store and device (the path calls below are the 2.0 API, for a sync folder on disk).
    private static (ISyncStore Store, string Device) OnDisk(string folder) {
      string full = Path.GetFullPath(folder).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
      return (new FolderSyncStore(Path.GetDirectoryName(full)), Path.GetFileName(full));
    }

    /// <summary>
    /// Writes the index: index.tmp first, then index.json becomes index.prev.json and index.tmp becomes index.json. The
    /// item and blob files must be written before.
    /// </summary>
    public static void WriteIndex(string folder, SyncIndex index) {
      (ISyncStore store, string device) = OnDisk(folder);
      WriteIndex(store, device, index);
    }

    /// <summary>Writes a PC's index through its store (a folder renames, a cloud replaces the file in one call).</summary>
    public static void WriteIndex(ISyncStore store, string device, SyncIndex index) {
      index.Format = SyncIndex.CurrentFormat;
      index.Check = CheckOf(index);
      store.ReplaceIndex(device, JsonSerializer.SerializeToUtf8Bytes(index, Json));
    }

    /// <summary>Reads a PC's index: index.json, else index.prev.json when index.json is bad. Never throws for bad files.</summary>
    public static SyncIndexRead ReadIndex(string folder) {
      (ISyncStore store, string device) = OnDisk(folder);
      return ReadIndex(store, device);
    }

    /// <summary>Reads a PC's index through its store. Throws only when the store can't be reached.</summary>
    public static SyncIndexRead ReadIndex(ISyncStore store, string device) {
      byte[] current = ReadQuietly(store, device, IndexName);
      byte[] previous = current == null || Parse(current) == null ? ReadQuietly(store, device, PreviousName) : null;
      if (current == null && previous == null) {
        return new SyncIndexRead { State = SyncIndexState.Missing };
      }
      SyncIndexRead read = current == null ? null : Parse(current);
      if (read != null) {
        return read;
      }
      read = previous == null ? null : Parse(previous);
      if (read != null) {
        if (read.State == SyncIndexState.Ok) {
          read.State = SyncIndexState.FromPrevious;
        }
        return read;
      }
      return new SyncIndexRead { State = SyncIndexState.Unreadable };
    }

    // A file that is locked or vanishes while read counts as missing; a store that can't be reached still throws.
    private static byte[] ReadQuietly(ISyncStore store, string device, string path) {
      try {
        return store.Read(device, path);
      } catch (SyncStoreException) {
        throw;
      } catch (IOException) {
        return null;
      } catch (UnauthorizedAccessException) {
        return null;
      }
    }

    // One index file: null when it is bad.
    private static SyncIndexRead Parse(byte[] bytes) {
      try {
        using (JsonDocument doc = JsonDocument.Parse(bytes)) {
          JsonElement rootElement = doc.RootElement;
          if (rootElement.ValueKind != JsonValueKind.Object) {
            return null;
          }
          int format = rootElement.TryGetProperty("format", out JsonElement f) && f.ValueKind == JsonValueKind.Number ? f.GetInt32() : 0;
          if (format > SyncIndex.CurrentFormat) {
            return new SyncIndexRead {
              State = SyncIndexState.NewerFormat,
              Name = rootElement.TryGetProperty("name", out JsonElement n) && n.ValueKind == JsonValueKind.String ? n.GetString() : null,
              AppVersion = rootElement.TryGetProperty("appVersion", out JsonElement v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null
            };
          }
        }
        SyncIndex index = JsonSerializer.Deserialize<SyncIndex>(bytes, Json);
        if (index == null) {
          return null;
        }
        // The same ordinal order as when it was written, so the check is made the same way.
        index.Items = new SortedDictionary<string, SyncIndexItem>(index.Items ?? new SortedDictionary<string, SyncIndexItem>(), StringComparer.Ordinal);
        index.Deleted = new SortedDictionary<string, SyncIndexDelete>(index.Deleted ?? new SortedDictionary<string, SyncIndexDelete>(), StringComparer.Ordinal);
        index.Seen = new SortedDictionary<string, long>(index.Seen ?? new SortedDictionary<string, long>(), StringComparer.Ordinal);
        index.Blobs = new SortedSet<string>(index.Blobs ?? new SortedSet<string>(), StringComparer.Ordinal);
        if (index.Format < 1 || string.IsNullOrEmpty(index.Check) || index.Check != CheckOf(index)) {
          return null;
        }
        return new SyncIndexRead { State = SyncIndexState.Ok, Index = index, Name = index.Name, AppVersion = index.AppVersion };
      } catch (JsonException) {
        return null;
      }
    }

    // The hash of the index written with an empty check (the input is not changed).
    private static string CheckOf(SyncIndex index) {
      string saved = index.Check;
      try {
        index.Check = "";
        return Hash(JsonSerializer.SerializeToUtf8Bytes(index, Json));
      } finally {
        index.Check = saved;
      }
    }
    #endregion INDEX

    #region ITEMS AND BLOBS
    /// <summary>Writes an item file (when it is not there yet) and returns its hash.</summary>
    public static string WriteItem(string folder, byte[] content) {
      (ISyncStore store, string device) = OnDisk(folder);
      return WriteItem(store, device, content);
    }

    /// <summary>Writes an item file through the store (when it is not there yet) and returns its hash.</summary>
    public static string WriteItem(ISyncStore store, string device, byte[] content) => WriteNamed(store, device, ItemsName, ".json", content);

    /// <summary>The content of an item file, or null when it is missing or its hash does not match (still being copied).</summary>
    public static byte[] ReadItem(string folder, string hash) {
      (ISyncStore store, string device) = OnDisk(folder);
      return ReadItem(store, device, hash);
    }

    /// <summary>The content of an item file through the store, or null when it is missing or not whole yet.</summary>
    public static byte[] ReadItem(ISyncStore store, string device, string hash) => ReadNamed(store, device, ItemsName, ".json", hash);

    /// <summary>Writes a blob file (when it is not there yet) and returns its hash.</summary>
    public static string WriteBlob(string folder, byte[] content) {
      (ISyncStore store, string device) = OnDisk(folder);
      return WriteBlob(store, device, content);
    }

    /// <summary>Writes a blob file through the store (when it is not there yet) and returns its hash.</summary>
    public static string WriteBlob(ISyncStore store, string device, byte[] content) => WriteNamed(store, device, BlobsName, ".bin", content);

    /// <summary>The content of a blob file, or null when it is missing or its hash does not match.</summary>
    public static byte[] ReadBlob(string folder, string hash) {
      (ISyncStore store, string device) = OnDisk(folder);
      return ReadBlob(store, device, hash);
    }

    /// <summary>The content of a blob file through the store, or null when it is missing or not whole yet.</summary>
    public static byte[] ReadBlob(ISyncStore store, string device, string hash) => ReadNamed(store, device, BlobsName, ".bin", hash);

    private static string WriteNamed(ISyncStore store, string device, string folder, string ext, byte[] content) {
      string hash = Hash(content);
      if (ReadNamed(store, device, folder, ext, hash) != null) {
        return hash;
      }
      store.Write(device, folder + "/" + hash + ext, content);
      return hash;
    }

    private static byte[] ReadNamed(ISyncStore store, string device, string folder, string ext, string hash) {
      if (string.IsNullOrEmpty(hash) || hash.Any(c => !Uri.IsHexDigit(c))) {
        return null;
      }
      byte[] bytes = ReadQuietly(store, device, folder + "/" + hash + ext);
      return bytes != null && Hash(bytes) == hash ? bytes : null;
    }

    /// <summary>
    /// Deletes the item and blob files that neither index.json nor index.prev.json uses once they are 7 days old (another
    /// PC may still be reading an older index). Files that can't be deleted are left for the next time.
    /// </summary>
    public static void DeleteUnused(string folder, DateTime utcNow) {
      (ISyncStore store, string device) = OnDisk(folder);
      DeleteUnused(store, device, utcNow);
    }

    /// <summary>Deletes the unused item and blob files of this PC through the store (see the folder overload).</summary>
    public static void DeleteUnused(ISyncStore store, string device, DateTime utcNow) {
      HashSet<string> items = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
      HashSet<string> blobs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
      foreach (string name in new[] { IndexName, PreviousName }) {
        byte[] bytes = ReadQuietly(store, device, name);
        if (bytes == null) {
          continue;
        }
        SyncIndexRead read = Parse(bytes);
        if (read?.Index == null) {
          return; // a bad index: don't guess what it uses
        }
        items.UnionWith(read.Index.Items.Values.Select(i => i.Hash));
        blobs.UnionWith(read.Index.Blobs);
      }
      DeleteOld(store, device, ItemsName, ".json", items, utcNow);
      DeleteOld(store, device, BlobsName, ".bin", blobs, utcNow);
    }

    private static void DeleteOld(ISyncStore store, string device, string folder, string ext, HashSet<string> used, DateTime utcNow) {
      foreach (SyncStoreFile file in store.List(device, folder)) {
        string hash = file.Name.EndsWith(ext, StringComparison.OrdinalIgnoreCase) ? file.Name.Substring(0, file.Name.Length - ext.Length) : null;
        if (hash != null && used.Contains(hash)) {
          continue;
        }
        try {
          if (utcNow - file.ModifiedUtc >= KeepUnused) {
            store.Delete(device, folder + "/" + file.Name);
          }
        } catch (SyncStoreException) {
          throw;
        } catch (IOException) {
        } catch (UnauthorizedAccessException) {
        }
      }
    }
    #endregion ITEMS AND BLOBS
  }
}
