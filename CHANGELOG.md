# Changelog

All notable changes to MDSHO are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).
The MSIX package version uses four parts (`Major.Minor.Patch.0`); the fourth part is reserved by the Microsoft Store.

## [1.2.4]

### Added

- **Find other lost windows** in the tray menu: moves every window of another app whose title bar can't be reached on
  any screen (for example one that opened on a screen that is no longer connected) to the screen with the mouse
  pointer, keeping its size unless it is bigger than that screen, then says how many windows moved. Minimized windows,
  windows on other virtual desktops and MDSHO's own windows are left alone; windows that Windows doesn't let MDSHO move
  (for example apps running as administrator) are counted as skipped.

- **Comments** on buttons and groups: right-click → **Comment...** opens the Comment window to write a comment of
  several lines (Save or Ctrl+Enter saves; Cancel, the X button or Esc close without saving). The comment shows in the
  button's tooltip below the target or command line, cut after 10 lines, and a small "i" marker after the name shows
  which buttons have one. A comment of only spaces counts as none. The Comment window looks like the Note window.
- **Note buttons**, a third kind of button next to shortcuts and run commands: **New note** (right-click empty space or
  a group) creates one and opens the Note window. Opening a note (double-click, or single click when that is set) opens
  the Note window to read and edit its text (several notes can be open side by side; the text fills the window and scrolls sideways instead of wrapping, with each scroll bar shown only when it is needed); its tooltip shows the first 10 lines. Note windows remember their place, size and state: notes that were open come back
  where they were after MDSHO or Windows restarts (unsaved typing is saved at exit), a note opens again where it was last,
  and a place that is off every screen moves to the middle of the main screen. The Note window has pin (keep on top of
  other apps, off for new notes and remembered per note), save and close in its title bar and no bottom bar: Save (also
  Ctrl+Enter or Ctrl+S) is greyed out while there is nothing to save and keeps the note open; X or Esc close without saving.
  The colours button (brush) in its title bar opens **Note colours**: the Settings window with only the five colours
  (border, title background, title text, background, text), the palette, **Default colours** and Apply. It opens beside
  the note, 20 pixels away: on the right when there is room, otherwise on the left. The note's window
  follows the colours live; they are saved on Apply and kept with the note (exported, imported and copied with it). A note's right-click menu has **Edit
  note...** and **Copy note text** (puts the whole note on the clipboard). Notes can be renamed, marked,
  moved, copied, exported and imported like other buttons, and need no shortcut file. They use the text-box glyph
  until the icon font gets a dedicated note glyph.

### Changed

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
  with a capital letter ("New window", "Set run command...", "Single click to open", "Run command"). New groups and
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

## [1.1.5]

First public version of the source code, published under GPLv3 with a commercial license available.
This is the baseline for all future changes; earlier history is not recorded here.
