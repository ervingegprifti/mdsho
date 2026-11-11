using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using MDSHO.ViewModels;
using Xunit;

namespace MDSHO.Tests {

  /// <summary>
  /// The view models expose plain values (bool, string, enums) instead of WPF types; XAML converters in MDSHO.App turn
  /// them into Visibility, brushes and corner radii. The view tests check that the windows still look the same.
  /// </summary>
  public class PlainViewModelTests {

    /// <summary>Public properties of the view model types whose type is <paramref name="wpfType"/>.</summary>
    internal static List<string> PropertiesOfType(Type wpfType) {
      return typeof(AppViewModel).Assembly.GetExportedTypes()
        .SelectMany(t => t.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly)
          .Where(p => wpfType.IsAssignableFrom(p.PropertyType))
          .Select(p => t.Name + "." + p.Name))
        .ToList();
    }

    private static T Named<T>(FrameworkElement root, string name) where T : class {
      return (T)root.FindName(name);
    }

    #region VISIBILITY
    [Fact]
    public void No_view_model_property_is_a_Visibility() {
      Assert.Empty(PropertiesOfType(typeof(Visibility)));
    }

    [Fact]
    public void Show_flags_follow_the_shortcut_kind() {
      ShortcutViewModel group = new ShortcutViewModel(null, null, 1, null, "g", "", "", "g1", true, false, null, true, null, 0, false, "", "");
      ShortcutViewModel file = new ShortcutViewModel(null, null, 2, null, "f", @"C:\x.txt", ".lnk", "g2", false, false, null, false, null, 0, false, "", "");
      ShortcutViewModel command = new ShortcutViewModel(null, null, 3, null, "c", "", "", "g3", false, false, null, false, null, 0, true, "cmd.exe", "");
      Assert.True(group.ShowGroup);
      Assert.True(group.ShowNewShortcut);
      Assert.False(group.ShowSetNameTheSameAsTarget);
      Assert.False(group.ShowCopyTargetToMemory);
      Assert.False(group.ShowShortcut);
      Assert.False(group.ShowCommand);
      Assert.True(group.ShowMark);
      Assert.False(file.ShowGroup);
      Assert.True(file.ShowShortcut);
      Assert.True(file.ShowSetNameTheSameAsTarget);
      Assert.True(file.ShowCopyTargetToMemory);
      Assert.False(file.ShowCommand);
      Assert.False(file.ShowMark);
      Assert.True(command.ShowCommand);
      Assert.False(command.ShowShortcut);
    }

    [Fact]
    public void Header_buttons_and_hints_are_shown_or_collapsed_as_before() {
      using (TempDb db = new TempDb()) {
        WpfAppHost.Run(() => {
          AppViewModel app = ViewModelFactory.CreateAppViewModel();
          DesktopWindowViewModel win = app.Windows[0];
          win.CloneWindow("TEST", 0, 0);
          DesktopWindowViewModel clone = win.Clones[0];
          Assert.True(win.ShowCloneWindow);
          Assert.False(win.ShowCloseWindow);
          Assert.True(clone.ShowCloseWindow);
          Assert.False(clone.ShowCloneWindow);
          Assert.True(win.ShowDragItemsHereInfo);

          foreach (DesktopWindowViewModel vm in new[] { win, clone }) {
            DesktopWindowView view = new DesktopWindowView(vm) { ShowActivated = false, ShowInTaskbar = false, Left = -10000, Top = -10000 };
            view.Show();
            try {
              WpfAppHost.Pump();
              Visibility cloneButton = Named<Button>(view, "ButtonHeaderCloneThisWindowToAllScreens").Visibility;
              Visibility closeButton = Named<Button>(view, "ButtonHeaderCloseWindow").Visibility;
              Assert.Equal(vm.IsClone ? Visibility.Collapsed : Visibility.Visible, cloneButton);
              Assert.Equal(vm.IsClone ? Visibility.Visible : Visibility.Collapsed, closeButton);
              // The title menu binds once it has the window's data context (as when it opens).
              MenuItem cloneItem = Named<MenuItem>(view, "MenuItemHeaderCloneThisWindowToAllScreens");
              DependencyObject menu = cloneItem;
              while (!(menu is ContextMenu)) { menu = LogicalTreeHelper.GetParent(menu); }
              ((ContextMenu)menu).DataContext = vm;
              WpfAppHost.Pump();
              Assert.Equal(vm.IsClone ? Visibility.Collapsed : Visibility.Visible, cloneItem.Visibility);
              Assert.Equal(vm.IsClone ? Visibility.Visible : Visibility.Collapsed, Named<MenuItem>(view, "MenuItemHeaderCloseWindow").Visibility);
            } finally {
              view.Close();
            }
          }
        });
      }
    }
    #endregion VISIBILITY

