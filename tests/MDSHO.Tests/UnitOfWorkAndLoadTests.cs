using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Microsoft.Data.Sqlite;
using MDSHO.Core;
using MDSHO.Data;
using Xunit;
using Xunit.Abstractions;

namespace MDSHO.Tests {

  /// <summary>Transaction scope semantics (commit / rollback / nesting / thread affinity), the connection factory and the tree load.</summary>
  public class UnitOfWorkAndLoadTests : IDisposable {
    private readonly ITestOutputHelper output;
    private readonly TempDb db = new TempDb();
    public UnitOfWorkAndLoadTests(ITestOutputHelper output) { this.output = output; }
    public void Dispose() { db.Dispose(); }

    private long Win() { return DataStore.Windows.LoadAll()[0].ID; }
    private long Shortcuts() { return db.Scalar("SELECT count(*) FROM Shortcuts"); }

    #region CONNECTION FACTORY
    [Fact]
    public void Connections_enforce_foreign_keys_and_use_the_overridden_path() {
      using (SqliteConnection c = ConnectionFactory.Open()) {
        Assert.Equal(db.DbPath, c.DataSource);
        SqliteCommand cmd = c.CreateCommand();
        cmd.CommandText = "PRAGMA foreign_keys;";
        Assert.Equal(1L, Convert.ToInt64(cmd.ExecuteScalar()));
      }
    }
    #endregion CONNECTION FACTORY

    #region TRANSACTION SCOPE
    [Fact]
    public void Dispose_without_commit_rolls_back_and_commit_persists() {
      long win = Win();
      using (DataStore.BeginTransaction()) {
        DataStore.Shortcuts.CreateGroup(null, win);
      }
      Assert.Equal(0, Shortcuts());
      using (ITransactionScope tx = DataStore.BeginTransaction()) {
        DataStore.Shortcuts.CreateGroup(null, win);
        DataStore.Shortcuts.CreateGroup(null, win);
        tx.Commit();
      }
      Assert.Equal(2, Shortcuts());
    }

    [Fact]
    public void Exception_inside_the_scope_rolls_everything_back_and_reads_see_uncommitted_rows() {
      long win = Win();
      Assert.Throws<InvalidOperationException>((Action)(() => {
        using (DataStore.BeginTransaction()) {
          DataStore.Shortcuts.CreateGroup(null, win);
          DataStore.Shortcuts.CreateGroup(null, win);
          Assert.Equal(2, DataStore.Shortcuts.CreateGroup(null, win).Position); // sees its own uncommitted rows
          Assert.Equal(3, DataStore.Shortcuts.GetAll().Count);
          Assert.Equal(0, Shortcuts());                                          // another connection does not
          throw new InvalidOperationException("boom");
        }
      }));
      Assert.Equal(0, Shortcuts());
    }

    [Fact]
    public void Calls_inside_a_scope_share_one_connection_and_calls_outside_do_not() {
      long win = Win();
      long before = ConnectionFactory.OpenCount;
      using (ITransactionScope tx = DataStore.BeginTransaction()) {
        DataStore.Shortcuts.CreateGroup(null, win);
        DataStore.Windows.UpdateTitle(win, "x");
        DataStore.Clones.Create(win, "D", 0, 0);
        DataStore.Windows.LoadAll();
        tx.Commit();
      }
      Assert.Equal(1, ConnectionFactory.OpenCount - before);
      before = ConnectionFactory.OpenCount;
      DataStore.Windows.UpdateTitle(win, "y");
      DataStore.Windows.UpdateTitle(win, "z");
      Assert.Equal(2, ConnectionFactory.OpenCount - before);
    }

    [Fact]
    public void Nested_scope_joins_the_outer_one_and_the_outer_decides() {
      long win = Win();
      using (ITransactionScope outer = DataStore.BeginTransaction()) {
        using (ITransactionScope inner = DataStore.BeginTransaction()) {
          DataStore.Shortcuts.CreateGroup(null, win);
          inner.Commit(); // an inner commit alone persists nothing
        }
        Assert.Equal(0, Shortcuts());
        DataStore.Shortcuts.CreateGroup(null, win);
        outer.Commit();
      }
      Assert.Equal(2, Shortcuts());

      using (DataStore.BeginTransaction()) { // outer never commits: inner commit is rolled back with it
        using (ITransactionScope inner = DataStore.BeginTransaction()) {
          DataStore.Shortcuts.CreateGroup(null, win);
          inner.Commit();
        }
      }
      Assert.Equal(2, Shortcuts());
    }

    [Fact]
    public void Nested_scope_that_is_not_committed_prevents_the_outer_commit() {
      long win = Win();
      using (ITransactionScope outer = DataStore.BeginTransaction()) {
        using (ITransactionScope inner = DataStore.BeginTransaction()) {
          DataStore.Shortcuts.CreateGroup(null, win);
          // no inner.Commit()
        }
        Assert.Throws<InvalidOperationException>(() => outer.Commit());
      }
      Assert.Equal(0, Shortcuts());
    }

