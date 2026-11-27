using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MDSHO.Common;
using MDSHO.Core;
using MDSHO.Data;
using Xunit;

namespace MDSHO.Tests {

  /// <summary>
  /// 2.0.0: sync through a folder, run on two or three simulated PCs that share one temp folder. Items are matched only
  /// by SyncId; the newer change wins (ties: the higher device id); a text both PCs changed goes into Text history.
  /// </summary>
  public class SyncEngineTests : IDisposable {
    private readonly SyncPcs pcs = new SyncPcs();
    private readonly SyncPc a;
    private readonly SyncPc b;

    public SyncEngineTests() {
      a = pcs.Add("Home PC");
      b = pcs.Add("Work PC");
    }

    public void Dispose() { pcs.Dispose(); }

    private long T => pcs.NowMs;

    private static Shortcut Note(SyncPc pc, long? group, long? panel, string name, string text = "") {
      pc.Use();
      Shortcut s = DataStore.Shortcuts.CreateNote(group, panel);
      DataStore.Shortcuts.UpdateName(s.ID, name);
      DataStore.Shortcuts.UpdateNoteText(s.ID, text);
      return DataStore.Shortcuts.Get(s.ID);
    }

    private static Shortcut Group(SyncPc pc, long? group, long? panel, string name) {
      pc.Use();
      Shortcut g = DataStore.Shortcuts.CreateGroup(group, panel);
      DataStore.Shortcuts.UpdateName(g.ID, name);
      return DataStore.Shortcuts.Get(g.ID);
    }

    // Both PCs merge everything: A, B, then A again.
    private void SyncAll() {
      a.Sync();
      b.Sync();
      a.Sync();
    }

    // ---- First join and plain changes ----

    [Fact]
    public void The_first_join_merges_both_PCs_and_keeps_doubles() {
      Shortcut homeNote = Note(a, null, a.FirstPanelId, "Groceries", "milk");
      Shortcut workNote = Note(b, null, b.FirstPanelId, "Standup", "notes");
      SyncAll();
      // Each PC's own first panel plus the other's: doubles are fine, nothing is matched by name.
      Assert.Equal(2, a.Panels().Count);
      Assert.Equal(2, b.Panels().Count);
      Assert.Equal("milk", b.Button(homeNote.SyncId).NoteText);
      Assert.Equal("notes", a.Button(workNote.SyncId).NoteText);
      Assert.Equal(a.Snapshot(), b.Snapshot());
      // The arriving note sits in the arriving panel.
      Assert.Equal(b.Panel(a.FirstPanelSyncId).ID, b.Button(homeNote.SyncId).ParentWindowID);
    }

    [Fact]
    public void A_change_reaches_the_other_PC_with_its_change_record() {
      Shortcut note = Note(a, null, a.FirstPanelId, "Groceries", "milk");
      SyncAll();
      a.Use();
      DataStore.Shortcuts.UpdateName(note.ID, "Shopping");
      Shortcut changed = DataStore.Shortcuts.Get(note.ID);
      a.Sync();
      b.Sync();
      Shortcut there = b.Button(note.SyncId);
      Assert.Equal("Shopping", there.Name);
      Assert.Equal((changed.ChangedAt, changed.ChangedBy), (there.ChangedAt, there.ChangedBy));
      Assert.Equal(a.Snapshot(), b.Snapshot());
    }

    [Fact]
    public void Panel_colours_sync_but_window_places_stay_on_each_PC() {
      a.Use();
      DataStore.Windows.UpdateBorderColor(a.FirstPanelId, "#FF112233");
      DataStore.Windows.UpdateLeftTopWidthHeight(a.FirstPanelId, 111, 222, 333, 444);
      SyncAll();
      DesktopWindow there = b.Panel(a.FirstPanelSyncId);
      Assert.Equal("#FF112233", there.BorderColor);
      Assert.NotEqual(111, there.Left);
      b.Use();
      DataStore.Windows.UpdateLeftTopWidthHeight(there.ID, 5, 6, 333, 444);
      DataStore.Windows.UpdateTitle(there.ID, "Renamed at work");
      SyncAll();
      DesktopWindow home = a.Panel(a.FirstPanelSyncId);
      Assert.Equal("Renamed at work", home.Title);
      Assert.Equal((111L, 222L), (home.Left, home.Top));
    }

