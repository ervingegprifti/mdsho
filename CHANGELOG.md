# Changelog

All notable changes to MDSHO are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).
The MSIX package version uses four parts (`Major.Minor.Patch.0`); the fourth part is reserved by the Microsoft Store.

## [1.15.0]

### Added

- **Colour presets**: the Colours window of a panel or a note has eight ready-made themes, such as Nord and Dracula. Click one to try it, then Apply to keep it. "Preset: Nord" shows which one is in use.

## [1.14.3]

### Added

- **Copy command to memory** in a run command's menu copies the command as its tooltip shows it.

## [1.14.2]

### Added

- **Run command Examples**: new Git and Docker groups with everyday commands, such as git status, git pull and docker ps.
- Double-click the MDSHO icon at the top left of a panel to open About.

### Changed

- **Settings** is wider, so its pages have more room.
- Updated globe icon for web links.

## [1.14.1]

### Added

- **About** shows how MDSHO was installed after the version: Microsoft Store or Sideload.

## [1.14.0]

### Added

- **Support MDSHO**: a link in About and in the panel menu if you'd like to support its development.

## [1.13.0]

### Added

- **Review prompt**: after two weeks of use, MDSHO asks once whether you'd like to rate it in the Microsoft Store (Store
  installs only, at most twice). On Store installs, Rate opens the Store's rating dialog inside MDSHO.

## [1.12.3]

### Added

- **Automatic backups**: MDSHO backs up your data every day it changes, when you exit MDSHO after a change, and always
  before a database upgrade, to an `mdsho-backups` folder in your user folder, or a folder you choose. See Settings >
  Backups.

### Changed

- **Settings** is tidier: one button style, even spacing and short hints under each setting.

### Docs

- **How-to pages** in the docs for common tasks.

## [1.11.0]

### Changed

- **Settings** has pages on the left: Appearance, General, Keyboard and Screens. It opens on the page you used last.

## [1.10.1]

### Added

- **Window theme** in Settings: Same as Windows, Light or Dark. It changes MDSHO's own windows, comments and menus,
  and you see it as you choose. Panels and notes keep their own colours.

## [1.9.0]

### Changed

- **Group values**: a group name can hold up to 9 values, such as `Prod {SRV01} {user=admin}`. Run commands in the group
  use them as `{=1}`, `{=2}` or by name as `{=user}`. The Run command window lists the group's values; click one to
  insert it.
- `{}` is replaced by `{=1}` and is no longer filled in. Old commands that use `{}` show an error until they are
  edited, and they don't run.
- Mistakes in group values show as errors in the Run command window, in the button's tooltip, and when you run the
  command.

## [1.8.0]

### Added

- **Getting started**: on a new install, the first panel has a group with a sample note, shortcut, run command and web
  link, and a welcome note opens. Delete the group when you are done.

## [1.7.1]

### Added

- **Places per screen setup**: MDSHO remembers where your panels, clones and open notes are for each set of connected
  screens. When you connect or disconnect a screen, they move back to where they were last time with those screens.
  With screens MDSHO hasn't seen before, they stay where Windows put them, and any that are off screen are moved onto
  one. Docked panels stay docked.
- **Screen setups** in the tray menu: lists the screen setups MDSHO knows, newest first, with a tick on the current one.
  **Put panels back for this setup** moves everything to its saved place, and **Forget the other setups** deletes the
  rest. MDSHO keeps the 10 most recently used setups.
- **Remember places for each screen setup** in Settings (on at first). Turn it off to stop the moves.

### Changed

- Database version 17: new tables for the screen setups and their places (backed up first). They stay on this PC and
  aren't exported.

## [1.6.1]

### Added

- **Bring panels to front**: press **Ctrl+Shift+Alt+M** anywhere to bring all panels to the front, with the keyboard
  focus on the first one. Press it again, or Esc, to send them back. Opening a button also sends them back.
- **Keyboard shortcut** in Settings: click the box and press a new shortcut, or use **Default** or **Off**. If another
  app already uses the shortcut, MDSHO says so.
- **Bring panels to front** as the first item in the tray menu.

### Changed

- Database version 16: a new table for app settings that stay on this PC (backed up first).

## [1.5.10] - 2026-11-14

### Added

- **Find other lost windows** in the tray menu: moves every window of another app whose title bar can't be reached on
  any screen (for example one that opened on a screen that is no longer connected) to the screen with the mouse
  pointer, keeping its size unless it is bigger than that screen, then says how many windows moved. Minimized windows,
  windows on other virtual desktops and MDSHO's own windows are left alone; windows that Windows doesn't let MDSHO move
  (for example apps running as administrator) are counted as skipped.

