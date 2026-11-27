using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Microsoft.Data.Sqlite;
using MDSHO.Common;
using MDSHO.Core;

namespace MDSHO.Data {

  /// <summary>What one sync round needs.</summary>
  public sealed class SyncOptions {
    /// <summary>The sync folder.</summary>
    public string Folder { get; set; }
    /// <summary>This PC's device id (its subfolder).</summary>
    public string DeviceId { get; set; }
    /// <summary>This PC's name, written to its index and to Text history.</summary>
    public string DeviceName { get; set; }
    /// <summary>This MDSHO's version.</summary>
    public string AppVersion { get; set; }
    /// <summary>The buttons whose note, comment or snippet window is open: their texts are never changed under the user.</summary>
    public ISet<long> Editing { get; set; } = new HashSet<long>();
    /// <summary>Now as UTC milliseconds.</summary>
    public Func<long> NowMs { get; set; } = () => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    /// <summary>Makes a .lnk file point at a target on this PC (the app sets it); null keeps the file's bytes as they came.</summary>
    public Action<string, string> FixLink { get; set; }
  }

  /// <summary>Another PC in the sync, for Settings.</summary>
  public sealed class SyncPeer {
    /// <summary>Its device id.</summary>
    public string DeviceId { get; set; }
    /// <summary>Its name.</summary>
    public string Name { get; set; }
    /// <summary>Its MDSHO version.</summary>
    public string AppVersion { get; set; }
    /// <summary>When it last wrote its index.</summary>
    public DateTimeOffset? LastSync { get; set; }
    /// <summary>It has a newer MDSHO: it is not read.</summary>
    public bool NewerFormat { get; set; }
  }

  /// <summary>What a sync round did.</summary>
  public sealed class SyncResult {
    /// <summary>False when the folder could not be reached: nothing was changed.</summary>
    public bool FolderReachable { get; set; }
    /// <summary>True when changes from other PCs were applied to this PC's data.</summary>
    public bool Applied { get; set; }
    /// <summary>True when this PC's index was written.</summary>
    public bool Wrote { get; set; }
    /// <summary>The names of the PCs with a newer MDSHO.</summary>
    public List<string> NewerPcs { get; } = new List<string>();
    /// <summary>The other PCs.</summary>
    public List<SyncPeer> Peers { get; } = new List<SyncPeer>();
    /// <summary>Panels (row IDs) added by this round.</summary>
    public HashSet<long> AddedPanels { get; } = new HashSet<long>();
    /// <summary>Panels (row IDs) deleted by this round.</summary>
    public HashSet<long> RemovedPanels { get; } = new HashSet<long>();
    /// <summary>Panels (row IDs) whose look or buttons changed.</summary>
    public HashSet<long> ChangedPanels { get; } = new HashSet<long>();
    /// <summary>Why the folder could not be reached, or null.</summary>
    public Exception Error { get; set; }
  }

  /// <summary>
  /// One sync round through a folder: reads the other PCs' changes since the last merge, merges them into this PC's data
  /// in one transaction, then writes this PC's own index when something changed. Items are matched only by SyncId. The
  /// newer change wins, a tie goes to the higher device id, so every PC picks the same winner. A text both PCs changed
  /// keeps the newer one and puts the other in Text history, marked with the PC it came from.
  /// </summary>
  public static class SyncEngine {
    private static readonly string[] TextFields = { "Comment", "NoteText", "SnippetText" };
    private static readonly string[] PathFields = { "Target", "ProcessFileName", "ProcessWorkingFolder" };
    private const string Panels = "MDSHOWindows";
    private const string Buttons = "Shortcuts";

    /// <summary>
    /// A short text that changes whenever a panel or button is changed, added or deleted here (or by a merge): the
    /// newest change records and the counts. Cheap enough to check every few seconds.
    /// </summary>
    public static string ChangeStamp() {
      using (ConnectionLease lease = ConnectionLease.Acquire())
      using (SqliteCommand cmd = lease.Connection.CreateCommand()) {
        cmd.Transaction = lease.Transaction;
        cmd.CommandText = "SELECT (SELECT COALESCE(MAX(ChangedAt), 0) || '/' || COUNT(*) FROM " + Panels + ") || ' ' || "
          + "(SELECT COALESCE(MAX(ChangedAt), 0) || '/' || COUNT(*) FROM " + Buttons + ") || ' ' || "
          + "(SELECT COALESCE(MAX(DeletedAt), 0) || '/' || COUNT(*) FROM DeletedItems)";
        return Convert.ToString(cmd.ExecuteScalar(), CultureInfo.InvariantCulture);
      }
    }

    /// <summary>Runs one round. Never blocks on a missing folder: it returns at once with FolderReachable false.</summary>
    public static SyncResult Run(SyncOptions options) {
      SyncResult result = new SyncResult();
      if (string.IsNullOrWhiteSpace(options.Folder) || !Directory.Exists(options.Folder)) {
        return result;
      }
      result.FolderReachable = true;
      try {
        Round round = new Round(options, result);
        round.Run();
      } catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException) {
        AppLog.Warning(ex, "Sync could not reach the folder {Folder}", options.Folder);
        result.FolderReachable = false;
        result.Error = ex;
      }
      return result;
    }

    // One version of an item: a panel, a button or a delete, from this PC (Source null) or another.
    private sealed class Version {
      public string SyncId;
      public long At;
      public string By = "";
      public bool IsDelete;
      public string Kind = "";
      public SyncItem Item;
      public string Source;
      public long BaseAt;
      public string BaseBy = "";
      public bool Bumped;

      public bool SameAs(Version other) => other != null && At == other.At && By == other.By;
      public bool IsBaseOf(Version other) => other.BaseAt == At && other.BaseBy == By && At != 0;
    }

    // A row of this PC's data.
    private sealed class Row {
      public long Id;
      public Dictionary<string, object> Values = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
      public object this[string column] => Values.TryGetValue(column, out object v) ? v : null;
      public long Long(string column) => this[column] is long l ? l : 0;
      public string Text(string column) => this[column] as string ?? "";
    }