    [Fact]
    public void The_newer_change_wins_and_ties_go_to_the_higher_device_id() {
      Shortcut note = Note(a, null, a.FirstPanelId, "Start");
      SyncAll();
      a.Use();
      DataStore.Shortcuts.UpdateName(note.ID, "From home");
      a.Stamp(note.SyncId, T + 1000);
      b.Use();
      DataStore.Shortcuts.UpdateName(b.Button(note.SyncId).ID, "From work");
      b.Stamp(note.SyncId, T + 2000);
      SyncAll();
      Assert.Equal("From work", a.Button(note.SyncId).Name);
      Assert.Equal(a.Snapshot(), b.Snapshot());

      // Same time on both: the higher device id wins on both PCs.
      a.Use();
      DataStore.Shortcuts.UpdateName(note.ID, "Tie home");
      a.Stamp(note.SyncId, T + 3000);
      b.Use();
      DataStore.Shortcuts.UpdateName(b.Button(note.SyncId).ID, "Tie work");
      b.Stamp(note.SyncId, T + 3000);
      SyncAll();
      string expected = string.CompareOrdinal(a.DeviceId, b.DeviceId) > 0 ? "Tie home" : "Tie work";
      Assert.Equal(expected, a.Button(note.SyncId).Name);
      Assert.Equal(expected, b.Button(note.SyncId).Name);
    }

    [Fact]
    public void A_move_keeps_the_SyncId_and_never_doubles() {
      Shortcut group = Group(a, null, a.FirstPanelId, "Folder");
      Shortcut note = Note(a, null, a.FirstPanelId, "Loose");
      SyncAll();
      a.Use();
      DataStore.Shortcuts.UpdateParentShortcutIDAndParentWindowID(note.ID, group.ID, null);
      a.Sync();
      b.Sync();
      Shortcut there = b.Button(note.SyncId);
      Assert.Equal(b.Button(group.SyncId).ID, there.ParentShortcutID);
      Assert.Null(there.ParentWindowID);
      Assert.Single(b.Buttons(), s => s.Name == "Loose");
      Assert.Equal(a.Snapshot(), b.Snapshot());
    }

    [Fact]
    public void A_move_to_another_panel_arrives_there() {
      a.Use();
      DesktopWindow second = DataStore.Windows.Create("", 0, 0);
      Shortcut note = Note(a, null, a.FirstPanelId, "Traveller");
      SyncAll();
      a.Use();
      DataStore.Shortcuts.UpdateParentWindowID(note.ID, second.ID);
      SyncAll();
      Assert.Equal(b.Panel(second.SyncId).ID, b.Button(note.SyncId).ParentWindowID);
    }

    [Fact]
    public void Sort_keys_sync() {
      Shortcut first = Note(a, null, a.FirstPanelId, "First");
      Shortcut second = Note(a, null, a.FirstPanelId, "Second");
      SyncAll();
      a.Use();
      DataStore.Shortcuts.UpdatePositions(new[] { second.ID, first.ID });
      SyncAll();
      b.Use();
      long panel = b.Panel(a.FirstPanelSyncId).ID;
      Assert.Equal(new[] { "Second", "First" }, DataStore.Shortcuts.GetRoots(panel).Select(s => s.Name));
    }

    [Fact]
    public void Deletes_sync_with_their_record() {
      Shortcut note = Note(a, null, a.FirstPanelId, "Gone soon");
      SyncAll();
      a.Use();
      DataStore.Shortcuts.Delete(note.ID);
      DeletedItem deleted = a.Deleted().Single(d => d.SyncId == note.SyncId);
      a.Sync();
      b.Sync();
      Assert.Null(b.Button(note.SyncId));
      DeletedItem there = b.Deleted().Single(d => d.SyncId == note.SyncId);
      Assert.Equal((deleted.DeletedAt, deleted.DeletedBy), (there.DeletedAt, there.DeletedBy));
    }