    #region CORNER RADIUS
    private static readonly System.Globalization.CultureInfo Invariant = System.Globalization.CultureInfo.InvariantCulture;

    [Fact]
    public void No_view_model_property_is_a_CornerRadius() {
      Assert.Empty(PropertiesOfType(typeof(CornerRadius)));
    }

    [Fact]
    public void Docked_corner_converter_gives_square_corners_when_docked_else_the_parameter() {
      MDSHO.Converters.DockedCornerRadiusConverter converter = new MDSHO.Converters.DockedCornerRadiusConverter();
      Assert.Equal(new CornerRadius(5, 5, 0, 0), converter.Convert(false, typeof(CornerRadius), "5,5,0,0", Invariant));
      Assert.Equal(new CornerRadius(0, 0, 0, 5), converter.Convert(false, typeof(CornerRadius), "0,0,0,5", Invariant));
      Assert.Equal(new CornerRadius(0), converter.Convert(true, typeof(CornerRadius), "5,5,0,0", Invariant));
      Assert.Throws<NotSupportedException>(() => converter.ConvertBack(new CornerRadius(0), typeof(bool), "5,5,0,0", Invariant));
    }

    private static CornerRadius ButtonCorners(Button button) {
      return VisualDescendants<Border>(button).First().CornerRadius;
    }

