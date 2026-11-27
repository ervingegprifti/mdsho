using System;
using System.Collections.Generic;
using System.Linq;
using MDSHO.Core;
using MDSHO.Data;
using MDSHO.ViewModels;
using Xunit;

namespace MDSHO.Tests {

  /// <summary>2.0.0: changes from other PCs show at once in the open panels: changed, added and removed panels.</summary>
  public class SyncRefreshTests : IDisposable {
    private readonly TempDb db = new TempDb();
    private readonly Action<ShortcutViewModel> originalNotesDeleted = ShortcutViewModel.NotesDeleted;
    private readonly AppViewModel app;
    private readonly long panelId;

    public SyncRefreshTests() {
      panelId = DataStore.Windows.GetAll().Single().ID;
      DataStore.Shortcuts.CreateNote(null, panelId);
      app = ViewModelFactory.CreateAppViewModel(db.Folder);
    }

    public void Dispose() {
      ShortcutViewModel.NotesDeleted = originalNotesDeleted;
      db.Dispose();
    }

    private static SyncResult Result(IEnumerable<long> changed = null, IEnumerable<long> added = null, IEnumerable<long> removed = null) {
      SyncResult r = new SyncResult { FolderReachable = true, Applied = true };
      foreach (long id in changed ?? new long[0]) { r.ChangedPanels.Add(id); }
      foreach (long id in added ?? new long[0]) { r.AddedPanels.Add(id); }
      foreach (long id in removed ?? new long[0]) { r.RemovedPanels.Add(id); }
      return r;
    }

    [Fact]
    public void A_changed_panel_shows_its_new_title_colours_and_buttons() {
      DesktopWindowViewModel panel = app.Windows.Single();
      Shortcut old = DataStore.Shortcuts.GetAll().Single();
      DataStore.Windows.UpdateTitle(panelId, "From work");
      DataStore.Windows.UpdateBorderColor(panelId, "#FF102030");
      DataStore.Shortcuts.UpdateName(old.ID, "Renamed at work");
      Shortcut added = DataStore.Shortcuts.CreateNote(null, panelId);
      List<ShortcutViewModel> closed = new List<ShortcutViewModel>();
      ShortcutViewModel.NotesDeleted = s => closed.Add(s);
      app.ApplySync(Result(changed: new[] { panelId }), w => { }, w => { });
      Assert.Equal("From work", panel.Appearance.Title);
      Assert.Equal("#FF102030", panel.Appearance.BorderColor);
      Assert.Equal(new[] { old.ID, added.ID }, panel.RootShortcuts.Select(s => s.ID));
      Assert.Equal("Renamed at work", panel.RootShortcuts[0].Name);
      Assert.Empty(closed);
    }

    [Fact]
    public void A_button_deleted_by_another_PC_closes_its_note() {
      DesktopWindowViewModel panel = app.Windows.Single();
      ShortcutViewModel note = panel.RootShortcuts.Single();
      DataStore.Shortcuts.Delete(note.ID);
      List<ShortcutViewModel> closed = new List<ShortcutViewModel>();
      ShortcutViewModel.NotesDeleted = s => closed.Add(s);
      app.ApplySync(Result(changed: new[] { panelId }), w => { }, w => { });
      Assert.Empty(panel.RootShortcuts);
      Assert.Equal(new[] { note }, closed);
    }

    [Fact]
    public void An_added_panel_opens_and_a_removed_one_closes() {
      DesktopWindow added = DataStore.Windows.Create("", 10, 10);
      List<DesktopWindowViewModel> shown = new List<DesktopWindowViewModel>();
      List<DesktopWindowViewModel> closed = new List<DesktopWindowViewModel>();
      app.ApplySync(Result(added: new[] { added.ID }), w => shown.Add(w), w => closed.Add(w));
      Assert.Equal(new[] { added.ID }, shown.Select(w => w.ID));
      Assert.Equal(2, app.Windows.Count);

      DesktopWindowViewModel first = app.Windows.First(w => w.ID == panelId);
      DataStore.Windows.Delete(panelId);
      ShortcutViewModel.NotesDeleted = s => { };
      app.ApplySync(Result(removed: new[] { panelId }), w => shown.Add(w), w => closed.Add(w));
      Assert.Equal(new[] { first }, closed);
      Assert.Equal(new[] { added.ID }, app.Windows.Select(w => w.ID));
    }
  }
}