    // This PC's data at one moment.
    private sealed class Local {
      public Dictionary<string, Row> Panels = new Dictionary<string, Row>(StringComparer.Ordinal);
      public Dictionary<string, Row> Buttons = new Dictionary<string, Row>(StringComparer.Ordinal);
      public Dictionary<long, string> PanelSync = new Dictionary<long, string>();
      public Dictionary<long, string> ButtonSync = new Dictionary<long, string>();
      public Dictionary<long, (long Wrap, long Size)> NoteWindows = new Dictionary<long, (long, long)>();
      public Dictionary<string, DeletedItem> Deleted = new Dictionary<string, DeletedItem>(StringComparer.Ordinal);
    }

    private sealed class Round {
      private readonly SyncOptions o;
      private readonly SyncResult result;
      private readonly string myFolder;
      private readonly KnownFolders folders = KnownFolders.Current();
      private readonly long now;
      private SyncIndex prev;
      private readonly Dictionary<string, string> names = new Dictionary<string, string>(StringComparer.Ordinal);
      private readonly SortedDictionary<string, long> seen = new SortedDictionary<string, long>(StringComparer.Ordinal);
      // What was merged of each PC before this round, to go back to when one of its items has to wait.
      private readonly Dictionary<string, long> seenBefore = new Dictionary<string, long>(StringComparer.Ordinal);
      private readonly List<Version> incoming = new List<Version>();
      private readonly Dictionary<string, byte[]> blobs = new Dictionary<string, byte[]>(StringComparer.Ordinal);
      // The base of each version adopted from another PC this round.
      private readonly Dictionary<string, (long At, string By)> adoptedBase = new Dictionary<string, (long, string)>(StringComparer.Ordinal);

      public Round(SyncOptions options, SyncResult result) {
        this.o = options;
        this.result = result;
        this.myFolder = SyncFolder.DeviceFolder(options.Folder, options.DeviceId);
        this.now = options.NowMs();
        this.names[options.DeviceId] = options.DeviceName ?? "";
      }

      public void Run() {
        SyncIndexRead own = SyncFolder.ReadIndex(this.myFolder);
        this.prev = own.Index;
        if (this.prev != null) {
          foreach (KeyValuePair<string, long> s in this.prev.Seen) {
            this.seen[s.Key] = s.Value;
          }
        }
        // A round that only read is not written to the folder: what it merged is kept on this PC.
        foreach (KeyValuePair<string, long> s in SyncSettings.Seen) {
          if (!this.seen.TryGetValue(s.Key, out long known) || s.Value > known) {
            this.seen[s.Key] = s.Value;
          }
        }
        ReadOthers();
        Merge();
        WriteOwn();
        SyncSettings.LastTime = DateTimeOffset.FromUnixTimeMilliseconds(this.now);
      }

      #region READING THE OTHER PCS
      private void ReadOthers() {
        foreach (string device in SyncFolder.OtherDevices(this.o.Folder, this.o.DeviceId)) {
          string folder = SyncFolder.DeviceFolder(this.o.Folder, device);
          SyncIndexRead read = SyncFolder.ReadIndex(folder);
          if (read.State == SyncIndexState.Missing) {
            continue;
          }
          SyncPeer peer = new SyncPeer { DeviceId = device, Name = read.Name ?? device, AppVersion = read.AppVersion ?? "" };
          if (read.State == SyncIndexState.NewerFormat) {
            peer.NewerFormat = true;
            this.result.Peers.Add(peer);
            this.result.NewerPcs.Add(peer.Name);
            this.names[device] = peer.Name;
            continue;
          }
          if (read.Index == null) {
            // Unreadable: wait for the next round.
            this.result.Peers.Add(peer);
            continue;
          }
          SyncIndex index = read.Index;
          peer.LastSync = index.WrittenAt > 0 ? DateTimeOffset.FromUnixTimeMilliseconds(index.WrittenAt) : null;
          this.result.Peers.Add(peer);
          this.names[index.Device ?? device] = index.Name ?? device;
          this.names[device] = index.Name ?? device;
          long last = this.seen.TryGetValue(device, out long s) ? s : 0;
          if (index.Seq == last || (read.State == SyncIndexState.FromPrevious && index.Seq < last)) {
            continue;
          }
          if (index.Seq < last) {
            // That PC started again from scratch: read it all.
            last = 0;
          }
          this.seenBefore[device] = last;
          bool complete = true;
          foreach (KeyValuePair<string, SyncIndexItem> e in index.Items.Where(e => e.Value.Seq > last)) {
            SyncItem item = LoadItem(folder, e.Key, e.Value);
            if (item == null) {
              complete = false;
              continue;
            }
            this.incoming.Add(new Version {
              SyncId = item.SyncId, At = item.ChangedAt, By = item.ChangedBy, Item = item, Source = device,
              BaseAt = item.BaseAt, BaseBy = item.BaseBy, Kind = item.IsPanel ? DeletedItemKinds.Panel : ""
            });
          }
          foreach (KeyValuePair<string, SyncIndexDelete> d in index.Deleted.Where(d => d.Value.Seq > last)) {
            this.incoming.Add(new Version { SyncId = d.Key, At = d.Value.DeletedAt, By = d.Value.DeletedBy ?? "", IsDelete = true, Kind = d.Value.Kind ?? "", Source = device });
          }
          // A file still being copied is read again next round.
          this.seen[device] = complete ? index.Seq : last;
        }
      }

      // The item of an index entry, or null when its file (or its blob) is missing, still being copied or not this entry.
      private SyncItem LoadItem(string folder, string syncId, SyncIndexItem entry) {
        byte[] bytes = SyncFolder.ReadItem(folder, entry.Hash);
        SyncItem item = bytes == null ? null : SyncItem.Parse(bytes);
        if (item == null || item.SyncId != syncId) {
          return null;
        }
        if (item.FileHash != null && !this.blobs.ContainsKey(item.FileHash)) {
          byte[] blob = SyncFolder.ReadBlob(folder, item.FileHash);
          if (blob == null) {
            return null;
          }
          this.blobs[item.FileHash] = blob;
        }
        return item;
      }
      #endregion READING THE OTHER PCS

