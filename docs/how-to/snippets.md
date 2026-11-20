---
title: Paste text with snippets
permalink: /how-to/snippets/
---

[MDSHO](../index.md) › How-to

# Paste text with snippets

This page helps you keep text you type often, like an address or a customer number, on a panel and paste it with a
double-click.

## Make a snippet

1. Right-click an empty part of the panel, or a group, and choose **New snippet**.
2. Type a name for the snippet and press Enter.
3. Right-click the snippet and choose **Edit snippet...**.
4. Type the text. It can have several lines.
5. Click **OK** in the title bar, or press Ctrl+Enter. Esc closes the window without saving.

To change the font of the text, click **Font** in the title bar and pick one. The snippet keeps its font, even
if you close the window without OK. Snippets start in Courier New. The **Text size** button left of **Font** shows the size
in points: click it to pick a size, or hold Ctrl and turn the mouse wheel. The size is only for editing; the editor
starts at 12 pt each time.

Hover over the snippet to see its name and its text. To name it after its text, right-click it and choose
**Set name the same as value**.

## Paste a snippet

1. Click in a text field, for example in a web form or in Notepad.
2. Double-click the snippet. In a panel set to open buttons with a single click, click it once.

MDSHO goes back to the window you were in, pastes the text, and then puts back what was on your clipboard. If your
clipboard was empty, it stays empty. If you copy something else in the meantime, MDSHO leaves it alone. The text is
not added to your clipboard history.

Enter does nothing on a snippet, so that nothing is pasted by accident: double-click it to paste.

## Copy the text yourself

Right-click the button and choose **Copy snippet to memory**. The text stays on your clipboard, so you can paste it with
Ctrl+V as often as you like.

## When MDSHO can't paste

MDSHO doesn't touch your clipboard when it can't paste. It tells you why:

- **No place to paste into.** There was no text field to paste into. Click in a text field first, then double-click the
  button again.
- **Can't paste into this window.** The window runs as administrator, and Windows doesn't let MDSHO paste into it.
  Use **Copy snippet to memory**, then press Ctrl+V in that window.

## Keep secrets out

MDSHO stores snippets as plain text in its data file, in exports and in backups. Don't put passwords or other secrets
in a snippet.
