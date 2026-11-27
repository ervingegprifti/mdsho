---
title: Database
permalink: /database/
---

# MDSHO database

MDSHO keeps its data in one SQLite file, `mdsho.sqlite3`, in its app data folder (see [Architecture](architecture.md#where-the-data-lives)).
Debug builds use `mdsho-debug.sqlite3` instead, so they never touch the data of the installed app. Versions before
1.1.2 called the file `MDSHO.sqlite3` (Debug: `MDSHODebug.sqlite3`); MDSHO renames it automatically at start-up.

The current schema is **database version 27**. The version is stored in the file itself (`PRAGMA user_version`).
The schema is created and upgraded by `src/MDSHO.Data/SchemaManager.cs`; a test (`tests/MDSHO.Tests/DocsTests.cs`)
checks that this page lists exactly the tables, columns and indexes of a freshly created database.

## Tables

All tables but `DefaultColors` and `AppSettings` use `ID INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT UNIQUE`, so an ID is never reused,
even after a row is deleted. Flags are `INTEGER` columns holding `0` (no) or `1` (yes). Colors are text in the form `#AARRGGBB`.

### MDSHOWindows

One row per panel (MDSHO window).

| Column | Type | Null | Default | Meaning |
|---|---|---|---|---|
| `ID` | INTEGER | NOT NULL | | Primary key. |
| `Title` | TEXT | NOT NULL | | Title shown in the panel's header. |
| `ScreenDeviceName` | TEXT | NOT NULL | | Name of the screen the panel was last on. |
| `Left` | INTEGER | NOT NULL | | Left edge, measured from the left of the whole desktop (all screens). |
| `Top` | INTEGER | NOT NULL | | Top edge, measured from the top of the whole desktop. |
| `Width` | INTEGER | NOT NULL | | Panel width. |
| `Height` | INTEGER | NOT NULL | | Panel height. |
| `BorderColor` | TEXT | NOT NULL | | Border color. |
| `BackgroundColor` | TEXT | NOT NULL | | Background color. |
| `TextColor` | TEXT | NOT NULL | | Color of the button text and icons. |
| `TitleBackgroundColor` | TEXT | NOT NULL | | Header background color. |
| `TitleTextColor` | TEXT | NOT NULL | | Header text color. |
| `Notes` | TEXT | NOT NULL | | Free notes about the panel (kept, but the current app has no field to edit them). |
| `SolidIcons` | INTEGER | NOT NULL | | `1` = filled icons, `0` = outline icons. |
| `SingleClickToOpen` | INTEGER | NOT NULL | | `1` = one click opens a button, `0` = double click. |
| `ShowVersionInTitle` | INTEGER | NOT NULL | `1` | `1` = the title bar of the panel and its clones shows the MDSHO version after the title, for example `MDSHO v1.5.10` ("Show version in title" in Settings). Exports carry it; older exports import with it on. Added in version 13. |
| `SyncId` | TEXT | NOT NULL | `''` | The fixed ID that names the panel on every PC, a GUID such as `3f2b8c1e-...`. Made when the row is added and never changed; a copy gets a new one, a move keeps it. Unique among the panels with a SyncId. Added in version 27. |
| `SortKey` | TEXT | NOT NULL | `''` | Order of the panels. A text key (digits `0-9`, `A-Z`, `a-z`) compared byte by byte, then by `SyncId` when two keys are equal. A new or moved panel gets a key between its neighbours, so the others keep theirs (see [Sort keys](#sort-keys)). Added in version 27. |
| `ChangedAt` | INTEGER | NOT NULL | `0` | When a synced column of the panel last changed: UTC milliseconds since 1970. A local column (see [Synced and local](#synced-and-local)) doesn't count, and saving the same value again doesn't either. Kept by database triggers. Added in version 27. |
| `ChangedBy` | TEXT | NOT NULL | `''` | The `DeviceId` of the PC that made that change. Added in version 27. |

### MDSHOWindowClones

One row per clone: the same panel shown again, usually on another screen. A clone has its own position and size and
shares everything else (title, colors, buttons) with its panel.

| Column | Type | Null | Default | Meaning |
|---|---|---|---|---|
| `ID` | INTEGER | NOT NULL | | Primary key. |
| `ParentMDSHOWindowID` | INTEGER | NOT NULL | | The panel this is a clone of (`MDSHOWindows.ID`). |
| `ScreenDeviceName` | TEXT | NOT NULL | | Name of the screen the clone was last on. |
| `Left` | INTEGER | NOT NULL | | Left edge, measured from the left of the whole desktop. |
| `Top` | INTEGER | NOT NULL | | Top edge, measured from the top of the whole desktop. |
| `Width` | INTEGER | NOT NULL | | Clone width. |
| `Height` | INTEGER | NOT NULL | | Clone height. |

### Shortcuts

One row per button: a shortcut (file, folder or web link), a group or a run command. Buttons form a tree: a root button
belongs directly to a panel (`ParentMDSHOWindowID` set, `ParentShortcutID` NULL); a button inside a group has
`ParentShortcutID` set to the group and `ParentMDSHOWindowID` NULL.

| Column | Type | Null | Default | Meaning |
|---|---|---|---|---|
| `ID` | INTEGER | NOT NULL | | Primary key. |
| `ParentShortcutID` | INTEGER | NULL | | The group this button is in (`Shortcuts.ID`); NULL for a root button. |
| `ParentMDSHOWindowID` | INTEGER | NULL | | The panel of a root button (`MDSHOWindows.ID`); NULL inside a group. |
| `Name` | TEXT | NOT NULL | | Name shown on the button. For a group, each `{value}` or `{name=value}` in the name is a group value used by its run commands. |
| `Target` | TEXT | NOT NULL | | The file, folder or web address the shortcut opens; empty for groups and run commands. |
| `Ext` | TEXT | NOT NULL | | Extension of the button's shortcut file (`.lnk` or `.url`); empty for groups and run commands. |
| `Guid` | TEXT | NOT NULL | | Unique name (without extension) of the button's shortcut file in the shortcuts folder. |
| `IsGroup` | INTEGER | NOT NULL | | `1` for a group. |
| `IsGroupExpanded` | INTEGER | NOT NULL | | `1` when the group is open. |
| `Comment` | TEXT | NOT NULL | | The button's comment, edited with **Comment...** and shown in its tooltip (empty = no comment). Called `Notes` before database version 6. |
| `IsMarked` | INTEGER | NOT NULL | | `1` when the button is marked with a color. |
| `MarkColor` | TEXT | NOT NULL | | The mark color. |
| `IsProcessCommand` | INTEGER | NOT NULL | | `1` for a run command. |
| `ProcessFileName` | TEXT | NOT NULL | | Program a run command starts, e.g. `cmd.exe`. |
| `ProcessArguments` | TEXT | NOT NULL | | Arguments of a run command, exactly as typed (one command per line; `{=1}` to `{=9}` and `{=name}` are replaced by group values). |
| `ProcessRunAsAdmin` | INTEGER | NOT NULL | `0` | `1` = the run command starts as administrator (after the Windows prompt). Added in version 4. |
| `ProcessWorkingFolder` | TEXT | NOT NULL | `''` | Folder the run command starts in; empty = none. Added in version 4. |
| `IsNote` | INTEGER | NOT NULL | `0` | `1` = a note button (its text is `NoteText`; no shortcut file). Added in version 7. |
| `NoteText` | TEXT | NOT NULL | `''` | The text of a note button; empty for other buttons. Added in version 7. |
| `NoteBorderColor` | TEXT | NOT NULL | `''` | Border colour of the note's window (`#AARRGGBB`); empty = MDSHO's own default colour. Added in version 10. |
| `NoteTitleBackgroundColor` | TEXT | NOT NULL | `''` | Title bar background colour of the note's window; empty = the default. Added in version 10. |
| `NoteTitleTextColor` | TEXT | NOT NULL | `''` | Title text (and title button) colour of the note's window; empty = the default. Added in version 10. |
| `NoteBackgroundColor` | TEXT | NOT NULL | `''` | Background colour of the note's window; empty = the default. Added in version 10. |
| `NoteTextColor` | TEXT | NOT NULL | `''` | Text colour of the note's window; empty = the default. Added in version 10. |
| `IsSnippet` | INTEGER | NOT NULL | `0` | `1` = a snippet (it pastes `SnippetText`; no shortcut file). Added in version 19. |
| `SnippetText` | TEXT | NOT NULL | `''` | The text a snippet pastes, stored as plain text; empty for other buttons. Added in version 19. |
| `TextFont` | TEXT | NOT NULL | `'Courier New'` | The font of a note's or snippet's text, by name (see the font menu); a name MDSHO doesn't know shows Courier New. Added in version 20. |
| `CommentFont` | TEXT | NOT NULL | `'Courier New'` | The font of the button's comment, by name, like `TextFont`. Added in version 22. |
| `CommentFontSize` | INTEGER | NOT NULL | `0` | The text size of the button's comment in whole points (8 to 72); `0` = 12 pt. Added in version 22. |
| `TextFontSize` | INTEGER | NOT NULL | `0` | The text size of a snippet in whole points (8 to 72); `0` = 12 pt. A note keeps its size in `NoteWindows.FontSize`. Added in version 23. |
| `NoteHighlights` | TEXT | NOT NULL | `''` | A note's saved colour highlights, apart from its text: a JSON list of terms and colours, for example `[{"term":"pay","color":"Yellow"}]` (colours `Yellow`, `Green`, `Blue`, `Pink`, `Orange`); empty = none. Each term colours every match in the text, by default ignoring case and also inside other words (see `TextWholeWords` and `TextMatchCase`). Added in version 25. |
| `TextCurrentLine` | INTEGER | NOT NULL | `1` | `1` = the note's or snippet's window shades the line with the caret (**Highlight current line** in More options). Added in version 26. |
| `TextWholeWords` | INTEGER | NOT NULL | `0` | `1` = the note's or snippet's highlights (the live grey ones, the match count and a note's colours) match whole words only: no letter, digit or `_` right before or after. Added in version 26. |
| `TextMatchCase` | INTEGER | NOT NULL | `0` | `1` = the note's or snippet's highlights match the case. Added in version 26. |
| `TextWrapText` | INTEGER | NOT NULL | `0` | `1` = the snippet's text wraps. A note keeps its wrap in `NoteWindows.IsWrapText`. Added in version 26. |
| `CommentCurrentLine` | INTEGER | NOT NULL | `1` | Like `TextCurrentLine`, for the button's Comment window. Added in version 26. |
| `CommentWholeWords` | INTEGER | NOT NULL | `0` | Like `TextWholeWords`, for the comment. Added in version 26. |
| `CommentMatchCase` | INTEGER | NOT NULL | `0` | Like `TextMatchCase`, for the comment. Added in version 26. |
| `CommentWrapText` | INTEGER | NOT NULL | `0` | `1` = the comment's text wraps. Added in version 26. |
| `SyncId` | TEXT | NOT NULL | `''` | The fixed ID that names the button on every PC, a GUID such as `3f2b8c1e-...`. Made when the row is added and never changed; a copy gets a new one, a move keeps it. Unique among the buttons with a SyncId. Added in version 27. |
| `SortKey` | TEXT | NOT NULL | `''` | Order among buttons with the same parent; it replaces `Position` (an integer from 0) of version 26 and older. A text key (digits `0-9`, `A-Z`, `a-z`) compared byte by byte, then by `SyncId` when two keys are equal. A new or moved button gets a key between its neighbours, so the others keep theirs (see [Sort keys](#sort-keys)). Added in version 27. |
| `ChangedAt` | INTEGER | NOT NULL | `0` | When a synced column of the button last changed, or of its note window's `FontSize` or `IsWrapText`: UTC milliseconds since 1970. A local column (see [Synced and local](#synced-and-local)) doesn't count, and saving the same value again doesn't either. Kept by database triggers. Added in version 27. |
| `ChangedBy` | TEXT | NOT NULL | `''` | The `DeviceId` of the PC that made that change. Added in version 27. |

### NoteWindows

One row per note window: where it was last, how big, in which state, and whether it was open, so open notes come back
where they were after a restart. Exports carry them from 1.16.0 (export format 2), linked to their note; older
exports import with default note windows. Added in version 8.

| Column | Type | Null | Default | Meaning |
|---|---|---|---|---|
| `ID` | INTEGER | NOT NULL | | Primary key. |
| `ShortcutID` | INTEGER | NOT NULL | | The note (`Shortcuts.ID`); one row per note. |
| `Left` | INTEGER | NOT NULL | | Left edge of the normal (not maximized) window. |
| `Top` | INTEGER | NOT NULL | | Top edge of the normal window. |
| `Width` | INTEGER | NOT NULL | | Width of the normal window. |
| `Height` | INTEGER | NOT NULL | | Height of the normal window. |
| `State` | INTEGER | NOT NULL | `0` | `0` = normal, `1` = minimized, `2` = maximized. |
| `IsOpen` | INTEGER | NOT NULL | `0` | `1` = the window was open (also when MDSHO or Windows closed it at exit), so it opens again at start. |
| `IsTopmost` | INTEGER | NOT NULL | `0` | `1` = the window stays on top of other apps (the pin in its title bar). Added in version 9. |
| `IsWrapText` | INTEGER | NOT NULL | `0` | `1` = the note's text wraps at the window's edge (the wrap text button in its title bar). Added in version 11. |
| `FontSize` | INTEGER | NOT NULL | `0` | Size of the note's text in whole points, set with the Text size button or Ctrl + mouse wheel (Word's sizes, 8 to 72 pt); a saved size outside 8 to 72 loads as the nearest limit; `0` = 12 pt. Added in version 18 in DIPs; version 21 converts it to points. |

### History

The text history: earlier copies of each button's text and comment, newest first by `KeptAt`, at most the
**Text history** setting per button and field (10 unless set). A copied or pasted button starts with none. Exports and
backups carry them (still export format 3), linked to their button. Added in version 24.

| Column | Type | Null | Default | Meaning |
|---|---|---|---|---|
| `ID` | INTEGER | NOT NULL | | Primary key. Not exported. |
| `ShortcutID` | INTEGER | NOT NULL | | The button (`Shortcuts.ID`). |
| `Field` | TEXT | NOT NULL | | `Text` = a note's or snippet's text, `Comment` = the button's comment. |
| `Text` | TEXT | NOT NULL | | The text as it was (empty for an empty text). |
| `KeptAt` | TEXT | NOT NULL | | When it was kept, local time `yyyy-MM-ddTHH:mm:ss`. |

### DeletedItems

The panels and buttons deleted on this PC, so a later version can delete them on the other PCs too. Deleting a group or
a panel adds a row for it and for everything inside; clones are not items and add none. Adding an item with the same
`SyncId` again (an import that brings it back) removes its row. Exports and backups carry the rows; an import that
replaces the data adds the file's rows to this PC's, Add as new ignores them. Added in version 27.

| Column | Type | Null | Default | Meaning |
|---|---|---|---|---|
| `SyncId` | TEXT | NOT NULL | | Primary key: the `SyncId` the item had. |
| `Kind` | TEXT | NOT NULL | | What it was: `Panel`, `Group`, `Shortcut` (a file, folder or program), `Link` (a web link), `Note`, `Snippet` or `RunCommand`. |
| `DeletedAt` | INTEGER | NOT NULL | | When it was deleted: UTC milliseconds since 1970. |
| `DeletedBy` | TEXT | NOT NULL | | The `DeviceId` of the PC that deleted it. |

### DefaultColors

The default colours saved with **Set as default** in the Colours window: at most one row for panels and one for notes.
They are what **Default colours** puts back and what new panels and new notes start with; existing panels and notes keep
their colours. Without a row MDSHO's own colours are used. Exports carry them from 1.16.0 (format 3); an import that
replaces the data sets them, Add as new keeps this PC's. Added in version 15.

| Column | Type | Null | Default | Meaning |
|---|---|---|---|---|
| `Kind` | TEXT | NOT NULL | | Primary key: `Window` (panels) or `Note`. |
| `BorderColor` | TEXT | NOT NULL | | Default border colour. |
| `TitleBackgroundColor` | TEXT | NOT NULL | | Default title bar background colour. |
| `TitleTextColor` | TEXT | NOT NULL | | Default title text colour. |
| `BackgroundColor` | TEXT | NOT NULL | | Default background colour. |
| `TextColor` | TEXT | NOT NULL | | Default text colour. |

### AppSettings

Settings of the whole app on this PC, one row per setting. Without a row the setting has its default. From 1.16.0
(export format 3) exports carry `WindowTheme`, `BringToFrontShortcut` and `RememberScreenSetupPlaces`, and from 1.22.0
`TextHistoryKeep`; an import that replaces the data sets those (a shortcut another app holds on this PC is turned off). The other settings
belong to this PC: they are not exported and every import keeps them. Added in version 16.

| Column | Type | Null | Default | Meaning |
|---|---|---|---|---|
| `Key` | TEXT | NOT NULL | | Primary key: the setting's name. |
| `Value` | TEXT | NOT NULL | | The setting's value as text. |

#### The settings

| Key | Value |
|---|---|
| `BringToFrontShortcut` | The **Bring panels to front** keyboard shortcut (Settings > Keyboard), for example `Ctrl+Shift+Alt+M`, or `Off`. Without a row: `Ctrl+Shift+Alt+M`. |
| `RememberScreenSetupPlaces` | **Remember places for each screen setup** (Settings > Screens): `On` or `Off`. Without a row: on. Added in version 17. |
| `WindowTheme` | **Window theme** (Settings > Appearance): `SameAsWindows`, `Light` or `Dark`. Without a row, or with any other value: `SameAsWindows`. Added in 1.10.1; the database version stays 17. |
| `SettingsPage` | The Settings page used last, which Settings opens on: `Appearance`, `General`, `Keyboard`, `Screens` or `Backups` (from 1.12.3). Without a row, or with any other value: `Appearance`. Added in 1.11.0; the database version stays 17. |
| `BackupOn` | **Back up my data every day** (Settings > Backups): `On` or `Off`. Without a row: on. Added in 1.12.3; the database version stays 17. |
| `BackupFolder` | The backup folder (Settings > Backups). Without a row, empty, or an old default (`MDSHO backups` in the profile or Documents folder): `%USERPROFILE%\mdsho-backups` (`mdsho-backups-debug` in the Debug build). Added in 1.12.3; the database version stays 17. |
| `BackupKeep` | How many regular backups of this PC to keep: `3`, `5`, `10`, `20` or `30`. Without a row, or with any other value: `10`. Added in 1.12.3; the database version stays 17. |
| `BackupLastTime` | When the last regular backup was written (UTC, round-trip format). Added in 1.12.3; the database version stays 17. |
| `BackupLastHash` | A SHA-256 of the data in the last regular backup, to tell when the data changed. Added in 1.12.3; the database version stays 17. |
| `BackupLastError` | Why the last backup failed, shown in red in Settings; empty after a success. Added in 1.12.3; the database version stays 17. |
| `ReviewFirstStart` | The local date (`yyyy-MM-dd`) of the first start this PC recorded, for the review prompt. Added in 1.13.0; the database version stays 17. |
| `ReviewDaysUsed` | On how many different local days MDSHO ran, for the review prompt. Added in 1.13.0; the database version stays 17. |
| `ReviewLastDay` | The last local day counted in `ReviewDaysUsed` (`yyyy-MM-dd`). Added in 1.13.0; the database version stays 17. |
| `ReviewShown` | How many times the review prompt was shown: `1` or `2`. Without a row: never. Added in 1.13.0; the database version stays 17. |
| `ReviewLastShown` | The local date (`yyyy-MM-dd`) the review prompt was last shown. Added in 1.13.0; the database version stays 17. |
| `TextHistoryKeep` | **earlier texts to keep** under **Text history** (Settings > General): `0` to `50`; `0` keeps none. Without a row, or with a value that is not a number: `10`; a number outside 0 to 50 counts as the nearest. Added in version 24. |
| `ReviewRated` | `Yes` after a rating in the Store dialog, or after Rate opened the Store page (the review prompt, the panel menu or About); the prompt then never shows. Added in 1.13.0; the database version stays 17. |
| `DeviceId` | This PC's device ID for sync, a GUID made once (when the database is created or upgraded to version 27, or again if the row is lost). It is the `ChangedBy` and `DeletedBy` of the changes made here. Never exported, imported or synced. Added in version 27. |
| `DeviceName` | This PC's name for sync. Without a row, or empty: the Windows computer name. Nothing sets it yet. Never exported, imported or synced. Added in version 27. |
| `SyncFolder` | The sync folder of a later version (nothing uses it yet). Belongs to this PC: never exported or synced. Added in version 27. |

### ScreenSetups

The screen setups MDSHO has seen on this PC: one row per set of connected screens. Not part of exports, and an import
that replaces the data keeps them. **Forget the other setups** in the tray menu deletes every row but the current setup,
and a setup beyond the 10 most recently used ones is deleted. Added in version 17.

| Column | Type | Null | Default | Meaning |
|---|---|---|---|---|
| `ID` | INTEGER | NOT NULL | | Primary key. |
| `Key` | TEXT | NOT NULL | | Unique. Which screens make the setup: each screen as `widthxheight` (physical pixels), `*` for the primary screen, then `@c` and its column and `r` and its row, in column, then row order, joined with `\|`, for example `1920x1080*@c0r0\|2560x1440@c1r0` (a monitor right of the primary laptop screen). The column is the number of screens entirely left of the screen, the row the number entirely above it. Device names, exact offsets, the working area and the display scale don't count. MDSHO 1.7.0 wrote `DeviceName@left,top,widthxheight` keys; they are changed to this form, and merged, at start. |
| `Name` | TEXT | NOT NULL | | The name shown in the tray menu's **Screen setups** list: the number of screens and their resolutions, primary screen first, for example `2 screens: 1920×1080, 2560×1440`. Two different setups can have the same name (the same resolutions arranged differently); the list then numbers the one added later. |
| `LastUsed` | TEXT | NOT NULL | `''` | When the setup was last the current one, as UTC text (`2026-11-15T10:00:00.0000000Z`), always later than the others' so the order is right; empty when never recorded. The tray list shows the most recently used first, and only the 10 most recently used setups are kept (never forgetting the current one). Added in 1.7.1 to version 17; a 1.7.0 database gets it at start. |

### ScreenSetupPlaces

Where each panel, clone and note window was last in each screen setup, so they go back there when those screens are
connected again (see [Architecture](architecture.md#places-per-screen-setup)). One row per setup and item. Not part of
exports. From 1.16.0 an import that replaces the data deletes all rows, so the imported places are the ones that show;
Add as new keeps them. Added in version 17.

| Column | Type | Null | Default | Meaning |
|---|---|---|---|---|
| `ID` | INTEGER | NOT NULL | | Primary key. |
| `SetupID` | INTEGER | NOT NULL | | The setup (`ScreenSetups.ID`). |
| `Kind` | INTEGER | NOT NULL | | `0` = panel, `1` = clone, `2` = note window. |
| `ItemID` | INTEGER | NOT NULL | | The panel's `MDSHOWindows.ID`, the clone's `MDSHOWindowClones.ID` or the note's `Shortcuts.ID`. Not a foreign key, so an import that replaces the data (which adds the panels again with the same IDs) keeps the places; places of items that are gone are deleted when MDSHO starts. |
| `ScreenDeviceName` | TEXT | NOT NULL | | Name of the screen the item was mostly on. |
| `Left` | INTEGER | NOT NULL | | Left edge, measured from the left of the whole desktop (for a note, of the normal, not maximized, window). |
| `Top` | INTEGER | NOT NULL | | Top edge, measured from the top of the whole desktop. |
| `Width` | INTEGER | NOT NULL | | Width. |
| `Height` | INTEGER | NOT NULL | | Height. |
| `Dock` | INTEGER | NOT NULL | `0` | `0` = not docked, `1` = docked to the left, `2` = docked to the right of the screen's working area. A docked panel is docked again to the full height of that screen's working area. |

`SetupID`, `Kind` and `ItemID` together are unique.

## Keys and relationships

Foreign keys are switched on for every connection, and all of them use `ON DELETE CASCADE`:

- `MDSHOWindowClones.ParentMDSHOWindowID` → `MDSHOWindows.ID`: deleting a panel deletes its clones.
- `Shortcuts.ParentMDSHOWindowID` → `MDSHOWindows.ID`: deleting a panel deletes its root buttons.
- `Shortcuts.ParentShortcutID` → `Shortcuts.ID`: deleting a group deletes everything inside it.
- `NoteWindows.ShortcutID` → `Shortcuts.ID`: deleting a note deletes its window place.
- `History.ShortcutID` → `Shortcuts.ID`: deleting a button deletes its text history.
- `DeletedItems.SyncId` is not a foreign key: it names items that are gone.
- `ScreenSetupPlaces.SetupID` → `ScreenSetups.ID`: forgetting a screen setup deletes its places.

The shortcut files in the shortcuts folder are not part of the database; the app deletes them itself.

### Indexes

| Index | Column |
|---|---|
| `IX_History_ShortcutID_Field_KeptAt` | `History(ShortcutID, Field, KeptAt)` |
| `IX_MDSHOWindowClones_ParentMDSHOWindowID` | `MDSHOWindowClones(ParentMDSHOWindowID)` |
| `IX_Shortcuts_ParentMDSHOWindowID` | `Shortcuts(ParentMDSHOWindowID)` |
| `IX_Shortcuts_ParentShortcutID` | `Shortcuts(ParentShortcutID)` |
| `UX_MDSHOWindows_SyncId` | `MDSHOWindows(SyncId)` |
| `UX_Shortcuts_SyncId` | `Shortcuts(SyncId)` |

The two `UX_` indexes are unique and cover only rows with a `SyncId` (`WHERE SyncId <> ''`).

### Triggers

Database triggers named `Sync_...` keep the sync columns, so every way of writing (the app, an import, a delete that
cascades) is covered. MDSHO makes them when it creates the database and again at start when they differ from what this
version builds from its list of synced columns; it drops them before an upgrade step and makes them after the last one.

- `Sync_MDSHOWindows_Insert`, `Sync_Shortcuts_Insert`: a new row without a `SyncId` or `ChangedAt` gets one (now, this
  PC), and a `DeletedItems` row with its `SyncId` is removed.
- `Sync_MDSHOWindows_Update`, `Sync_Shortcuts_Update`: when a synced column changes and the update didn't set
  `ChangedAt` itself, `ChangedAt` becomes now (but always later than before) and `ChangedBy` this PC.
- `Sync_MDSHOWindows_Delete`, `Sync_Shortcuts_Delete`: a deleted row adds a `DeletedItems` row.
- `Sync_NoteWindows_Insert`, `Sync_NoteWindows_Update`: a change of a note window's `FontSize` or `IsWrapText` (or a new
  row with other values than the defaults) is a change of its note.

## Synced and local

Every column of every table, and every app setting, is marked as **synced** (the same on every PC) or **local** (this
PC's own) in `src/MDSHO.Data/SyncFields.cs`; a test fails for a column that isn't. Local are, for example, window
places, clones, screen setups and their places, the text history, `IsGroupExpanded`, the shortcut file name (`Guid`),
the backup settings, the text history setting and the device identity. Synced are, for example, names, targets, text,
comments, fonts, colours, highlights, the order and the parent of each item. A change of a synced column of a panel or
button is what moves its `ChangedAt`.

## Sort keys

Siblings are ordered by `SortKey`, then `SyncId`. The keys use fractional indexing: a key has an integer part, whose
first character gives its length (`a0`, `a1`, ... `az`, `b00`, ...), and an optional fraction, so there is always a key
between two others (`a0` < `a0V` < `a1`). Saving a new order keeps the keys that are already in order and gives new
keys only to the moved or added items, so two PCs that change the same list while apart never need to renumber it.
Version 27 gives every list keys in its old order (`Position`, then `ID`; panels by `ID`).

## Upgrade history

A new database is created directly at the latest version. An older database is upgraded at start-up, one step at a
time, each step in its own transaction. If a step fails, it is rolled back and the database stays at the last version
that worked.

| Version | App version | Change |
|---|---|---|
| 1 | before 1.1.0 | Original schema, before versioned upgrades existed (such databases report version 0 or 1). It already had `MDSHOWindows.RoundedCorners`. |
| 2 | 1.1.0 | `MDSHOWindowClones.RoundedCorners` (`INTEGER NOT NULL DEFAULT 1`). The column first appeared in app 1.0.25.0, so the step adds it only when it is missing. |
| 3 | 1.1.0 | Indexes on the foreign key columns (see [Indexes](#indexes)). |
| 4 | 1.1.5 | `Shortcuts.ProcessRunAsAdmin` (`INTEGER NOT NULL DEFAULT 0`) and `Shortcuts.ProcessWorkingFolder` (`TEXT NOT NULL DEFAULT ''`) for the Run command window. |
| 5 | 1.1.5 | `RoundedCorners` dropped from `MDSHOWindows` and `MDSHOWindowClones` (corners now follow the docked state). Uses `ALTER TABLE ... DROP COLUMN`, so no rows, IDs, indexes or foreign keys change. |
| 6 | 1.5.10 | `Shortcuts.Notes` renamed to `Comment` (`ALTER TABLE ... RENAME COLUMN`; the text, rows, indexes and foreign keys stay). `MDSHOWindows.Notes` is unchanged. Exports made before this still import: their `notes` becomes the comment. |
| 7 | 1.5.10 | `Shortcuts.IsNote` (`INTEGER NOT NULL DEFAULT 0`) and `Shortcuts.NoteText` (`TEXT NOT NULL DEFAULT ''`) for note buttons. Exports made before this still import, as normal buttons. |
| 8 | 1.5.10 | `NoteWindows` table: the place, size and state of each note window and whether it was open, so open notes reopen where they were. |
| 9 | 1.5.10 | `NoteWindows.IsTopmost` (`INTEGER NOT NULL DEFAULT 0`): whether a note window stays on top of other apps. |
| 10 | 1.5.10 | The five note window colours on `Shortcuts` (`NoteBorderColor`, `NoteTitleBackgroundColor`, `NoteTitleTextColor`, `NoteBackgroundColor`, `NoteTextColor`, `TEXT NOT NULL DEFAULT ''`). Exports carry them; older exports import with the default colours. |
| 11 | 1.5.10 | Wrap text: `NoteWindows.IsWrapText` and `MDSHOWindows.CommentWrapText` (`INTEGER NOT NULL DEFAULT 0`). |
| 12 | 1.5.10 | `MDSHOWindows.ReserveDockedSpace` (`INTEGER NOT NULL DEFAULT 0`) for "Reserve screen space when docked", which only test builds had. Version 14 drops it again. |
| 13 | 1.5.10 | `MDSHOWindows.ShowVersionInTitle` (`INTEGER NOT NULL DEFAULT 1`): "Show version in title", on for every panel. |
| 14 | 1.5.10 | `MDSHOWindows.ReserveDockedSpace` dropped (`ALTER TABLE ... DROP COLUMN`, like version 5; the rows and other columns stay): "Reserve screen space when docked" was removed. Exports made before this still import. |
| 15 | 1.5.10 | `DefaultColors` table: the panel and note default colours saved with **Set as default** in the Colours window (empty at first). |
| 16 | 1.6.1 | `AppSettings` table: settings of the whole app on this PC, starting with the **Bring panels to front** keyboard shortcut (empty at first). |
| 17 | 1.7.1 | `ScreenSetups` and `ScreenSetupPlaces` tables: the screen setups seen on this PC and the place of every panel, clone and note window in each (empty at first). The 1.7.0 test build created this version without `ScreenSetups.LastUsed`; MDSHO adds the column at start when it is missing. |
| 18 | 1.16.0 | `NoteWindows.FontSize` (`INTEGER NOT NULL DEFAULT 0`): the text size of each note window (`0` = 12). From 1.16.0 exports and backups also carry the note window places (export format 2); older exports still import, with default note windows. The step also adds `ScreenSetups.LastUsed` when a 1.7.0 test build database lacks it. |
| 19 | 1.19.2 | `Shortcuts.IsSnippet` (`INTEGER NOT NULL DEFAULT 0`) and `Shortcuts.SnippetText` (`TEXT NOT NULL DEFAULT ''`) for snippets. Exports and backups carry them (still export format 3); older exports import with no snippets. |
| 20 | 1.20.0 | `Shortcuts.TextFont` (`TEXT NOT NULL DEFAULT 'Courier New'`): the font of each note and snippet, so existing ones switch to Courier New. Copies, exports and backups carry it (still export format 3); older exports import with Courier New. |
| 21 | 1.20.2 | `NoteWindows.FontSize` changes from DIPs to whole points: `0` stays `0` (the default, 12 pt), others become `round(DIPs × 0.75)` within 8 to 72 (12 → 9, 16 → 12, 22 → 17). Still export format 3: an import whose manifest has `schemaVersion` 20 or lower converts its sizes the same way. |
| 22 | 1.21.0 | `Shortcuts.CommentFont` (`TEXT NOT NULL DEFAULT 'Courier New'`) and `Shortcuts.CommentFontSize` (`INTEGER NOT NULL DEFAULT 0`): each comment keeps its own font and text size. The text history: `TextPrevious1`, `TextPrevious2`, `CommentPrevious1` and `CommentPrevious2`, each with a `...Time` column (all `TEXT NOT NULL DEFAULT ''`). Copies, exports and backups carry them (still export format 3); older exports import with Courier New, 12 pt and no history. |
| 23 | 1.21.1 | `Shortcuts.TextFontSize` (`INTEGER NOT NULL DEFAULT 0`): each snippet keeps its own text size. Copies, exports and backups carry it (still export format 3); older exports import with `0` (12 pt). |
| 24 | 1.22.0 | `History` table and its index: the text history, up to the **Text history** setting per button and field. The step moves each non-empty `TextPrevious1/2` and `CommentPrevious1/2` (with its time, the older copy first) into `History`, then drops those eight `Shortcuts` columns (`ALTER TABLE ... DROP COLUMN`). Exports and backups carry the rows (still export format 3); older exports import their two earlier copies as rows. A copied button starts with no history. |
| 25 | 1.26.2 | `Shortcuts.NoteHighlights` (`TEXT NOT NULL DEFAULT ''`): the saved colour highlights of each note, as terms rather than places, so they survive edits. Copies, exports and backups carry it (still export format 3); older exports import with none. |
| 27 | 1.30.0 | Groundwork for sync, nothing visible changes. `MDSHOWindows` and `Shortcuts` get `SyncId` (a new GUID for every row), `SortKey` (keys in the old order: `Position`, then `ID`; panels by `ID`), `ChangedAt` (the upgrade time) and `ChangedBy` (this PC); `Shortcuts.Position` is dropped. New: the `DeletedItems` table, the `UX_` indexes, the `Sync_` triggers and the `DeviceId` setting. Exports and backups carry the new columns and `deletedItems` (still export format 3); older exports import with new IDs and keys in their old order. |
| 26 | 1.27.0 | More options settings per button: `Shortcuts.TextCurrentLine`, `TextWholeWords`, `TextMatchCase`, `TextWrapText`, `CommentCurrentLine`, `CommentWholeWords`, `CommentMatchCase` and `CommentWrapText` (`INTEGER NOT NULL`, current line `DEFAULT 1`, the rest `DEFAULT 0`). The step turns current line off on every button when the app setting `HighlightCurrentLine` (1.26.2) was `Off`, then deletes that setting; it copies each panel's `MDSHOWindows.CommentWrapText` to its buttons' comment wrap and its snippets' text wrap, then drops that column. Exports and backups carry the new columns (still export format 3); older exports import the same way as the step. |

### Backup before an upgrade

Before the first upgrade step runs, MDSHO copies the database next to itself with the SQLite online backup, as
`mdsho.sqlite3.bak-v<old version>-<yyyyMMddHHmmss>` (for example `mdsho.sqlite3.bak-v3-20261109143000`). Nothing is
backed up when there is nothing to upgrade, and MDSHO never deletes these backups.

To go back to a backup, open the app data folder with **Browse app data**, close MDSHO, then rename the current `mdsho.sqlite3`, and rename the backup to `mdsho.sqlite3`. Use
it only with the MDSHO version that wrote it, or a newer one.

### A database from a newer MDSHO

If the database version is higher than any version the app knows, MDSHO says that the data was made by a newer
version, asks you to update, and exits without changing, upgrading or backing up the file.
