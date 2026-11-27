using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace MDSHO.Data {

  /// <summary>The sync settings of this PC (AppSettings, all Local): on or off, the folder, and what sync keeps for itself.</summary>
  public static class SyncSettings {
    /// <summary>"On" when this PC syncs; no row or anything else means off.</summary>
    public const string OnKey = "SyncOn";
    /// <summary>The sync folder as this PC sees it.</summary>
    public const string FolderKey = "SyncFolder";
    /// <summary>The seq of this PC's last write, so it never goes back even when its index is lost.</summary>
    public const string SeqKey = "SyncSeq";
    /// <summary>When this PC last synced (UTC, round-trip format).</summary>
    public const string LastTimeKey = "SyncLastTime";
    /// <summary>The backup made when this PC joined, until Settings has said so once.</summary>
    public const string BackupNoticeKey = "SyncBackupNotice";

    /// <summary>The last seq of each other PC that this PC merged, as "device=seq" pairs split by ";".</summary>
    public const string SeenKey = "SyncSeen";

    /// <summary>True when sync is on and has a folder.</summary>
    public static bool IsOn => DataStore.AppSettings.Get(OnKey) == "On" && !string.IsNullOrWhiteSpace(Folder);

    /// <summary>The folder, or "".</summary>
    public static string Folder => DataStore.AppSettings.Get(FolderKey) ?? "";

    /// <summary>Saves on/off and the folder.</summary>
    public static void Save(bool on, string folder) {
      DataStore.AppSettings.Set(OnKey, on ? "On" : "Off");
      DataStore.AppSettings.Set(FolderKey, folder ?? "");
    }

    /// <summary>The seq of this PC's last write, or 0.</summary>
    internal static long Seq {
      get { return long.TryParse(DataStore.AppSettings.Get(SeqKey), NumberStyles.Integer, CultureInfo.InvariantCulture, out long seq) ? seq : 0; }
      set { DataStore.AppSettings.Set(SeqKey, value.ToString(CultureInfo.InvariantCulture)); }
    }

    /// <summary>The last seq of each other PC that this PC merged.</summary>
    internal static IReadOnlyDictionary<string, long> Seen {
      get {
        Dictionary<string, long> seen = new Dictionary<string, long>(StringComparer.Ordinal);
        foreach (string line in (DataStore.AppSettings.Get(SeenKey) ?? "").Split(';')) {
          int eq = line.LastIndexOf('=');
          if (eq > 0 && long.TryParse(line.Substring(eq + 1), NumberStyles.Integer, CultureInfo.InvariantCulture, out long seq)) {
            seen[line.Substring(0, eq)] = seq;
          }
        }
        return seen;
      }
      set {
        string text = string.Join(";", value.OrderBy(s => s.Key, StringComparer.Ordinal).Select(s => s.Key + "=" + s.Value.ToString(CultureInfo.InvariantCulture)));
        if (DataStore.AppSettings.Get(SeenKey) != text) {
          DataStore.AppSettings.Set(SeenKey, text);
        }
      }
    }

    /// <summary>When this PC last synced, or null.</summary>
    public static DateTimeOffset? LastTime {
      get {
        return DateTimeOffset.TryParse(DataStore.AppSettings.Get(LastTimeKey), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out DateTimeOffset t) ? t : null;
      }
      internal set {
        DataStore.AppSettings.Set(LastTimeKey, value?.ToString("o", CultureInfo.InvariantCulture) ?? "");
      }
    }

    /// <summary>The backup made when this PC joined, while Settings has not said so yet; else null.</summary>
    public static string BackupNotice {
      get {
        string path = DataStore.AppSettings.Get(BackupNoticeKey);
        return string.IsNullOrEmpty(path) ? null : path;
      }
      set {
        if (string.IsNullOrEmpty(value)) {
          DataStore.AppSettings.Delete(BackupNoticeKey);
        } else {
          DataStore.AppSettings.Set(BackupNoticeKey, value);
        }
      }
    }
  }
}
