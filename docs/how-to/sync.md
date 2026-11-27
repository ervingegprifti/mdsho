---
title: Sync your PCs
permalink: /how-to/sync/
---

[MDSHO](../index.md) › How-to

# Sync your PCs

This page helps you keep the same panels, buttons and notes on your home PC, your work PC and your laptop.

MDSHO syncs through a folder that all your PCs can reach, for example a Google Drive or OneDrive for desktop folder,
Dropbox or a network drive. Nothing goes to any other server.

## What syncs

- Panels, groups, buttons, notes, comments and snippets, with their text and colours.
- Their order, moves between panels and groups, and deletes.

Window places and each PC's settings (backups, keyboard shortcut, screen setups) stay on that PC.

## Turn it on

1. Open a panel's **Settings** and choose **Sync**.
2. Click **...** and pick the folder, for example `G:\My Drive\MDSHO Sync`. Pick the same folder on each PC.
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
- **Work PC has a newer MDSHO.** Update this PC to sync with it. The other PCs keep syncing.

The table below it lists your other PCs, when they last synced and their MDSHO version.

## Turn it off

Clear **Keep this PC in sync with my other PCs** and click **Apply**. This PC keeps everything as it is and stops
syncing. The folder is not touched.

## The folder

The folder holds one subfolder per PC, named by an ID; only that PC writes in it. Don't edit the files. Old files that
nothing uses any more are deleted after 7 days.
