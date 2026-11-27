---
title: Architecture
permalink: /architecture/
---

# Architecture

MDSHO is a WPF desktop app for Windows, written in C# on .NET 10 (x64). It ships only as an MSIX package. This page
describes how the source code is organised and where the app keeps its data.

## Projects

The solution is `MDSHO.slnx` at the repository root. The projects are:

| Project | Folder | What it contains |
|---|---|---|
| MDSHO.Common | `src/MDSHO.Common` | Shared helpers with no data access: app constants, data folder paths, screen geometry (with its own `ScreenRect`/`ScreenPoint`), `#AARRGGBB` colour text, Microsoft Store links, start-with-Windows logic, and the interfaces of the Windows services (`PlatformServices.cs`). Plain .NET, no WPF. |
| MDSHO.Core | `src/MDSHO.Core` | The data model (windows, clones, shortcuts), the repository and transaction interfaces, and run-command logic such as group values. Plain .NET, no WPF and no SQLite. |
| MDSHO.Data | `src/MDSHO.Data` | SQLite storage: Dapper repositories, the transaction scope, schema creation and upgrades, export and import. Plain .NET. |
| MDSHO.Infrastructure | `src/MDSHO.Infrastructure` | The Windows services behind those interfaces, with no UI: starting processes, links and Explorer, `.lnk`/`.url` files, package identity and the app data folder, the Start with Windows task, monitors and DPI, the clipboard, the Microsoft Store launcher, and the log file set-up. |
| MDSHO.ViewModels | `src/MDSHO.ViewModels` | View models for the panels, clones and buttons, and the services that make and delete shortcut files. Built on CommunityToolkit.Mvvm. Plain .NET (`net10.0`) with no WPF types: see [View models without WPF](#view-models-without-wpf). |
| MDSHO.App | `src/MDSHO.App` | The WPF executable (`MDSHO.exe`): the composition root, windows, XAML, converters, the drag and drop handler, styles, icons, tray menu and dialogs. |
| MDSHO.Package | `src/MDSHO.Package` | The MSIX packaging project (`.wapproj`) with the manifest and Store images. Built with Visual Studio only. |
| MDSHO.Tests | `tests/MDSHO.Tests` | xUnit tests for all of the above, including tests that open the real windows off screen. |

`MDSHO.Cli.slnf` is a solution filter with every project except MDSHO.Package, for building and testing with the
`dotnet` command line. See [Building MDSHO](building.md).

Other folders and files at the repository root:

| Path | What it contains |
|---|---|
| `assets/Font` | The MDSHOFont icon font and the SVG sources of its icons. |
| `assets/Logo` | The logo (`logo.svg`) and its PNG and ICO versions. |
| `docs` | This documentation, published with GitHub Pages. |
| `Directory.Build.props` | Build settings shared by all projects, including the app version. |
| `Directory.Packages.props` | NuGet package versions for all projects (central package management). |
| `global.json` | The pinned .NET SDK version. |

## Dependencies

Which MDSHO projects each project references:

- MDSHO.Package: MDSHO.App
- MDSHO.App: MDSHO.ViewModels, MDSHO.Data, MDSHO.Core, MDSHO.Common, MDSHO.Infrastructure
- MDSHO.ViewModels: MDSHO.Data, MDSHO.Core, MDSHO.Common
- MDSHO.Data: MDSHO.Core, MDSHO.Common
- MDSHO.Infrastructure: MDSHO.Common
- MDSHO.Tests: MDSHO.App, MDSHO.ViewModels, MDSHO.Data, MDSHO.Core, MDSHO.Common, MDSHO.Infrastructure

MDSHO.Core and MDSHO.Common reference no other MDSHO project, and MDSHO.Infrastructure references only MDSHO.Common.
Third-party packages: Dapper and Microsoft.Data.Sqlite (in MDSHO.Data), gong-wpf-dragdrop (MDSHO.App), Serilog and Serilog.Sinks.File (MDSHO.Infrastructure and MDSHO.App), PixiEditor.ColorPicker,
Microsoft.Extensions.Hosting and Serilog.Extensions.Logging (MDSHO.App). They are listed with their licences in
[THIRD-PARTY-NOTICES.md](https://github.com/ervingegprifti/mdsho/blob/main/THIRD-PARTY-NOTICES.md).

## Composition root

`App.OnStartup` (`src/MDSHO.App/App.xaml.cs`) is the one place where MDSHO is put together:

1. The single-instance check runs first; a second copy shows "MDSHO seems to be running already" and exits.
2. The log file's Serilog logger is created (see [Logging](#logging)).
3. `AppHost.Build` (`src/MDSHO.App/AppHost.cs`) builds a .NET Generic Host with an empty builder (no configuration
   files, no console or event log output). It registers, as single instances: the SQLite data store and its three
   repositories, every MDSHO.Infrastructure service under its MDSHO.Common interface, the Rate / Share actions,
   `AppServices`, the app view model (loaded by `ViewModelFactory` on first use, after the database upgrade), the main
   window and the `WindowFactory`; the About window is created new each time. The container checks every registration
   when it is built. Microsoft.Extensions.Logging goes to the same Serilog logger. The host has no background services,
   so it is built but not started.
4. `AppHost.UseServices` points the static seams of the code that the container does not create at the container's
   instances: `DataStore.Current` (used by the view models), `AppServices.Current` (used by windows created in XAML
   or by other windows, and by the static error dialog), and the view models' process starter, clipboard and
   shortcut file writer.
5. The database is upgraded (a database from a newer MDSHO stops startup with "Please update MDSHO"), the app view
   model is resolved and the saved windows open through the `WindowFactory`.

`App.OnExit` logs the exit, closes the log file and disposes the host, releases the single-instance lock and, after an
import, starts MDSHO again.

The view models are still created by the static `ViewModelFactory` and reach the data through `DataStore`; they are not
constructor-injected yet. Tests replace single services with fakes (`AppServices.Current`, `ShortcutViewModel.ProcessStarter`,
`ShortcutFileService.LinkWriter`), so no test starts a process, opens Explorer or touches the clipboard.

## View models without WPF

MDSHO.ViewModels, MDSHO.Common, MDSHO.Data and MDSHO.Core target plain `net10.0` and reference no WPF assembly
(`ArchitectureTests` fails otherwise), so they can be reused outside the WPF app. The view models expose plain values
and MDSHO.App turns them into WPF ones:

| View model value | In the view |
|---|---|
| `Show...` flags (bool) | `Visibility` through WPF's `BooleanToVisibilityConverter` (`BoolToVisibility`) |
| `IsDocked` (bool) | corner radii through `DockedCornerRadiusConverter`, with the floating radius as the parameter |
| colours as `"#AARRGGBB"` text | brushes through `HexToBrushConverter`; the Settings colour pickers use `HexToColorConverter` (two way) |
| `ShortcutKey` / `ShortcutModifiers` | mapped from WPF keys by `Helpers/KeyInput` |
| screen geometry (`ScreenRect`, `ScreenPoint`) | converted to WPF `Rect`/`Point` by `Helpers/GeometryConversions` |

Drag and drop on a window's shortcut tree goes through `Helpers/ShortcutDropHandler` (gong-wpf-dragdrop); it calls the
view model's plain methods to move items and to make shortcuts from dropped files and links. `Helpers/WpfBridge`,
connected when the `App` type is first used, makes the view models' prompts (`Dialogs`) MDSHO dialogs.

Every message is the one MDSHO dialog, `MessageWindow`, shown with `MdshoMessage.Show` (the app's own messages are in
`AppMessages`). It has MDSHO's title bar with the logo, an Info or Warning icon, the text in one size and colour (the first
line bold) and OK, OK/Cancel, Yes/No or Yes/No/Cancel. `MessageRules` in Common decides the buttons, the default and the
answer of Esc and the close button. `ErrorWindow`, `CommandErrorWindow` and `PasteMessageWindow` are built on it.

The view models use [CommunityToolkit.Mvvm](https://github.com/CommunityToolkit/dotnet) (plain .NET, MIT):

- They derive from `ObservableObject`. Simple properties are generated from fields marked `[ObservableProperty]`,
  with `[NotifyPropertyChangedFor]` for values worked out from them (for example `Preview`, `ToolTipText`, the icon
  glyphs). Extra work after a change goes in the generated `OnXxxChanged` hook (`IsGroupExpanded` saves itself,
  `Name` refreshes the commands in its group, `SolidIcons` switches the glyphs). `ShortcutViewModel` still saves its
  other fields from its own `PropertyChanged` handler, so a move can update the parent in memory without saving it again.
- Properties stay hand-written with `SetProperty` when a generated one cannot do the job: a private setter
  (`IsDocked`, `IsEditingTitle`, `EditField`), turning `null` into `""` (`ProcessWorkingFolder` and the Run Command
  window's fields) or a getter that is worked out (`Target`).
- Commands are generated from methods marked `[RelayCommand]` (with `CanExecute =` where there is a check); the names
  the XAML binds to (`PasteShortcutCommand`, `EditRunCommandCommand`, ...) are pinned by a test. Toolkit commands do not
  listen to WPF's `CommandManager`, so `AppViewModel.NotifyPasteTargetsChanged` tells every Paste command to ask again
  after a cut, copy, paste or move.

## Where the data lives

MDSHO stores everything for the current Windows user in one folder, `%LOCALAPPDATA%\MDSHO`:

| Item | Release build | Debug build |
|---|---|---|
| Database (see [Database](database.md)) | `mdsho.sqlite3` | `mdsho-debug.sqlite3` |
| Shortcut files of the buttons (`.lnk`, `.url`) | `shortcuts\` | `shortcuts-debug\` |
| Backups made before an import that replaces the data (the newest 5 are kept) | `ImportBackups\` | `ImportBackupsDebug\` |
| Backups made before a database upgrade | `mdsho.sqlite3.bak-v<N>-<timestamp>` | `mdsho-debug.sqlite3.bak-v<N>-<timestamp>` |
| Automatic backups (see [Automatic backups](#automatic-backups)) | `%USERPROFILE%\mdsho-backups\` | `%USERPROFILE%\mdsho-backups-debug\` |
| Log files (see [Logging](#logging)) | `logs\mdsho-<yyyyMMdd>.log` | the same folder |

Debug builds use their own names so that running the code from Visual Studio never changes the data of the installed
app.

**Names before 1.1.2.** Older versions used `MDSHO.sqlite3` and `MDSHOShortcuts\` (Debug: `MDSHODebug.sqlite3` and
`MDSHOShortcutsDebug\`). At start-up, right after the single-instance check and before anything opens the folder,
`src/MDSHO.Data/DataNamesUpgrade.cs` renames them to the new names: the database together with its `-wal`, `-shm` and
`-journal` files (the case-only rename goes through a temporary name, so the new case is really written on disk), and
the shortcuts folder. It never overwrites or deletes anything: if the old and the new name both exist, MDSHO uses the
new one, leaves the old one as it is and writes a warning to the log. Upgrade backups (`.bak-v*`) keep their old names.
Dropped `.lnk` and `.url` files are stored with their full path in the shortcuts folder (`Shortcuts.Target`), so after
the database upgrade every stored path inside the old folder is moved to the new folder. This runs on every start and
changes nothing the second time, so data imported from an older export file is fixed after the restart too. Every
rename is written to the log.

**The MSIX package folder.** When MDSHO runs as an installed package, Windows may redirect its writes to
`%LOCALAPPDATA%\MDSHO` into the package's private folder,
`%LOCALAPPDATA%\Packages\<package folder>\LocalCache\Local\MDSHO`. This happens unless `%LOCALAPPDATA%\MDSHO` already
existed before MDSHO was installed. The app itself sees the normal path either way, but File Explorer shows the real
location, and uninstalling the package deletes the private folder.

**Browse app data.** The tray menu and each panel's title menu have **Browse app data**, which opens the folder that
really holds the database and shortcuts: the package's private folder when MDSHO runs as a package and that folder
exists, otherwise `%LOCALAPPDATA%\MDSHO`. The decision is made in `src/MDSHO.Common/AppDataFolder.cs`.

## Logging

MDSHO logs with [Serilog](https://serilog.net/) and its file sink. Only MDSHO.Infrastructure and MDSHO.App reference
Serilog. The other projects log through a small static seam, `AppLog` in `src/MDSHO.Common/AppLog.cs`, which does
nothing until the app plugs in a writer. The host's `ILogger<T>` goes to the same logger, with events of the
`Microsoft.*` loggers below Warning dropped. Tests plug in an in-memory Serilog sink, so they never write log files.

- **Where:** the `logs` folder inside the app data folder, the same folder **Browse app data** opens, so the packaged
  (redirected) case works too. One file per day, `mdsho-yyyyMMdd.log`. A file that reaches 1 MB rolls over to a new
  one, and at most 14 files, none older than 14 days, are kept. Minimum level: Information.
- **Set-up:** `src/MDSHO.Infrastructure/Logging/AppLogging.cs` creates the logger right after the single-instance
  check; the host owns it. The log is flushed and closed in `App.OnExit`, before the restart after an import. If the folder can't be created, MDSHO
  runs without file logging. Logging never throws.
- **What is logged:** startup (version, Windows version, 64-bit, packaged, app data folder, database version) and exit,
  each database upgrade step and the `.bak` backup, a database from a newer MDSHO, export and import (start, counts,
  failure), and every error the error window shows, including the global unhandled-exception handlers (the window
  logs inside `ErrorDialog`).
- **Run commands:** a run command that can't be started is logged with the program name and the Windows error code
  only. The arguments and the working folder are never logged, because they can contain passwords or server names, and
  .NET copies the working folder into its exception message. A declined administrator prompt is logged at Information.

The error window shows a short message and the exception's message, never the stack trace. **Copy details** puts the
full exception text on the clipboard, and **Open logs folder** opens the logs folder.

## Export and import

**Export data...** (tray menu and each panel's header menu) saves everything to one `.zip` file, named
`MDSHO-export-<PC>-<yyyy-MM-dd-HHmm>.zip` by default, where you choose. The file contains:

- `manifest.json`: the format name (`MDSHO-export`) and version, the MDSHO version and database version that wrote it,
  the date, and the counts of windows, clones, shortcuts, note windows and files;
- `data.json`: all windows, clones and shortcuts; the note window places (where each note window was, its size,
  state, whether it was open, pinned or wrapped, and its text size), each linked to its note by the note's ID; the
  default colours (`defaultColors`: `Window` and `Note`, `null` when never set); and three settings (`settings`:
  `WindowTheme`, `BringToFrontShortcut` and `RememberScreenSetupPlaces`, `null` when never set). Settings tied to the PC
  (backups, the review prompt, the last Settings page) are never in the file. From 1.30.0 each window and shortcut also
  has its `syncId`, `sortKey`, `changedAt` and `changedBy`, and `deletedItems` lists the deleted panels and buttons
  (see [Database](database.md#synced-and-local)). A replace keeps the IDs in the file; Add as new, and files from before
  1.30.0, get new IDs, and older files' `position` numbers become sort keys in the same order;
- `shortcuts/`: the shortcut files of the buttons.

**Import data...** first checks the whole file (format, versions, counts, size limits) and then either:

- **replaces** all current data, after saving an automatic export of the current data in the import backups folder, or
- **adds** the imported panels next to the existing ones, with new IDs and new shortcut file names.

The database part of an import runs in one transaction. The shortcut files are put in place just before it commits, in
a way that can be undone: Replace renames the shortcuts folder aside and the new files' folder in its place, and deletes
the old folder only after the commit. A failed import rolls back the database and puts the files back, so it leaves the
existing data as it was. MDSHO restarts after a successful import.

If the database can't be opened at start (a corrupt file, or an upgrade that failed), MDSHO offers to restore the latest
automatic backup, start with empty data, or exit. The first two rename the old file to `MDSHO.sqlite3.unreadable-<time>`
and make a new database. Export files
from older MDSHO versions still import; missing newer fields get their defaults.

The format version is 3 since 1.16.0: 2 added the note window places, 3 the default colours and settings. MDSHO reads
formats 1 to 3. A format 1 file has no note window places, so its notes open in default windows; format 1 and 2 files
have no colours or settings, so the ones on this PC stay as they are.

Only **replace** applies the default colours and settings: a value in the file is saved, a `null` removes this PC's
value (its default applies), and a missing one is kept. Replace also deletes the places remembered for each screen
setup, so the imported places are what shows after the restart. If another app holds the imported Bring panels to front
shortcut on this PC, MDSHO turns it off and says so after the import. **Add as new** leaves colours, settings and
screen setup places alone. An import links each place to its note's new
ID (Add as new gives new IDs) and skips a place whose note is not in the file. Before the import MDSHO saves the open
notes; after it, it closes them without saving, so the old windows can't write over the imported places. After the
restart the notes the file had open come back where they were.

The code is in `src/MDSHO.Data/DataExporter.cs`, `DataImporter.cs` and `DataPortability.cs`.

## Automatic backups

`src/MDSHO.ViewModels/BackupService.cs` writes the same zip as Export data to a local folder, by default
`%USERPROFILE%\mdsho-backups` (MSIX deletes the app's own data folder on uninstall, and antivirus ransomware
shields and Windows' Controlled folder access block writes to Documents). There is no cloud
API: a OneDrive or Google Drive folder gives a cloud copy. The manifest also holds the PC name (`pcName`, optional).

- It checks at start and every hour. A backup is due when backups are on, the last one is 24 hours old or more, and
  `DataExporter.DataHash()` (a SHA-256 of data.json and the shortcut files, without the export time) changed.
- On exit (Exit, and Windows sign-out or shutdown through `SessionEnding`), after open notes are saved, `App` calls
  `RunOnExit()`: a backup when backups are on and the hash changed, whatever the last one's age. It runs on the way out,
  shares the lock, and a failure is only logged and saved for Settings.
- Files are `MDSHO-backup-<PC>-yyyy-MM-dd-HHmmss.zip`. After each one, the oldest of this PC's regular backups past the
  keep count are deleted; other PCs' files and upgrade backups are never touched, so several PCs can share a folder.
- Before a database upgrade, `App` calls `BackUpBeforeUpgrade()`, whatever the on/off setting:
  `MDSHO-backup-<PC>-before-<version>-yyyy-MM-dd-HHmmss.zip`, read from the old tables as they are, with the old
  database version in the manifest. A failure is logged and MDSHO starts anyway.
- A failure shows no popup: it is logged and saved (`BackupLastError`) for the red line in Settings > Backups. A denied
  write says the antivirus or Windows blocked MDSHO. A denied delete of an old backup is only logged.
- A saved folder that is an old default (`MDSHO backups` in the profile or Documents folder, from 1.12.0 and 1.12.1)
  counts as no folder chosen, so the current default applies; files there are left alone.
- The settings are in `AppSettings` (see [Database](database.md)). Import data's file dialog opens in the backup folder.

## Sync

Sync (2.0.0) keeps the panels and buttons of several PCs the same through a folder they all reach; there is no server.
App settings and default colours are not synced: every one is Local in `SyncFields.cs`, so each PC keeps its own.

- **Folder** (`src/MDSHO.Data/Sync/SyncFolder.cs`): one subfolder per PC, named by its `DeviceId`; only that PC writes
  in it. `index.json` lists the PC's items (`syncId` → hash, change record, `seq`), its deletes and how far it merged
  each other PC (`seen`), with a `check` (SHA-256 of the rest). Item files (`items/<sha256>.json`) and shortcut files
  (`blobs/<sha256>.bin`) are named by the hash of their content and never change; a file whose hash doesn't match is
  still being copied and is read next round. A write puts new files first, then `index.tmp`, then renames
  `index.json` to `index.prev.json` and `index.tmp` to `index.json`; a bad `index.json` means `index.prev.json` is read.
  Files no index uses are deleted after 7 days. A PC with a newer `format` is not read.
- **Engine** (`SyncEngine.Run`): reads the entries of each other PC with a `seq` above the last one merged, merges
  them into this PC's data in one transaction, then writes this PC's index when an item or delete changed (a round
  that only read writes nothing; `seen` is also kept in the `SyncSeen` setting). Items are matched only by `SyncId`.
  Per item the newer `ChangedAt` wins, a tie goes to the higher device id. Each item file carries the version it was
  changed from (`baseAt`, `baseBy`): a losing version the winner was not made from is a clash, and its texts (note,
  snippet, comment) go into `History` with `FromPc`. A change to a button whose open window has unsaved typing waits
  for a later round (nothing is forced); after the round the app shows the saved texts in the other open windows
  (`TextEditWindow.ShowSyncedTexts`, the old text into `History`). An item whose group is gone goes
  to the top of its panel, a deleted panel with a living item comes back, and of two moves that make a loop the older
  one is undone; these fixes are new changes by this PC, so they reach the others. Writes set `ChangedAt` themselves,
  so the triggers keep the other PC's change record.
- **Service** (`src/MDSHO.ViewModels/SyncService.cs`): a round at start, at the first 10-second check after a local
  change (`SyncEngine.ChangeStamp`), every minute, at exit and on Sync now, each in the background. The exit round and
  the folder check when turning on give up after `WaitLimit` (5 s), so a hanging folder never blocks closing. Turning on makes a
  backup first (`BackupService.BackUpForSync`, never deleted). The app hands a round's result to
  `AppViewModel.ApplySync`, which opens, closes and rebuilds the panels it changed. A panel new to this PC is inserted
  at `SyncOptions.NewPanelPlace` (the app passes `SyncService.CentreOf` the primary screen, like New panel), each next
  one in the round 30 px right and down; without it, at 100, 100.

## Review prompt

`src/MDSHO.ViewModels/ReviewPrompt.cs` decides when the one-time "Enjoying MDSHO?" window (`ReviewWindow`) shows:
only for Microsoft Store installs (`Package.Current.SignatureKind == Store`; otherwise it logs why once and stays off),
installed 14 days (from `ReviewFirstStart`), used on 7 different local days, a minute after start, and not while another
MDSHO dialog is open (it tries again a minute later). A long run checks every hour, so new days count. Later or X on the
first showing asks again after 30 days; there are at most two showings. Off in the Debug build. It opens with
`ShowActivated=false` beside the panel used last (`DesktopWindowView.LastUsed`), else bottom-right of the primary screen.

Rate (the prompt, the panel menu and About) goes through `StoreActions.RateAsync`. On a Store install it shows the Store's
in-app rating dialog (`Infrastructure.StoreRating`: `StoreContext.RequestRateAndReviewAppAsync`, initialised with an MDSHO
window handle). A rating raises `StoreActions.Rated`, which stops the prompt for good; a cancel counts as Later; a
failure opens the Store review page and also counts as rated. Other installs open the Store review page as before.
The clock, timer, dialog check and the rating dialog (`IStoreRating`) are injected, so the rules are tested without
waiting or calling the Store. The settings are in `AppSettings`.

## Bring panels to front

A global keyboard shortcut (default **Ctrl+Shift+Alt+M**) and the first tray menu item bring every visible panel above
the other windows; pressing it again, Esc in a panel or opening a button sends them back. The parts:

- `src/MDSHO.Common/Hotkey.cs`: the shortcut as plain values (modifiers and a Win32 virtual key), which combinations are
  allowed (Ctrl, Alt or Win with a key whose name doesn't depend on the keyboard layout), and its text in the Settings
  box (`Ctrl + Shift + Alt + M`), the tray menu and the database (`Ctrl+Shift+Alt+M`, or `Off`).
- `src/MDSHO.Common/PanelsToFront.cs`: bring to front and send back, deciding from what is on screen. When another app's
  normal window covers a panel, it brings the panels up and remembers the window in front and, for each panel, the
  window right above it, so sending back puts each panel where it was in the z-order and gives the focus back. When the
  panels are already in front without it (a panel has the focus, or no other app's window is above any panel; MDSHO and
  always-on-top windows don't count), the press sends each panel behind the nearest other app's window below it and
  focuses the one under the top panel. Switching to another app ends it without moving anything.
- `src/MDSHO.ViewModels/BringToFrontShortcut.cs`: the saved shortcut (the `AppSettings` table, see
  [Database](database.md#appsettings); exported, and set by an import that replaces the data). It registers the shortcut
  at startup; when another app holds the keys it stays unregistered without a message, and the Settings window shows it
  in red. After a Replace import, `ApplyImported()` registers the imported keys; if another app holds them it turns the
  shortcut off and the import's message says so.
  `ShortcutRecorder.cs` is the Settings box (record, Default, Off, Apply).
- `src/MDSHO.Infrastructure/GlobalHotkeyService.cs` (`RegisterHotKey` with `MOD_NOREPEAT` on a message-only window,
  released at exit) and `WindowZOrder.cs` (z-order and foreground window calls) are the thin Win32 part;
  `src/MDSHO.App/Helpers/WpfPanelWindows.cs` raises the panels (Topmost on and off) and focuses the first one's buttons.

## Places per screen setup

MDSHO remembers, for each screen setup (each set of connected screens), where every panel, clone and open note window
was, and moves them there when the screens change (on by default; **Remember places for each screen setup** in
Settings). The parts:

- `src/MDSHO.Common/ScreenSetups.cs`: plain logic. `ScreenSetup` identifies a setup from the screens' resolutions,
  which one is primary, and each screen's column and row (how many screens are entirely left of it and above it), for
  example `1920x1080*@c0r0|2560x1440@c1r0`. Device names, which Windows renumbers, and exact offsets don't count. It
  names the setup ("2 screens: 1920×1080, 2560×1440", primary screen first, then left to right) and turns a 1.7.0 key
  (device names and bounds) into the new one (`FromLegacyKey`). `ScreenSetupPlacement` says which screen a window is mostly on
  and whether it is docked left or right, and chooses where it goes: its saved place (a docked one docked again to that
  screen's working area), otherwise where it is, and pulled onto a screen when its title bar can't be reached
  (`ScreenGeometry.EnsureOnScreen`, the startup rule). A docked panel goes to the screen under its saved place, since a
  saved device name may be out of date. `Debouncer` counts several display changes within 1.5 seconds
  as one.
- `src/MDSHO.ViewModels/ScreenSetupTracker.cs`: the tracker, created by `AppServices` and started after the windows and
  notes open. It saves places (`Remember`) for the current setup; panels and clones call it where their own place is
  saved (end of a move or resize, dock, centre, Bring MDSHO windows to this screen, a new panel or clone), note windows when their
  place is saved. On a display change it waits, then identifies the setup: a known one moves every item to its saved
  place, a new one only pulls items that are off screen back; then it saves every item's place for that setup. With
  the setting off it still tracks the setup and saves places, but moves nothing. It never saves while a change is
  waiting or while it moves the windows, and drops a save when the screens no longer match the current setup (Windows
  can move windows before the event comes), so one setup's moves never land in another setup's places. Each time a
  setup becomes the current one it records the time (`LastUsed`) and forgets the least recently used setups beyond
  `MaxSetups` (10), never the current one. At startup it first merges setups whose keys now mean the same screens (1.7.0
  keys), keeping the most recently used one's places. The tray menu's **Screen setups** submenu uses `KnownSetups` (most
  recently used first), `PutBack` and `ForgetOthers`.
- `DesktopWindowPlacedItem` (same file) moves a panel or clone through its view model and saves its own row too;
  `src/MDSHO.App/Helpers/NoteWindowPlacedItem.cs` moves a note window's normal bounds (a maximized note is maximized
  again on its new screen).
- `src/MDSHO.Infrastructure/DisplayChangeWatcher.cs` listens to `SystemEvents.DisplaySettingsChanged` (Windows'
  `WM_DISPLAYCHANGE`); the app passes each event to the tracker on the UI thread, and
  `src/MDSHO.App/Helpers/DispatcherOneShotTimer.cs` is the wait.
- The places are in the `ScreenSetups` and `ScreenSetupPlaces` tables and the setting in `AppSettings` (see
  [Database](database.md#screensetups)): on this PC only and not exported. An import that replaces the data keeps the
  setups but deletes their places; the setting is exported and set by such an import.

## Window theme

**Window theme** in Settings (Same as Windows, Light or Dark) changes MDSHO's own windows and menus: Settings, Colours
(but not its colour swatches and palette), Run command, Examples, About, the error windows, the Comment window, the
right-click menus and the tray menu. Panels, clones and notes keep their own colours. Settings shows a choice at once
(`WindowThemeSetting.Preview`); Apply saves it and closing without Apply goes back. The parts:

- `src/MDSHO.ViewModels/WindowThemeSetting.cs`: the choice, saved in `AppSettings` (`WindowTheme`), and the theme in
  use. Same as Windows asks `IWindowsAppMode`, which `src/MDSHO.Infrastructure/WindowsAppMode.cs` reads from
  `HKCU\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize\AppsUseLightTheme` and watches through
  `SystemEvents.UserPreferenceChanged`.
- `src/MDSHO.App/Themes/Light.xaml` and `Dark.xaml`: the same `Theme.*` brushes. Light has the colours the windows
  always had and no styles; Dark also styles the right-click menus. The windows use the brushes through
  `DynamicResource`, so they change at once.
- `src/MDSHO.App/Helpers/WindowThemes.cs`: puts Light or Dark in the app's resources, follows the setting, and adds
  `Themes/DarkControls.xaml` (dark text boxes, combo boxes, check boxes, radio buttons, buttons and lists) only to the
  windows marked `WindowThemes.IsThemed="True"`, and only in Dark. It also gives the WinForms tray menu
  `DarkTrayMenuRenderer` and light icons in Dark, and the Windows look in Light. Tooltips keep the Windows look.
- `WindowThemeAppTests` fails if a themed window gets a hard-coded colour.

## Settings window

`src/MDSHO.App/SettingsWindow.xaml` has a list of pages on the left (`ListSettingsPages`: Appearance, General,
Keyboard, Screens, Backups, Sync) and shows the chosen page on the right. All pages share one cell of `PanelBody`; hidden pages keep
their space, so the window fits the tallest page and its height never changes. Apply saves every page at once; when the
shortcut is taken it switches to the Keyboard page. The page used last is saved in `AppSettings` (`SettingsPage`).
To add a page, add a `ListBoxItem` to the list and a panel in `PanelBody` with `Style="{StaticResource StyleSettingsPage}"`
and its `Tag` bound to that item.
Like Run command and Colours, every outer margin of the page and of the Apply bar (`Footer`, on `Theme.BarBackground`)
equals the Apply button height, set in code; the list starts at that height too, so its first item lines up with the
page title (`StyleSettingsCategory`, 15 px semibold). Groups are 16 px apart, and a hint (`StyleSettingsHint`, 12 px)
sits 4 px under its control. Buttons on the pages use `StyleSettingsButton` (filled like Apply, 26 px high).

## Database upgrades

The database carries its schema version (`PRAGMA user_version`). At start-up, `src/MDSHO.Data/SchemaManager.cs`:

1. creates the database at the latest version if there is none;
2. refuses a database that is newer than the app, without changing it (the app then asks you to update MDSHO and
   exits);
3. otherwise copies the database next to itself as `mdsho.sqlite3.bak-v<old version>-<yyyyMMddHHmmss>`, if there is
   anything to upgrade, and then
4. applies each missing upgrade step in its own transaction.

The steps are only ever appended, never edited. The full history is on the [Database](database.md#upgrade-history)
page.
