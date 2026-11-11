---
title: Database
permalink: /database/
---

# MDSHO database

MDSHO keeps its data in one SQLite file, `mdsho.sqlite3`, in its app data folder (see [Architecture](architecture.md#where-the-data-lives)).
Debug builds use `mdsho-debug.sqlite3` instead, so they never touch the data of the installed app. Versions before
1.1.2 called the file `MDSHO.sqlite3` (Debug: `MDSHODebug.sqlite3`); MDSHO renames it automatically at start-up.

The current schema is **database version 10**. The version is stored in the file itself (`PRAGMA user_version`).
The schema is created and upgraded by `src/MDSHO.Data/SchemaManager.cs`; a test (`tests/MDSHO.Tests/DocsTests.cs`)
checks that this page lists exactly the tables, columns and indexes of a freshly created database.

## Tables

All tables use `ID INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT UNIQUE`, so an ID is never reused, even after a row is
deleted. Flags are `INTEGER` columns holding `0` (no) or `1` (yes). Colors are text in the form `#AARRGGBB`.

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
| `Name` | TEXT | NOT NULL | | Name shown on the button. For a group, a `{value}` in the name is the group value used by its run commands. |
| `Target` | TEXT | NOT NULL | | The file, folder or web address the shortcut opens; empty for groups and run commands. |
| `Ext` | TEXT | NOT NULL | | Extension of the button's shortcut file (`.lnk` or `.url`); empty for groups and run commands. |
| `Guid` | TEXT | NOT NULL | | Unique name (without extension) of the button's shortcut file in the shortcuts folder. |
| `IsGroup` | INTEGER | NOT NULL | | `1` for a group. |
| `IsGroupExpanded` | INTEGER | NOT NULL | | `1` when the group is open. |
| `Comment` | TEXT | NOT NULL | | The button's comment, edited with **Comment...** and shown in its tooltip (empty = no comment). Called `Notes` before database version 6. |
| `IsMarked` | INTEGER | NOT NULL | | `1` when the button is marked with a color. |
| `MarkColor` | TEXT | NOT NULL | | The mark color. |
| `Position` | INTEGER | NOT NULL | | Order among buttons with the same parent, from 0. |
| `IsProcessCommand` | INTEGER | NOT NULL | | `1` for a run command. |
| `ProcessFileName` | TEXT | NOT NULL | | Program a run command starts, e.g. `cmd.exe`. |
| `ProcessArguments` | TEXT | NOT NULL | | Arguments of a run command, exactly as typed (one command per line; `{}` is replaced by the group value). |
| `ProcessRunAsAdmin` | INTEGER | NOT NULL | `0` | `1` = the run command starts as administrator (after the Windows prompt). Added in version 4. |
| `ProcessWorkingFolder` | TEXT | NOT NULL | `''` | Folder the run command starts in; empty = none. Added in version 4. |
| `IsNote` | INTEGER | NOT NULL | `0` | `1` = a note button (its text is `NoteText`; no shortcut file). Added in version 7. |
| `NoteText` | TEXT | NOT NULL | `''` | The text of a note button; empty for other buttons. Added in version 7. |
| `NoteBorderColor` | TEXT | NOT NULL | `''` | Border colour of the note's window (`#AARRGGBB`); empty = the default colour. Added in version 10. |
| `NoteTitleBackgroundColor` | TEXT | NOT NULL | `''` | Title bar background colour of the note's window; empty = the default. Added in version 10. |
| `NoteTitleTextColor` | TEXT | NOT NULL | `''` | Title text (and title button) colour of the note's window; empty = the default. Added in version 10. |
| `NoteBackgroundColor` | TEXT | NOT NULL | `''` | Background colour of the note's window; empty = the default. Added in version 10. |
| `NoteTextColor` | TEXT | NOT NULL | `''` | Text colour of the note's window; empty = the default. Added in version 10. |

### NoteWindows

One row per note window: where it was last, how big, in which state, and whether it was open, so open notes come back
where they were after a restart. Added in version 8.

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

## Keys and relationships

Foreign keys are switched on for every connection, and all of them use `ON DELETE CASCADE`:

- `MDSHOWindowClones.ParentMDSHOWindowID` → `MDSHOWindows.ID`: deleting a panel deletes its clones.
- `Shortcuts.ParentMDSHOWindowID` → `MDSHOWindows.ID`: deleting a panel deletes its root buttons.
- `Shortcuts.ParentShortcutID` → `Shortcuts.ID`: deleting a group deletes everything inside it.
- `NoteWindows.ShortcutID` → `Shortcuts.ID`: deleting a note deletes its window place.

The shortcut files in the shortcuts folder are not part of the database; the app deletes them itself.

### Indexes

| Index | Column |
|---|---|
| `IX_MDSHOWindowClones_ParentMDSHOWindowID` | `MDSHOWindowClones(ParentMDSHOWindowID)` |
| `IX_Shortcuts_ParentMDSHOWindowID` | `Shortcuts(ParentMDSHOWindowID)` |
| `IX_Shortcuts_ParentShortcutID` | `Shortcuts(ParentShortcutID)` |

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
| 6 | 1.3.1 | `Shortcuts.Notes` renamed to `Comment` (`ALTER TABLE ... RENAME COLUMN`; the text, rows, indexes and foreign keys stay). `MDSHOWindows.Notes` is unchanged. Exports made before this still import: their `notes` becomes the comment. |
| 7 | 1.3.1 | `Shortcuts.IsNote` (`INTEGER NOT NULL DEFAULT 0`) and `Shortcuts.NoteText` (`TEXT NOT NULL DEFAULT ''`) for note buttons. Exports made before this still import, as normal buttons. |
| 8 | 1.3.1 | `NoteWindows` table: the place, size and state of each note window and whether it was open, so open notes reopen where they were. |
| 9 | 1.3.1 | `NoteWindows.IsTopmost` (`INTEGER NOT NULL DEFAULT 0`): whether a note window stays on top of other apps. |
| 10 | 1.3.1 | The five note window colours on `Shortcuts` (`NoteBorderColor`, `NoteTitleBackgroundColor`, `NoteTitleTextColor`, `NoteBackgroundColor`, `NoteTextColor`, `TEXT NOT NULL DEFAULT ''`). Exports carry them; older exports import with the default colours. |

### Backup before an upgrade

Before the first upgrade step runs, MDSHO copies the database next to itself with the SQLite online backup, as
`mdsho.sqlite3.bak-v<old version>-<yyyyMMddHHmmss>` (for example `mdsho.sqlite3.bak-v3-20261109143000`). Nothing is
backed up when there is nothing to upgrade, and MDSHO never deletes these backups.

To go back to a backup, open the app data folder with **Browse app data**, close MDSHO, then rename the current `mdsho.sqlite3`, and rename the backup to `mdsho.sqlite3`. Use
it only with the MDSHO version that wrote it, or a newer one.

### A database from a newer MDSHO

If the database version is higher than any version the app knows, MDSHO says that the data was made by a newer
version, asks you to update, and exits without changing, upgrading or backing up the file.
