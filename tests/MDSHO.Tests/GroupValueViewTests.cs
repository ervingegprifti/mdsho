using System.Linq;
using System.Windows.Controls;
using MDSHO.Data;
using MDSHO.ViewModels;
using Xunit;

namespace MDSHO.Tests {

  /// <summary>
  /// The tooltip of a run command in the real desktop window shows the command line that will run, with the value of its
  /// group put in for <c>{}</c>, and follows a rename of the group.
  /// </summary>
  public class GroupValueViewTests {

    [Fact]
    public void Run_command_tooltip_shows_the_resolved_command_line_and_follows_a_group_rename() {
      WpfAppHost.Run(() => {
        using (TempDb db = new TempDb()) {
          var window = DataStore.Windows.LoadAll()[0];
          var group = DataStore.Shortcuts.CreateGroup(null, window.ID);
          DataStore.Shortcuts.UpdateName(group.ID, "Test {SRV01}");
          DataStore.Shortcuts.UpdateIsGroupExpanded(group.ID, true);
          var command = DataStore.Shortcuts.CreateProcessCommand(group.ID, null);
          DataStore.Shortcuts.UpdateName(command.ID, "Ping");
          DataStore.Shortcuts.UpdateProcessFileName(command.ID, "cmd.exe");
          DataStore.Shortcuts.UpdateProcessArguments(command.ID, "/K ping {}");

          AppViewModel appVm = ViewModelFactory.CreateAppViewModel();
          DesktopWindowViewModel windowVm = appVm.Windows[0];
          DesktopWindowView view = new DesktopWindowView(windowVm) { ShowActivated = false };
          try {
            view.Show();
            view.UpdateLayout();
            WpfAppHost.Pump();
            TreeView tree = (TreeView)view.FindName("TreeViewShortcuts");
            System.Func<string, object> toolTipOf = name => {
              WpfAppHost.Pump();
              TreeViewItem item = WpfAppHost.VisualDescendants<TreeViewItem>(tree)
                .Single(i => i.DataContext is ShortcutViewModel s && s.Name == name);
              Button button = WpfAppHost.VisualDescendants<Button>(item).First(b => b.DataContext == item.DataContext);
              // The tooltip is a panel: its first line is the command line (the comment, if any, follows below a divider).
              ToolTip tip = Assert.IsType<ToolTip>(button.ToolTip);
              StackPanel panel = Assert.IsType<StackPanel>(tip.Content);
              panel.DataContext = button.DataContext;
              return panel.Children.OfType<TextBlock>().First().Text;
            };

            Assert.Equal("cmd.exe /K ping SRV01", toolTipOf("Ping"));
            ShortcutViewModel groupVm = windowVm.RootShortcuts.Single(s => s.Name == "Test {SRV01}");
            groupVm.Name = "Test {SRV02}";
            Assert.Equal("cmd.exe /K ping SRV02", toolTipOf("Ping"));
            // The group itself is shown exactly as typed.
            Assert.Contains(WpfAppHost.VisualDescendants<TextBlock>(tree), t => t.Text == "Test {SRV02}");
          } finally {
            view.Close();
            WpfAppHost.Pump();
          }
        }
      });
    }
  }
}
