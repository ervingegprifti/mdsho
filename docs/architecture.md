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
| MDSHO.Core | `src/MDSHO.Core` | The data model (windows, clones, shortcuts), the repository and transaction interfaces, and run-command logic such as the group value. Plain .NET, no WPF and no SQLite. |
| MDSHO.Data | `src/MDSHO.Data` | SQLite storage: Dapper repositories, the transaction scope, schema creation and upgrades, export and import. Plain .NET. |
| MDSHO.Infrastructure | `src/MDSHO.Infrastructure` | The Windows services behind those interfaces, with no UI: starting processes, links and Explorer, `.lnk`/`.url` files, package identity and the app data folder, the Start with Windows task, monitors and DPI, the clipboard, the Microsoft Store launcher, and the log file set-up. |
| MDSHO.ViewModels | `src/MDSHO.ViewModels` | View models for the panels, clones and buttons, and the services that make and delete shortcut files. Built on CommunityToolkit.Mvvm. Plain .NET (`net10.0`) with no WPF types: see [View models without WPF](#view-models-without-wpf). |
| MDSHO.App | `src/MDSHO.App` | The WPF executable (`MDSHO.exe`): the composition root, windows, XAML, converters, the drag and drop handler, styles, icons, tray menu and dialogs. |
| MDSHO.Package | `src/MDSHO.Package` | The MSIX packaging project (`.wapproj`) with the manifest and Store images. Built with Visual Studio only. |
| MDSHO.Tests | `tests/MDSHO.Tests` | xUnit tests for all of the above, including tests that open the real windows off screen. |

`MDSHO.Cli.slnf` is a solution filter with every project except MDSHO.Package, for building and testing with the
`dotnet` command line. See [Building MDSHO](building.md).

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
connected when the `App` type is first used, makes the view models' prompts (`Dialogs`) message boxes.

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
  the XAML binds to (`PasteShortcutCommand`, `SetRunCommandCommand`, ...) are pinned by a test. Toolkit commands do not
  listen to WPF's `CommandManager`, so `AppViewModel.NotifyPasteTargetsChanged` tells every Paste command to ask again
  after a cut, copy, paste or move.

## Where the data lives

MDSHO stores everything for the current Windows user in one folder, `%LOCALAPPDATA%\MDSHO`:

| Item | Release build | Debug build |
|---|---|---|
| Database (see [Database](database.md)) | `mdsho.sqlite3` | `mdsho-debug.sqlite3` |
| Shortcut files of the buttons (`.lnk`, `.url`) | `shortcuts\` | `shortcuts-debug\` |
| Backups made before an import that replaces the data | `ImportBackups\` | `ImportBackupsDebug\` |
| Backups made before a database upgrade | `mdsho.sqlite3.bak-v<N>-<timestamp>` | `mdsho-debug.sqlite3.bak-v<N>-<timestamp>` |
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
`MDSHO-export-<yyyy-MM-dd-HHmm>.zip` by default, where you choose. The file contains:

- `manifest.json`: the format name (`MDSHO-export`) and version, the MDSHO version and database version that wrote it,
  the date, and the counts of windows, clones, shortcuts and files;
- `data.json`: all windows, clones and shortcuts;
- `shortcuts/`: the shortcut files of the buttons.

**Import data...** first checks the whole file (format, versions, counts, size limits) and then either:

- **replaces** all current data, after saving an automatic export of the current data in the import backups folder, or
- **adds** the imported panels next to the existing ones, with new IDs and new shortcut file names.

The database part of an import runs in one transaction, and the shortcut files are only moved into place after it
commits, so a failed import leaves the existing data as it was. MDSHO restarts after a successful import. Export files
from older MDSHO versions still import; missing newer fields get their defaults.

The code is in `src/MDSHO.Data/DataExporter.cs`, `DataImporter.cs` and `DataPortability.cs`.

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