    [Fact]
    public void A_deleted_panel_goes_with_its_buttons_on_the_other_PC() {
      a.Use();
      DesktopWindow second = DataStore.Windows.Create("", 0, 0);
      Shortcut note = Note(a, null, second.ID, "Inside");
      SyncAll();
      a.Use();
      DataStore.Windows.Delete(second.ID);
      SyncAll();
      Assert.Null(b.Panel(second.SyncId));
      Assert.Null(b.Button(note.SyncId));
      Assert.Equal(a.Snapshot(), b.Snapshot());
    }

    [Fact]
    public void A_change_newer_than_a_delete_brings_the_item_back() {
      Shortcut note = Note(a, null, a.FirstPanelId, "Keep me");
      SyncAll();
      a.Use();
      DataStore.Shortcuts.Delete(note.ID);
      a.StampDelete(note.SyncId, T + 1000);
      b.Use();
      DataStore.Shortcuts.UpdateNoteText(b.Button(note.SyncId).ID, "edited later");
      b.Stamp(note.SyncId, T + 2000);
      SyncAll();
      Assert.Equal("edited later", a.Button(note.SyncId).NoteText);
      Assert.Equal("edited later", b.Button(note.SyncId).NoteText);
    }

    // ---- Text clashes ----

    [Fact]
    public void A_text_both_PCs_changed_keeps_the_newer_and_puts_the_other_in_Text_history() {
      Shortcut note = Note(a, null, a.FirstPanelId, "Shared", "base");
      SyncAll();
      a.Use();
      DataStore.Shortcuts.UpdateNoteText(note.ID, "home text");
      a.Stamp(note.SyncId, T + 1000);
      b.Use();
      DataStore.Shortcuts.UpdateNoteText(b.Button(note.SyncId).ID, "work text");
      b.Stamp(note.SyncId, T + 2000);
      SyncAll();
      Assert.Equal("work text", a.Button(note.SyncId).NoteText);
      Assert.Equal("work text", b.Button(note.SyncId).NoteText);
      HistoryEntry home = a.History().Single();
      HistoryEntry work = b.History().Single();
      Assert.Equal(("home text", "Home PC", HistoryFields.Text), (home.Text, home.FromPc, home.Field));
      Assert.Equal(("home text", "Home PC", HistoryFields.Text), (work.Text, work.FromPc, work.Field));
      Assert.Equal(a.Snapshot(), b.Snapshot());
    }

    [Fact]
    public void A_comment_clash_goes_into_the_comment_history() {
      Shortcut note = Note(a, null, a.FirstPanelId, "Shared");
      SyncAll();
      a.Use();
      DataStore.Shortcuts.UpdateComment(note.ID, "home comment");
      a.Stamp(note.SyncId, T + 2000);
      b.Use();
      DataStore.Shortcuts.UpdateComment(b.Button(note.SyncId).ID, "work comment");
      b.Stamp(note.SyncId, T + 1000);
      SyncAll();
      Assert.Equal("home comment", b.Button(note.SyncId).Comment);
      HistoryEntry kept = b.History().Single();
      Assert.Equal(("work comment", "Work PC", HistoryFields.Comment), (kept.Text, kept.FromPc, kept.Field));
      // Work's text never left Work: it is kept there only.
      Assert.Empty(a.History());
    }

    [Fact]
    public void Changes_one_after_the_other_are_not_a_clash() {
      Shortcut note = Note(a, null, a.FirstPanelId, "Shared", "base");
      SyncAll();
      a.Use();
      DataStore.Shortcuts.UpdateNoteText(note.ID, "first");
      a.Stamp(note.SyncId, T + 1000);
      SyncAll();
      b.Use();
      DataStore.Shortcuts.UpdateNoteText(b.Button(note.SyncId).ID, "second");
      b.Stamp(note.SyncId, T + 2000);
      SyncAll();
      Assert.Equal("second", a.Button(note.SyncId).NoteText);
      Assert.Empty(a.History());
      Assert.Empty(b.History());
    }

    [Fact]
    public void An_edit_from_another_PC_reaches_a_PC_with_the_note_open_and_stays_on_both() {
      Shortcut note = Note(a, null, a.FirstPanelId, "Open", "base");
      SyncAll();
      b.Use();
      DataStore.Shortcuts.UpdateNoteText(b.Button(note.SyncId).ID, "from work");
      b.Sync();
      // Home has the note open but typed nothing since its last save: it is not in Editing.
      a.Sync();
      Assert.Equal("from work", a.Button(note.SyncId).NoteText);
      SyncAll();
      b.Sync();
      Assert.Equal("from work", b.Button(note.SyncId).NoteText);
      Assert.Empty(b.History());
      Assert.Equal(a.Snapshot(), b.Snapshot());
    }

