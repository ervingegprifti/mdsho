---
title: Paste text with clipboard buttons
permalink: /how-to/clipboard/
---

[MDSHO](../index.md) › How-to

# Paste text with clipboard buttons

This page helps you keep text you type often, like an address or a customer number, on a panel and paste it with a
double-click.

## Make a clipboard button

1. Right-click an empty part of the panel, or a group, and choose **New clipboard**.
2. Type a name for the button and press Enter.
3. Right-click the button and choose **Edit value...**.
4. Type the text. It can have several lines.
5. Click **OK** in the title bar, or press Ctrl+Enter. Esc closes the window without saving.

Hover over the button to see its name and its value.

## Paste the value

1. Click in a text field, for example in a web form or in Notepad.
2. Double-click the clipboard button. In a panel set to open buttons with a single click, click it once.

MDSHO goes back to the window you were in, pastes the value, and then puts back what was on your clipboard. If your
clipboard was empty, it stays empty. If you copy something else in the meantime, MDSHO leaves it alone. The value is
not added to your clipboard history.

You can also select the button and press Enter.

## Copy the value yourself

Right-click the button and choose **Copy value to memory**. The value stays on your clipboard, so you can paste it with
Ctrl+V as often as you like.

## When MDSHO can't paste

MDSHO doesn't touch your clipboard when it can't paste. It tells you why:

- **No place to paste into.** There was no text field to paste into. Click in a text field first, then double-click the
  button again.
- **Can't paste into this window.** The window runs as administrator, and Windows doesn't let MDSHO paste into it.
  Use **Copy value to memory**, then press Ctrl+V in that window.

## Keep secrets out

MDSHO stores the value as plain text in its data file, in exports and in backups. Don't put passwords or other secrets
in a clipboard button.
