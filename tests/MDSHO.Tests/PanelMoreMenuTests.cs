using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using MDSHO.ViewModels;
using Xunit;

namespace MDSHO.Tests {

  /// <summary>
  /// The panel's More options button (•••) sits just left of Clone (or Close this clone) and opens the title menu, the
  /// same one a right-click on the title opens. Settings and Colours moved from the title bar to the top of that menu.
  /// </summary>
  public class PanelMoreMenuTests {

    private static void WithView(bool clone, Action<DesktopWindowViewModel, DesktopWindowView> body) {
      using (TempDb db = new TempDb()) {
        WpfAppHost.Run(() => {
          DesktopWindowViewModel w = new AppViewModel().AddWindow("TEST", 100, 100);
          if (clone) {
            w.CloneWindow("TEST", 0, 0);
            w = w.Clones[0];
          }
          DesktopWindowView view = new DesktopWindowView(w) { ShowActivated = false, ShowInTaskbar = false, Left = -10000, Top = -10000 };
          view.Show();
          try {
            WpfAppHost.Pump();
            body(w, view);
          } finally {
            view.Close();
          }
        });
      }
    }

    private static T Named<T>(FrameworkElement root, string name) where T : class {
      return Assert.IsAssignableFrom<T>(root.FindName(name));
    }

    private static ContextMenu TitleMenu(DesktopWindowView view) {
      return Assert.IsType<ContextMenu>(Named<Grid>(view, "GridHeader").ContextMenu);
    }

    private static Rect BoundsIn(FrameworkElement element, FrameworkElement root) {
      return element.TransformToAncestor(root).TransformBounds(new Rect(element.RenderSize));
    }