- **Comments** on buttons and groups: right-click → **Comment...** opens the Comment window to write a comment of
  several lines. OK in its title bar (left of X, greyed out while nothing changed) or Ctrl+Enter saves and closes;
  the X button or Esc close without saving. The title bar buttons' tooltips name their shortcuts. The Comment window is
  small (300 by 150 pixels) and opens beside its panel, 20 pixels away (on the right when there is room, otherwise on
  the left), with its title level with the button; with no room on either side it opens in the middle of the screen. The comment shows in the
  button's tooltip below the target or command line, cut after 10 lines, and a comment icon after the name (the same
  size as the other icons) shows which buttons have one. A comment of only spaces counts as none. The Comment window looks like the Note window.
- **Note buttons**, a third kind of button next to shortcuts and run commands: **New note** (right-click empty space or
  a group) creates one and opens the Note window. Opening a note (double-click, or single click when that is set) opens
  the Note window to read and edit its text (several notes can be open side by side; the text fills the window and scrolls sideways instead of wrapping, with each scroll bar shown only when it is needed); its tooltip shows the first 10 lines. Note windows remember their place, size and state: notes that were open come back
  where they were after MDSHO or Windows restarts (unsaved typing is saved at exit), a note opens again where it was last,
  and a place that is off every screen moves to the middle of the main screen. The Note window has pin (keep on top of
  other apps, off for new notes and remembered per note; a thumbtack pointing left, or down while on top) and close in its title bar and no bottom bar. A note saves
  itself half a second after you stop typing and when its window closes (X or Esc), so it needs no Save button.
  The colours button (brush) in its title bar opens **Note colours**: the Colours window with the five colours
  (border, title background, title text, background, text), the palette, **Default colours**, **Set as default** and Apply. It opens beside
  the note, 20 pixels away: on the right when there is room, otherwise on the left. The note's window
  follows the colours live; they are saved on Apply and kept with the note (exported, imported and copied with it). A note's right-click menu has **Edit
  note...**. Notes can be renamed, marked, moved, copied, exported and imported like other buttons, and need no shortcut file. They use the new note icon on
  the button, in the Note window and in the menu. The Note and Comment windows show just the button's name as their title.