      #region THIS PC'S DATA
      private static Local ReadLocal() {
        Local local = new Local();
        using (ConnectionLease lease = ConnectionLease.Acquire()) {
          foreach (Row row in Rows(lease, "SELECT * FROM " + Panels)) {
            string id = row.Text("SyncId");
            if (id.Length > 0) {
              local.Panels[id] = row;
              local.PanelSync[row.Id] = id;
            }
          }
          foreach (Row row in Rows(lease, "SELECT * FROM " + Buttons)) {
            string id = row.Text("SyncId");
            if (id.Length > 0) {
              local.Buttons[id] = row;
              local.ButtonSync[row.Id] = id;
            }
          }
          foreach (Row row in Rows(lease, "SELECT ShortcutID AS ID, IsWrapText, FontSize FROM NoteWindows")) {
            local.NoteWindows[row.Id] = (row.Long("IsWrapText"), row.Long("FontSize"));
          }
          foreach (Row row in Rows(lease, "SELECT 0 AS ID, SyncId, Kind, DeletedAt, DeletedBy FROM DeletedItems")) {
            local.Deleted[row.Text("SyncId")] = new DeletedItem { SyncId = row.Text("SyncId"), Kind = row.Text("Kind"), DeletedAt = row.Long("DeletedAt"), DeletedBy = row.Text("DeletedBy") };
          }
        }
        return local;
      }

      private static List<Row> Rows(ConnectionLease lease, string sql) {
        List<Row> rows = new List<Row>();
        using (SqliteCommand cmd = lease.Connection.CreateCommand()) {
          cmd.Transaction = lease.Transaction;
          cmd.CommandText = sql;
          using (SqliteDataReader r = cmd.ExecuteReader()) {
            while (r.Read()) {
              Row row = new Row();
              for (int i = 0; i < r.FieldCount; i++) {
                row.Values[r.GetName(i)] = r.IsDBNull(i) ? null : r.GetValue(i);
              }
              row.Id = row["ID"] is long id ? id : 0;
              rows.Add(row);
            }
          }
        }
        return rows;
      }

      private static IEnumerable<string> ItemColumns(string table) {
        return SyncFields.WatchedColumns(table).Where(c => c != "ParentShortcutID" && c != "ParentMDSHOWindowID");
      }

      // The item of a panel row.
      private SyncItem PanelItem(Row row) {
        SyncItem item = new SyncItem { Kind = SyncItem.PanelKind, SyncId = row.Text("SyncId"), ChangedAt = row.Long("ChangedAt"), ChangedBy = row.Text("ChangedBy") };
        foreach (string column in ItemColumns(Panels)) {
          item.Fields[column] = row[column];
        }
        return item;
      }

      // The item of a button row: parents as SyncIds, paths portable, the note window's synced columns.
      private SyncItem ButtonItem(Local local, Row row, bool withFile) {
        SyncItem item = new SyncItem { Kind = SyncItem.ButtonKind, SyncId = row.Text("SyncId"), ChangedAt = row.Long("ChangedAt"), ChangedBy = row.Text("ChangedBy") };
        foreach (string column in ItemColumns(Buttons)) {
          object value = row[column];
          if (value is string text && PathFields.Contains(column)) {
            value = PortablePaths.ToPortable(text, this.folders);
          }
          item.Fields[column] = value;
        }
        if (row.Long("IsNote") == 1) {
          (long wrap, long size) = local.NoteWindows.TryGetValue(row.Id, out var place) ? place : (0, 0);
          item.Fields[SyncItem.NoteWindowPrefix + "IsWrapText"] = wrap;
          item.Fields[SyncItem.NoteWindowPrefix + "FontSize"] = size;
        }
        if (row["ParentShortcutID"] is long group && local.ButtonSync.TryGetValue(group, out string groupSync)) {
          item.Parent = groupSync;
        }
        item.Panel = RootPanel(local, row);
        if (withFile) {
          byte[] file = FileBytes(row);
          if (file != null) {
            item.FileHash = SyncFolder.Hash(file);
          }
        }
        return item;
      }

      // The SyncId of the panel a button sits in, through its groups.
      private static string RootPanel(Local local, Row row) {
        Row cur = row;
        for (int depth = 0; cur != null && depth <= local.Buttons.Count; depth++) {
          if (cur["ParentMDSHOWindowID"] is long panel && cur["ParentShortcutID"] == null) {
            return local.PanelSync.TryGetValue(panel, out string p) ? p : null;
          }
          cur = cur["ParentShortcutID"] is long g && local.ButtonSync.TryGetValue(g, out string gs) ? local.Buttons[gs] : null;
        }
        return null;
      }

      private static byte[] FileBytes(Row row) {
        string guid = row.Text("Guid");
        string ext = row.Text("Ext");
        if (guid.Length == 0 || ext.Length == 0) {
          return null;
        }
        try {
          string path = Paths.GetShortcutFilePath(guid, ext);
          return File.Exists(path) ? File.ReadAllBytes(path) : null;
        } catch (IOException) {
          return null;
        } catch (UnauthorizedAccessException) {
          return null;
        }
      }

      // The base of this PC's version of an item: what it was changed from, as far as this PC's last index tells.
      private (long At, string By) LocalBase(string syncId, long at, string by) {
        if (this.adoptedBase.TryGetValue(syncId, out var adopted)) {
          return adopted;
        }
        if (this.prev == null || !this.prev.Items.TryGetValue(syncId, out SyncIndexItem entry)) {
          return (0, "");
        }
        if (entry.ChangedAt == at && entry.ChangedBy == by) {
          return (entry.BaseAt, entry.BaseBy ?? "");
        }
        return (entry.ChangedAt, entry.ChangedBy ?? "");
      }
      #endregion THIS PC'S DATA