    [Fact]
    public void A_note_with_unsaved_typing_waits_and_nothing_is_reverted_on_the_other_PC() {
      Shortcut note = Note(a, null, a.FirstPanelId, "Open", "base");
      SyncAll();
      b.Use();
      DataStore.Shortcuts.UpdateNoteText(b.Button(note.SyncId).ID, "from work");
      DataStore.Shortcuts.UpdateName(b.Button(note.SyncId).ID, "Renamed at work");
      b.Stamp(note.SyncId, T + 1000);
      b.Sync();
      a.Editing.Add(note.ID);
      SyncResult result = a.Sync();
      Shortcut home = a.Button(note.SyncId);
      Assert.Equal(("base", "Open"), (home.NoteText, home.Name));
      Assert.Empty(a.History());
      Assert.False(result.Applied);
      b.Sync();
      Assert.Equal(("from work", "Renamed at work"), (b.Button(note.SyncId).NoteText, b.Button(note.SyncId).Name));
      // Typing saved: nothing new typed, so the waiting change comes in.
      a.Editing.Clear();
      a.Sync();
      Assert.Equal("from work", a.Button(note.SyncId).NoteText);
      Assert.Empty(a.History());
      SyncAll();
      Assert.Equal(a.Snapshot(), b.Snapshot());
    }

    [Fact]
    public void Once_the_typing_is_saved_the_normal_rules_decide() {
      Shortcut note = Note(a, null, a.FirstPanelId, "Open", "base");
      SyncAll();
      b.Use();
      DataStore.Shortcuts.UpdateNoteText(b.Button(note.SyncId).ID, "from work");
      b.Stamp(note.SyncId, T + 1000);
      b.Sync();
      a.Editing.Add(note.ID);
      a.Sync();
      // Home saves its typing later: the newer text wins and Work's goes into Text history on both.
      a.Use();
      DataStore.Shortcuts.UpdateNoteText(note.ID, "typed at home");
      a.Stamp(note.SyncId, T + 2000);
      a.Editing.Clear();
      SyncAll();
      b.Sync();
      Assert.Equal("typed at home", a.Button(note.SyncId).NoteText);
      Assert.Equal("typed at home", b.Button(note.SyncId).NoteText);
      Assert.Contains(a.History(), h => h.Text == "from work" && h.FromPc == "Work PC");
      Assert.Contains(b.History(), h => h.Text == "from work" && h.FromPc == "Work PC");
      Assert.Equal(a.Snapshot(), b.Snapshot());
    }

    // ---- Deleted parents and loops ----

    [Fact]
    public void An_item_whose_group_was_deleted_goes_to_the_top_of_its_panel() {
      Shortcut group = Group(a, null, a.FirstPanelId, "Folder");
      SyncAll();
      a.Use();
      DataStore.Shortcuts.Delete(group.ID);
      a.StampDelete(group.SyncId, T + 1000);
      Shortcut added = Note(b, b.Button(group.SyncId).ID, null, "Added at work");
      b.Stamp(added.SyncId, T + 2000);
      SyncAll();
      foreach (SyncPc pc in new[] { a, b }) {
        Assert.Null(pc.Button(group.SyncId));
        Shortcut moved = pc.Button(added.SyncId);
        Assert.NotNull(moved);
        Assert.Null(moved.ParentShortcutID);
        Assert.Equal(pc.Panel(a.FirstPanelSyncId).ID, moved.ParentWindowID);
      }
      Assert.Equal(a.Snapshot(), b.Snapshot());
    }

