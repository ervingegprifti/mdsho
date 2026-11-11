using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using MDSHO.Core;
using MDSHO.Data;
using MDSHO.ViewModels;
using Xunit;

namespace MDSHO.Tests {

  /// <summary>
  /// Pins what the view model setters and commands do (database saves, the change events they raise, values they clean up),
  /// so moving the view models to CommunityToolkit.Mvvm cannot drop a side effect.
  /// </summary>
  public class MvvmToolkitPinTests : IDisposable {
    private readonly TempDb db = new TempDb();
    private readonly AppViewModel app = new AppViewModel();
    private readonly DesktopWindowViewModel win;

    public MvvmToolkitPinTests() { win = app.AddWindow("TEST", 0, 0); }
    public void Dispose() { db.Dispose(); }

    private static List<string> Record(INotifyPropertyChanged source) {
      List<string> raised = new List<string>();
      source.PropertyChanged += (s, e) => raised.Add(e.PropertyName);
      return raised;
    }
    private ShortcutViewModel Group() {
      ShortcutViewModel g = ViewModelFactory.NewGroupShortcut(app, null, win);
      win.RootShortcuts.Add(g);
      return g;
    }
    private ShortcutViewModel Command(ShortcutViewModel parent) {
      ShortcutViewModel s = ViewModelFactory.NewProcessCommandShortcut(app, parent, parent == null ? win : null);
      (parent == null ? win.RootShortcuts : parent.Children).Add(s);
      return s;
    }
    private static Shortcut Row(ShortcutViewModel s) { return DataStore.Shortcuts.Get(s.ID); }

    #region SHORTCUT SETTERS
    [Fact]
    public void IsGroupExpanded_saves_and_raises_only_when_it_changes() {
      ShortcutViewModel g = Group();
      bool start = g.IsGroupExpanded;
      List<string> raised = Record(g);
      g.IsGroupExpanded = start;
      Assert.Empty(raised);
      g.IsGroupExpanded = !start;
      Assert.Equal(new[] { nameof(ShortcutViewModel.IsGroupExpanded) }, raised);
      Assert.Equal(!start, Row(g).IsGroupExpanded != 0);
    }

    [Fact]
    public void Name_saves_and_refreshes_the_command_line_of_the_commands_in_the_group() {
      ShortcutViewModel g = Group();
      ShortcutViewModel child = Command(g);
      child.ProcessFileName = "cmd.exe";
      child.ProcessArguments = "/K echo {}";
      List<string> raisedGroup = Record(g), raisedChild = Record(child);
      g.Name = "Servers {alpha}";
      Assert.Contains(nameof(ShortcutViewModel.Name), raisedGroup);
      Assert.Equal("Servers {alpha}", Row(g).Name);
      Assert.Contains(nameof(ShortcutViewModel.ResolvedProcessArguments), raisedChild);
      Assert.Contains(nameof(ShortcutViewModel.ToolTipText), raisedChild);
      Assert.Equal("/K echo alpha", child.ResolvedProcessArguments);
    }

    [Fact]
    public void ProcessFileName_saves_and_raises_target_and_command_line() {
      ShortcutViewModel s = Command(null);
      List<string> raised = Record(s);
      s.ProcessFileName = "powershell.exe";
      Assert.Equal("powershell.exe", Row(s).ProcessFileName);
      foreach (string name in new[] { nameof(s.ProcessFileName), nameof(s.Target), nameof(s.ResolvedProcessArguments), nameof(s.ToolTipText) }) {
        Assert.Contains(name, raised);
      }
    }

    [Fact]
    public void ProcessArguments_saves_and_raises_target_and_command_line() {
      ShortcutViewModel s = Command(null);
      List<string> raised = Record(s);
      s.ProcessArguments = "-NoExit dir";
      Assert.Equal("-NoExit dir", Row(s).ProcessArguments);
      foreach (string name in new[] { nameof(s.ProcessArguments), nameof(s.Target), nameof(s.ResolvedProcessArguments), nameof(s.ToolTipText) }) {
        Assert.Contains(name, raised);
      }
    }