      #region MERGING
      private void Merge() {
        if (this.incoming.Count == 0) {
          return;
        }
        using (ITransactionScope tx = new UnitOfWork()) {
          Local local = ReadLocal();
          bool changed = Apply(local);
          tx.Commit();
          this.result.Applied = changed;
        }
        WriteFiles();
      }

      private readonly List<(string Path, byte[] Bytes, string Target, string Ext)> filesToWrite = new List<(string, byte[], string, string)>();

      private bool Apply(Local local) {
        // Every version of every item: this PC's first (so an equal version keeps this PC's), then the others.
        Dictionary<string, List<Version>> versions = new Dictionary<string, List<Version>>(StringComparer.Ordinal);
        void Add(Version v) {
          if (!versions.TryGetValue(v.SyncId, out List<Version> list)) {
            versions[v.SyncId] = list = new List<Version>();
          }
          list.Add(v);
        }
        foreach (KeyValuePair<string, Row> p in local.Panels) {
          SyncItem item = PanelItem(p.Value);
          (long ba, string bb) = LocalBase(p.Key, item.ChangedAt, item.ChangedBy);
          Add(new Version { SyncId = p.Key, At = item.ChangedAt, By = item.ChangedBy, Item = item, BaseAt = ba, BaseBy = bb, Kind = DeletedItemKinds.Panel });
        }
        foreach (KeyValuePair<string, Row> b in local.Buttons) {
          SyncItem item = ButtonItem(local, b.Value, false);
          (long ba, string bb) = LocalBase(b.Key, item.ChangedAt, item.ChangedBy);
          Add(new Version { SyncId = b.Key, At = item.ChangedAt, By = item.ChangedBy, Item = item, BaseAt = ba, BaseBy = bb });
        }
        foreach (DeletedItem d in local.Deleted.Values) {
          if (!local.Panels.ContainsKey(d.SyncId) && !local.Buttons.ContainsKey(d.SyncId)) {
            Add(new Version { SyncId = d.SyncId, At = d.DeletedAt, By = d.DeletedBy ?? "", IsDelete = true, Kind = d.Kind ?? "" });
          }
        }
        HashSet<string> touched = new HashSet<string>(this.incoming.Select(v => v.SyncId), StringComparer.Ordinal);
        foreach (Version v in this.incoming) {
          Add(v);
        }

        // The winner of each item: the newer change, a tie to the higher device id.
        Dictionary<string, Version> final = new Dictionary<string, Version>(StringComparer.Ordinal);
        foreach (KeyValuePair<string, List<Version>> e in versions) {
          Version best = e.Value[0];
          foreach (Version v in e.Value.Skip(1)) {
            if (v.At > best.At || (v.At == best.At && string.CompareOrdinal(v.By, best.By) > 0)) {
              best = v;
            }
          }
          final[e.Key] = best;
        }

        // An item whose group or panel is not known yet (its file is still being copied) waits for the next round.
        bool waited = true;
        while (waited) {
          waited = false;
          foreach (string id in touched.ToList()) {
            Version w = final[id];
            if (w.Source == null || w.IsDelete || w.Item.IsPanel) {
              continue;
            }
            string needed = w.Item.Parent ?? w.Item.Panel;
            if (needed != null && final.ContainsKey(needed)) {
              continue;
            }
            Version mine = versions[id][0].Source == null ? versions[id][0] : null;
            if (mine != null) {
              final[id] = mine;
            } else {
              final.Remove(id);
            }
            touched.Remove(id);
            foreach (Version v in versions[id].Where(v => v.Source != null)) {
              this.seen[v.Source] = this.seenBefore.TryGetValue(v.Source, out long before) ? before : 0;
            }
            waited = true;
          }
        }

        Dictionary<string, List<(string Field, string Text, string From)>> kept = new Dictionary<string, List<(string, string, string)>>(StringComparer.Ordinal);
        void Keep(string syncId, string field, string text, string by) {
          if (!kept.TryGetValue(syncId, out var list)) {
            kept[syncId] = list = new List<(string, string, string)>();
          }
          string from = this.names.TryGetValue(by ?? "", out string n) && !string.IsNullOrEmpty(n) ? n : "another PC";
          if (!list.Contains((field, text, from))) {
            list.Add((field, text, from));
          }
        }

        // A text window open on this PC keeps its texts: the incoming ones go to Text history and this PC's version wins next.
        foreach (string id in touched) {
          Version w = final[id];
          if (w.Source == null || w.IsDelete || w.Item.IsPanel || !local.Buttons.TryGetValue(id, out Row row) || !this.o.Editing.Contains(row.Id)) {
            continue;
          }
          bool differs = false;
          SyncItem copy = Copy(w.Item);
          foreach (string field in TextFields) {
            string mine = row.Text(field);
            if (copy.Text(field) != mine) {
              Keep(id, HistoryField(field), copy.Text(field), w.By);
              copy.Fields[field] = mine;
              differs = true;
            }
          }
          if (differs) {
            final[id] = Bump(w, copy);
          }
        }

        // Clashes: a text that loses to a version not made from it goes to Text history.
        foreach (string id in touched) {
          Version w = final[id];
          if (w.IsDelete || w.Item.IsPanel) {
            continue;
          }
          foreach (Version v in versions[id]) {
            if (v.IsDelete || v.SameAs(w) || v.By == w.By || v.IsBaseOf(w) || w.IsBaseOf(v)) {
              continue;
            }
            foreach (string field in TextFields) {
              string lost = v.Item.Text(field);
              if (lost.Length > 0 && lost != w.Item.Text(field)) {
                Keep(id, HistoryField(field), lost, v.By);
              }
            }
          }
        }

        FixStructure(local, versions, final);

        // What changes on this PC: winners that are not this PC's version.
        List<Version> changes = final.Values.Where(v => v.Bumped || (v.Source != null && !IsLocalVersion(local, v))).ToList();
        if (changes.Count == 0 && kept.Count == 0) {
          return false;
        }
        foreach (Version v in changes.Where(v => v.Source != null && !v.Bumped)) {
          this.adoptedBase[v.SyncId] = (v.BaseAt, v.BaseBy);
        }

        Dictionary<string, long> panelIds = local.Panels.ToDictionary(p => p.Key, p => p.Value.Id, StringComparer.Ordinal);
        Dictionary<string, long> buttonIds = local.Buttons.ToDictionary(b => b.Key, b => b.Value.Id, StringComparer.Ordinal);
        HashSet<string> changedPanels = new HashSet<string>(StringComparer.Ordinal);

        // Panels.
        int added = 0;
        foreach (Version v in changes.Where(v => !v.IsDelete && v.Item.IsPanel)) {
          if (panelIds.TryGetValue(v.SyncId, out long id)) {
            Exec("UPDATE " + Panels + " SET ChangedAt = -1 WHERE ID = @id", new Dictionary<string, object> { ["@id"] = id });
            UpdateRow(Panels, id, v.Item.Fields, ("ChangedAt", v.At), ("ChangedBy", v.By));
            changedPanels.Add(v.SyncId);
          } else {
            Dictionary<string, object> values = new Dictionary<string, object>(v.Item.Fields, StringComparer.Ordinal) {
              ["ScreenDeviceName"] = "", ["Left"] = 100L + 40L * added, ["Top"] = 100L + 40L * added,
              ["Width"] = (long)AppConstants.InitialWindowWidth, ["Height"] = (long)AppConstants.InitialWindowHeight,
              ["SyncId"] = v.SyncId, ["ChangedAt"] = v.At, ["ChangedBy"] = v.By
            };
            added++;
            id = InsertRow(Panels, values);
            panelIds[v.SyncId] = id;
            this.result.AddedPanels.Add(id);
          }
        }

        // Buttons, first at the top of their panel, then (once all exist) in their group.
        List<Version> buttonChanges = changes.Where(v => !v.IsDelete && !v.Item.IsPanel).ToList();
        HashSet<string> placed = new HashSet<string>(StringComparer.Ordinal);
        foreach (Version v in buttonChanges) {
          string panel = FinalPanel(final, v);
          if (panel == null || !panelIds.TryGetValue(panel, out long panelId)) {
            AppLog.Warning(null, "Sync could not place an item from another PC; it waits");
            continue;
          }
          placed.Add(v.SyncId);
          Dictionary<string, object> values = ButtonValues(v.Item);
          values["ParentShortcutID"] = null;
          values["ParentMDSHOWindowID"] = panelId;
          values["ChangedAt"] = -1L;
          if (buttonIds.TryGetValue(v.SyncId, out long id)) {
            UpdateRow(Buttons, id, values);
            if (v.Source != null && v.Item.FileHash != null && this.blobs.TryGetValue(v.Item.FileHash, out byte[] blob)) {
              Row row = local.Buttons[v.SyncId];
              string guid = row.Text("Guid");
              if (guid.Length == 0) {
                guid = Guid.NewGuid().ToString();
                UpdateRow(Buttons, id, new Dictionary<string, object> { ["Guid"] = guid });
              }
              QueueFile(guid, v.Item, blob);
            }
          } else {
            string guid = Guid.NewGuid().ToString();
            values["Guid"] = guid;
            values["IsGroupExpanded"] = v.Item.Text("IsGroup") == "1" ? 1L : 0L;
            values["SyncId"] = v.SyncId;
            values["ChangedBy"] = "";
            id = InsertRow(Buttons, values);
            buttonIds[v.SyncId] = id;
            if (v.Item.FileHash != null && this.blobs.TryGetValue(v.Item.FileHash, out byte[] blob)) {
              QueueFile(guid, v.Item, blob);
            }
          }
          changedPanels.Add(panel);
          if (local.Buttons.TryGetValue(v.SyncId, out Row before) && RootPanel(local, before) is string was) {
            changedPanels.Add(was);
          }
          // The note window's text size and wrap; its trigger touches the note, which the next step sets right.
          if (v.Item.Fields.ContainsKey(SyncItem.NoteWindowPrefix + "FontSize")) {
            long wrap = v.Item.Fields.TryGetValue(SyncItem.NoteWindowPrefix + "IsWrapText", out object w) && w is long wl ? wl : 0;
            long size = v.Item.Fields.TryGetValue(SyncItem.NoteWindowPrefix + "FontSize", out object s) && s is long sl ? sl : 0;
            if (local.NoteWindows.TryGetValue(id, out var place)) {
              if (place != (wrap, size)) {
                Exec("UPDATE NoteWindows SET IsWrapText = @wrap, FontSize = @size WHERE ShortcutID = @id",
                  new Dictionary<string, object> { ["@wrap"] = wrap, ["@size"] = size, ["@id"] = id });
              }
            } else if (wrap != 0 || size != 0) {
              Exec("INSERT INTO NoteWindows (ShortcutID, Left, Top, Width, Height, State, IsOpen, IsTopmost, IsWrapText, FontSize) VALUES (@id, 0, 0, 0, 0, 0, 0, 0, @wrap, @size)",
                new Dictionary<string, object> { ["@wrap"] = wrap, ["@size"] = size, ["@id"] = id });
            }
          }
        }
        foreach (Version v in buttonChanges) {
          if (!placed.Contains(v.SyncId) || !buttonIds.TryGetValue(v.SyncId, out long id)) {
            continue;
          }
          string group = FinalGroup(final, v);
          object parentGroup = group != null && buttonIds.TryGetValue(group, out long g) ? g : null;
          object parentPanel = parentGroup == null && FinalPanel(final, v) is string p && panelIds.TryGetValue(p, out long pid) ? pid : null;
          UpdateRow(Buttons, id, new Dictionary<string, object> { ["ParentShortcutID"] = parentGroup, ["ParentMDSHOWindowID"] = parentPanel, ["ChangedAt"] = v.At, ["ChangedBy"] = v.By });
        }

        // Deletes: buttons, then panels. A group's items that stay were moved out above.
        foreach (Version v in changes.Where(v => v.IsDelete)) {
          if (buttonIds.TryGetValue(v.SyncId, out long id)) {
            if (RootPanel(local, local.Buttons[v.SyncId]) is string was) {
              changedPanels.Add(was);
            }
            Exec("DELETE FROM " + Buttons + " WHERE ID = @id", new Dictionary<string, object> { ["@id"] = id });
          }
        }
        foreach (Version v in changes.Where(v => v.IsDelete)) {
          if (panelIds.TryGetValue(v.SyncId, out long id)) {
            Exec("DELETE FROM " + Panels + " WHERE ID = @id", new Dictionary<string, object> { ["@id"] = id });
            this.result.RemovedPanels.Add(id);
            changedPanels.Remove(v.SyncId);
          }
        }
        // The deletes keep the other PC's record (the triggers wrote this PC's), also for items this PC never had.
        foreach (Version v in final.Values.Where(v => v.IsDelete && v.Source != null)) {
          Exec(@"INSERT INTO DeletedItems (SyncId, Kind, DeletedAt, DeletedBy) VALUES (@id, @kind, @at, @by)
            ON CONFLICT(SyncId) DO UPDATE SET Kind = CASE WHEN excluded.Kind = '' THEN Kind ELSE excluded.Kind END, DeletedAt = excluded.DeletedAt, DeletedBy = excluded.DeletedBy",
            new Dictionary<string, object> { ["@id"] = v.SyncId, ["@kind"] = v.Kind ?? "", ["@at"] = v.At, ["@by"] = v.By });
        }

        // Text history.
        int keep = Math.Max(1, TextHistorySetting.Keep);
        string keptAt = TextHistory.Stamp(DateTimeOffset.FromUnixTimeMilliseconds(this.now).LocalDateTime);
        foreach (KeyValuePair<string, List<(string Field, string Text, string From)>> k in kept) {
          if (!buttonIds.TryGetValue(k.Key, out long id) || final[k.Key].IsDelete) {
            continue;
          }
          foreach ((string field, string text, string from) in k.Value) {
            long exists = Convert.ToInt64(Scalar("SELECT count(*) FROM History WHERE ShortcutID = @id AND Field = @field AND Text = @text AND FromPc = @from",
              new Dictionary<string, object> { ["@id"] = id, ["@field"] = field, ["@text"] = text, ["@from"] = from }), CultureInfo.InvariantCulture);
            if (exists == 0) {
              DataStore.History.Add(new HistoryEntry { ShortcutID = id, Field = field, Text = text, KeptAt = keptAt, FromPc = from });
            }
          }
          DataStore.History.Trim(id, HistoryFields.Text, keep);
          DataStore.History.Trim(id, HistoryFields.Comment, keep);
        }

        foreach (string panel in changedPanels) {
          if (panelIds.TryGetValue(panel, out long id) && !this.result.AddedPanels.Contains(id) && !this.result.RemovedPanels.Contains(id)) {
            this.result.ChangedPanels.Add(id);
          }
        }
        AppLog.Information("Sync applied {Changes} changes from other PCs", changes.Count);
        return true;
      }

