using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using MDSHO.Core;
using MDSHO.Data;
using MDSHO.ViewModels;
using Xunit;

namespace MDSHO.Tests {

  /// <summary>
  /// Safety net for renames: builds the real views over a real (temporary) database, forces layout and open context menus,
  /// and fails when WPF reports any binding error (a renamed view model property that XAML still binds to by its old name).
  /// </summary>
  public class BindingSmokeTests {

    /// <summary>Collects everything WPF writes to the DataBindingSource trace at Warning level or above.</summary>
    private sealed class BindingErrorListener : TraceListener {
      public readonly List<string> Messages = new List<string>();
      private readonly StringBuilder current = new StringBuilder();
      public override void Write(string message) { current.Append(message); }
      public override void WriteLine(string message) {
        current.Append(message);
        Messages.Add(current.ToString());
        current.Clear();
      }
    }

    private static void Pump() {
      Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ContextIdle);
      Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.Background);
    }

    private static IEnumerable<T> VisualDescendants<T>(DependencyObject root) where T : DependencyObject {
      int count = VisualTreeHelper.GetChildrenCount(root);
      for (int i = 0; i < count; i++) {
        DependencyObject child = VisualTreeHelper.GetChild(root, i);
        if (child is T match) {
          yield return match;
        }
        foreach (T nested in VisualDescendants<T>(child)) {
          yield return nested;
        }
      }
    }

    private static void ShowOffScreen(Window window) {
      window.ShowActivated = false;
      window.WindowStartupLocation = WindowStartupLocation.Manual;
      window.Left = -10000;
      window.Top = -10000;
      window.Show();
      window.UpdateLayout();
      Pump();
    }

    private static void OpenAndCloseContextMenu(FrameworkElement owner) {
      ContextMenu menu = owner.ContextMenu;
      if (menu == null) {
        return;
      }
      menu.PlacementTarget = owner;
      menu.IsOpen = true;
      menu.UpdateLayout();
      Pump();
      foreach (MenuItem item in VisualDescendants<MenuItem>(menu)) {
        item.UpdateLayout();
      }
      Pump();
      menu.IsOpen = false;
    }

    /// <summary>Window with a clone, a group with children, and a file / folder / url / command shortcut.</summary>
    private static void SeedData() {
      DesktopWindowSeed.Seed();
    }

    private static class DesktopWindowSeed {
      public static void Seed() {
        var window = DataStore.Windows.LoadAll()[0];
        DataStore.Clones.Create(window.ID, "\\\\.\\DISPLAY1", 50, 50);
        DataStore.Shortcuts.Create(null, window.ID, "File", System.IO.Path.GetTempFileName(), ".lnk", "11111111111111111111111111111111");
        DataStore.Shortcuts.Create(null, window.ID, "Folder", System.IO.Path.GetTempPath(), ".lnk", "22222222222222222222222222222222");
        DataStore.Shortcuts.Create(null, window.ID, "Url", "https://example.com/", ".url", "33333333333333333333333333333333");
        var command = DataStore.Shortcuts.CreateProcessCommand(null, window.ID);
        DataStore.Shortcuts.UpdateProcessFileName(command.ID, "cmd.exe");
        DataStore.Shortcuts.UpdateProcessArguments(command.ID, "/K echo hi");
        var group = DataStore.Shortcuts.CreateGroup(null, window.ID);
        DataStore.Shortcuts.UpdateIsGroupExpanded(group.ID, true);
        DataStore.Shortcuts.Create(group.ID, null, "Child file", System.IO.Path.GetTempFileName(), ".lnk", "44444444444444444444444444444444");
        var inner = DataStore.Shortcuts.CreateGroup(group.ID, null);
        DataStore.Shortcuts.UpdateIsGroupExpanded(inner.ID, true);
        DataStore.Shortcuts.Create(inner.ID, null, "Child url", "https://example.org/", ".url", "55555555555555555555555555555555");
      }
    }

    [Fact]
    public void Views_report_no_binding_errors() {
      List<string> errors = new List<string>();
      Exception failure = null;
      WpfAppHost.Run(() => {
        BindingErrorListener listener = new BindingErrorListener();
        try {
          using (TempDb db = new TempDb()) {
            SeedData();

            PresentationTraceSources.Refresh();
            PresentationTraceSources.DataBindingSource.Listeners.Add(listener);
            PresentationTraceSources.DataBindingSource.Switch.Level = SourceLevels.Warning;

            AppViewModel appVm = ViewModelFactory.CreateAppViewModel();
            List<Window> windows = new List<Window>();
            foreach (DesktopWindowViewModel windowVm in appVm.Windows) {
              windows.Add(new DesktopWindowView(windowVm));
              foreach (DesktopWindowViewModel cloneVm in windowVm.Clones) {
                windows.Add(new DesktopWindowView(cloneVm));
              }
            }
            Assert.True(windows.Count >= 2, "expected a window and a clone");
            foreach (Window view in windows) {
              view.ShowActivated = false;
              view.Show();
              view.UpdateLayout();
              Pump();
              // Expand every tree item so the whole shortcut tree gets its container and bindings.
              TreeView tree = (TreeView)view.FindName("TreeViewShortcuts");
              Assert.NotNull(tree);
              for (int pass = 0; pass < 4; pass++) {
                foreach (TreeViewItem item in VisualDescendants<TreeViewItem>(tree)) {
                  item.IsExpanded = true;
                  item.UpdateLayout();
                }
                Pump();
              }
              Assert.True(VisualDescendants<TreeViewItem>(tree).Count() >= 8, "the whole shortcut tree should be rendered");
              // Rate / Share moved to the About window and the title menu; the settings gear is in the title bar (windows and clones).
              Assert.NotNull(view.FindName("ButtonHeaderSettings"));
              Assert.Null(view.FindName("ButtonRateAndReview"));
              Assert.Null(view.FindName("ButtonShare"));
              // Context menus bind separately from the window's visual tree.
              OpenAndCloseContextMenu(tree);
              OpenAndCloseContextMenu((FrameworkElement)view.FindName("GridHeader"));
              foreach (Button button in VisualDescendants<Button>(tree).Where(b => b.ContextMenu != null).ToList()) {
                OpenAndCloseContextMenu(button);
              }
              Pump();
            }
            // Settings dialog content (not shown modally: shown as a normal window).
            Window first = windows[0];
            SettingsWindow settings = new SettingsWindow((DesktopWindowViewModel)first.DataContext);
            settings.ShowActivated = false;
            settings.Show();
            settings.UpdateLayout();
            Pump();
            settings.Close();
            ColorsWindow colors = new ColorsWindow((DesktopWindowViewModel)first.DataContext);
            colors.ShowActivated = false;
            colors.Show();
            colors.UpdateLayout();
            Pump();
            colors.Close();
            AboutWindow about = new AboutWindow();
            about.ShowActivated = false;
            about.Show();
            about.UpdateLayout();
            Pump();
            Assert.Equal("Rate", new System.Windows.Documents.TextRange(((System.Windows.Documents.Hyperlink)about.FindName("HyperlinkRate")).ContentStart, ((System.Windows.Documents.Hyperlink)about.FindName("HyperlinkRate")).ContentEnd).Text.Trim());
            Assert.Equal("Share", new System.Windows.Documents.TextRange(((System.Windows.Documents.Hyperlink)about.FindName("HyperlinkShare")).ContentStart, ((System.Windows.Documents.Hyperlink)about.FindName("HyperlinkShare")).ContentEnd).Text.Trim());
            about.Close();
            // Run Command window and its Examples popup, over the seeded run command (shown off screen, never activated).
            ShortcutViewModel command = appVm.Windows[0].RootShortcuts.Single(s => s.IsProcessCommand);
            RunCommandViewModel runVm = new RunCommandViewModel(command, false);
            RunCommandWindow runWindow = new RunCommandWindow(runVm);
            ShowOffScreen(runWindow);
            RunCommandExamplesWindow examples = new RunCommandExamplesWindow(runVm);
            ShowOffScreen(examples);
            ListBox list = (ListBox)examples.FindName("ListExamples");
            Assert.Equal(RunCommandExamples.All.Count, list.Items.Count);
            // Select every example so each grouped item template and its tooltip (description and "Runs:" text) get bound.
            foreach (RunCommandExample example in RunCommandExamples.All) {
              list.SelectedItem = example;
              Assert.Equal(new[] { example.Description, example.RunsText }, RunCommandWindowTests.ExampleToolTipTexts(list, example));
            }
            examples.Close();
            runWindow.Close();
            Pump();
            // A note: its Note window and the Colours window as "Note colours".
            Action<ShortcutViewModel> oldNoteEditor = ShortcutViewModel.OpenNoteEditor;
            ShortcutViewModel.OpenNoteEditor = s => { };
            try {
              appVm.Windows[0].NewNoteShortcutCommand.Execute(null);
            } finally {
              ShortcutViewModel.OpenNoteEditor = oldNoteEditor;
            }
            ShortcutViewModel note = appVm.Windows[0].RootShortcuts.Last();
            TextEditWindow noteWindow = TextEditWindow.ForNote(note);
            ShowOffScreen(noteWindow);
            ColorsWindow noteColours = new ColorsWindow(note);
            ShowOffScreen(noteColours);
            noteColours.Close();
            noteWindow.Close();
            Pump();
            foreach (Window view in windows) {
              view.Close();
            }
            Pump();
          }
        } catch (Exception ex) {
          failure = ex;
        } finally {
          errors.AddRange(listener.Messages);
          PresentationTraceSources.DataBindingSource.Listeners.Remove(listener);
        }
      });
      if (failure != null) {
        throw new Exception("Smoke test setup failed: " + failure, failure);
      }
      Assert.True(errors.Count == 0, "WPF binding errors:" + Environment.NewLine + string.Join(Environment.NewLine, errors));
    }
  }
}
