# Changelog

All notable changes to MDSHO are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).
The MSIX package version uses four parts (`Major.Minor.Patch.0`); the fourth part is reserved by the Microsoft Store.

## [1.7.1]

### Added

- **Places per screen setup**: MDSHO now remembers where every panel, clone and open note window was for each screen
  setup, that is each set of connected screens. A setup is identified by the screens' resolutions, which one is
  primary, and how they are arranged (which screen is left of, right of, above or below which), not by the names Windows
  gives the screens, which change when screens are reconnected, and not by exact offsets, so dragging a monitor a little
  in Display settings is still the same setup (two identical monitors swapped count as the same setup too). When you undock a
  laptop, Windows piles all windows onto the laptop screen, and before this version docking again didn't bring them
  back. Now, when screens are connected or disconnected or a resolution changes, MDSHO waits a moment (1.5 seconds, so
  several changes in a row count as one) and then:
  - with screens it has seen before, moves every panel, clone and open note window back to where it was last time with
    those screens. Docked panels are docked again on their screen (with the full height of its working area, also when
    the taskbar changed). A panel, clone or note opened since then has no place for that setup: it stays where it is,
    and is only pulled onto a screen if it is off every screen;
  - with screens it hasn't seen before, leaves things where Windows put them, pulls anything that is off every screen
    back onto a screen, and remembers the places for those screens from then on.

  Places are saved at the same moments as before: when you stop moving or resizing a panel, dock or centre it, find lost
  windows, create a panel or a clone, and shortly after a note window moves. Nothing is saved while the screens are
  changing or while MDSHO moves the windows, so the moves never overwrite the places of another setup. When MDSHO starts
  with screens it knows, the panels, clones and open notes go to their places for those screens. The places stay on this
  PC: they are not part of exports, and an import that replaces the data keeps them.
- **Screen setups** submenu in the tray menu, right after Bring panels to front. It lists the screen setups MDSHO knows,
  most recently used first, for example "2 screens: 1920×1080, 2560×1440" (the primary screen first, then the others
  from left to right), with a check mark on the current one. Only two different setups with the same name get a number,
  for example a monitor on the right and then on the left of the laptop: "2 screens: 1920×1080, 2560×1440 (2)" is the
  one seen later. MDSHO keeps the 10 most recently used setups: an 11th forgets the least recently used one with its
  places, and never the current one. The list is information only. Below it, **Put panels back for this setup** moves everything to its saved place for the current
  screens, and **Forget the other setups** deletes every setup but the current one (greyed out when there is no other).
- **Screen setups** section in the Settings window, below Keyboard shortcut: **Remember places for each screen setup**,
  on at first, with the hint "For all panels and notes, on this PC. When screens are connected or disconnected, they move
  to where they were last time with those screens." It is one setting for the whole app on this PC, takes effect on
  Apply, and closing without Apply changes nothing. When it is off, MDSHO behaves exactly as before: nothing moves when
  the screens change, and the two actions of the tray submenu are greyed out (the list still shows). Places keep being
  saved while it is off, so turning it on again starts from where the windows are now, not from where they were when it
  was turned off.

### Changed

- The database is now at version 17: the new `ScreenSetups` and `ScreenSetupPlaces` tables hold the screen setups and
  the places of the panels, clones and note windows in each. As with every upgrade, the database is backed up first.
  Places of panels, clones and notes that were deleted are cleaned up when MDSHO starts.
- For the 1.7.0 test build only: its database gets the new last-used time of each setup at the next start, and the
  setups it saved several times for the same screens (because Windows had renumbered them, or a monitor had moved a
  little) are merged into one, keeping the places of the most recently used one (the last one added, since 1.7.0 didn't
  record when a setup was used) and taking places it lacks from the others.

## [1.6.1]

### Added

- **Bring panels to front**, a keyboard shortcut for all panels: press **Ctrl+Shift+Alt+M** anywhere in Windows and
  every visible panel and clone comes to the front above the other windows (a minimized one is restored), with the
  keyboard focus on the first panel's selected button (or its first button), so the arrow keys and Enter pick a button.
  Press the shortcut again, or Esc in a panel, and the panels go back exactly where they were behind the other windows
  and the window you were using gets the focus back. One press always does what the screen suggests: when the panels
  are already in front without the shortcut (you clicked a panel, or no app window covers any panel), the first press
  sends them back behind the app windows below them, and the window right under the top panel gets the focus; with no
  app window below, they stay where they are. Esc sends panels back only after the shortcut or the tray menu item
  brought them up. Opening a button (a shortcut, a run command or a note) from the
  panels also sends them back, leaving the focus to what it opened. Switching to another app ends it without moving
  anything, so the next press brings the panels up again. Esc still cancels typing a name, a title or a command line
  first. Ctrl+Shift+Alt+M was chosen because it clashes with no app shortcut, menu key or AltGr character.
- **Keyboard shortcut** section in the Settings window, below Other: **Bring panels to front** with a shortcut box
  showing the current shortcut, for example "Ctrl + Shift + Alt + M". Click the box and press the new shortcut; it needs
  Ctrl, Alt or Win with a letter, digit, F1 to F24, a number pad digit, an arrow or an editing key (otherwise the hint
  says "Add Ctrl, Alt or Win to the key." in red), and Esc cancels. **Default** puts back Ctrl+Shift+Alt+M and **Off**
  turns the shortcut off. The change takes effect on Apply; closing the window without Apply changes nothing. If
  another app already uses the shortcut, the hint says so in red, the window stays open and the old shortcut keeps
  working. The shortcut is one setting for the whole app, kept on this PC: it is the same in every panel's Settings and
  is not part of exports, and an import keeps it. If the saved shortcut can't be registered when MDSHO starts (another
  app took it), MDSHO starts normally without a message and the Settings window shows the red hint.
- **Bring panels to front** as the first item of the tray menu, followed by a separator, with the current shortcut at
  the right (nothing when it is off or doesn't work). It works also when the shortcut is off.

### Changed

- The database is now at version 16: a new `AppSettings` table holds settings of the whole app on this PC, starting
  with the Bring panels to front shortcut. As with every upgrade, the database is backed up first.

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
