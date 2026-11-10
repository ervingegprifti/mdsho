---
title: Privacy Policy
permalink: /privacy/
---

# MDSHO Privacy Policy

Last updated: 9 November 2026

This page explains what the MDSHO app (Multi Desktop Shortcut Organizer) does with your data. It covers the MDSHO app only.

## In short

MDSHO does not collect, store elsewhere or send any personal data. It has no analytics, no ads and no tracking, and it
makes no network connections of its own. Everything you create in MDSHO stays on your PC.

## What stays on your PC

MDSHO keeps your panels (windows), their clones and positions, buttons, groups, run commands, colors and settings in a
small database file, together with the shortcut files it makes for your buttons. All of this is kept in MDSHO's app data
folder on your PC (`%LOCALAPPDATA%\MDSHO`, or the private folder Windows gives the installed package). You can open that
folder with **Browse app data** in the tray menu or in a panel's title menu.

None of this data leaves your PC. Nobody, including the author of MDSHO, can see it.

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

MDSHO only reaches the internet when you click a link:

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
