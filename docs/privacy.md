---
title: Privacy Policy
permalink: /privacy/
---

# MDSHO Privacy Policy

Last updated: 27 November 2026

This page explains what the MDSHO app (Multi Desktop Shortcut Organizer) does with your data. It covers the MDSHO app only.

## In short

MDSHO does not collect any personal data. It has no analytics, no ads and no tracking, and the author of MDSHO never
receives your data. Everything you create in MDSHO stays on your PC, unless you turn on **Sync**: then MDSHO copies your
panels and buttons to the place you choose (a folder, your Google Drive or your OneDrive), so your other PCs can read
them. Nothing is sent anywhere else.

## What stays on your PC

MDSHO keeps your panels (windows), their clones and positions, buttons, groups, run commands, colors and settings in a
small database file (including, for each set of screens you connect, the screens' names and resolutions and where your
panels and notes were), together with the shortcut files it makes for your buttons. All of this is kept in MDSHO's app data
folder on your PC (`%LOCALAPPDATA%\MDSHO`, or the private folder Windows gives the installed package). You can open that
folder with **Browse app data** in the tray menu or in a panel's title menu.

The database also holds a random ID that MDSHO makes for your PC, and a name for your PC that you can change in
**Settings** › **Sync**. Each panel and button records which PC changed it last by that ID, so exports, backups and sync
files contain it. The ID says nothing about you or your PC.

Unless you turn on Sync, none of this data leaves your PC. Nobody, including the author of MDSHO, can see it.

## Sync

Sync is off until you turn it on in **Settings** › **Sync**. It keeps your panels the same on all your PCs.

**What is copied:** your panels, groups and buttons with their names, targets, arguments, colours and order; your notes,
comments and snippets with their text and highlights; the shortcut files behind your buttons; and each PC's ID, name and
MDSHO version. Paths in your user folder are stored without your Windows user name (as `%USERPROFILE%`).

**What is not copied:** where your windows are on screen, panel clones, your settings, text history, log files and
backups. They stay on each PC.

**Where it goes:** only to the place you choose, and all your PCs in one sync use the same kind:

- **A folder.** MDSHO writes files to that folder and reads your other PCs' files from it. If the folder belongs to a
  cloud app (for example Google Drive, OneDrive or Dropbox for desktop), that app uploads the files to your account
  under its own privacy policy. MDSHO itself makes no network connection.
- **Google Drive** (version 2.1 and later). See [Google Drive](#google-drive) below.
- **OneDrive** (version 2.1 and later). See [OneDrive](#onedrive) below.

Before the first sync, MDSHO saves a backup of your data in its app data folder. Turning Sync off stops syncing; your
data stays on your PC and the files in the sync location stay there until you delete them.

### Google Drive

MDSHO connects to Google only when you choose **Google Drive** and click **Connect**, which opens Google's own sign-in
page in your browser. MDSHO never sees your Google password.

- **What MDSHO can access:** only the files and folders MDSHO itself creates in your Google Drive (the `drive.file`
  permission). It cannot see, open or change any other file in your Drive.
- **What it does with them:** it writes your sync files (described above) to an "MDSHO Sync" folder in your Drive and
  reads them back on your other PCs. It uses this data only to sync your MDSHO data between your PCs.
- **What it keeps:** a sign-in token from Google, stored on your PC and protected by your Windows account. It is never
  put in the sync files and never sent to the author. **Disconnect** deletes it and asks Google to cancel it. You can also
  remove MDSHO's access at any time in your Google Account, under **Security** › **Third-party apps and services**.
- **Not shared or sold:** MDSHO does not use Google data for ads, does not sell it, does not share it with anyone, and no
  person reads it. The only copies are on your PCs and in your own Google Drive.

MDSHO's use and transfer to any other app of information received from Google APIs will adhere to the
[Google API Services User Data Policy](https://developers.google.com/terms/api-services-user-data-policy), including the
Limited Use requirements.

### OneDrive

MDSHO connects to Microsoft only when you choose **OneDrive** and click **Connect**, which opens Microsoft's own sign-in
page in your browser. MDSHO never sees your Microsoft password.

- **What MDSHO can access:** only its own app folder in your OneDrive, `Apps\MDSHO` (the `Files.ReadWrite.AppFolder`
  permission). It cannot see, open or change any other file in your OneDrive.
- **What it does with them:** it writes your sync files there and reads them back on your other PCs, only to sync your
  MDSHO data.
- **What it keeps:** a sign-in token from Microsoft, stored on your PC and protected by your Windows account. It is never
  put in the sync files and never sent to the author. **Disconnect** deletes it. You can also remove MDSHO's access at any
  time at [account.live.com/consent/Manage](https://account.live.com/consent/Manage).
- **Not shared or sold:** MDSHO does not use this data for ads, does not sell it, does not share it with anyone, and no
  person reads it.

Google and Microsoft store your files under their own privacy policies:
[Google Privacy Policy](https://policies.google.com/privacy) and
[Microsoft Privacy Statement](https://privacy.microsoft.com/privacystatement).

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

MDSHO only reaches the internet when you click a link, or when you sync through Google Drive or OneDrive:

- **Sync** through **Google Drive** or **OneDrive** connects to Google or Microsoft, as described in [Sync](#sync).
  Sync through a folder makes no connection of its own.
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