    [Fact]
    public void ProcessRunAsAdmin_saves_and_raises_only_when_it_changes() {
      ShortcutViewModel s = Command(null);
      List<string> raised = Record(s);
      s.ProcessRunAsAdmin = false;
      Assert.Empty(raised);
      s.ProcessRunAsAdmin = true;
      Assert.Equal(new[] { nameof(s.ProcessRunAsAdmin) }, raised);
      Assert.True(Row(s).ProcessRunAsAdmin != 0);
    }

    [Fact]
    public void ProcessWorkingFolder_turns_null_into_empty_saves_and_raises_only_when_it_changes() {
      ShortcutViewModel s = Command(null);
      List<string> raised = Record(s);
      s.ProcessWorkingFolder = null;
      Assert.Equal("", s.ProcessWorkingFolder);
      Assert.Empty(raised);
      s.ProcessWorkingFolder = @"C:\Work";
      Assert.Equal(new[] { nameof(s.ProcessWorkingFolder) }, raised);
      Assert.Equal(@"C:\Work", Row(s).ProcessWorkingFolder);
      s.ProcessWorkingFolder = null;
      Assert.Equal("", s.ProcessWorkingFolder);
      Assert.Equal("", Row(s).ProcessWorkingFolder);
    }

    [Fact]
    public void IsMarked_and_MarkColor_save_and_raise_only_when_they_change() {
      ShortcutViewModel s = Command(null);
      List<string> raised = Record(s);
      s.IsMarked = false;
      Assert.Empty(raised);
      s.IsMarked = true;
      Assert.Equal(new[] { nameof(s.IsMarked), nameof(s.ShowMark) }, raised);
      Assert.True(Row(s).IsMarked != 0);
      raised.Clear();
      s.MarkColor = "#FF112233";
      Assert.Equal(new[] { nameof(s.MarkColor) }, raised);
      Assert.Equal("#FF112233", Row(s).MarkColor);
      raised.Clear();
      s.MarkColor = "#FF112233";
      Assert.Empty(raised);
    }

    [Fact]
    public void Mark_command_with_a_parameter_that_is_not_text_removes_the_mark() {
      ShortcutViewModel s = Command(null);
      s.MarkShortcutCommand.Execute("#FF112233");
      Assert.True(s.IsMarked);
      s.MarkShortcutCommand.Execute(42);
      Assert.False(s.IsMarked);
      s.MarkShortcutCommand.Execute("#FF112233");
      s.MarkShortcutCommand.Execute(null);
      Assert.False(s.IsMarked);
    }

    [Fact]
    public void Target_Comment_IsSelected_and_EditText_raise_their_change() {
      ShortcutViewModel s = Command(null);
      List<string> raised = Record(s);
      s.Comment = "n";
      s.IsSelected = true;
      s.EditText = "e";
      s.Target = "https://example.com/";
      string[] watched = { nameof(s.Comment), nameof(s.IsSelected), nameof(s.EditText), nameof(s.Target) };
      Assert.Equal(watched, raised.Where(watched.Contains).Take(4));
      // A comment also refreshes what the view shows from it.
      Assert.Contains(nameof(s.HasComment), raised);
    }

    [Fact]
    public void SetParentInMemory_raises_parent_and_command_line_without_saving() {
      ShortcutViewModel g = Group();
      g.Name = "Box {beta}";
      ShortcutViewModel s = Command(null);
      s.ProcessFileName = "cmd.exe";
      s.ProcessArguments = "/K echo {}";
      List<string> raised = Record(s);
      s.SetParentInMemory(g, null);
      foreach (string name in new[] { nameof(s.ParentShortcut), nameof(s.ParentWindowID), nameof(s.ResolvedProcessArguments), nameof(s.ToolTipText) }) {
        Assert.Contains(name, raised);
      }
      Assert.Equal("/K echo beta", s.ResolvedProcessArguments);
      Shortcut row = Row(s);
      Assert.Null(row.ParentShortcutID);
      Assert.Equal(win.ID, row.ParentWindowID);
    }
    #endregion SHORTCUT SETTERS

    #region WINDOW, APPEARANCE, ICONS
    [Fact]
    public void Window_geometry_and_screen_raise_their_change() {
      List<string> raised = Record(win);
      win.Left = 11; win.Top = 12; win.Width = 13; win.Height = 14; win.ScreenDeviceName = "OTHER";
      Assert.Equal(new[] { nameof(win.Left), nameof(win.Top), nameof(win.Width), nameof(win.Height), nameof(win.ScreenDeviceName) }, raised);
    }