      private static bool IsLocalVersion(Local local, Version v) {
        if (v.IsDelete) {
          return !local.Panels.ContainsKey(v.SyncId) && !local.Buttons.ContainsKey(v.SyncId)
            && local.Deleted.TryGetValue(v.SyncId, out DeletedItem d) && d.DeletedAt == v.At && d.DeletedBy == v.By;
        }
        Row row = v.Item.IsPanel ? (local.Panels.TryGetValue(v.SyncId, out Row p) ? p : null) : (local.Buttons.TryGetValue(v.SyncId, out Row b) ? b : null);
        return row != null && row.Long("ChangedAt") == v.At && row.Text("ChangedBy") == v.By;
      }

      private static string HistoryField(string field) => field == "Comment" ? HistoryFields.Comment : HistoryFields.Text;

      private static SyncItem Copy(SyncItem item) {
        return new SyncItem {
          Kind = item.Kind, SyncId = item.SyncId, ChangedAt = item.ChangedAt, ChangedBy = item.ChangedBy, BaseAt = item.BaseAt, BaseBy = item.BaseBy,
          Parent = item.Parent, Panel = item.Panel, FileHash = item.FileHash,
          Fields = new SortedDictionary<string, object>(item.Fields, StringComparer.Ordinal)
        };
      }

