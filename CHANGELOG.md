# Changelog

All notable changes to MDSHO are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).
The MSIX package version uses four parts (`Major.Minor.Patch.0`); the fourth part is reserved by the Microsoft Store.

## [1.1.6]

### Added

- **Find other lost windows** in the tray menu: moves every window of another app whose title bar can't be reached on
  any screen (for example one that opened on a screen that is no longer connected) to the screen with the mouse
  pointer, keeping its size unless it is bigger than that screen, then says how many windows moved. Minimized windows,
  windows on other virtual desktops and MDSHO's own windows are left alone; windows that Windows doesn't let MDSHO move
  (for example apps running as administrator) are counted as skipped.

### Changed

- The tray menu item **Find Windows** is now called **Find MDSHO lost windows**; it works as before.
- Menus, buttons, labels and window titles use sentence case, as in Windows 11: only the first word and names start
  with a capital letter ("New window", "Set run command...", "Single click to open", "Run command"). New groups and
  run commands are named "New group" and "New command"; existing buttons keep their names.

## [1.1.5]

First public version of the source code, published under GPLv3 with a commercial license available.
This is the baseline for all future changes; earlier history is not recorded here.
