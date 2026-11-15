---
title: MDSHO
permalink: /
---

# MDSHO

**MDSHO** (Multi Desktop Shortcut Organizer) gives you custom button panels on your Windows desktop. Each panel is a small
window with your own buttons, and you can have as many panels as you like, on as many screens as you have.

## What it does

- **Shortcuts:** drop files and programs on a panel to get a button for each.
- **Folders:** buttons that open a folder in File Explorer.
- **Web links:** buttons that open a web address in your browser.
- **Run commands:** buttons that run a program with arguments, for example a `ping` or a PowerShell line, optionally in a
  working folder or as administrator.
- **Notes:** buttons that hold a note of their own. The note opens in its own window, which you can keep on top,
  colour and wrap, and it saves itself as you type. Opening the button again closes it.
- **Comments:** add a comment to any button or group; it shows in the button's tooltip.
- **Groups:** fold buttons into groups, and put values in a group's name, like `Prod {SRV01} {user=admin}`, to reuse them in the commands inside it as `{=1}` or `{=user}`.
- **Clones:** show the same panel on another screen; a change in one shows in all of its clones.
- **Docking:** dock a panel to the left or right edge of a screen.
- **Screen setups:** MDSHO remembers where your panels and notes were for each set of connected screens. Undock your
  laptop and dock it again, and they go back where they were with those screens. The tray menu's **Screen setups** lists
  the setups MDSHO knows and can put the panels back; turn it off in a panel's Settings on the **Screens** page.
- **Bring panels to front:** press **Ctrl+Shift+Alt+M** (or use the first item of the tray menu) to bring every panel
  above your other windows, then pick a button with the arrow keys and Enter. Press it again, or Esc, to send them back.
  Change or turn off the shortcut in a panel's Settings on the **Keyboard** page.
- **Window theme:** MDSHO's own windows, comments and menus follow Windows' light or dark mode. Choose Light or Dark
  instead in a panel's Settings on the **Appearance** page. Panels and notes keep their own colours.
- **Colors:** set the colors of each panel and note in its Colours window, save your own defaults, and mark buttons with a color.
- **Export and import:** save all your panels and buttons to one `.zip` file and load them back.
- **Automatic backups:** every day your data changes, when you exit after a change, and always before a database
  upgrade, MDSHO saves the same
  `.zip` to an `mdsho-backups` folder in your user folder (`C:\Users\<you>\mdsho-backups`). Pick another folder, such as a OneDrive or Google Drive folder, in a panel's
  Settings on the **Backups** page. Restore a backup with **Import data**.

## Install

Install MDSHO from the
[Microsoft Store](https://www.microsoft.com/store/productId/9P8DNCD0PPG8).
It needs Windows 10 version 1809 or later, or Windows 11, on a 64-bit (x64) PC.

The first time you start MDSHO, its panel has a **Getting started** group with a sample of each kind of button and a
welcome note. Delete the group when you are done.

You can also build it yourself from the source code; see [Building MDSHO](building.md).

## Documentation

- [Privacy Policy](privacy.md)
- [Terms and Conditions](terms.md)
- [Architecture](architecture.md): how the code is organised and where your data is kept.
- [Database](database.md): the tables, columns and upgrade history of the MDSHO database.
- [Building MDSHO](building.md): build, test and make your own signed install package.

## Licence

MDSHO is open source under the
[GNU General Public License v3.0](https://github.com/ervingegprifti/mdsho/blob/main/LICENSE): you can read, change and
share the code, as long as what you share stays open source under the same licence.
A [commercial licence](https://github.com/ervingegprifti/mdsho/blob/main/COMMERCIAL-LICENSE.md) is available for uses the
GPLv3 does not allow, such as closed-source redistribution.
The name "MDSHO" and its logo are not covered by these licences; see
[TRADEMARKS.md](https://github.com/ervingegprifti/mdsho/blob/main/TRADEMARKS.md).

## Source code

The source code, issue tracker and releases are on GitHub:
[github.com/ervingegprifti/mdsho](https://github.com/ervingegprifti/mdsho).
Bug reports and questions go to [GitHub Issues](https://github.com/ervingegprifti/mdsho/issues).