    [Fact]
    public void An_item_that_arrives_for_a_deleted_panel_brings_the_panel_back() {
      a.Use();
      DesktopWindow second = DataStore.Windows.Create("", 0, 0);
      DataStore.Windows.UpdateTitle(second.ID, "Projects");
      SyncAll();
      a.Use();
      DataStore.Windows.Delete(second.ID);
      a.StampDelete(second.SyncId, T + 1000);
      Shortcut added = Note(b, null, b.Panel(second.SyncId).ID, "Added at work");
      b.Stamp(added.SyncId, T + 2000);
      SyncAll();
      b.Sync();
      foreach (SyncPc pc in new[] { a, b }) {
        DesktopWindow back = pc.Panel(second.SyncId);
        Assert.NotNull(back);
        Assert.Equal("Projects", back.Title);
        Assert.Equal(back.ID, pc.Button(added.SyncId).ParentWindowID);
      }
      Assert.Equal(a.Snapshot(), b.Snapshot());
    }

    [Fact]
    public void A_move_that_would_make_a_loop_is_undone_and_the_newer_move_wins() {
      Shortcut g1 = Group(a, null, a.FirstPanelId, "G1");
      Shortcut g2 = Group(a, null, a.FirstPanelId, "G2");
      SyncAll();
      // Home puts G1 into G2; Work, later, puts G2 into G1.
      a.Use();
      DataStore.Shortcuts.UpdateParentShortcutIDAndParentWindowID(g1.ID, g2.ID, null);
      a.Stamp(g1.SyncId, T + 1000);
      b.Use();
      DataStore.Shortcuts.UpdateParentShortcutIDAndParentWindowID(b.Button(g2.SyncId).ID, b.Button(g1.SyncId).ID, null);
      b.Stamp(g2.SyncId, T + 2000);
      SyncAll();
      b.Sync();
      foreach (SyncPc pc in new[] { a, b }) {
        Shortcut one = pc.Button(g1.SyncId);
        Shortcut two = pc.Button(g2.SyncId);
        Assert.Equal(one.ID, two.ParentShortcutID);
        Assert.Null(one.ParentShortcutID);
        Assert.Equal(pc.Panel(a.FirstPanelSyncId).ID, one.ParentWindowID);
      }
      Assert.Equal(a.Snapshot(), b.Snapshot());
    }

    // ---- Files the reader must not trust yet ----

    private string IndexPath(SyncPc pc, string name = "index.json") => Path.Combine(pcs.Folder, pc.DeviceId, name);

    [Fact]
    public void A_half_written_index_is_read_from_index_prev() {
      Shortcut first = Note(b, null, b.FirstPanelId, "First");
      b.Sync();
      Shortcut second = Note(b, null, b.FirstPanelId, "Second");
      b.Sync();
      string text = File.ReadAllText(IndexPath(b));
      File.WriteAllText(IndexPath(b), text.Substring(0, text.Length / 3));
      a.Sync();
      Assert.NotNull(a.Button(first.SyncId));
      Assert.Null(a.Button(second.SyncId));
      // The next good index brings the rest.
      b.Sync();
      a.Sync();
      Assert.NotNull(a.Button(second.SyncId));
    }

    [Fact]
    public void When_no_index_can_be_read_the_reader_waits() {
      Shortcut note = Note(b, null, b.FirstPanelId, "Note");
      b.Sync();
      File.WriteAllText(IndexPath(b), "{");
      File.WriteAllText(IndexPath(b, "index.prev.json"), "{");
      SyncResult result = a.Sync();
      Assert.True(result.FolderReachable);
      Assert.Null(a.Button(note.SyncId));
    }

    [Fact]
    public void An_item_file_still_being_copied_is_skipped_this_round() {
      Shortcut note = Note(b, null, b.FirstPanelId, "Big note", "text");
      b.Sync();
      string items = Path.Combine(pcs.Folder, b.DeviceId, "items");
      string file = Directory.GetFiles(items).Single(f => File.ReadAllText(f).Contains("Big note"));
      byte[] good = File.ReadAllBytes(file);
      File.WriteAllBytes(file, good.Take(good.Length / 2).ToArray());
      a.Sync();
      Assert.Null(a.Button(note.SyncId));
      // The panel came: only the bad file waits.
      Assert.NotNull(a.Panel(b.FirstPanelSyncId));
      File.WriteAllBytes(file, good);
      a.Sync();
      Assert.Equal("text", a.Button(note.SyncId).NoteText);
    }

