using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using MDSHO.Data;
using MDSHO.ViewModels;
using Xunit;

namespace MDSHO.Tests {

  /// <summary>
  /// Screen readers: every button, toggle button and menu item of the real windows (context menus included) has a name a
  /// screen reader can say, icon-only ones also have a tooltip, and shortcut buttons are named after their shortcut.
  /// </summary>
  public class AccessibilityNamesTests {

    // The icon fonts (MDSHOFont and Segoe MDL2 Assets) use the Unicode private use area: a glyph alone is not readable text.
    private static bool IsReadable(string text) {
      return !string.IsNullOrWhiteSpace(text) && text.Any(c => !char.IsWhiteSpace(c) && (c < '' || c > ''));
    }

    /// <summary>The name a screen reader gets for the element (UI Automation).</summary>
    private static string SpokenName(FrameworkElement element) {
      AutomationPeer peer = UIElementAutomationPeer.CreatePeerForElement(element);
      return peer?.GetName();
    }

    /// <summary>The text the element shows: its string content or header, or the text blocks inside its content.</summary>
    private static string VisibleText(FrameworkElement element) {
      object content = element is HeaderedItemsControl headered ? headered.Header : element is ContentControl control ? control.Content : null;
      if (content is string text) {
        return text;
      }
      if (content is DependencyObject root) {
        return string.Join(" ", Descendants(root).OfType<TextBlock>().Select(t => t.Text));
      }
      return null;
    }

    private static IEnumerable<DependencyObject> Descendants(DependencyObject root) {
      HashSet<DependencyObject> seen = new HashSet<DependencyObject>();
      Stack<DependencyObject> stack = new Stack<DependencyObject>();
      stack.Push(root);
      while (stack.Count > 0) {
        DependencyObject current = stack.Pop();
        if (!seen.Add(current)) {
          continue;
        }
        yield return current;
        if (current is Visual || current is System.Windows.Media.Media3D.Visual3D) {
          for (int i = 0; i < VisualTreeHelper.GetChildrenCount(current); i++) {
            stack.Push(VisualTreeHelper.GetChild(current, i));
          }
        }
        foreach (object child in LogicalTreeHelper.GetChildren(current)) {
          if (child is DependencyObject d) {
            stack.Push(d);
          }
        }
      }
    }

    // Parts made by a control's own template (a tree item's expander, a scroll bar's arrows) are not ours to name;
    // elements made from our data templates are (their templated parent is a ContentPresenter).
    private static bool IsOurs(FrameworkElement element) {
      return element.TemplatedParent == null || element.TemplatedParent is ContentPresenter;
    }

    // Third-party controls (the PixiEditor colour picker) build their own insides.
    private static bool IsThirdParty(DependencyObject element) {
      return element.GetType().Assembly.GetName().Name.StartsWith("ColorPicker", StringComparison.Ordinal);
    }

    /// <summary>Every Button, ToggleButton and MenuItem of the window and of every context menu in it.</summary>
    private static List<FrameworkElement> Controls(Window window) {
      List<FrameworkElement> found = new List<FrameworkElement>();
      List<FrameworkElement> owners = new List<FrameworkElement>();
      Stack<DependencyObject> stack = new Stack<DependencyObject>();
      stack.Push(window);
      HashSet<DependencyObject> seen = new HashSet<DependencyObject>();
      while (stack.Count > 0) {
        DependencyObject current = stack.Pop();
        if (!seen.Add(current) || IsThirdParty(current)) {
          continue;
        }
        if (current is FrameworkElement fe) {
          if ((fe is Button || fe is ToggleButton || fe is MenuItem) && IsOurs(fe)) {
            found.Add(fe);
          }
          if (fe.ContextMenu != null) {
            owners.Add(fe);
          }
        }
        if (current is Visual) {
          for (int i = 0; i < VisualTreeHelper.GetChildrenCount(current); i++) {
            stack.Push(VisualTreeHelper.GetChild(current, i));
          }
        }
        foreach (object child in LogicalTreeHelper.GetChildren(current)) {
          if (child is DependencyObject d) {
            stack.Push(d);
          }
        }
      }
      foreach (FrameworkElement owner in owners) {
        ContextMenu menu = owner.ContextMenu;
        menu.DataContext = owner.DataContext; // what opening the menu does: its items bind to the owner's data
        found.AddRange(Descendants(menu).OfType<MenuItem>());
      }
      return found.Distinct().ToList();
    }

    private static string Describe(FrameworkElement element) {
      string text = element.Name;
      if (string.IsNullOrEmpty(text)) {
        text = element.GetType().Name + " in " + (VisualTreeHelper.GetParent(element) ?? LogicalTreeHelper.GetParent(element))?.GetType().Name;
      }
      return text + " [Tag=" + (element.Tag ?? "") + "]";
    }

    /// <summary>The problems of one window: controls without a spoken name, icon-only controls without a tooltip.</summary>
    private static List<string> Problems(Window window) {
      List<string> problems = new List<string>();
      foreach (FrameworkElement element in Controls(window)) {
        if (!IsReadable(SpokenName(element))) {
          problems.Add(window.GetType().Name + ": " + Describe(element) + " has no name for screen readers");
        }
        if (!IsReadable(VisibleText(element)) && element.ToolTip == null) {
          problems.Add(window.GetType().Name + ": " + Describe(element) + " shows only an icon and has no tooltip");
        }
      }
      return problems;
    }