      // A change this PC makes while merging: newer than the version it changes, by this PC, so it reaches the others.
      private Version Bump(Version v, SyncItem item) {
        long at = Math.Max(this.now, v.At + 1);
        item.ChangedAt = at;
        item.ChangedBy = this.o.DeviceId;
        return new Version { SyncId = v.SyncId, At = at, By = this.o.DeviceId, Item = item, Kind = v.Kind, Source = v.Source, Bumped = true };
      }

      private static bool IsLiveGroup(Dictionary<string, Version> final, string id) {
        return id != null && final.TryGetValue(id, out Version g) && !g.IsDelete && !g.Item.IsPanel && g.Item.Text("IsGroup") == "1";
      }

      private static bool IsLivePanel(Dictionary<string, Version> final, string id) {
        return id != null && final.TryGetValue(id, out Version p) && !p.IsDelete && p.Item.IsPanel;
      }

      // The group a button ends up in, or null at the top of its panel.
      private static string FinalGroup(Dictionary<string, Version> final, Version v) {
        return IsLiveGroup(final, v.Item.Parent) ? v.Item.Parent : null;
      }

      // The panel a button ends up in, through its groups.
      private static string FinalPanel(Dictionary<string, Version> final, Version v) {
        Version cur = v;
        for (int depth = 0; depth <= final.Count; depth++) {
          string group = FinalGroup(final, cur);
          if (group == null) {
            return cur.Item.Panel;
          }
          cur = final[group];
        }
        return null;
      }

