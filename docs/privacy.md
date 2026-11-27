---
title: Privacy Policy
permalink: /privacy/
---

# MDSHO Privacy Policy

Last updated: 27 November 2026

This page explains what the MDSHO app (Multi Desktop Shortcut Organizer) does with your data. It covers the MDSHO app only.

## In short

MDSHO does not collect any personal data. It has no analytics, no ads and no tracking. Everything you create in MDSHO
stays on your PC, unless you turn on sync: then it goes only where you choose (a folder, or your own Google Drive or
OneDrive), and only to keep your own PCs the same. MDSHO connects to Google or Microsoft only when you choose Google
Drive or OneDrive for sync.

## What stays on your PC

MDSHO keeps your panels (windows), their clones and positions, buttons, groups, run commands, colors and settings in a
small database file (including, for each set of screens you connect, the screens' names and resolutions and where your
panels and notes were), together with the shortcut files it makes for your buttons. All of this is kept in MDSHO's app data
folder on your PC (`%LOCALAPPDATA%\MDSHO`, or the private folder Windows gives the installed package). You can open that
folder with **Browse app data** in the tray menu or in a panel's title menu.

The database also holds a random ID that MDSHO makes for your PC, for sync. Each panel and button records which PC
changed it last by that ID, so exports and backups contain it. It says nothing about you or your PC.

Without sync, none of this data leaves your PC. Nobody, including the author of MDSHO, can see it.

## Sync

Sync is off until you turn it on in Settings > Sync. It keeps your panels, buttons, notes, comments and snippets (with
their text and colours) the same on your PCs, together with the name you give each PC and its MDSHO version. Window
places and your settings stay on each PC. You choose where the sync files go:

- **Folder**: a folder you pick. MDSHO writes the files there and sends them nowhere itself; if the folder belongs to a
  cloud drive app (for example Google Drive or OneDrive for desktop, or Dropbox), that app copies them under its own
  privacy policy.
- **Google Drive**: after you click **Connect...** and allow it in your browser, MDSHO reads and writes its sync files in a
  folder **MDSHO Sync** in your Google Drive over Google's API. It asks only for the files it makes (`drive.file`), so it
  can't see your other files. It reads your account's e-mail address only to show which account is connected.
  [Google's Privacy Policy](https://policies.google.com/privacy) covers what Google does.
- **OneDrive**: after you click **Connect...** and allow it, MDSHO reads and writes its sync files in its own app folder
  (Apps\MDSHO) in your OneDrive over Microsoft Graph. It can't see your other files. It reads your account name only to
  show which account is connected. The [Microsoft Privacy Statement](https://privacy.microsoft.com/privacystatement)
  covers what Microsoft does.

The sign-in for Google Drive or OneDrive stays on your PC, protected by Windows for your Windows account; it is never
put in the sync files, exports or backups, and is never sent to the author of MDSHO. **Disconnect** signs your PC out and
forgets the sign-in (for Google it also asks Google to cancel it). The sync files go only to the place you chose, and
nobody else, including the author of MDSHO, gets them.

## Log files

MDSHO writes log files to the `logs` folder in its app data folder on your PC. They record when MDSHO starts and
exits, database upgrades, exports and imports, and errors, so problems can be found and fixed. Each file is at most
1 MB, and files older than 14 days are deleted automatically. Log files are never sent anywhere. For run commands, a log
file holds at most the program name (for example when it could not be started), never the arguments or the working
folder. You can open the folder with **Open logs folder** in the error window, or through **Browse app data**, and
you can delete the files at any time. If you report a problem, you decide whether to share a log file or the text from
**Copy details**.

## Export files

When you choose **Export data...**, MDSHO writes a `.zip` file with your panels, buttons and shortcut files to the place
you pick. Export files are made only when you ask, and MDSHO does not send them anywhere. Before an import that replaces
your data, MDSHO saves a backup of your current data in its app data folder.

## Uninstalling

Uninstalling MDSHO may delete its app data, because Windows removes the package's private folder with the app. If you
want to keep your panels and buttons, export them first.

## Links and network use

Apart from sync through Google Drive or OneDrive (see [Sync](#sync)), MDSHO only reaches the internet when you click a
link:

- **Rate** and **Share** open the MDSHO page in the Microsoft Store (Share also copies the Store link to your clipboard
  and opens it in your browser).
- The links in the **About** window open web pages in your browser.
- Your own web-link buttons open the addresses you added, in your browser.

Those websites have their own privacy policies; MDSHO does not control them and does not send them any data.

## Run commands

A run command runs exactly the program and arguments you set, with your own Windows permissions. It runs as
administrator only if you ticked **Run as administrator** and accepted the Windows prompt. MDSHO does not look at or
send the output of your commands.

## Microsoft Store and Windows

If you install MDSHO from the Microsoft Store, Microsoft and Windows may collect data (for example about downloads,
installs and crashes) under the
[Microsoft Privacy Statement](https://privacy.microsoft.com/privacystatement). That collection is done by Microsoft, not
by MDSHO. Microsoft may show the author summary statistics about the app, such as download, rating and crash counts,
which do not identify you.

## Children

MDSHO does not collect data from anyone, including children.

## Changes to this policy

If this policy changes, the new version will be posted on this page with a new "Last updated" date.

## Contact

Questions about this policy go to
[GitHub Issues](https://github.com/ervingegprifti/mdsho/issues). Please don't post personal data there; issues are
public.

See also: [Terms and Conditions](terms.md).