    [Fact]
    public void Scopes_can_be_reused_after_a_previous_one_ended_even_after_a_failure() {
      long win = Win();
      Assert.ThrowsAny<Exception>(() => {
        using (DataStore.BeginTransaction()) {
          DataStore.Clones.Create(99999, "D", 0, 0); // FK violation
        }
      });
      using (ITransactionScope tx = DataStore.BeginTransaction()) {
        DataStore.Shortcuts.CreateGroup(null, win);
        tx.Commit();
      }
      Assert.Equal(1, Shortcuts());
    }

    [Fact]
    public void The_scope_belongs_to_its_thread_other_threads_do_not_join_it() {
      long win = Win();
      long seenOnOtherThread = -1;
      using (DataStore.BeginTransaction()) {
        DataStore.Shortcuts.CreateGroup(null, win);
        Thread t = new Thread(() => { seenOnOtherThread = DataStore.Shortcuts.GetAll().Count; });
        t.Start();
        t.Join();
      }
      Assert.Equal(0, seenOnOtherThread);
    }

    [Fact]
    public void New_window_and_clone_in_one_scope_roll_back_together() {
      long windows = db.Scalar("SELECT count(*) FROM MDSHOWindows");
      Assert.ThrowsAny<Exception>(() => {
        using (DataStore.BeginTransaction()) {
          DataStore.Windows.Create("D", 0, 0);
          DataStore.Clones.Create(99999, "D", 0, 0); // FK violation -> exception
        }
      });
      Assert.Equal(windows, db.Scalar("SELECT count(*) FROM MDSHOWindows"));
    }

    [Fact]
    public void UpdatePositions_is_atomic() {
      long win = Win();
      long a = DataStore.Shortcuts.CreateGroup(null, win).ID;
      long b = DataStore.Shortcuts.CreateGroup(null, win).ID;
      DataStore.Shortcuts.UpdatePositions(new[] { b, a });
      Assert.Equal(0, db.Scalar("SELECT Position FROM Shortcuts WHERE ID=" + b));
      Assert.Equal(1, db.Scalar("SELECT Position FROM Shortcuts WHERE ID=" + a));
      db.Exec("CREATE TRIGGER t BEFORE UPDATE OF Position ON Shortcuts WHEN NEW.Position = 1 AND OLD.ID = " + b + " BEGIN SELECT RAISE(ABORT,'no'); END;");
      Assert.ThrowsAny<Exception>(() => DataStore.Shortcuts.UpdatePositions(new[] { a, b }));
      Assert.Equal(0, db.Scalar("SELECT Position FROM Shortcuts WHERE ID=" + b));
      Assert.Equal(1, db.Scalar("SELECT Position FROM Shortcuts WHERE ID=" + a));
    }
    #endregion TRANSACTION SCOPE

    #region TREE LOAD
    /// <summary>
    /// Window 1: G1[ S1, G2[ S2 ] ], S3, P(command) + 2 clones; window 2: S4.
    /// Children are inserted so that Position order differs from insertion (ID) order.
    /// </summary>
    private (long w1, long w2) BuildSample() {
      IShortcutRepository sc = DataStore.Shortcuts;
      long w1 = Win();
      long w2 = DataStore.Windows.Create("D2", 0, 0).ID;
      Shortcut g1 = sc.CreateGroup(null, w1);
      g1.Name = "G1"; sc.UpdateName(g1.ID, "G1");
      Shortcut s3 = sc.Create(null, w1, "S3", "t", ".lnk", "g-s3");
      Shortcut p = sc.CreateProcessCommand(null, w1);
      sc.UpdateName(p.ID, "P");
      Shortcut s1 = sc.Create(g1.ID, null, "S1", "t", ".lnk", "g-s1");
      Shortcut g2 = sc.CreateGroup(g1.ID, null);
      sc.UpdateName(g2.ID, "G2");
      sc.Create(g2.ID, null, "S2", "t", ".lnk", "g-s2");
      sc.Create(null, w2, "S4", "t", ".lnk", "g-s4");
      sc.UpdatePositions(new[] { p.ID, g1.ID, s3.ID });     // roots of w1: P, G1, S3
      sc.UpdatePositions(new[] { g2.ID, s1.ID });           // children of G1: G2, S1
      DataStore.Clones.Create(w1, "C1", 1, 1);
      DataStore.Clones.Create(w1, "C2", 2, 2);
      return (w1, w2);
    }

