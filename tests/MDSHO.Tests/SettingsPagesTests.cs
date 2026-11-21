using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using MDSHO.Data;
using MDSHO.Helpers;
using MDSHO.ViewModels;
using Xunit;

namespace MDSHO.Tests {

  /// <summary>
  /// The Settings window's pages: a list on the left (Appearance, General, Keyboard, Screens, Backups) and the chosen page on the
  /// right. Settings opens on the page used last, saved in AppSettings.
  /// </summary>
  public class SettingsPagesTests {

    private static readonly string[] PageNames = { "Appearance", "General", "Keyboard", "Screens", "Backups" };

    // The headings each page shows, in order.
    private static readonly Dictionary<string, string[]> PageHeadings = new Dictionary<string, string[]> {
      ["Appearance"] = new[] { "Icons appearance", "Window theme" },
      ["General"] = new[] { "Other", "Text history" },
      ["Keyboard"] = new[] { "Keyboard shortcut" },
      ["Screens"] = new[] { "Screen setups" },
      ["Backups"] = new[] { "Backups" },
    };

    private static SettingsWindow Open() {
      DesktopWindowViewModel w = new AppViewModel().AddWindow("TEST", 100, 100);
      SettingsWindow settings = new SettingsWindow(w) {
        WindowStartupLocation = WindowStartupLocation.Manual,
        Left = -10000,
        Top = -10000,
        ShowActivated = false,
        ShowInTaskbar = false
      };
      settings.Show();
      WpfAppHost.Pump();
      return settings;
    }

    private static void WithSettings(Action<SettingsWindow> check) {
      WpfAppHost.Run(() => {
        SettingsWindow settings = Open();
        try {
          check(settings);
        } finally {
          settings.Close();
          WindowThemes.Apply(WindowThemeKind.Light);
          WpfAppHost.Pump();
        }
      });
    }

    private static T Named<T>(FrameworkElement root, string name) where T : class {
      return Assert.IsAssignableFrom<T>(root.FindName(name));
    }

    private static ListBox Pages(Window window) {
      return Named<ListBox>(window, "ListSettingsPages");
    }

    private static List<ListBoxItem> Items(Window window) {
      return Pages(window).Items.Cast<ListBoxItem>().ToList();
    }

    private static string SelectedPage(Window window) {
      return (string)((ListBoxItem)Pages(window).SelectedItem).Content;
    }

    private static void Choose(Window window, string page) {
      Items(window).Single(i => (string)i.Content == page).IsSelected = true;
      WpfAppHost.Pump();
    }

    private static string[] VisibleHeadings(Window window) {
      return WpfAppHost.VisualDescendants<Label>(window).Where(l => l.IsVisible).Select(l => l.Content as string).ToArray();
    }

    private static string Hex(object brush) {
      return Assert.IsType<SolidColorBrush>(brush).Color.ToString();
    }

    [Fact]
    public void The_list_has_the_five_pages_in_order_with_names_for_screen_readers() {
      using (TempDb db = new TempDb()) {
        WithSettings(window => {
          ListBox list = Pages(window);
          Assert.Equal(PageNames, Items(window).Select(i => (string)i.Content).ToArray());
          Assert.Equal("Settings pages", AutomationProperties.GetName(list));
          Assert.All(Items(window), i => Assert.Equal((string)i.Content, AutomationProperties.GetName(i)));
          Assert.Equal(SelectionMode.Single, list.SelectionMode);
          Assert.Equal(140, Named<FrameworkElement>(window, "BorderSettingsPages").ActualWidth, 1);
          Assert.Equal(720, window.Width);
        });
      }
    }

    [Fact]
    public void Each_page_shows_only_its_own_sections_with_the_heading_style() {
      using (TempDb db = new TempDb()) {
        WithSettings(window => {
          foreach (string page in PageNames) {
            Choose(window, page);
            Assert.Equal(PageHeadings[page], VisibleHeadings(window));
            Assert.All(WpfAppHost.VisualDescendants<Label>(window).Where(l => l.IsVisible),
              l => Assert.Equal(window.FindResource("StyleSettingsCategory"), l.Style));
          }
          Choose(window, "Appearance");
          Assert.True(Named<RadioButton>(window, "RadioThemeDark").IsVisible);
          Assert.False(Named<CheckBox>(window, "CheckSingleClickToOpen").IsVisible);
          Assert.False(Named<Button>(window, "ButtonShortcutBox").IsVisible);
          Assert.False(Named<CheckBox>(window, "CheckRememberScreenSetups").IsVisible);
          Choose(window, "Screens");
          Assert.True(Named<CheckBox>(window, "CheckRememberScreenSetups").IsVisible);
          Assert.True(Named<TextBlock>(window, "TextScreenSetupsHint").IsVisible);
          Assert.False(Named<RadioButton>(window, "RadioThemeDark").IsVisible);
        });
      }
    }

    [Fact]
    public void The_height_stays_the_same_on_every_page_and_every_page_fits_without_scrolling() {
      using (TempDb db = new TempDb()) {
        WithSettings(window => {
          FrameworkElement body = Named<FrameworkElement>(window, "PanelBody");
          // No page scrolls (a text box's own scroll viewer doesn't count).
          Assert.Empty(WpfAppHost.VisualDescendants<ScrollViewer>(body).Where(v => !(v.TemplatedParent is System.Windows.Controls.Primitives.TextBoxBase)));
          double height = window.ActualHeight;
          foreach (string page in PageNames) {
            Choose(window, page);
            Assert.Equal(height, window.ActualHeight, 0.5);
            FrameworkElement panel = Named<FrameworkElement>(window, "Page" + page);
            Assert.True(panel.IsVisible, page);
            panel.Measure(new Size(panel.ActualWidth, double.PositiveInfinity));
            Assert.True(panel.DesiredSize.Height <= body.ActualHeight + 0.5, page + " is taller than the body.");
            Point bottom = panel.TranslatePoint(new Point(0, panel.ActualHeight), body);
            Assert.True(bottom.Y <= body.ActualHeight + 0.5, page + " is clipped.");
          }
        });
      }
    }