    [Theory]
    [InlineData(false, "ButtonHeaderCloneThisWindowToAllScreens")]
    [InlineData(true, "ButtonHeaderCloseWindow")]
    public void More_sits_just_left_of_Clone_or_Close_with_its_tooltip(bool clone, string rightButton) {
      WithView(clone, (w, view) => {
        Grid header = Named<Grid>(view, "GridHeader");
        Button more = Named<Button>(view, "ButtonHeaderMore");
        Button right = Named<Button>(view, rightButton);
        Button dockRight = Named<Button>(view, "ButtonHeaderDockWindowRight");
        Assert.Same(header, more.Parent);
        Assert.Equal(Visibility.Visible, more.Visibility);
        Assert.Equal(Visibility.Visible, right.Visibility);
        Assert.Equal(BoundsIn(right, header).Left, BoundsIn(more, header).Right, 1);
        Assert.Equal(BoundsIn(more, header).Left, BoundsIn(dockRight, header).Right, 1);
        Assert.Equal("More options", more.ToolTip);
        Assert.Equal("More options", System.Windows.Automation.AutomationProperties.GetName(more));
        // Same look and keyboard behaviour as the other title buttons.
        Assert.Same(dockRight.Style, more.Style);
        Assert.Equal(dockRight.Focusable, more.Focusable);
        Assert.Equal(dockRight.ActualWidth, more.ActualWidth, 1);
        // Three dots in Segoe UI, as on the note title bar, following the icon set.
        Assert.Equal(w.Appearance.Icons.More, more.Content);
        Assert.Equal("Segoe UI", more.FontFamily.Source);
        w.Appearance.SolidIcons = !w.Appearance.SolidIcons;
        WpfAppHost.Pump();
        Assert.Equal(w.Appearance.Icons.More, more.Content);
      });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void The_Settings_and_Colours_buttons_are_gone_from_the_title_bar(bool clone) {
      WithView(clone, (w, view) => {
        Assert.Null(view.FindName("ButtonHeaderSettings"));
        Assert.Null(view.FindName("ButtonHeaderColors"));
        Grid header = Named<Grid>(view, "GridHeader");
        // Icon, title, Dock left, Dock right, More, Clone / Close.
        Assert.Equal(6, header.ColumnDefinitions.Count);
      });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void The_title_menu_starts_with_Settings_and_Colours_with_their_icons(bool clone) {
      WithView(clone, (w, view) => {
        ContextMenu menu = TitleMenu(view);
        MenuItem settings = Assert.IsType<MenuItem>(menu.Items[0]);
        MenuItem colours = Assert.IsType<MenuItem>(menu.Items[1]);
        Assert.IsType<Separator>(menu.Items[2]);
        Assert.Equal("MenuItemHeaderSettings", settings.Name);
        Assert.Equal("Settings...", settings.Header);
        Assert.Equal("MenuItemHeaderColors", colours.Name);
        Assert.Equal("Colours...", colours.Header);
        foreach (Tuple<MenuItem, string> item in new[] { Tuple.Create(settings, "Appearance.Icons.Settings"), Tuple.Create(colours, "Appearance.Icons.Brush") }) {
          TextBlock icon = Assert.IsType<TextBlock>(item.Item1.Icon);
          BindingExpression binding = BindingOperations.GetBindingExpression(icon, TextBlock.TextProperty);
          Assert.NotNull(binding);
          Assert.Equal(item.Item2, binding.ParentBinding.Path.Path);
          Assert.Equal(Application.Current.FindResource("StyleMenuItemFontIcon"), icon.Style);
        }
      });
    }

    [Theory]
    [InlineData("MenuItemHeaderSettings", typeof(SettingsWindow))]
    [InlineData("MenuItemHeaderColors", typeof(ColorsWindow))]
    public void Settings_and_Colours_in_the_menu_open_their_window_20_pixels_beside_the_panel(string name, Type windowType) {
      WithView(false, (w, view) => {
        Func<Window, bool?> old = DesktopWindowView.ShowSettingsDialog;
        Window shown = null;
        DesktopWindowView.ShowSettingsDialog = s => { shown = s; return false; };
        try {
          MenuItem item = Named<MenuItem>(view, name);
          item.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent, item));
          WpfAppHost.Pump();
        } finally {
          DesktopWindowView.ShowSettingsDialog = old;
        }
        Assert.NotNull(shown);
        Assert.IsType(windowType, shown);
        Assert.Same(w, shown.DataContext);
        Assert.Same(view, shown.Owner);
        Assert.Equal(WindowStartupLocation.Manual, shown.WindowStartupLocation);
        Assert.Equal(view.Left + view.ActualWidth + DesktopWindowView.DialogGap, shown.Left, 1);
        shown.Close();
      });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void More_opens_the_same_title_menu_below_the_button_with_right_edges_lined_up(bool clone) {
      WithView(clone, (w, view) => {
        Button more = Named<Button>(view, "ButtonHeaderMore");
        ContextMenu menu = TitleMenu(view);
        more.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent, more));
        WpfAppHost.Pump();
        try {
          Assert.True(menu.IsOpen);
          Assert.Same(menu, TitleMenu(view));
          Assert.Same(more, menu.PlacementTarget);
          Assert.Equal(PlacementMode.Custom, menu.Placement);
          CustomPopupPlacement first = menu.CustomPopupPlacementCallback(new Size(200, 150), new Size(32, 32), new Point())[0];
          Assert.Equal(new Point(-168, 32), first.Point);
          // The items that depend on the window are set as for a right-click: no Delete window on a clone or the last panel.
          Assert.Equal(Visibility.Collapsed, Named<MenuItem>(view, "MenuItemHeaderDeleteWindow").Visibility);
        } finally {
          menu.IsOpen = false;
          WpfAppHost.Pump();
        }
        // A right-click afterwards opens it at the mouse again.
        Assert.Null(menu.PlacementTarget);
        Assert.Equal(PlacementMode.MousePoint, menu.Placement);
      });
    }

    [Fact]
    public void Alt_F_opens_the_title_menu_below_More() {
      WithView(false, (w, view) => {
        Func<ModifierKeys> old = DesktopWindowView.KeyboardModifiers;
        DesktopWindowView.KeyboardModifiers = () => ModifierKeys.Alt;
        ContextMenu menu = TitleMenu(view);
        try {
          TreeView tree = Named<TreeView>(view, "TreeViewShortcuts");
          KeyEventArgs args = new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(tree), 0, Key.F) { RoutedEvent = Keyboard.PreviewKeyDownEvent };
          typeof(KeyEventArgs).GetMethod("MarkSystem", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).Invoke(args, null);
          tree.RaiseEvent(args);
          WpfAppHost.Pump();
          Assert.True(args.Handled);
          Assert.True(menu.IsOpen);
          Assert.Same(Named<Button>(view, "ButtonHeaderMore"), menu.PlacementTarget);
        } finally {
          DesktopWindowView.KeyboardModifiers = old;
          menu.IsOpen = false;
          WpfAppHost.Pump();
        }
      });
    }
  }
}