    private static void OffScreen(Window window) {
      window.WindowStartupLocation = WindowStartupLocation.Manual;
      window.Left = -10000;
      window.Top = -10000;
      window.ShowActivated = false;
      window.ShowInTaskbar = false;
    }

    /// <summary>Shows the window off screen, collects its problems, closes it.</summary>
    private static List<string> Check(Window window, Action<Window> afterShow = null) {
      OffScreen(window);
      window.Show();
      try {
        window.Left = -10000;
        window.Top = -10000;
        window.UpdateLayout();
        WpfAppHost.Pump();
        afterShow?.Invoke(window);
        return Problems(window);
      } finally {
        window.Close();
        WpfAppHost.Pump();
      }
    }

    private static void ExpandTree(Window window) {
      TreeView tree = (TreeView)window.FindName("TreeViewShortcuts");
      for (int pass = 0; pass < 4; pass++) {
        foreach (TreeViewItem item in Descendants(tree).OfType<TreeViewItem>().ToList()) {
          item.IsExpanded = true;
          item.UpdateLayout();
        }
        WpfAppHost.Pump();
      }
    }

    /// <summary>A window with a clone and a nested tree of every kind of shortcut.</summary>
    private static void Seed() {
      var window = DataStore.Windows.LoadAll()[0];
      DataStore.Clones.Create(window.ID, "\\\\.\\DISPLAY1", -10000, -10000);
      DataStore.Shortcuts.Create(null, window.ID, "File", System.IO.Path.GetTempFileName(), ".lnk", "11111111111111111111111111111111");
      DataStore.Shortcuts.Create(null, window.ID, "Url", "https://example.com/", ".url", "33333333333333333333333333333333");
      var command = DataStore.Shortcuts.CreateProcessCommand(null, window.ID);
      DataStore.Shortcuts.UpdateName(command.ID, "Ping");
      DataStore.Shortcuts.UpdateProcessFileName(command.ID, "cmd.exe");
      var group = DataStore.Shortcuts.CreateGroup(null, window.ID);
      DataStore.Shortcuts.UpdateName(group.ID, "Tools");
      DataStore.Shortcuts.UpdateIsGroupExpanded(group.ID, true);
      DataStore.Shortcuts.Create(group.ID, null, "Child file", System.IO.Path.GetTempFileName(), ".lnk", "44444444444444444444444444444444");
    }

    [Fact]
    public void Every_button_and_menu_item_of_the_real_windows_has_a_name_for_screen_readers() {
      List<string> problems = new List<string>();
      List<string> shortcutNames = new List<string>();
      List<string> spokenShortcutNames = new List<string>();
      WpfAppHost.Run(() => {
        Action<ProcessStartInfo> oldStarter = ShortcutViewModel.ProcessStarter;
        ShortcutViewModel.ProcessStarter = psi => throw new InvalidOperationException("A process was started in a test.");
        try {
          using (TempDb db = new TempDb()) {
            Seed();
            AppViewModel app = ViewModelFactory.CreateAppViewModel();
            DesktopWindowViewModel windowVm = app.Windows[0];
            DesktopWindowViewModel cloneVm = windowVm.Clones.Single();

            // The window and its clone (the clone shows "Close this clone" instead of "Clone this Window").
            foreach (DesktopWindowViewModel vm in new[] { windowVm, cloneVm }) {
              problems.AddRange(Check(new DesktopWindowView(vm), view => {
                ExpandTree(view);
                foreach (Button button in Descendants(view).OfType<Button>().Where(b => b.Name == "ButtonShortcut")) {
                  shortcutNames.Add(((ShortcutViewModel)button.DataContext).Name);
                  spokenShortcutNames.Add(SpokenName(button));
                }
              }));
            }

            ShortcutViewModel command = windowVm.RootShortcuts.Single(s => s.IsProcessCommand);
            RunCommandViewModel runCommand = new RunCommandViewModel(command, false);
            problems.AddRange(Check(new RunCommandWindow(runCommand)));
            problems.AddRange(Check(new RunCommandExamplesWindow(runCommand)));
            problems.AddRange(Check(new SettingsWindow(windowVm)));
            problems.AddRange(Check(new ColorsWindow(windowVm)));
            problems.AddRange(Check(new AboutWindow()));
            problems.AddRange(Check(new ErrorWindow(new InvalidOperationException("Test error"))));
          }
        } finally {
          ShortcutViewModel.ProcessStarter = oldStarter;
        }
      });
      Assert.True(problems.Count == 0, "Accessibility problems:" + Environment.NewLine + string.Join(Environment.NewLine, problems.Distinct()));
      // Every shortcut of the tree was checked, in both windows, and is named after its shortcut.
      Assert.Equal(new[] { "Child file", "File", "Ping", "Tools", "Url" }, shortcutNames.Distinct().OrderBy(n => n));
      Assert.Equal(shortcutNames, spokenShortcutNames);
    }
  }
}
