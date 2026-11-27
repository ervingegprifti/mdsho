---
title: Sync your PCs
permalink: /how-to/sync/
---

[MDSHO](../index.md) › How-to

# Sync your PCs

This page helps you keep the same panels, buttons and notes on your home PC, your work PC and your laptop.

MDSHO syncs through one of these, the same on every PC:

- **Folder**: a folder all your PCs reach, for example a Google Drive or OneDrive for desktop folder, Dropbox or a
  network drive. Nothing goes to any other server.
- **Google Drive** (from 2.1.0): MDSHO talks to Google Drive itself, no Google app needed.
- **OneDrive** (from 2.1.0): MDSHO talks to OneDrive itself with a personal Microsoft account.

A sync is one kind everywhere. If a PC set to OneDrive finds a place used by a folder sync (or the other way round), it
says so and reads and changes nothing there.

## What syncs

- Panels, groups, buttons, notes, comments and snippets, with their text and colours.
- Their order, moves between panels and groups, and deletes.

Window places, the default colours and all of each PC's settings (window theme, keyboard shortcut, screen setups,
backups, text history) stay on that PC.

## Turn it on

1. Open a panel's **Settings** and choose **Sync**.
2. Under **Sync through**, choose **Folder**, **Google Drive** or **OneDrive**.
   - Folder: click **...** and pick the folder, for example `G:\My Drive\MDSHO Sync`. Pick the same folder on each PC.
   - Google Drive or OneDrive: click **Connect...**, sign in in your browser and allow MDSHO. The page then says you can
     close the tab. Settings shows **Connected:** and the account. Use the same account on each PC.
3. Tick **Keep this PC in sync with my other PCs**.
4. Check **This PC's name**. Your other PCs show it, and so does Text history.
5. Click **Apply**.

MDSHO makes a backup of this PC first, in your backup folder (`MDSHO-backup-<PC>-before-sync-....zip`), and says so
once on the Sync page. **Import data** restores it.

Then this PC's panels go to the folder and the other PCs' panels come in. Nothing is matched by name: if you made the
same panel on two PCs, it shows twice. Delete the double once; the delete syncs.

## When it syncs

- When MDSHO starts.
- About 10 seconds after you change something.
- Every minute, to pick up your other PCs' changes.
- When MDSHO closes. If the folder doesn't answer within about 5 seconds (a cloud drive offline, a network drive
  asleep), MDSHO closes anyway and your changes go the next time it starts.
- When you click **Sync now**.

Changes from your other PCs show at once in open panels and in open notes, comments and snippets. A panel that comes
from another PC opens in the middle of your main screen; move it where you want it, and it stays there.

## When two PCs changed the same thing

The newer change wins. If both PCs changed the same text (a note, a snippet or a comment) while apart, the newer text
wins and the other one goes into **Text history**, marked with the PC it came from.

An open note, comment or snippet shows the other PC's text at once, as long as you typed nothing since it was last
saved; the text it had goes into Text history. While you are typing in it, it is never changed under you: the change
from the other PC waits until your typing is saved, then the rules above decide.

If a PC deleted a group while another PC added a button to it, the button goes to the top of its panel. If a PC
deleted a panel while another PC added a button to it, the panel comes back.

## The status line

- **Up to date.** with the time of the last sync.
- **Syncing...**
- **Can't reach the folder.** The drive isn't there, for example Google Drive isn't running. Your changes are kept and
  sync later; MDSHO tries again every minute.
- **Can't reach Google Drive.** (or OneDrive) No connection, or this PC was signed out: click **Connect...** again.
  Your changes are kept and sync later.
- **This sync folder is used by a OneDrive sync.** The place is used by another kind of sync. Choose the same
  **Sync through** on every PC.
- **Work PC has a newer MDSHO.** Update this PC to sync with it. The other PCs keep syncing.

The table below it lists your other PCs, when they last synced and their MDSHO version.

## Turn it off

Clear **Keep this PC in sync with my other PCs** and click **Apply**. This PC keeps everything as it is and stops
syncing. The folder is not touched.

## What Google and Microsoft can see

- **Google Drive**: MDSHO asks only for the files it makes itself (the `drive.file` permission). It makes a folder
  **MDSHO Sync** in your My Drive, with one subfolder per PC, and can't see or change your other files. Google stores the
  sync files like any file in your Drive: your panels, buttons, notes, comments and snippets.
- **OneDrive**: MDSHO asks only for its own app folder (**Apps\MDSHO** in your OneDrive) and to stay signed in. It can't
  see your other files. Microsoft stores the sync files there.

MDSHO keeps the sign-in on this PC only, protected by Windows for your Windows account; it never goes into the sync
files. **Disconnect** signs this PC out and forgets the sign-in; for Google it also tells Google to cancel it. To take
MDSHO's access away from a Microsoft account completely, remove MDSHO in your Microsoft account's app permissions.

## The folder

The folder (or the MDSHO Sync folder in Google Drive, or the app folder in OneDrive) holds `sync.json`, which says which
kind of sync uses it, and one subfolder per PC, named by an ID; only that PC writes in it. Don't edit the files. Old files that
nothing uses any more are deleted after 7 days.
