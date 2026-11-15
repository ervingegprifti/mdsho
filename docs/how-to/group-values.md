---
title: Run commands with group values
permalink: /how-to/group-values/
---

[MDSHO](../index.md) › How-to

# Run commands with group values

This page helps you write one command and reuse it for many servers, users or folders.

## Make a run command

1. Right-click an empty part of the panel, or a group, and choose **New command**. The **Run command** window opens.
2. Pick or type a **Program**, for example `cmd.exe`, or click **Browse...**.
3. Type the **Arguments**, one command per line, for example `ping localhost`.
4. Optionally set a **Working folder**, and tick **Keep window open after it runs** or **Run as administrator**.
5. Check the line under **Will run as**, then click **Test run** to try it without saving.
6. Click **Save**.

To change it later, right-click the button and choose **Edit run command...**. Click **Examples** in the window's title
bar for ready-made commands.

## Put values in a group's name

A group's name can hold up to 9 values in braces. For example:

`Prod {SRV01} {user=admin}`

- `{SRV01}` is value 1.
- `{user=admin}` is value 2, and it also has the name `user`.

## Use the values in a command

In the arguments or the working folder of a command inside that group, write:

- `{=1}`, `{=2}` … `{=9}` for a value by its place, so `{=1}` becomes `SRV01`;
- `{=user}` for a value by its name, so `{=user}` becomes `admin`.

For example, `ping {=1}` runs `ping SRV01`. The **Run command** window lists the group's values under the arguments;
click one to insert it.

Only commands directly inside the group use its values, not commands in a group inside it.

## Reuse a command for another server

1. Right-click the group and choose **Copy**.
2. Right-click an empty part of the panel and choose **Paste**.
3. Rename the copy, for example to `Test {SRV02} {user=tester}`.

Every command in the copy now runs with the new values.

## When a command shows an error

A command with a mistake does not run. Its tooltip shows the error in red. Right-click it and choose
**Edit run command...** to fix it: the **Run command** window shows the error under the arguments and saves only once
it is fixed. The errors are:

- **This command uses group values, but its group has none.** The command is not in a group, or the group's name has
  no values in braces.
- **The group has no value 3.** The command uses `{=3}`, but the group has fewer values.
- **The group has no value named "user".** No value in the group's name is called `user`.
- **"{=x y}" is not valid. Use {=1} or {=name}.** A value is written wrongly. A name uses letters, digits and `_`, and
  does not start with a digit.
- **{} is no longer supported. Use {=1}.** Older commands used `{}`; change it to `{=1}`.

> Value names ignore case: `{=User}` and `{=user}` are the same.
