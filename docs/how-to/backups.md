---
title: Back up, restore and move to another PC
permalink: /how-to/backups/
---

[MDSHO](../index.md) › How-to

# Back up, restore and move to another PC

This page helps you find your backups, keep a copy in the cloud, restore a backup and move MDSHO to another PC.

## Where your backups are

MDSHO backs up your panels, buttons and notes on its own, to `C:\Users\<you>\mdsho-backups`. It makes a backup:

- once a day, when something changed;
- when you exit MDSHO after a change;
- always before a database upgrade.

It keeps the latest 10 backups of each PC and deletes older ones. Backups made before a database upgrade are never
deleted. Each file name has the PC's name and the date, for example `MDSHO-backup-<PC>-2026-11-15-091200.zip`.

To see them, open a panel's **Settings**, choose **Backups** and click **Open**.

## Change the backup settings

1. Open a panel's **Settings** and choose **Backups**.
2. Tick or clear **Back up my data every day**.
3. Choose how many **latest backups of this PC to keep**: 3, 5, 10, 20 or 30.
4. Click **Apply**.

**Last backup** shows when the last one was made. If a backup failed, the reason shows in red below it.

## Keep a copy in the cloud

1. Open a panel's **Settings** and choose **Backups**.
2. Click **...** (Change folder) and pick a OneDrive or Google Drive folder, for example `G:\My Drive\mdsho-backups`.
3. Click **Back up now** to check that it works.
4. Click **Apply**.

Several PCs can share the same folder. Each PC deletes only its own old backups.

## Restore a backup

1. Right-click the MDSHO icon in the taskbar's notification area, or a panel's title, and choose **Import data...**.
   It opens in the backup folder.
2. Pick a file. MDSHO says when it was made, which PC it is from (for a backup), and how many panels, clones and
   buttons it has.
3. Choose **Yes** to replace all your current panels and buttons with the file. MDSHO saves a backup of your current
   data first.
4. Or choose **No** to keep your data and add the file's panels next to it.
5. MDSHO restarts to load the data.

## Move to another PC

1. On the old PC, choose **Export data...** from the same menu and save the `.zip` file. Or use the latest backup.
2. Copy the file to the new PC, or use a backup folder both PCs share.
3. Install MDSHO on the new PC from the Microsoft Store.
4. Choose **Import data...**, pick the file and choose **Yes** to replace the sample panel.

> Set the backup folder on the new PC too. In a shared folder, each PC keeps its own backups, so the backups of the
> old PC stay until you delete them.