    private static string Dump(IEnumerable<Shortcut> list) {
      return string.Join(",", list.Select(s => s.Name + (s.Shortcuts.Count > 0 ? "[" + Dump(s.Shortcuts) + "]" : "")));
    }
    /// <summary>The old N+1 algorithm expressed with the repository's per-node queries.</summary>
    private static Shortcut Recursive(long id) {
      Shortcut s = DataStore.Shortcuts.Get(id);
      foreach (Shortcut child in DataStore.Shortcuts.GetChildren(id)) { s.Shortcuts.Add(Recursive(child.ID)); }
      return s;
    }

    [Fact]
    public void LoadAll_returns_the_tree_ordered_by_Position() {
      (long w1, long w2) = BuildSample();
      List<DesktopWindow> all = DataStore.Windows.LoadAll();
      Assert.Equal(2, all.Count);
      DesktopWindow a = all.Single(w => w.ID == w1);
      DesktopWindow b = all.Single(w => w.ID == w2);
      Assert.Equal("P,G1[G2[S2],S1],S3", Dump(a.RootShortcuts));
      Assert.Equal("S4", Dump(b.RootShortcuts));
      Assert.Equal(new[] { 0L, 1L, 2L }, a.RootShortcuts.Select(s => s.Position).ToArray());
      Assert.Equal(2, a.Clones.Count);
      Assert.Empty(b.Clones);
      // Clones share the window's root shortcut list instance (existing behaviour).
      Assert.All(a.Clones, c => Assert.Same(a.RootShortcuts, c.Shortcuts));
      Assert.Equal(new[] { "C1", "C2" }, a.Clones.Select(c => c.ScreenDeviceName).ToArray());
    }

    [Fact]
    public void LoadAll_equals_the_recursive_per_node_load() {
      (long w1, long w2) = BuildSample();
      List<DesktopWindow> all = DataStore.Windows.LoadAll();
      foreach (long w in new[] { w1, w2 }) {
        List<Shortcut> expected = DataStore.Shortcuts.GetRoots(w).Select(r => Recursive(r.ID)).ToList();
        List<Shortcut> actual = all.Single(x => x.ID == w).RootShortcuts;
        Assert.Equal(Dump(expected), Dump(actual));
        AssertSameTree(expected, actual);
      }
    }
    private static void AssertSameTree(List<Shortcut> expected, List<Shortcut> actual) {
      Assert.Equal(expected.Count, actual.Count);
      for (int i = 0; i < expected.Count; i++) {
        Shortcut e = expected[i], a = actual[i];
        Assert.Equal(
          (e.ID, e.ParentShortcutID, e.ParentWindowID, e.Name, e.Target, e.Ext, e.Guid, e.IsGroup, e.IsGroupExpanded, e.Comment, e.IsMarked, e.MarkColor, e.Position, e.IsProcessCommand, e.ProcessFileName, e.ProcessArguments),
          (a.ID, a.ParentShortcutID, a.ParentWindowID, a.Name, a.Target, a.Ext, a.Guid, a.IsGroup, a.IsGroupExpanded, a.Comment, a.IsMarked, a.MarkColor, a.Position, a.IsProcessCommand, a.ProcessFileName, a.ProcessArguments));
        AssertSameTree(e.Shortcuts, a.Shortcuts);
      }
    }

    [Fact]
    public void LoadAll_uses_one_connection_however_big_the_tree_is() {
      IShortcutRepository sc = DataStore.Shortcuts;
      long w = Win();
      using (ITransactionScope tx = DataStore.BeginTransaction()) {
        for (int g = 0; g < 10; g++) {
          Shortcut group = sc.CreateGroup(null, w);
          for (int i = 0; i < 20; i++) { sc.Create(group.ID, null, "s" + i, "t", ".lnk", "g" + g + "_" + i); }
        }
        tx.Commit();
      }
      long before = ConnectionFactory.OpenCount;
      System.Diagnostics.Stopwatch sw = System.Diagnostics.Stopwatch.StartNew();
      List<DesktopWindow> all = DataStore.Windows.LoadAll();
      sw.Stop();
      Assert.Equal(1, ConnectionFactory.OpenCount - before); // before: 1 connection per query, about 2 per shortcut (>400 here)
      Assert.Equal(10, all[0].RootShortcuts.Count);
      Assert.All(all[0].RootShortcuts, g => Assert.Equal(20, g.Shortcuts.Count));
      output.WriteLine("LoadAll of 210 shortcuts: " + sw.ElapsedMilliseconds + " ms");
    }

