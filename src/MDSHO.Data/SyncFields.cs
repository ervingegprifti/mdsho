using System;
using System.Collections.Generic;
using System.Linq;

namespace MDSHO.Data {

  /// <summary>Whether a column or setting goes to the other PCs.</summary>
  public enum SyncScope {
    /// <summary>The same on every PC.</summary>
    Synced,
    /// <summary>Belongs to this PC only.</summary>
    Local
  }

  /// <summary>One column of the database and whether it syncs; <see cref="Reason"/> says why a local one stays here.</summary>
  public sealed record SyncField(string Table, string Column, SyncScope Scope, string Reason);

  /// <summary>One app setting and whether it syncs. <see cref="InDatabase"/> is false for a setting Windows keeps.</summary>
  public sealed record SyncSetting(string Key, SyncScope Scope, string Reason, bool InDatabase = true);

  /// <summary>
  /// Marks every column of every table, and every app setting, as Synced or Local. A test fails for a column that is
  /// not here, so each new column must be decided. The database triggers that keep the change records are built from
  /// this list: a change of a Synced column of a panel or button (or of its note window) is a change of that item.
  /// </summary>
  public static class SyncFields {
    private const string Place = "window place on this PC's screens";
    private const string RowId = "row number on this PC; SyncId names the item everywhere";

    private static SyncField S(string table, string column, string reason = "") { return new SyncField(table, column, SyncScope.Synced, reason); }
    private static SyncField L(string table, string column, string reason) { return new SyncField(table, column, SyncScope.Local, reason); }