    [Fact]
    public void Settings_opens_on_Appearance_the_first_time_and_then_on_the_page_used_last() {
      using (TempDb db = new TempDb()) {
        WithSettings(window => {
          Assert.Equal("Appearance", SelectedPage(window));
          Assert.Equal(PageHeadings["Appearance"], VisibleHeadings(window));
          Choose(window, "Keyboard");
        });
        Assert.Equal("Keyboard", DataStore.AppSettings.Get(SettingsWindow.PageSettingKey));
        WithSettings(window => {
          Assert.Equal("Keyboard", SelectedPage(window));
          Assert.Equal(PageHeadings["Keyboard"], VisibleHeadings(window));
        });
      }
    }

    [Fact]
    public void An_unknown_saved_page_opens_Appearance() {
      using (TempDb db = new TempDb()) {
        DataStore.AppSettings.Set(SettingsWindow.PageSettingKey, "Nowhere");
        WithSettings(window => Assert.Equal("Appearance", SelectedPage(window)));
      }
    }

    [Fact]
    public void Apply_works_for_every_page_at_once() {
      using (TempDb db = new TempDb()) {
        WithSettings(window => {
          Choose(window, "General");
          Assert.True(Named<Button>(window, "ButtonApply").IsVisible);
          Choose(window, "Screens");
          Assert.True(Named<Button>(window, "ButtonApply").IsVisible);
        });
      }
    }

    [Fact]
    public void The_list_uses_the_theme_brushes_in_Light_and_Dark() {
      using (TempDb db = new TempDb()) {
        WithSettings(window => {
          foreach (WindowThemeKind kind in new[] { WindowThemeKind.Light, WindowThemeKind.Dark }) {
            WindowThemes.Apply(kind);
            WpfAppHost.Pump();
            ResourceDictionary d = WindowThemes.ActiveDictionary;
            ListBoxItem selected = (ListBoxItem)Pages(window).ItemContainerGenerator.ContainerFromItem(Pages(window).SelectedItem);
            Border itemBorder = Assert.IsType<Border>(selected.Template.FindName("ItemBorder", selected));
            Border bar = Assert.IsType<Border>(selected.Template.FindName("SelectionBar", selected));
            Assert.Equal(Hex(d["Theme.ItemSelected"]), Hex(itemBorder.Background));
            Assert.Equal(Hex(d["Theme.Accent"]), Hex(bar.Background));
            Assert.Equal(3, bar.ActualWidth, 0.5);
            Assert.True(bar.IsVisible);
            ListBoxItem other = Items(window).First(i => !i.IsSelected);
            Assert.False(((Border)other.Template.FindName("SelectionBar", other)).IsVisible);
            Border separator = Named<Border>(window, "BorderSettingsPages");
            Assert.Equal(Hex(d["Theme.BarBackground"]), Hex(separator.BorderBrush));
            Assert.Equal(Hex(d["Theme.Text"]), Hex(selected.Foreground));
          }
          // Hover uses Theme.ItemHover (a DynamicResource, so it follows the theme).
          ListBoxItem item = Items(window)[0];
          Setter hover = item.Template.Triggers.OfType<Trigger>()
            .Where(t => t.Property == UIElement.IsMouseOverProperty)
            .SelectMany(t => t.Setters.OfType<Setter>())
            .Single(s => s.Property == Border.BackgroundProperty);
          Assert.Equal("Theme.ItemHover", Assert.IsType<DynamicResourceExtension>(hover.Value).ResourceKey);
        });
      }
    }

    [Fact]
    public void Down_and_Up_move_through_the_list_and_Tab_goes_into_the_page() {
      using (TempDb db = new TempDb()) {
        WpfAppHost.Run(() => {
          SettingsWindow window = Open();
          try {
            ListBox list = Pages(window);
            Assert.Equal(KeyboardNavigationMode.Once, KeyboardNavigation.GetTabNavigation(list));
            ListBoxItem first = Items(window)[0];
            Assert.True(first.Focus());
            WpfAppHost.Pump();
            Press(first, Key.Down);
            Assert.Equal("General", SelectedPage(window));
            Press((UIElement)Keyboard.FocusedElement, Key.Down);
            Assert.Equal("Keyboard", SelectedPage(window));
            Press((UIElement)Keyboard.FocusedElement, Key.Up);
            Assert.Equal("General", SelectedPage(window));
            // Tab leaves the list for the first control of the shown page.
            ((UIElement)Keyboard.FocusedElement).MoveFocus(new TraversalRequest(FocusNavigationDirection.Next));
            WpfAppHost.Pump();
            Assert.Same(Named<CheckBox>(window, "CheckSingleClickToOpen"), Keyboard.FocusedElement);
          } finally {
            window.Close();
            WpfAppHost.Pump();
          }
        });
      }
    }

    private static void Press(UIElement target, Key key) {
      KeyEventArgs args = new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(target), 0, key) { RoutedEvent = Keyboard.KeyDownEvent };
      target.RaiseEvent(args);
      WpfAppHost.Pump();
    }
  }
}