    private static IEnumerable<T> VisualDescendants<T>(DependencyObject root) where T : DependencyObject {
      for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++) {
        DependencyObject child = VisualTreeHelper.GetChild(root, i);
        if (child is T match) {
          yield return match;
        }
        foreach (T nested in VisualDescendants<T>(child)) {
          yield return nested;
        }
      }
    }

    [Fact]
    public void Window_corners_are_rounded_when_floating_and_square_when_docked() {
      using (TempDb db = new TempDb()) {
        WpfAppHost.Run(() => {
          DesktopWindowViewModel w = new AppViewModel().AddWindow("TEST", 100, 100);
          DesktopWindowView view = new DesktopWindowView(w) { ShowActivated = false, ShowInTaskbar = false, Left = -10000, Top = -10000 };
          view.Show();
          try {
            WpfAppHost.Pump();
            Assert.False(w.IsDocked);
            Assert.Equal(new CornerRadius(5, 5, 0, 0), Named<Border>(view, "BorderHeaderRoundedCorners").CornerRadius);
            Assert.Equal(new CornerRadius(0, 0, 5, 5), Named<Border>(view, "BorderBodyRoundedCorners").CornerRadius);
            Assert.Equal(new CornerRadius(0, 5, 0, 0), ButtonCorners(Named<Button>(view, "ButtonHeaderCloneThisWindowToAllScreens")));

            w.ApplyDock(-10000, -10000, 500);
            WpfAppHost.Pump();
            Assert.Equal(new CornerRadius(0), Named<Border>(view, "BorderHeaderRoundedCorners").CornerRadius);
            Assert.Equal(new CornerRadius(0), Named<Border>(view, "BorderBodyRoundedCorners").CornerRadius);
            Assert.Equal(new CornerRadius(0), ButtonCorners(Named<Button>(view, "ButtonHeaderCloneThisWindowToAllScreens")));
          } finally {
            view.Close();
          }
        });
      }
    }
    #endregion CORNER RADIUS

    #region COLOURS
    [Fact]
    public void No_view_model_property_is_a_Brush_or_Color() {
      Assert.Empty(PropertiesOfType(typeof(Brush)));
      Assert.Empty(PropertiesOfType(typeof(Color)));
    }

    [Theory]
    [InlineData("#ff112233", "#FF112233")]
    [InlineData("#80AbCdEf", "#80ABCDEF")]
    [InlineData("#FFFFFF", "#FFFFFFFF")]
    [InlineData("", "#FFFFFFFF")]
    [InlineData(null, "#FFFFFFFF")]
    [InlineData("#GG112233", "#FFFFFFFF")]
    public void NormalizeHex_upper_cases_valid_colours_and_turns_invalid_ones_white(string input, string expected) {
      Assert.Equal(expected, MDSHO.Common.ColorConversions.NormalizeHex(input));
    }

    [Fact]
    public void Hex_to_brush_converter_makes_the_colour_and_falls_back_to_white() {
      MDSHO.Converters.HexToBrushConverter converter = new MDSHO.Converters.HexToBrushConverter();
      Assert.Equal(Color.FromArgb(0x80, 0x11, 0x22, 0x33), ((SolidColorBrush)converter.Convert("#80112233", typeof(Brush), null, Invariant)).Color);
      Assert.Equal(Colors.White, ((SolidColorBrush)converter.Convert("bad", typeof(Brush), null, Invariant)).Color);
      Assert.Equal(Colors.White, ((SolidColorBrush)converter.Convert(null, typeof(Brush), null, Invariant)).Color);
      Assert.Throws<NotSupportedException>(() => converter.ConvertBack(Brushes.Red, typeof(string), null, Invariant));
    }

    [Fact]
    public void Hex_to_color_converter_works_both_ways_for_the_colour_picker() {
      MDSHO.Converters.HexToColorConverter converter = new MDSHO.Converters.HexToColorConverter();
      Assert.Equal(Color.FromArgb(0x80, 0x11, 0x22, 0x33), converter.Convert("#80112233", typeof(Color), null, Invariant));
      Assert.Equal(Colors.White, converter.Convert("bad", typeof(Color), null, Invariant));
      Assert.Equal("#80112233", converter.ConvertBack(Color.FromArgb(0x80, 0x11, 0x22, 0x33), typeof(string), null, Invariant));
    }

    [Fact]
    public void Shortcut_mark_colour_is_kept_as_normalised_hex_and_saved() {
      using (TempDb db = new TempDb()) {
        AppViewModel app = new AppViewModel();
        DesktopWindowViewModel win = app.AddWindow("TEST", 0, 0);
        ShortcutViewModel group = ViewModelFactory.NewGroupShortcut(app, null, win);
        win.RootShortcuts.Add(group);
        group.MarkColor = "#FF00FF00";
        using (Microsoft.Data.Sqlite.SqliteConnection c = db.Open())
        using (Microsoft.Data.Sqlite.SqliteCommand cmd = c.CreateCommand()) {

          cmd.CommandText = "SELECT MarkColor FROM Shortcuts WHERE ID = " + group.ID;
          Assert.Equal("#FF00FF00", (string)cmd.ExecuteScalar());
        }
      }
    }

    [Fact]
    public void Window_colours_reach_the_view_and_the_colour_picker_writes_them_back() {
      using (TempDb db = new TempDb()) {
        WpfAppHost.Run(() => {
          DesktopWindowViewModel w = new AppViewModel().AddWindow("TEST", 100, 100);
          w.Appearance.BorderColor = "#FF102030";
          w.Appearance.TitleBackgroundColor = "#CC405060";
          w.Appearance.BackgroundColor = "#99708090";
          DesktopWindowView view = new DesktopWindowView(w) { ShowActivated = false, ShowInTaskbar = false, Left = -10000, Top = -10000 };
          view.Show();
          try {
            WpfAppHost.Pump();
            Border header = Named<Border>(view, "BorderHeaderRoundedCorners");
            Border body = Named<Border>(view, "BorderBodyRoundedCorners");
            Assert.Equal(Color.FromArgb(0xFF, 0x10, 0x20, 0x30), ((SolidColorBrush)header.BorderBrush).Color);
            Assert.Equal(Color.FromArgb(0xCC, 0x40, 0x50, 0x60), ((SolidColorBrush)header.Background).Color);
            Assert.Equal(Color.FromArgb(0x99, 0x70, 0x80, 0x90), ((SolidColorBrush)body.Background).Color);
            w.Appearance.BorderColor = "#FF0000FF";
            WpfAppHost.Pump();
            Assert.Equal(Color.FromArgb(0xFF, 0, 0, 0xFF), ((SolidColorBrush)header.BorderBrush).Color);
          } finally {
            view.Close();
          }

          SettingsWindow settings = new SettingsWindow(w) { ShowActivated = false, ShowInTaskbar = false, Left = -10000, Top = -10000 };
          settings.Show();
          try {
            WpfAppHost.Pump();
            // One palette for the selected colour button (Border at first); the Text button is the fifth.
            ColorPicker.PickerControlBase palette = (ColorPicker.PickerControlBase)settings.FindName("PickerColor");
            Assert.Equal(Color.FromArgb(0xFF, 0, 0, 0xFF), palette.SelectedColor);
            ((System.Windows.Controls.ListBox)settings.FindName("ListColorTargets")).SelectedIndex = 4;
            WpfAppHost.Pump();
            palette.SelectedColor = Color.FromArgb(0xFF, 0x12, 0x80, 0x34);
            WpfAppHost.Pump();
            Assert.Equal("#FF128034", w.Appearance.TextColor);
          } finally {
            settings.Close();
          }
          // Closing without Apply puts the old colours back.
          Assert.NotEqual("#FF128034", w.Appearance.TextColor);
        });
      }
    }
    [Fact]
    public void Mark_command_takes_the_colour_text_and_an_empty_one_removes_the_mark() {
      using (TempDb db = new TempDb()) {
        AppViewModel app = new AppViewModel();
        DesktopWindowViewModel win = app.AddWindow("TEST", 0, 0);
        ShortcutViewModel group = ViewModelFactory.NewGroupShortcut(app, null, win);
        win.RootShortcuts.Add(group);
        group.MarkShortcutCommand.Execute("#ffff0000");
        Assert.True(group.IsMarked);
        Assert.Equal("#FFFF0000", group.MarkColor);
        group.MarkShortcutCommand.Execute("");
        Assert.False(group.IsMarked);
      }
    }

    [Fact]
    public void Mark_menu_items_pass_their_colour_tag_to_the_mark_command() {
      string xaml = System.IO.File.ReadAllText(System.IO.Path.Combine(ThirdPartyNoticesTests.RepoRoot(), "src", "MDSHO.App", "DesktopWindowView.xaml"));
      Assert.Contains("<Setter Property=\"CommandParameter\" Value=\"{Binding Tag, RelativeSource={RelativeSource Self}}\"/>", xaml);
    }
    #endregion COLOURS

    #region KEYS, COMMANDS, DIALOGS
    /// <summary>Public methods of the view model types with a parameter from <paramref name="assemblyName"/>.</summary>
    internal static List<string> MethodsWithParametersFrom(string assemblyName) {
      return typeof(AppViewModel).Assembly.GetExportedTypes()
        .SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly)
          .Where(m => m.GetParameters().Any(p => p.ParameterType.Assembly.GetName().Name == assemblyName))
          .Select(m => t.Name + "." + m.Name))
        .ToList();
    }

    [Fact]
    public void Key_handling_takes_plain_keys() {
      Assert.DoesNotContain("DesktopWindowViewModel.HandleKey", MethodsWithParametersFrom("WindowsBase"));
      Assert.DoesNotContain("DesktopWindowViewModel.HandleKey", MethodsWithParametersFrom("PresentationCore"));
    }

    [Fact]
    public void Key_input_maps_WPF_keys_to_the_shortcut_keys() {
      Assert.Equal(ShortcutKey.V, Helpers.KeyInput.ToShortcutKey(System.Windows.Input.Key.V));
      Assert.Equal(ShortcutKey.C, Helpers.KeyInput.ToShortcutKey(System.Windows.Input.Key.C));
      Assert.Equal(ShortcutKey.X, Helpers.KeyInput.ToShortcutKey(System.Windows.Input.Key.X));
      Assert.Equal(ShortcutKey.F2, Helpers.KeyInput.ToShortcutKey(System.Windows.Input.Key.F2));
      Assert.Equal(ShortcutKey.Delete, Helpers.KeyInput.ToShortcutKey(System.Windows.Input.Key.Delete));
      Assert.Equal(ShortcutKey.Enter, Helpers.KeyInput.ToShortcutKey(System.Windows.Input.Key.Enter));
      Assert.Equal(ShortcutKey.Other, Helpers.KeyInput.ToShortcutKey(System.Windows.Input.Key.A));
      Assert.Equal(ShortcutModifiers.None, Helpers.KeyInput.ToShortcutModifiers(System.Windows.Input.ModifierKeys.None));
      Assert.Equal(ShortcutModifiers.Control, Helpers.KeyInput.ToShortcutModifiers(System.Windows.Input.ModifierKeys.Control));
      Assert.Equal(ShortcutModifiers.Other, Helpers.KeyInput.ToShortcutModifiers(System.Windows.Input.ModifierKeys.Control | System.Windows.Input.ModifierKeys.Shift));
      Assert.Equal(ShortcutModifiers.Other, Helpers.KeyInput.ToShortcutModifiers(System.Windows.Input.ModifierKeys.Alt));
    }

    [Fact]
    public void Commands_come_from_the_MVVM_toolkit_and_the_old_relay_command_is_gone() {
      Assert.DoesNotContain(typeof(AppViewModel).Assembly.GetTypes(), t => t.Name == "RelayCommand" || t.Name == "ViewModelBase");
      Assert.Equal("CommunityToolkit.Mvvm", typeof(ShortcutViewModel).GetProperty(nameof(ShortcutViewModel.PasteShortcutCommand)).PropertyType.Assembly.GetName().Name);
      Assert.True(typeof(CommunityToolkit.Mvvm.ComponentModel.ObservableObject).IsAssignableFrom(typeof(ShortcutViewModel)));
    }

    [Fact]
    public void In_the_app_dialogs_are_message_boxes() {
      Func<string, string, bool> oldConfirm = Dialogs.Confirm;
      Action<string, string> oldShow = Dialogs.ShowMessage;
      try {
        Helpers.WpfBridge.Connect();
        Assert.Equal("MDSHO", Dialogs.Confirm.Method.Module.Assembly.GetName().Name);
        Assert.Equal("MDSHO", Dialogs.ShowMessage.Method.Module.Assembly.GetName().Name);
      } finally {
        Dialogs.Confirm = oldConfirm;
        Dialogs.ShowMessage = oldShow;
      }
    }
    #endregion KEYS, COMMANDS, DIALOGS
  }
}