    [Fact]
    public void A_PC_with_a_newer_format_is_not_read_and_the_others_keep_syncing() {
      SyncPc c = pcs.Add("Laptop");
      Shortcut fromWork = Note(b, null, b.FirstPanelId, "From work");
      Shortcut fromLaptop = Note(c, null, c.FirstPanelId, "From laptop");
      // The laptop writes before it has read Work's note.
      c.Sync();
      b.Sync();
      string index = IndexPath(b);
      File.WriteAllText(index, File.ReadAllText(index).Replace("\"format\": 1", "\"format\": 99"));
      File.Delete(IndexPath(b, "index.prev.json"));
      SyncResult result = a.Sync();
      Assert.Null(a.Button(fromWork.SyncId));
      Assert.NotNull(a.Button(fromLaptop.SyncId));
      Assert.Equal(new[] { "Work PC" }, result.NewerPcs);
      Assert.Contains(result.Peers, p => p.Name == "Work PC" && p.NewerFormat);
    }

    [Fact]
    public void A_folder_that_cannot_be_reached_changes_nothing() {
      Note(a, null, a.FirstPanelId, "Waiting");
      string before = a.Snapshot();
      a.Use();
      SyncResult result = SyncEngine.Run(new SyncOptions { Folder = Path.Combine(pcs.Root, "missing-drive"), DeviceId = a.DeviceId, DeviceName = a.Name, AppVersion = "2.0.0" });
      Assert.False(result.FolderReachable);
      Assert.Equal(before, a.Snapshot());
      Assert.False(Directory.Exists(Path.Combine(pcs.Root, "missing-drive")));
    }

    // ---- Rounds, files and the other PCs ----

    [Fact]
    public void A_round_with_nothing_new_writes_nothing() {
      Note(a, null, a.FirstPanelId, "Note");
      SyncAll();
      b.Sync();
      long seq = SyncFolder.ReadIndex(Path.Combine(pcs.Folder, a.DeviceId)).Index.Seq;
      string before = a.Snapshot();
      SyncResult result = a.Sync();
      Assert.False(result.Wrote);
      Assert.False(result.Applied);
      Assert.Equal(seq, SyncFolder.ReadIndex(Path.Combine(pcs.Folder, a.DeviceId)).Index.Seq);
      Assert.Equal(before, a.Snapshot());
    }

    [Fact]
    public void Rounds_that_only_read_do_not_make_the_PCs_write_back_and_forth() {
      Note(a, null, a.FirstPanelId, "Note");
      SyncAll();
      b.Sync();
      for (int i = 0; i < 3; i++) {
        Assert.False(a.Sync().Wrote);
        Assert.False(b.Sync().Wrote);
      }
    }

    [Fact]
    public void The_index_names_the_PC_and_says_how_far_it_merged_the_others() {
      a.Sync();
      b.Sync();
      SyncIndex work = SyncFolder.ReadIndex(Path.Combine(pcs.Folder, b.DeviceId)).Index;
      SyncIndex home = SyncFolder.ReadIndex(Path.Combine(pcs.Folder, a.DeviceId)).Index;
      Assert.Equal(("Work PC", "2.0.0", b.DeviceId), (work.Name, work.AppVersion, work.Device));
      Assert.Equal(home.Seq, work.Seen[a.DeviceId]);
      SyncResult result = a.Sync();
      SyncPeer peer = result.Peers.Single();
      Assert.Equal(("Work PC", "2.0.0"), (peer.Name, peer.AppVersion));
      Assert.NotNull(peer.LastSync);
    }

    [Fact]
    public void Only_entries_newer_than_the_last_merge_are_read() {
      Shortcut note = Note(b, null, b.FirstPanelId, "Note", "one");
      SyncAll();
      b.Sync();
      // An entry not raised past what Home merged is not read again, even when its file changes.
      a.Use();
      DataStore.Shortcuts.UpdateNoteText(a.Button(note.SyncId).ID, "home edit");
      a.Stamp(note.SyncId, 1);
      SyncResult result = a.Sync();
      Assert.Equal("home edit", a.Button(note.SyncId).NoteText);
      Assert.False(result.Applied);
    }