- **Colours window for panels**: a new brush button in a panel's title bar (and its clones'), right of the Settings gear,
  opens **Window colours**: the five colours and the palette, which used to be in the Settings window, in the same window
  that notes use for their colours. The Settings window keeps Icons appearance and Other, and is smaller (380 pixels
  wide, as tall as its content; its title is "Settings: " and the panel's title). Both open 20 pixels beside the panel:
  on its right when there is room, otherwise on its left (they used to touch the panel's right edge).
- **Default colours** in the Colours window: puts the five colours back to the defaults (for a panel at first black
  border, see-through black background, white text). Apply saves the colours, and closing without Apply puts the old
  ones back.
- **Set as default** in the Colours window, next to Default colours: saves the colours on screen as the defaults, at
  once: from a panel's Colours window for panels, from a note's for notes. Saved defaults are what Default colours puts
  back and what new panels and new notes start with; existing panels and notes keep their colours. They stay on this
  PC (not in exports).
- **Wrap text** in the Note and Comment windows: a button in the title bar (right of the colours brush in the Note
  window, left of OK in the Comment window) wraps long lines at the window's edge instead of scrolling sideways. It is
  shaded while on and off at first. Each note remembers it with its window; the Comment windows opened from a panel
  share it, and it is kept with the panel (exported and imported with it).
- **Note buttons show and hide their note**: opening a note button again (click or double click, as the panel is set,
  or Enter) closes its Note window, saving the text. A minimized Note window is brought back instead. **Edit note...**
  always opens it.
- **Double and triple click** in the Note and Comment windows: a double click selects the word without the space after
  it, a triple click selects the whole paragraph (without its line break).

- **Rename a note in its Note window**: double-click the note's title (its tooltip says so) to edit it in place;
  Enter or clicking away saves, Esc keeps the old name. Dragging the title still moves the window, and a double click
  elsewhere on the title bar still maximises and restores. Rename in the right-click menu and F2 still rename the note
  on its button in the panel, like any other button.
- **Rename a run command in its Run command window**: the window's title is now the command's name (was "Run
  command"); double-click it to edit it in place, the same way as a note. Enter or clicking away keeps the new name,
  Esc the old one; **Save** writes it and **Cancel** throws it away with the other changes. Rename in the right-click
  menu and F2 still rename the command on its button in the panel. A run command's right-click menu no longer has "Set
  name the same as target" or "Copy target to memory", and its "Set run command..." is now **Edit run command...**.
- **Show version in title**, under Other in a panel's Settings (on at first, shared by the panel and its clones):
  shows the MDSHO version after the panel's title, for example "MDSHO v1.5.10". Closing Settings without Apply puts the
  setting back. Sideload test builds show their build number as a 4th part (for example "v1.5.10.7"); Store builds show
  three parts.
- Database version 13: the windows table gets `ShowVersionInTitle` (backed up first, on for every panel). Exports
  carry it; older exports import with it on.
- Database version 14: drops `ReserveDockedSpace` from the windows table (backed up first). Version 12 added it for
  "Reserve screen space when docked", which only test builds had and which was removed again: Windows lays out the
  desktop icons again every time a reserved strip changes. Exports no longer carry it; older exports that have it still
  import.
- Database version 15: a new `DefaultColors` table for the colours saved with **Set as default** (backed up first;
  empty at first).

### Changed

- **New group**, **New command** and **New note** in the right-click menus show the icon of the button they make (the
  group, run and note icons) instead of a plus. **New window** keeps the plus.
- The Run command window has no maximise button any more: double-click its title bar to maximise or restore it.
- The Note, Comment, Run command, Examples, Settings (and Note colours), About and Error windows have rounded corners
  on Windows 10 too, drawn the way the panels draw theirs, square while maximised. Their border now shows on Windows 11
  as well, so a note's border colour can be seen. Settings, About and Error have MDSHO's own title bar for it (icon,
  title and close). These windows have no Windows shadow any more, like the panels.
- The icon font is now MDSHOFont 1.17, with new comment, note, thumbtack, ok and wrap text icons and a redrawn globe (web links).
- The Comment window has no bottom bar any more: Save, Cancel and the hint line gave way to OK in the title bar.
- Notes save themselves: the Note window has no Save button any more. The text is saved half a second after you stop
  typing and when the window closes, so closing a note (X or Esc) no longer throws away what you typed.
- Panels have no bottom bar any more: the Settings button (gear) moved to the title bar, just left of Dock left, and
  the button list uses the freed 32 pixels. The unused PRO badge and its trial code are gone.
- Thin scroll bars in every window, as in Windows 11: a 3 pixel line that widens to 8 pixels when the mouse is over it,
  with no arrow buttons. They take a thin 12 pixel strip of their own (instead of 17), so the text never goes under
  them, and use the colour of the text (a panel's text colour, a note's text colour).
- The tray menu item **Find Windows** is now called **Find MDSHO lost windows**; it works as before.
- The Settings window has tighter, even margins: every outer margin of the content and of the Apply bar equals the
  Apply button height, and the window is 537 pixels high (was 560). The Run command window follows the same rule: every
  outer margin of its fields and of its bottom bar equals the button height, and the About and error windows use the
  same 26 px around their content. The About window opens tall enough to show every third-party notice without
  scrolling (never taller than the screen) and can be resized; a smaller window scrolls the notices.
- The Examples list of the Run command window has the same look as the Note window, shows what an example does and the
  command it runs when you hover it (instead of below the list), and opens centred on the Run command window at 80 % of
  its size.
- Menus, buttons, labels and window titles use sentence case, as in Windows 11: only the first word and names start
  with a capital letter ("New window", "Edit run command...", "Single click to open", "Run command"). New groups and
  run commands are named "New group" and "New command"; existing buttons keep their names.
- Database version 6: the buttons' unused `Notes` column is renamed to `Comment`, for the new button comments. The
  database is backed up first, as with every upgrade. Exports now call the field `comment`; exports from older versions
  still import. The windows' `Notes` column is unchanged.
- Database version 7: the buttons table gets `IsNote` and `NoteText` for note buttons (backed up first). Exports carry
  them; exports from older versions still import, as normal buttons.
- Database version 8: a new `NoteWindows` table keeps the place, size and state of each note window and whether it was
  open (backed up first). Database version 9 adds whether a note window stays on top (`IsTopmost`).
- Database version 10: the buttons table gets the five colours of a note's window (empty = the default colours); exports
  carry them, and older exports import with the default colours.

### Fixed

- Renaming a button now updates the title of its open Note or Comment window straight away (it used to show the old
  name until the window was closed and opened again).

## [1.1.5]

First public version of the source code, published under GPLv3 with a commercial license available.
This is the baseline for all future changes; earlier history is not recorded here.