    /// <summary>Every column of the schema.</summary>
    public static readonly IReadOnlyList<SyncField> Columns = new List<SyncField> {
      // Panels.
      L("MDSHOWindows", "ID", RowId),
      S("MDSHOWindows", "Title"),
      L("MDSHOWindows", "ScreenDeviceName", Place),
      L("MDSHOWindows", "Left", Place),
      L("MDSHOWindows", "Top", Place),
      L("MDSHOWindows", "Width", Place),
      L("MDSHOWindows", "Height", Place),
      S("MDSHOWindows", "BorderColor"),
      S("MDSHOWindows", "BackgroundColor"),
      S("MDSHOWindows", "TextColor"),
      S("MDSHOWindows", "TitleBackgroundColor"),
      S("MDSHOWindows", "TitleTextColor"),
      S("MDSHOWindows", "Notes"),
      S("MDSHOWindows", "SolidIcons"),
      S("MDSHOWindows", "SingleClickToOpen"),
      S("MDSHOWindows", "ShowVersionInTitle"),
      S("MDSHOWindows", "SyncId", "names the panel on every PC"),
      S("MDSHOWindows", "SortKey", "order of the panels"),
      S("MDSHOWindows", "ChangedAt", "change record"),
      S("MDSHOWindows", "ChangedBy", "change record"),

      // Clones: the same panel again on another screen of this PC.
      L("MDSHOWindowClones", "ID", "clones are per screen on this PC"),
      L("MDSHOWindowClones", "ParentMDSHOWindowID", "clones are per screen on this PC"),
      L("MDSHOWindowClones", "ScreenDeviceName", Place),
      L("MDSHOWindowClones", "Left", Place),
      L("MDSHOWindowClones", "Top", Place),
      L("MDSHOWindowClones", "Width", Place),
      L("MDSHOWindowClones", "Height", Place),

      // Buttons: shortcuts, links, groups, notes, snippets and run commands.
      L("Shortcuts", "ID", RowId),
      S("Shortcuts", "ParentShortcutID", "the parent group, sent as its SyncId"),
      S("Shortcuts", "ParentMDSHOWindowID", "the parent panel, sent as its SyncId"),
      S("Shortcuts", "Name"),
      S("Shortcuts", "Target"),
      S("Shortcuts", "Ext"),
      L("Shortcuts", "Guid", "names this PC's shortcut file; the file's bytes go as a blob"),
      S("Shortcuts", "IsGroup"),
      L("Shortcuts", "IsGroupExpanded", "a group open or closed in this PC's tree"),
      S("Shortcuts", "Comment"),
      S("Shortcuts", "IsMarked"),
      S("Shortcuts", "MarkColor"),
      S("Shortcuts", "IsProcessCommand"),
      S("Shortcuts", "ProcessFileName"),
      S("Shortcuts", "ProcessArguments"),
      S("Shortcuts", "ProcessRunAsAdmin"),
      S("Shortcuts", "ProcessWorkingFolder"),
      S("Shortcuts", "IsNote"),
      S("Shortcuts", "NoteText"),
      S("Shortcuts", "NoteBorderColor"),
      S("Shortcuts", "NoteTitleBackgroundColor"),
      S("Shortcuts", "NoteTitleTextColor"),
      S("Shortcuts", "NoteBackgroundColor"),
      S("Shortcuts", "NoteTextColor"),
      S("Shortcuts", "IsSnippet"),
      S("Shortcuts", "SnippetText"),
      S("Shortcuts", "TextFont"),
      S("Shortcuts", "CommentFont"),
      S("Shortcuts", "CommentFontSize"),
      S("Shortcuts", "TextFontSize"),
      S("Shortcuts", "NoteHighlights"),
      S("Shortcuts", "TextCurrentLine"),
      S("Shortcuts", "TextWholeWords"),
      S("Shortcuts", "TextMatchCase"),
      S("Shortcuts", "TextWrapText"),
      S("Shortcuts", "CommentCurrentLine"),
      S("Shortcuts", "CommentWholeWords"),
      S("Shortcuts", "CommentMatchCase"),
      S("Shortcuts", "CommentWrapText"),
      S("Shortcuts", "SyncId", "names the button on every PC"),
      S("Shortcuts", "SortKey", "order among the siblings"),
      S("Shortcuts", "ChangedAt", "change record"),
      S("Shortcuts", "ChangedBy", "change record"),

      // Note windows: the place is this PC's; the text size and wrap belong to the note.
      L("NoteWindows", "ID", "row number on this PC"),
      L("NoteWindows", "ShortcutID", "links the place to this PC's note row"),
      L("NoteWindows", "Left", Place),
      L("NoteWindows", "Top", Place),
      L("NoteWindows", "Width", Place),
      L("NoteWindows", "Height", Place),
      L("NoteWindows", "State", "normal, minimized or maximized on this PC"),
      L("NoteWindows", "IsOpen", "whether the window is open on this PC"),
      L("NoteWindows", "IsTopmost", "keeps the window on top of this PC's other apps"),
      S("NoteWindows", "IsWrapText", "a change of the note"),
      S("NoteWindows", "FontSize", "a change of the note"),

      // Text history.
      L("History", "ID", "each PC keeps its own text history"),
      L("History", "ShortcutID", "each PC keeps its own text history"),
      L("History", "Field", "each PC keeps its own text history"),
      L("History", "Text", "each PC keeps its own text history"),
      L("History", "KeptAt", "each PC keeps its own text history"),
      L("History", "FromPc", "each PC keeps its own text history"),

      // Default colours: this PC's settings (only panels and buttons sync).
      L("DefaultColors", "Kind", "this PC's settings stay here"),
      L("DefaultColors", "BorderColor", "this PC's settings stay here"),
      L("DefaultColors", "TitleBackgroundColor", "this PC's settings stay here"),
      L("DefaultColors", "TitleTextColor", "this PC's settings stay here"),
      L("DefaultColors", "BackgroundColor", "this PC's settings stay here"),
      L("DefaultColors", "TextColor", "this PC's settings stay here"),

      // App settings: the table is this PC's; each key is decided in Settings below.
      L("AppSettings", "Key", "the table is this PC's; each key is decided in SyncFields.Settings"),
      L("AppSettings", "Value", "the table is this PC's; each key is decided in SyncFields.Settings"),

      // Screen setups and the places in each.
      L("ScreenSetups", "ID", "screen setups are this PC's"),
      L("ScreenSetups", "Key", "screen setups are this PC's"),
      L("ScreenSetups", "Name", "screen setups are this PC's"),
      L("ScreenSetups", "LastUsed", "screen setups are this PC's"),
      L("ScreenSetupPlaces", "ID", "places per screen setup are this PC's"),
      L("ScreenSetupPlaces", "SetupID", "places per screen setup are this PC's"),
      L("ScreenSetupPlaces", "Kind", "places per screen setup are this PC's"),
      L("ScreenSetupPlaces", "ItemID", "places per screen setup are this PC's"),
      L("ScreenSetupPlaces", "ScreenDeviceName", Place),
      L("ScreenSetupPlaces", "Left", Place),
      L("ScreenSetupPlaces", "Top", Place),
      L("ScreenSetupPlaces", "Width", Place),
      L("ScreenSetupPlaces", "Height", Place),
      L("ScreenSetupPlaces", "Dock", Place),

      // Deleted items: how deletes reach the other PCs.
      S("DeletedItems", "SyncId"),
      S("DeletedItems", "Kind"),
      S("DeletedItems", "DeletedAt"),
      S("DeletedItems", "DeletedBy"),
    };

