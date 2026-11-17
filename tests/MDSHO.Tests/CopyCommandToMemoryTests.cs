using System;
using System.Collections.Generic;
using MDSHO.ViewModels;
using Xunit;

namespace MDSHO.Tests {

  /// <summary>
  /// "Copy command to memory" on a run command's menu (1.14.3): it copies the command line exactly as the button's
  /// tooltip shows it, with the group values filled in. The clipboard is never touched: copies go through the
  /// <see cref="ShortcutViewModel.CopyText"/> seam, which only records here.
  /// </summary>
  public class CopyCommandToMemoryTests : IDisposable {
    private readonly TempDb db = new TempDb();
    private readonly Action<string> oldCopy = ShortcutViewModel.CopyText;
    private readonly Action<ShortcutViewModel, bool> oldEditor = ShortcutViewModel.OpenRunCommandEditor;
    private readonly List<string> copied = new List<string>();
    private readonly AppViewModel app = new AppViewModel();
    private readonly DesktopWindowViewModel win;

    public CopyCommandToMemoryTests() {
      ShortcutViewModel.CopyText = text => copied.Add(text);
      ShortcutViewModel.OpenRunCommandEditor = (s, isNew) => { };
      win = app.AddWindow("TEST", 0, 0);
    }
    public void Dispose() {
      ShortcutViewModel.CopyText = oldCopy;
      ShortcutViewModel.OpenRunCommandEditor = oldEditor;
      db.Dispose();
    }

    private ShortcutViewModel Group(string name) {
      ShortcutViewModel g = ViewModelFactory.NewGroupShortcut(app, null, win);
      win.RootShortcuts.Add(g);
      g.Name = name;
      return g;
    }
    private ShortcutViewModel Command(ShortcutViewModel parent, string fileName, string arguments) {
      ShortcutViewModel s = ViewModelFactory.NewProcessCommandShortcut(app, parent, parent == null ? win : null);
      (parent == null ? win.RootShortcuts : parent.Children).Add(s);
      s.ProcessFileName = fileName;
      s.ProcessArguments = arguments;
      return s;
    }

    [Fact]
    public void It_copies_the_command_with_the_group_values_filled_in_as_the_tooltip_shows_it() {
      ShortcutViewModel cmd = Command(Group("Prod {SRV01} {user=admin}"), "cmd.exe", "/K ping {=1} -u {=user}");
      cmd.CopyCommandToMemoryCommand.Execute(null);
      Assert.Equal(new[] { "cmd.exe /K ping SRV01 -u admin" }, copied);
      Assert.Equal(cmd.ToolTipText, copied[0]);
    }

    [Fact]
    public void Several_argument_lines_are_copied_joined_like_the_tooltip() {
      ShortcutViewModel cmd = Command(null, "cmd.exe", "/K ping a\r\nping b");
      cmd.CopyCommandToMemoryCommand.Execute(null);
      Assert.Equal(cmd.ToolTipText, Assert.Single(copied));
      Assert.DoesNotContain("\n", copied[0]);
    }

    [Fact]
    public void A_program_path_with_a_space_is_copied_in_quotes() {
      ShortcutViewModel cmd = Command(null, @"C:\Program Files\Tool\tool.exe", "-v");
      cmd.CopyCommandToMemoryCommand.Execute(null);
      Assert.Equal("\"C:\\Program Files\\Tool\\tool.exe\" -v", Assert.Single(copied));
      Assert.Equal(cmd.ToolTipText, copied[0]);
    }

    [Fact]
    public void With_a_group_value_error_it_copies_what_the_tooltip_shows_with_the_value_not_filled_in() {
      ShortcutViewModel cmd = Command(Group("Test {SRV01}"), "cmd.exe", "/K ping {=3} {=1}");
      Assert.True(cmd.HasRunCommandError);
      cmd.CopyCommandToMemoryCommand.Execute(null);
      Assert.Equal(new[] { "cmd.exe /K ping {=3} {=1}" }, copied);
      Assert.Equal(cmd.ToolTipText, copied[0]);
    }

    [Fact]
    public void An_empty_command_copies_nothing() {
      ShortcutViewModel cmd = Command(null, "", "");
      cmd.CopyCommandToMemoryCommand.Execute(null);
      Assert.Empty(copied);
    }

    [Fact]
    public void Only_run_commands_show_it_and_they_do_not_show_copy_target() {
      ShortcutViewModel group = Group("G");
      ShortcutViewModel cmd = Command(null, "cmd.exe", "/K dir");
      ShortcutViewModel note = ViewModelFactory.NewNoteShortcut(app, null, win);
      Assert.True(cmd.ShowCopyCommandToMemory);
      Assert.False(cmd.ShowCopyTargetToMemory);
      Assert.False(group.ShowCopyCommandToMemory);
      Assert.False(note.ShowCopyCommandToMemory);
      ShortcutViewModel file = new ShortcutViewModel(null, null, 2, null, "f", @"C:\x.txt", ".lnk", "g2", false, false, null, false, null, 0, false, "", "");
      Assert.False(file.ShowCopyCommandToMemory);
      Assert.True(file.ShowCopyTargetToMemory);
    }
  }
}
