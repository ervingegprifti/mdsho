# Changelog

All notable changes to MDSHO are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).
The MSIX package version uses four parts (`Major.Minor.Patch.0`); the fourth part is reserved by the Microsoft Store.

## [1.5.3]

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
  The colours button (brush) in its title bar opens **Note colours**: the Settings window with only the five colours
  (border, title background, title text, background, text), the palette, **Default colours** and Apply. It opens beside
  the note, 20 pixels away: on the right when there is room, otherwise on the left. The note's window
  follows the colours live; they are saved on Apply and kept with the note (exported, imported and copied with it). A note's right-click menu has **Edit
  note...** and **Copy note text** (puts the whole note on the clipboard). Notes can be renamed, marked,
  moved, copied, exported and imported like other buttons, and need no shortcut file. They use the new note icon on
  the button, in the Note window and in the menu. The Note and Comment windows show just the button's name as their title.
- **Default colours** in a panel's Settings window: puts the five colours back to those of a new window (black border,
  see-through black background, white text). Icons appearance and Single click to open stay as they are; Apply saves
  the colours, and closing without Apply puts the old ones back.
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
  Enter or clicking away saves, Esc keeps the old name. F2 on a note in its panel opens the Note window ready to rename.
  Dragging the title still moves the window, and a double click elsewhere on the title bar still maximises and restores.
  Notes have no Rename in their right-click menu any more.
- **Show version in title**, under Other in a panel's Settings (on at first, shared by the panel and its clones):
  shows the MDSHO version after the panel's title, for example "MDSHO v1.5.3". Closing Settings without Apply puts the
  setting back. Sideload test builds show their build number as a 4th part (for example "v1.5.3.7"); Store builds show
  three parts.
- Database version 13: the windows table gets `ShowVersionInTitle` (backed up first, on for every panel). Exports
  carry it; older exports import with it on.
- Database version 14: drops `ReserveDockedSpace` from the windows table (backed up first). Version 12 added it for
  "Reserve screen space when docked", which only test builds had and which was removed again: Windows lays out the
  desktop icons again every time a reserved strip changes. Exports no longer carry it; older exports that have it still
  import.

### Changed

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

### Fixed

- Renaming a button now updates the title of its open Note or Comment window straight away (it used to show the old
  name until the window was closed and opened again).

## [1.1.5]

First public version of the source code, published under GPLv3 with a commercial license available.
This is the baseline for all future changes; earlier history is not recorded here.