    [Fact]
    public void Window_raises_ShowDragItemsHereInfo_when_root_shortcuts_change_and_TitleDisplay_when_the_title_changes() {
      List<string> raised = Record(win);
      ShortcutViewModel g = Group();
      Assert.Contains(nameof(win.ShowDragItemsHereInfo), raised);
      raised.Clear();
      win.Appearance.Title = "Renamed";
      Assert.Equal(new[] { nameof(win.TitleDisplay) }, raised);
    }

    [Fact]
    public void Appearance_raises_title_only_on_change_and_colours_and_notes_on_set() {
      DesktopWindowAppearance a = win.Appearance;
      List<string> raised = Record(a);
      a.Title = a.Title;
      Assert.Empty(raised);
      a.Title = "T2";
      a.BorderColor = "#FF000001"; a.BackgroundColor = "#FF000002"; a.TextColor = "#FF000003";
      a.TitleBackgroundColor = "#FF000004"; a.TitleTextColor = "#FF000005"; a.Notes = "notes";
      Assert.Equal(new[] { nameof(a.Title), nameof(a.BorderColor), nameof(a.BackgroundColor), nameof(a.TextColor),
        nameof(a.TitleBackgroundColor), nameof(a.TitleTextColor), nameof(a.Notes) }, raised);
    }

    [Fact]
    public void Appearance_SolidIcons_switches_the_glyphs_and_SingleClickToOpen_raises_only_on_change() {
      DesktopWindowAppearance a = win.Appearance;
      bool solid = a.SolidIcons, single = a.SingleClickToOpen;
      List<string> raised = Record(a);
      a.SolidIcons = solid;
      a.SingleClickToOpen = single;
      Assert.Empty(raised);
      a.SolidIcons = !solid;
      Assert.Equal(!solid, a.Icons.SolidIcons);
      a.SingleClickToOpen = !single;
      Assert.Equal(new[] { nameof(a.SolidIcons), nameof(a.SingleClickToOpen) }, raised);
    }

    [Fact]
    public void Icon_glyphs_raise_every_glyph_when_the_set_switches() {
      IconGlyphs icons = new IconGlyphs(true);
      List<string> raised = Record(icons);
      icons.SolidIcons = true;
      Assert.Empty(raised);
      icons.SolidIcons = false;
      string[] glyphs = typeof(IconGlyphs).GetProperties().Where(p => p.PropertyType == typeof(string)).Select(p => p.Name).ToArray();
      Assert.Equal(28, glyphs.Length); // v1.14: Unknown removed; Warning, Run and Wizard added; Info added for the About icons; Desktop added for the tray menu; Brush added for note colours
      Assert.Equal(nameof(icons.SolidIcons), raised[0]);
      Assert.Equal(glyphs.OrderBy(n => n), raised.Skip(1).OrderBy(n => n));
    }
    #endregion WINDOW, APPEARANCE, ICONS

    #region RUN COMMAND WINDOW
    [Fact]
    public void Run_command_window_fields_clean_up_null_and_refresh_the_preview() {
      RunCommandViewModel vm = new RunCommandViewModel(Command(null), true);
      List<string> raised = Record(vm);
      vm.Program = null;
      Assert.Equal("", vm.Program);
      Assert.Equal(new[] { nameof(vm.Program), nameof(vm.IsKeepOpenEnabled), nameof(vm.Preview) }, raised);
      vm.WorkingFolder = @"C:\Work";
      vm.Arguments = "dir";
      raised.Clear();
      vm.WorkingFolder = null;
      vm.Arguments = null;
      Assert.Equal("", vm.WorkingFolder);
      Assert.Equal("", vm.Arguments);
      vm.KeepWindowOpen = false;
      vm.RunAsAdministrator = true;
      Assert.Equal(new[] { nameof(vm.WorkingFolder), nameof(vm.Preview), nameof(vm.Arguments), nameof(vm.Preview),
        nameof(vm.KeepWindowOpen), nameof(vm.Preview), nameof(vm.RunAsAdministrator), nameof(vm.Preview) }, raised);
    }
    #endregion RUN COMMAND WINDOW