      /// <summary>
      /// Nothing the user made is dropped: an item whose group was deleted goes to the top of its panel; a panel deleted
      /// while an item in it lives comes back; a move that makes a loop is undone (the older one, so the newer move wins).
      /// </summary>
      private void FixStructure(Local local, Dictionary<string, List<Version>> versions, Dictionary<string, Version> final) {
        List<string> live = final.Where(f => !f.Value.IsDelete && !f.Value.Item.IsPanel).Select(f => f.Key).ToList();
        // Loops among groups.
        bool again = true;
        while (again) {
          again = false;
          foreach (string id in live) {
            List<string> path = new List<string>();
            string cur = id;
            while (cur != null && !path.Contains(cur)) {
              path.Add(cur);
              cur = FinalGroup(final, final[cur]);
            }
            if (cur == null) {
              continue;
            }
            List<string> loop = path.Skip(path.IndexOf(cur)).ToList();
            string older = loop.OrderBy(x => final[x].At).ThenBy(x => final[x].By, StringComparer.Ordinal).First();
            Version v = final[older];
            SyncItem item = Copy(v.Item);
            item.Panel = LoopPanel(final, loop) ?? item.Panel;
            item.Parent = null;
            final[older] = Bump(v, item);
            again = true;
            break;
          }
        }
        // An item whose group is gone goes to the top of its panel.
        foreach (string id in live) {
          Version v = final[id];
          if (v.Item.Parent != null && !IsLiveGroup(final, v.Item.Parent)) {
            SyncItem item = Copy(v.Item);
            item.Parent = null;
            final[id] = Bump(v, item);
          }
        }
        // A deleted panel with a living item in it comes back.
        foreach (string id in live) {
          Version v = final[id];
          string panel = FinalPanel(final, v);
          if (IsLivePanel(final, panel)) {
            continue;
          }
          Version back = panel != null && versions.TryGetValue(panel, out List<Version> list)
            ? list.Where(x => !x.IsDelete && x.Item.IsPanel).OrderByDescending(x => x.At).ThenByDescending(x => x.By, StringComparer.Ordinal).FirstOrDefault()
            : null;
          if (back != null) {
            long deletedAt = final.TryGetValue(panel, out Version gone) ? gone.At : 0;
            Version revived = Bump(new Version { SyncId = panel, At = Math.Max(back.At, deletedAt), By = back.By, Item = back.Item, Kind = DeletedItemKinds.Panel, Source = back.Source }, Copy(back.Item));
            final[panel] = revived;
            AppLog.Information("Sync brought back a deleted panel that still has items");
            continue;
          }
          // The panel is not known at all: the first panel of this PC takes the item.
          string first = final.Where(f => !f.Value.IsDelete && f.Value.Item.IsPanel).OrderBy(f => f.Value.Item.Text("SortKey"), StringComparer.Ordinal).ThenBy(f => f.Key, StringComparer.Ordinal).Select(f => f.Key).FirstOrDefault();
          if (first != null) {
            Version top = TopOf(final, v);
            SyncItem item = Copy(top.Item);
            item.Parent = null;
            item.Panel = first;
            final[top.SyncId] = Bump(top, item);
          }
        }
      }

      // The panel of the first item of a loop that has one.
      private static string LoopPanel(Dictionary<string, Version> final, List<string> loop) {
        return loop.Select(x => final[x].Item.Panel).FirstOrDefault(p => p != null);
      }

      // The top button of the group chain of a button.
      private static Version TopOf(Dictionary<string, Version> final, Version v) {
        Version cur = v;
        for (int depth = 0; depth <= final.Count; depth++) {
          string group = FinalGroup(final, cur);
          if (group == null) {
            return cur;
          }
          cur = final[group];
        }
        return v;
      }
      #endregion MERGING

      #region WRITING
      private Dictionary<string, object> ButtonValues(SyncItem item) {
        Dictionary<string, object> values = new Dictionary<string, object>(StringComparer.Ordinal);
        foreach (KeyValuePair<string, object> f in item.Fields) {
          if (f.Key.StartsWith(SyncItem.NoteWindowPrefix, StringComparison.Ordinal)) {
            continue;
          }
          object value = f.Value;
          if (value is string text && PathFields.Contains(f.Key)) {
            value = PortablePaths.FromPortable(text, this.folders);
          }
          values[f.Key] = value;
        }
        return values;
      }

      private void QueueFile(string guid, SyncItem item, byte[] blob) {
        string ext = item.Text("Ext");
        if (ext.Length == 0) {
          return;
        }
        this.filesToWrite.Add((Paths.GetShortcutFilePath(guid, ext), blob, PortablePaths.FromPortable(item.Text("Target"), this.folders), ext));
      }

      // The shortcut files of the buttons that came, after the data is saved. A file that can't be written is logged.
      private void WriteFiles() {
        foreach ((string path, byte[] bytes, string target, string ext) in this.filesToWrite) {
          try {
            File.WriteAllBytes(path, bytes);
            if (this.o.FixLink != null && string.Equals(ext, AppConstants.LnkExtension, StringComparison.OrdinalIgnoreCase) && target.Length > 0) {
              this.o.FixLink(path, target);
            }
          } catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException) {
            AppLog.Warning(ex, "Sync could not write a shortcut file {Path}", path);
          }
        }
      }

      private static Dictionary<string, object> Columns(string table) {
        Dictionary<string, object> names = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        using (ConnectionLease lease = ConnectionLease.Acquire())
        using (SqliteCommand cmd = lease.Connection.CreateCommand()) {
          cmd.Transaction = lease.Transaction;
          cmd.CommandText = "SELECT name FROM pragma_table_info('" + table + "')";
          using (SqliteDataReader r = cmd.ExecuteReader()) {
            while (r.Read()) {
              names[r.GetString(0)] = null;
            }
          }
        }
        return names;
      }

      private static void UpdateRow(string table, long id, IDictionary<string, object> values, params (string Column, object Value)[] more) {
        Dictionary<string, object> columns = Columns(table);
        Dictionary<string, object> set = values.Where(v => columns.ContainsKey(v.Key) && v.Key != "ID").ToDictionary(v => v.Key, v => v.Value);
        foreach ((string column, object value) in more) {
          set[column] = value;
        }
        if (set.Count == 0) {
          return;
        }
        Dictionary<string, object> args = new Dictionary<string, object> { ["@id"] = id };
        List<string> parts = new List<string>();
        int i = 0;
        foreach (KeyValuePair<string, object> v in set) {
          parts.Add(v.Key + " = @p" + i);
          args["@p" + i] = v.Value;
          i++;
        }
        Exec("UPDATE " + table + " SET " + string.Join(", ", parts) + " WHERE ID = @id", args);
      }