    [Fact]
    public void Shortcut_files_travel_as_blobs_with_portable_targets() {
      a.Use();
      string documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
      string target = Path.Combine(documents, "report.txt");
      Shortcut link = DataStore.Shortcuts.Create(null, a.FirstPanelId, "Report", target, ".lnk", Guid.NewGuid().ToString());
      Directory.CreateDirectory(a.ShortcutsPath);
      byte[] bytes = { 76, 0, 0, 0, 1, 2, 3 };
      File.WriteAllBytes(Path.Combine(a.ShortcutsPath, link.Guid + ".lnk"), bytes);
      a.Sync();
      string items = Path.Combine(pcs.Folder, a.DeviceId, "items");
      string item = Directory.GetFiles(items).Select(File.ReadAllText).Single(t => t.Contains("Report"));
      Assert.Contains("%DOCUMENTS%", item);
      Assert.DoesNotContain(documents.Replace("\\", "\\\\"), item);
      b.Sync();
      Shortcut there = b.Button(link.SyncId);
      Assert.Equal(target, there.Target);
      Assert.NotEqual(link.Guid, there.Guid);
      Assert.Equal(bytes, File.ReadAllBytes(Path.Combine(b.ShortcutsPath, there.Guid + ".lnk")));
    }

    [Fact]
    public void A_notes_text_size_and_wrap_sync() {
      Shortcut note = Note(a, null, a.FirstPanelId, "Sized");
      a.Use();
      DataStore.NoteWindows.Save(new NoteWindowPlace(note.ID, 10, 10, 300, 200, NoteWindowPlace.Normal, false) { FontSize = 18, IsWrapText = true });
      SyncAll();
      b.Use();
      NoteWindowPlace place = DataStore.NoteWindows.Get(b.Button(note.SyncId).ID);
      Assert.NotNull(place);
      Assert.Equal((18L, true), (place.FontSize, place.IsWrapText));
      Assert.Equal(a.Snapshot(), b.Snapshot());
    }

    [Fact]
    public void Three_PCs_end_up_the_same() {
      SyncPc c = pcs.Add("Laptop");
      Shortcut note = Note(a, null, a.FirstPanelId, "Shared", "base");
      foreach (SyncPc pc in pcs.All) { pc.Sync(); }
      foreach (SyncPc pc in pcs.All) { pc.Sync(); }
      b.Use();
      DataStore.Shortcuts.UpdateNoteText(b.Button(note.SyncId).ID, "work");
      b.Stamp(note.SyncId, T + 1000);
      c.Use();
      DataStore.Shortcuts.UpdateNoteText(c.Button(note.SyncId).ID, "laptop");
      c.Stamp(note.SyncId, T + 2000);
      c.Use();
      Group(c, null, c.FirstPanelId, "Laptop group");
      for (int round = 0; round < 3; round++) {
        foreach (SyncPc pc in pcs.All) { pc.Sync(); }
      }
      Assert.Equal(a.Snapshot(), b.Snapshot());
      Assert.Equal(a.Snapshot(), c.Snapshot());
      Assert.Equal("laptop", a.Button(note.SyncId).NoteText);
      Assert.Contains(b.History(), h => h.Text == "work" && h.FromPc == "Work PC");
    }

    [Fact]
    public void Old_unused_files_are_deleted_from_this_PCs_folder() {
      Shortcut note = Note(a, null, a.FirstPanelId, "Note", "v1");
      a.Sync();
      a.Use();
      DataStore.Shortcuts.UpdateNoteText(note.ID, "v2");
      a.Sync();
      DataStore.Shortcuts.UpdateNoteText(note.ID, "v3");
      a.Sync();
      string items = Path.Combine(pcs.Folder, a.DeviceId, "items");
      string v1 = Directory.GetFiles(items).Single(f => File.ReadAllText(f).Contains("\"v1\""));
      foreach (string f in Directory.GetFiles(items)) {
        File.SetLastWriteTimeUtc(f, DateTime.UtcNow.AddDays(-8));
      }
      DataStore.Shortcuts.UpdateNoteText(note.ID, "v4");
      a.Sync();
      Assert.False(File.Exists(v1));
      Assert.Contains(Directory.GetFiles(items), f => File.ReadAllText(f).Contains("\"v3\""));
    }
  }
}