    #region COMMANDS
    [Fact]
    public void Command_properties_keep_the_names_the_views_bind_to() {
      string[] Commands(Type t) => t.GetProperties().Where(p => typeof(ICommand).IsAssignableFrom(p.PropertyType)).Select(p => p.Name).OrderBy(n => n).ToArray();
      Assert.Equal(new[] { "BeginEditTitleCommand", "CancelEditTitleCommand", "CloseWindowCommand", "CommitEditTitleCommand",
        "NewGroupShortcutCommand", "NewNoteShortcutCommand", "NewProcessCommandShortcutCommand", "PasteShortcutCommand" }, Commands(typeof(DesktopWindowViewModel)));
      Assert.Equal(new[] { "CancelEditCommand", "CommitEditCommand", "CopyNoteTextCommand", "CopyShortcutCommand", "CopyTargetToMemoryCommand", "CutShortcutCommand",
        "DeleteShortcutCommand", "EditCommentCommand", "EditNoteCommand", "MarkShortcutCommand", "NewGroupShortcutCommand", "NewNoteShortcutCommand", "NewProcessCommandShortcutCommand", "PasteShortcutCommand",
        "RenameCommand", "SetNameTheSameAsTargetCommand", "SetRunCommandCommand" }, Commands(typeof(ShortcutViewModel)));
      Assert.Equal(new[] { "TestRunCommand" }, Commands(typeof(RunCommandViewModel)));
    }

    [Fact]
    public void New_item_commands_run_only_on_a_group() {
      ShortcutViewModel g = Group(), s = Command(null);
      Assert.True(g.NewGroupShortcutCommand.CanExecute(null));
      Assert.True(g.NewProcessCommandShortcutCommand.CanExecute(null));
      Assert.False(s.NewGroupShortcutCommand.CanExecute(null));
      Assert.False(s.NewProcessCommandShortcutCommand.CanExecute(null));
    }

    [Fact]
    public void Copy_cut_and_paste_tell_every_paste_command_to_ask_again() {
      ShortcutViewModel g = Group(), s = Command(null);
      int groupRaised = 0, windowRaised = 0;
      g.PasteShortcutCommand.CanExecuteChanged += (o, e) => groupRaised++;
      win.PasteShortcutCommand.CanExecuteChanged += (o, e) => windowRaised++;
      Assert.False(g.PasteShortcutCommand.CanExecute(null));
      s.CopyShortcutCommand.Execute(null);
      Assert.True(groupRaised > 0 && windowRaised > 0, "copy");
      Assert.True(g.PasteShortcutCommand.CanExecute(null));
      groupRaised = windowRaised = 0;
      g.PasteShortcutCommand.Execute(null);
      Assert.True(groupRaised > 0 && windowRaised > 0, "paste");
      Assert.False(g.PasteShortcutCommand.CanExecute(null));
      groupRaised = windowRaised = 0;
      s.CutShortcutCommand.Execute(null);
      Assert.True(groupRaised > 0 && windowRaised > 0, "cut");
    }

    [Fact]
    public void Paste_menu_item_is_enabled_after_a_copy_when_WPF_asks_again() {
      PasteMenuItemTest(invalidate: true);
    }

    [Fact]
    public void Paste_menu_item_is_enabled_after_a_copy_without_waiting_for_input() {
      PasteMenuItemTest(invalidate: false);
    }

    private void PasteMenuItemTest(bool invalidate) {
      ShortcutViewModel s = Command(null);
      WpfAppHost.Run(() => {
        DesktopWindowView view = new DesktopWindowView(win) { ShowActivated = false, ShowInTaskbar = false };
        view.Left = -10000;
        view.Top = -10000;
        try {
          view.Show();
          view.Left = -10000;
          WpfAppHost.Pump();
          TreeView tree = (TreeView)view.FindName("TreeViewShortcuts");
          ContextMenu menu = tree.ContextMenu;
          menu.DataContext = win;
          MenuItem paste = menu.Items.OfType<MenuItem>().Single(m => (string)m.Header == "Paste");
          WpfAppHost.Pump();
          Assert.False(paste.IsEnabled);
          s.CopyShortcutCommand.Execute(null);
          if (invalidate) {
            CommandManager.InvalidateRequerySuggested();
          }
          WpfAppHost.Pump();
          Assert.True(paste.IsEnabled);
        } finally {
          view.Close();
        }
      });
    }
    #endregion COMMANDS
  }
}