    /// <summary>Every app setting: the AppSettings keys, and Start with Windows, which Windows keeps.</summary>
    public static readonly IReadOnlyList<SyncSetting> Settings = new List<SyncSetting> {
      new SyncSetting("WindowTheme", SyncScope.Local, "this PC's settings stay here"),
      new SyncSetting("BringToFrontShortcut", SyncScope.Local, "this PC's settings stay here"),
      new SyncSetting("RememberScreenSetupPlaces", SyncScope.Local, "this PC's settings stay here"),
      new SyncSetting("TextHistoryKeep", SyncScope.Local, "the text history is this PC's"),
      new SyncSetting("SettingsPage", SyncScope.Local, "the last Settings page used on this PC"),
      new SyncSetting("BackupOn", SyncScope.Local, "each PC backs up its own data"),
      new SyncSetting("BackupFolder", SyncScope.Local, "a folder on this PC"),
      new SyncSetting("BackupKeep", SyncScope.Local, "each PC backs up its own data"),
      new SyncSetting("BackupLastTime", SyncScope.Local, "the backups of this PC"),
      new SyncSetting("BackupLastHash", SyncScope.Local, "the backups of this PC"),
      new SyncSetting("BackupLastError", SyncScope.Local, "the backups of this PC"),
      new SyncSetting("ReviewFirstStart", SyncScope.Local, "the review prompt counts days on this PC"),
      new SyncSetting("ReviewDaysUsed", SyncScope.Local, "the review prompt counts days on this PC"),
      new SyncSetting("ReviewLastDay", SyncScope.Local, "the review prompt counts days on this PC"),
      new SyncSetting("ReviewShown", SyncScope.Local, "the review prompt on this PC"),
      new SyncSetting("ReviewLastShown", SyncScope.Local, "the review prompt on this PC"),
      new SyncSetting("ReviewRated", SyncScope.Local, "the Store rating of this PC's install"),
      new SyncSetting(DeviceIdentity.IdKey, SyncScope.Local, "identifies this PC"),
      new SyncSetting(DeviceIdentity.NameKey, SyncScope.Local, "names this PC"),
      new SyncSetting(SyncSettings.OnKey, SyncScope.Local, "each PC joins the sync itself"),
      new SyncSetting(SyncSettings.FolderKey, SyncScope.Local, "the sync place as this PC sees it"),
      new SyncSetting(SyncSettings.SeqKey, SyncScope.Local, "counts this PC's writes to the sync folder"),
      new SyncSetting(SyncSettings.SeenKey, SyncScope.Local, "how far this PC merged the others"),
      new SyncSetting(SyncSettings.LastTimeKey, SyncScope.Local, "when this PC last synced"),
      new SyncSetting(SyncSettings.BackupNoticeKey, SyncScope.Local, "the backup made when this PC joined"),
      new SyncSetting(SyncSettings.KindKey, SyncScope.Local, "each PC picks what it syncs through"),
      new SyncSetting("StartWithWindows", SyncScope.Local, "a Windows startup task of this PC's install", InDatabase: false),
    };

    /// <summary>The entry for a column, or null when it is not classified.</summary>
    public static SyncField Of(string table, string column) {
      return Columns.FirstOrDefault(f => string.Equals(f.Table, table, StringComparison.OrdinalIgnoreCase)
        && string.Equals(f.Column, column, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>The columns of a table that keep the change record itself, not watched for changes.</summary>
    internal static readonly string[] RecordColumns = { "ID", "SyncId", "ChangedAt", "ChangedBy" };

    /// <summary>The Synced columns of a table whose change is a change of the item.</summary>
    internal static IEnumerable<string> WatchedColumns(string table) {
      return Columns.Where(f => f.Table == table && f.Scope == SyncScope.Synced && !RecordColumns.Contains(f.Column)).Select(f => f.Column);
    }
  }
}