    /// <summary>Adds windows, each with clones and groups nested <paramref name="depth"/> levels deep holding a few shortcuts.</summary>
    private static void Seed(int windows, int clonesPerWindow, int groupsPerWindow, int depth, int shortcutsPerGroup) {
      IShortcutRepository sc = DataStore.Shortcuts;
      using (ITransactionScope tx = DataStore.BeginTransaction()) {
        for (int w = 0; w < windows; w++) {
          long windowId = DataStore.Windows.Create("SEED" + w, w, w).ID;
          for (int c = 0; c < clonesPerWindow; c++) { DataStore.Clones.Create(windowId, "SEED" + w + "C" + c, c, c); }
          for (int g = 0; g < groupsPerWindow; g++) {
            Shortcut group = sc.CreateGroup(null, windowId);
            for (int d = 0; d < depth; d++) {
              for (int i = 0; i < shortcutsPerGroup; i++) { sc.Create(group.ID, null, "s" + i, "t", ".lnk", Guid.NewGuid().ToString("N")); }
              group = sc.CreateGroup(group.ID, null);
            }
          }
        }
        tx.Commit();
      }
    }

    /// <summary>Counts the SELECT statements <paramref name="load"/> runs, read from SQLite's own trace of the scope's connection.</summary>
    private static int CountSelects<T>(Func<T> load, out T result) {
      List<string> statements = new List<string>();
      long connectionsBefore = ConnectionFactory.OpenCount;
      using (ITransactionScope tx = DataStore.BeginTransaction()) {
        SqliteConnection connection = UnitOfWork.Current.Connection;
        SQLitePCL.raw.sqlite3_trace(connection.Handle, (object userData, string sql) => statements.Add(sql), null);
        try {
          result = load();
        } finally {
          SQLitePCL.raw.sqlite3_trace(connection.Handle, (SQLitePCL.strdelegate_trace)null, null);
        }
        tx.Commit();
      }
      // Every query ran on the traced connection: no other connection was opened.
      Assert.Equal(1, ConnectionFactory.OpenCount - connectionsBefore);
      return statements.Count(s => s.TrimStart().StartsWith("SELECT", StringComparison.OrdinalIgnoreCase));
    }
    private static int CountLoadAllStatements(out List<DesktopWindow> windows) {
      return CountSelects(() => DataStore.Windows.LoadAll(), out windows);
    }

    [Fact]
    public void LoadAll_runs_the_same_three_queries_however_many_windows_clones_and_nested_groups_there_are() {
      Win();
      Seed(windows: 2, clonesPerWindow: 1, groupsPerWindow: 1, depth: 2, shortcutsPerGroup: 1);
      int small = CountLoadAllStatements(out List<DesktopWindow> smallLoad);
      Seed(windows: 6, clonesPerWindow: 3, groupsPerWindow: 3, depth: 4, shortcutsPerGroup: 3);
      int large = CountLoadAllStatements(out List<DesktopWindow> largeLoad);
      output.WriteLine("SELECTs: " + small + " for " + smallLoad.Count + " windows, " + large + " for " + largeLoad.Count + " windows");
      // One query each for the windows, the shortcuts and the clones; never one per window, clone or shortcut.
      Assert.Equal(3, small);
      Assert.Equal(3, large);
      Assert.Equal(9, largeLoad.Count);
      Assert.Equal(2 + 18, largeLoad.Sum(w => w.Clones.Count));
      DesktopWindow seeded = largeLoad.Last();
      Assert.Equal(3, seeded.Clones.Count);
      Assert.Equal(3, seeded.RootShortcuts.Count);
      // Four levels of groups below each root group, with three shortcuts on every level.
      Shortcut level = seeded.RootShortcuts[0];
      for (int d = 0; d < 4; d++) {
        Assert.Equal(4, level.Shortcuts.Count);
        level = level.Shortcuts.Single(s => s.IsGroup == 1);
      }
      Assert.Empty(level.Shortcuts);
    }

    [Fact]
    public void Building_the_app_view_model_at_startup_runs_the_same_three_queries_however_big_the_data_is() {
      Win();
      Seed(windows: 1, clonesPerWindow: 1, groupsPerWindow: 1, depth: 1, shortcutsPerGroup: 1);
      int small = CountSelects(() => MDSHO.ViewModels.ViewModelFactory.CreateAppViewModel(), out MDSHO.ViewModels.AppViewModel smallApp);
      Seed(windows: 5, clonesPerWindow: 2, groupsPerWindow: 3, depth: 3, shortcutsPerGroup: 4);
      int large = CountSelects(() => MDSHO.ViewModels.ViewModelFactory.CreateAppViewModel(), out MDSHO.ViewModels.AppViewModel largeApp);
      output.WriteLine("SELECTs: " + small + " for " + smallApp.Windows.Count + " windows, " + large + " for " + largeApp.Windows.Count + " windows");
      Assert.Equal(3, small);
      Assert.Equal(3, large);
      Assert.Equal(7, largeApp.Windows.Count);
      Assert.Equal(1 + 10, largeApp.Windows.Sum(w => w.Clones.Count));
      Assert.Equal(3, largeApp.Windows.Last().RootShortcuts.Count);
    }
    #endregion TREE LOAD
  }
}