      private static long InsertRow(string table, IDictionary<string, object> values) {
        Dictionary<string, object> columns = Columns(table);
        List<KeyValuePair<string, object>> set = values.Where(v => columns.ContainsKey(v.Key) && v.Key != "ID").ToList();
        Dictionary<string, object> args = new Dictionary<string, object>();
        for (int i = 0; i < set.Count; i++) {
          args["@p" + i] = set[i].Value;
        }
        string sql = "INSERT INTO " + table + " (" + string.Join(", ", set.Select(s => s.Key)) + ") VALUES ("
          + string.Join(", ", set.Select((s, i) => "@p" + i)) + "); SELECT last_insert_rowid();";
        return Convert.ToInt64(Scalar(sql, args), CultureInfo.InvariantCulture);
      }

      private static void Exec(string sql, Dictionary<string, object> args) {
        using (ConnectionLease lease = ConnectionLease.Acquire())
        using (SqliteCommand cmd = Command(lease, sql, args)) {
          cmd.ExecuteNonQuery();
        }
      }

      private static object Scalar(string sql, Dictionary<string, object> args) {
        using (ConnectionLease lease = ConnectionLease.Acquire())
        using (SqliteCommand cmd = Command(lease, sql, args)) {
          return cmd.ExecuteScalar();
        }
      }

      private static SqliteCommand Command(ConnectionLease lease, string sql, Dictionary<string, object> args) {
        SqliteCommand cmd = lease.Connection.CreateCommand();
        cmd.Transaction = lease.Transaction;
        cmd.CommandText = sql;
        foreach (KeyValuePair<string, object> a in args) {
          cmd.Parameters.AddWithValue(a.Key, a.Value ?? DBNull.Value);
        }
        return cmd;
      }

      // Writes this PC's index when something changed: new item and blob files first, then the index.
      private void WriteOwn() {
        Local local;
        using (ITransactionScope tx = new UnitOfWork()) {
          local = ReadLocal();
          tx.Commit();
        }
        long seq = Math.Max(this.prev?.Seq ?? 0, SyncSettings.Seq) + 1;
        SyncIndex index = new SyncIndex {
          Device = this.o.DeviceId, Name = this.o.DeviceName ?? "", AppVersion = this.o.AppVersion ?? "", Seq = seq, WrittenAt = this.now
        };
        foreach (KeyValuePair<string, long> s in this.seen) {
          index.Seen[s.Key] = s.Value;
        }
        Dictionary<string, byte[]> items = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        Dictionary<string, byte[]> files = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        IEnumerable<(string Id, SyncItem Item, Row Row)> all = local.Panels.Select(p => (p.Key, PanelItem(p.Value), (Row)null))
          .Concat(local.Buttons.Select(b => (b.Key, ButtonItem(local, b.Value, false), b.Value)));
        foreach ((string id, SyncItem item, Row row) in all) {
          if (row != null) {
            byte[] file = FileBytes(row);
            if (file != null) {
              item.FileHash = SyncFolder.Hash(file);
              files[item.FileHash] = file;
              index.Blobs.Add(item.FileHash);
            }
          }
          (item.BaseAt, item.BaseBy) = LocalBase(id, item.ChangedAt, item.ChangedBy);
          byte[] bytes = item.ToBytes();
          string hash = SyncFolder.Hash(bytes);
          items[hash] = bytes;
          long entrySeq = this.prev != null && this.prev.Items.TryGetValue(id, out SyncIndexItem old) && old.Hash == hash ? old.Seq : seq;
          index.Items[id] = new SyncIndexItem { Hash = hash, ChangedAt = item.ChangedAt, ChangedBy = item.ChangedBy, Seq = entrySeq, BaseAt = item.BaseAt, BaseBy = item.BaseBy };
        }
        foreach (DeletedItem d in local.Deleted.Values) {
          long entrySeq = this.prev != null && this.prev.Deleted.TryGetValue(d.SyncId, out SyncIndexDelete old) && old.DeletedAt == d.DeletedAt && old.DeletedBy == d.DeletedBy && old.Kind == d.Kind ? old.Seq : seq;
          index.Deleted[d.SyncId] = new SyncIndexDelete { Kind = d.Kind ?? "", DeletedAt = d.DeletedAt, DeletedBy = d.DeletedBy ?? "", Seq = entrySeq };
        }
        SyncSettings.Seen = this.seen;
        if (this.prev != null && Same(this.prev, index)) {
          return;
        }
        foreach (byte[] file in files.Values) {
          SyncFolder.WriteBlob(this.myFolder, file);
        }
        foreach (byte[] bytes in items.Values) {
          SyncFolder.WriteItem(this.myFolder, bytes);
        }
        SyncFolder.WriteIndex(this.myFolder, index);
        SyncSettings.Seq = seq;
        this.result.Wrote = true;
        SyncFolder.DeleteUnused(this.myFolder, DateTime.UtcNow);
      }

      // The same entries and name as the last index: nothing to write (only what was merged changed).
      private static bool Same(SyncIndex a, SyncIndex b) {
        return a.Name == b.Name && a.AppVersion == b.AppVersion && a.Format == SyncIndex.CurrentFormat
          && a.Items.Count == b.Items.Count && a.Items.All(e => b.Items.TryGetValue(e.Key, out SyncIndexItem x) && x.Hash == e.Value.Hash)
          && a.Deleted.Count == b.Deleted.Count && a.Deleted.All(e => b.Deleted.TryGetValue(e.Key, out SyncIndexDelete x) && x.DeletedAt == e.Value.DeletedAt && x.DeletedBy == e.Value.DeletedBy)
          && a.Blobs.SetEquals(b.Blobs);
      }
      #endregion WRITING
    }
  }
}
