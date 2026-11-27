using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Data.Sqlite;
using MDSHO.Common;
using MDSHO.Core;
using MDSHO.Data;

namespace MDSHO.Tests {

  /// <summary>
  /// Two or three simulated PCs, each with its own database and shortcuts folder, and one sync folder they share. Paths
  /// is static, so only one PC is "on" at a time: <see cref="SyncPc.Use"/> switches.
  /// </summary>
  public sealed class SyncPcs : IDisposable {
    private readonly Func<string, string, bool> originalConfirm = MDSHO.ViewModels.Dialogs.Confirm;
    private readonly Action<string, string> originalShowMessage = MDSHO.ViewModels.Dialogs.ShowMessage;

    /// <summary>The temp folder of the test.</summary>
    public string Root { get; } = Path.Combine(Path.GetTempPath(), "MDSHO.Tests", "pcs-" + Guid.NewGuid().ToString("N"));
    /// <summary>The shared sync folder.</summary>
    public string Folder { get; }
    /// <summary>The PCs made so far.</summary>
    public List<SyncPc> All { get; } = new List<SyncPc>();
    /// <summary>The time every sync round uses unless a test sets it.</summary>
    public long NowMs { get; set; } = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

    public SyncPcs() {
      MDSHO.ViewModels.Dialogs.Confirm = (message, caption) => true;
      MDSHO.ViewModels.Dialogs.ShowMessage = (message, caption) => throw new InvalidOperationException("A message box was shown in a test: " + caption + ": " + message);
      Folder = Path.Combine(Root, "Sync");
      Directory.CreateDirectory(Folder);
    }

    /// <summary>A new PC with a fresh database (one default panel) and the given name.</summary>
    public SyncPc Add(string name) {
      SyncPc pc = new SyncPc(this, name, Path.Combine(Root, "pc" + (All.Count + 1)));
      All.Add(pc);
      return pc;
    }

    public void Dispose() {
      MDSHO.ViewModels.Dialogs.Confirm = originalConfirm;
      MDSHO.ViewModels.Dialogs.ShowMessage = originalShowMessage;
      Paths.DatabaseFilePathOverride = null;
      Paths.ShortcutsPathOverride = null;
      Paths.ImportBackupsPathOverride = null;
      SqliteConnection.ClearAllPools();
      try { Directory.Delete(Root, true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }
  }

  /// <summary>One simulated PC.</summary>
  public sealed class SyncPc {
    private readonly SyncPcs pcs;
    public string Name { get; }
    public string Home { get; }
    public string DbPath => Path.Combine(Home, "test.sqlite3");
    public string ShortcutsPath => Path.Combine(Home, "Shortcuts");
    public string DeviceId { get; }
    /// <summary>The shortcut IDs whose text windows are open and typed in.</summary>
    public HashSet<long> Editing { get; } = new HashSet<long>();
    /// <summary>The panel made with the database.</summary>
    public long FirstPanelId { get; }

    internal SyncPc(SyncPcs pcs, string name, string home) {
      this.pcs = pcs;
      Name = name;
      Home = home;
      Directory.CreateDirectory(home);
      Use();
      DataStore.Windows.LoadAll();
      DeviceIdentity.Name = name;
      DeviceId = DeviceIdentity.Id;
      FirstPanelId = DataStore.Windows.GetAll().Single().ID;
    }

    /// <summary>Makes this PC the one Paths points at.</summary>
    public SyncPc Use() {
      Paths.DatabaseFilePathOverride = DbPath;
      Paths.ShortcutsPathOverride = ShortcutsPath;
      Paths.ImportBackupsPathOverride = Path.Combine(Home, "ImportBackups");
      TempDb.AssertNotRealPath();
      return this;
    }

    /// <summary>One sync round of this PC.</summary>
    public SyncResult Sync() {
      Use();
      return SyncEngine.Run(new SyncOptions {
        Folder = pcs.Folder,
        DeviceId = DeviceId,
        DeviceName = Name,
        AppVersion = "2.0.0",
        Editing = new HashSet<long>(Editing),
        NowMs = () => pcs.NowMs
      });
    }

    public SqliteConnection Open() {
      SqliteConnection c = new SqliteConnection("Data Source=" + DbPath + ";Mode=ReadWriteCreate;Pooling=False");
      c.Open();
      return c;
    }

    public void Exec(string sql) {
      using (SqliteConnection c = Open())
      using (SqliteCommand cmd = c.CreateCommand()) {
        cmd.CommandText = sql;
        cmd.ExecuteNonQuery();
      }
    }

    public object Scalar(string sql) {
      using (SqliteConnection c = Open())
      using (SqliteCommand cmd = c.CreateCommand()) {
        cmd.CommandText = sql;
        return cmd.ExecuteScalar();
      }
    }

    /// <summary>Sets the change record of an item (panel or button) by SyncId.</summary>
    public void Stamp(string syncId, long at) {
      Exec("UPDATE Shortcuts SET ChangedAt = " + at + " WHERE SyncId = '" + syncId + "'; UPDATE MDSHOWindows SET ChangedAt = " + at + " WHERE SyncId = '" + syncId + "';");
    }

    /// <summary>Sets the time of the delete of an item.</summary>
    public void StampDelete(string syncId, long at) {
      Exec("UPDATE DeletedItems SET DeletedAt = " + at + " WHERE SyncId = '" + syncId + "';");
    }

    public Shortcut Button(string syncId) { Use(); return DataStore.Shortcuts.GetAll().SingleOrDefault(s => s.SyncId == syncId); }
    public DesktopWindow Panel(string syncId) { Use(); return DataStore.Windows.GetAll().SingleOrDefault(w => w.SyncId == syncId); }
    public List<Shortcut> Buttons() { Use(); return DataStore.Shortcuts.GetAll(); }
    public List<DesktopWindow> Panels() { Use(); return DataStore.Windows.GetAll(); }
    public List<HistoryEntry> History() { Use(); return DataStore.History.GetAll(); }
    public List<DeletedItem> Deleted() { Use(); return DataStore.DeletedItems.GetAll(); }
    public string FirstPanelSyncId { get { Use(); return DataStore.Windows.Get(FirstPanelId).SyncId; } }

    /// <summary>Every item as text: SyncId, parent SyncIds, sort key, change record and the synced fields, sorted.</summary>
    public string Snapshot() {
      Use();
      List<DesktopWindow> panels = DataStore.Windows.GetAll();
      List<Shortcut> buttons = DataStore.Shortcuts.GetAll();
      Dictionary<long, string> panelSync = panels.ToDictionary(p => p.ID, p => p.SyncId);
      Dictionary<long, string> buttonSync = buttons.ToDictionary(b => b.ID, b => b.SyncId);
      IEnumerable<string> lines = panels.Select(p => "P " + p.SyncId + " " + p.Title + " " + p.Notes + " " + p.BorderColor + " " + p.SortKey + " " + p.ChangedAt + " " + p.ChangedBy)
        .Concat(buttons.Select(b => "B " + b.SyncId + " " + (b.ParentShortcutID != null ? buttonSync[b.ParentShortcutID.Value] : "-") + " "
          + (b.ParentWindowID != null ? panelSync[b.ParentWindowID.Value] : "-") + " " + b.Name + " " + b.NoteText + " " + b.Comment + " " + b.SortKey + " " + b.ChangedAt + " " + b.ChangedBy));
      return string.Join("\n", lines.OrderBy(l => l, StringComparer.Ordinal));
    }
  }
}
