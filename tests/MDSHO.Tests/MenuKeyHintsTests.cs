using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using MDSHO.Helpers;
using MDSHO.ViewModels;
using Xunit;

namespace MDSHO.Tests {

  /// <summary>
  /// Panel menus show the keys a panel already handles (F2, Delete, Ctrl+X/C/V) on the right of Rename, Cut, Copy,
  /// Paste and Delete. The texts live in <see cref="ShortcutKeyTexts"/> and must name keys HandleKey really acts on.
  /// </summary>
  public class MenuKeyHintsTests {

    // ---- The texts match the keys HandleKey handles ----

    /// <summary>Reads a text like "Ctrl+X" back into the key and modifiers HandleKey takes.</summary>
    private static (ShortcutKey Key, ShortcutModifiers Modifiers) Parse(string text) {
      Assert.DoesNotContain(" ", text);
      string[] parts = text.Split('+');
      Assert.InRange(parts.Length, 1, 2);
      ShortcutModifiers modifiers = ShortcutModifiers.None;
      if (parts.Length == 2) {
        Assert.Equal("Ctrl", parts[0]);
        modifiers = ShortcutModifiers.Control;
      }
      Assert.True(Enum.TryParse(parts[^1], false, out ShortcutKey key), "Not a handled key: " + text);
      Assert.NotEqual(ShortcutKey.Other, key);
      return (key, modifiers);
    }

    private sealed class Panel : IDisposable {
      public readonly TempDb Db = new TempDb();
      public readonly AppViewModel App = new AppViewModel();
      public readonly DesktopWindowViewModel Win;
      public Panel() { Win = App.AddWindow("TEST", 0, 0); }
      public ShortcutViewModel Group() {
        ShortcutViewModel g = ViewModelFactory.NewGroupShortcut(App, null, Win);
        Win.RootShortcuts.Add(g);
        return g;
      }
      public bool Press(string text, ShortcutViewModel selected) {
        (ShortcutKey key, ShortcutModifiers modifiers) = Parse(text);
        return Win.HandleKey(key, modifiers, selected, false);
      }
      public void Dispose() { Db.Dispose(); }
    }

    [Fact]
    public void The_texts_are_F2_Ctrl_X_C_V_and_Delete_without_spaces() {
      Assert.Equal("F2", ShortcutKeyTexts.Rename);
      Assert.Equal("Ctrl+X", ShortcutKeyTexts.Cut);
      Assert.Equal("Ctrl+C", ShortcutKeyTexts.Copy);
      Assert.Equal("Ctrl+V", ShortcutKeyTexts.Paste);
      Assert.Equal("Delete", ShortcutKeyTexts.Delete);
    }

    [Fact]
    public void The_Rename_text_is_the_key_that_starts_a_rename() {
      using (Panel p = new Panel()) {
        ShortcutViewModel a = p.Group();
        Assert.True(p.Press(ShortcutKeyTexts.Rename, a));
        Assert.Equal(EditField.Name, a.EditField);
      }
    }

    [Fact]
    public void The_Delete_text_is_the_key_that_deletes() {
      using (Panel p = new Panel()) {
        ShortcutViewModel a = p.Group();
        Assert.True(p.Press(ShortcutKeyTexts.Delete, a));
        Assert.DoesNotContain(a, p.Win.RootShortcuts);
      }
    }

    [Fact]
    public void The_Cut_and_Paste_texts_are_the_keys_that_move_a_button() {
      using (Panel p = new Panel()) {
        ShortcutViewModel a = p.Group(), target = p.Group();
        Assert.True(p.Press(ShortcutKeyTexts.Cut, a));
        Assert.True(p.Press(ShortcutKeyTexts.Paste, target));
        Assert.Contains(a, target.Children);
        Assert.DoesNotContain(a, p.Win.RootShortcuts);
      }
    }

    [Fact]
    public void The_Copy_and_Paste_texts_are_the_keys_that_copy_a_button() {
      using (Panel p = new Panel()) {
        ShortcutViewModel a = p.Group(), target = p.Group();
        Assert.True(p.Press(ShortcutKeyTexts.Copy, a));
        Assert.True(p.Press(ShortcutKeyTexts.Paste, target));
        Assert.Single(target.Children);
        Assert.Contains(a, p.Win.RootShortcuts);
      }
    }

    // ---- The menus show them ----

    private static readonly Dictionary<string, string> ButtonMenuTexts = new Dictionary<string, string> {
      ["Rename"] = "F2", ["Cut"] = "Ctrl+X", ["Copy"] = "Ctrl+C", ["Paste"] = "Ctrl+V", ["Delete"] = "Delete"
    };

    private static void WithPanelView(Action<DesktopWindowView, TreeView, Button> check) {
      WpfAppHost.Run(() => {
        using (TempDb db = new TempDb()) {
          AppViewModel app = new AppViewModel();
          DesktopWindowViewModel win = app.AddWindow("TEST", 0, 0);
          ShortcutViewModel group = ViewModelFactory.NewGroupShortcut(app, null, win);
          win.RootShortcuts.Add(group);
          DesktopWindowView view = new DesktopWindowView(win) { ShowActivated = false, Left = -10000, Top = -10000 };
          try {
            view.Show();
            WpfAppHost.Pump();
            TreeView tree = (TreeView)view.FindName("TreeViewShortcuts");
            Button button = WpfAppHost.VisualDescendants<Button>(tree).First(b => b.DataContext == group);
            check(view, tree, button);
          } finally {
            view.Close();
            WpfAppHost.Pump();
          }
        }
      });
    }

    private static string Header(MenuItem item) { return item.Header as string ?? ""; }

    [Fact]
    public void The_button_menu_shows_the_keys_of_Rename_Cut_Copy_Paste_and_Delete_only() {
      WithPanelView((view, tree, button) => {
        List<MenuItem> items = button.ContextMenu.Items.OfType<MenuItem>().ToList();
        foreach (MenuItem item in items) {
          string expected = ButtonMenuTexts.TryGetValue(Header(item), out string text) ? text : "";
          Assert.True(expected == item.InputGestureText, Header(item) + ": expected '" + expected + "', got '" + item.InputGestureText + "'");
        }
        Assert.Equal(ButtonMenuTexts.Count, items.Count(i => ButtonMenuTexts.ContainsKey(Header(i))));
      });
    }

    [Fact]
    public void The_empty_area_menu_shows_Ctrl_V_on_Paste_only() {
      WithPanelView((view, tree, button) => {
        foreach (MenuItem item in tree.ContextMenu.Items.OfType<MenuItem>()) {
          Assert.Equal(Header(item) == "Paste" ? "Ctrl+V" : "", item.InputGestureText);
        }
      });
    }

    [Fact]
    public void The_title_menu_shows_no_keys() {
      WithPanelView((view, tree, button) => {
        ContextMenu menu = ((FrameworkElement)view.FindName("GridHeader")).ContextMenu;
        Assert.All(menu.Items.OfType<MenuItem>(), i => Assert.Equal("", i.InputGestureText));
      });
    }

    /// <summary>The visible TextBlock in an item's template that shows its key text (the rightmost, as "Delete" is also the name).</summary>
    private static TextBlock KeyText(MenuItem item) {
      return WpfAppHost.VisualDescendants<TextBlock>(item).Where(t => t.Text == item.InputGestureText && t.IsVisible)
        .OrderBy(t => t.TranslatePoint(new Point(0, 0), item).X).Last();
    }

    /// <summary>Opens the button menu in the theme and checks the keys sit in one dimmed column on the right, like Windows menus.</summary>
    private static void CheckKeysShown(Button button) {
      ContextMenu menu = button.ContextMenu;
      menu.PlacementTarget = button;
      menu.IsOpen = true;
      menu.UpdateLayout();
      WpfAppHost.Pump();
      try {
        List<MenuItem> keyed = menu.Items.OfType<MenuItem>().Where(i => ButtonMenuTexts.ContainsKey(Header(i))).ToList();
        List<double> lefts = new List<double>();
        double menuWidth = menu.ActualWidth;
        foreach (MenuItem item in keyed) {
          TextBlock key = KeyText(item);
          Assert.True(key.ActualWidth > 0, Header(item) + " key has no width");
          ContentPresenter header = WpfAppHost.VisualDescendants<ContentPresenter>(item).First(c => c.Content as string == Header(item));
          double headerRight = header.TranslatePoint(new Point(header.ActualWidth, 0), menu).X;
          double keyLeft = key.TranslatePoint(new Point(0, 0), menu).X;
          Assert.True(keyLeft > headerRight, Header(item) + " key is not right of its name");
          lefts.Add(keyLeft);
          Assert.True(key.TranslatePoint(new Point(key.ActualWidth, 0), menu).X > menuWidth * 0.7, Header(item) + " key is not on the right");
          // Grey: dimmed against the name's text colour.
          Assert.True(key.Opacity < 1, Header(item) + " key is not dimmed");
        }
        // One column: the keys start at the same place.
        Assert.All(lefts, l => Assert.Equal(lefts.Max(), l, 0.5));
      } finally {
        menu.IsOpen = false;
        WpfAppHost.Pump();
      }
    }

    [Fact]
    public void The_keys_show_in_a_grey_column_on_the_right_in_light_and_dark() {
      WithPanelView((view, tree, button) => {
        try {
          foreach (WindowThemeKind theme in new[] { WindowThemeKind.Light, WindowThemeKind.Dark }) {
            WindowThemes.Apply(theme);
            WpfAppHost.Pump();
            CheckKeysShown(button);
          }
        } finally {
          WindowThemes.Apply(WindowThemeKind.Light);
          WpfAppHost.Pump();
        }
      });
    }

    /// <summary>
    /// Saves button-menu-light.png and button-menu-dark.png (the button menu, open) for review. Does nothing unless the
    /// MDSHO_SCREENSHOT_DIR environment variable names an existing folder.
    /// </summary>
    [Fact]
    public void Render_menu_pictures_when_asked() {
      string folder = Environment.GetEnvironmentVariable("MDSHO_SCREENSHOT_DIR");
      if (string.IsNullOrEmpty(folder) || !Directory.Exists(folder)) { return; }
      WithPanelView((view, tree, button) => {
        object oldFont = Application.Current.Resources["IconFont"];
        // Outside MDSHO.exe the icon font's application pack URI finds nothing; point it at the MDSHO assembly for the pictures only.
        Application.Current.Resources["IconFont"] = new FontFamily(new Uri("pack://application:,,,/MDSHO;component/"), "./Assets/Fonts/#MDSHOFont");
        try {
          foreach (WindowThemeKind theme in new[] { WindowThemeKind.Light, WindowThemeKind.Dark }) {
            WindowThemes.Apply(theme);
            WpfAppHost.Pump();
            ContextMenu menu = button.ContextMenu;
            menu.PlacementTarget = button;
            menu.IsOpen = true;
            WpfAppHost.Pump();
            foreach (TextBlock icon in menu.Items.OfType<MenuItem>().Select(i => i.Icon).OfType<TextBlock>()) {
              icon.FontFamily = (FontFamily)Application.Current.Resources["IconFont"];
            }
            menu.UpdateLayout();
            WpfAppHost.Pump();
            Save(menu, Path.Combine(folder, "button-menu-" + theme.ToString().ToLowerInvariant() + ".png"));
            menu.IsOpen = false;
            WpfAppHost.Pump();
          }
        } finally {
          Application.Current.Resources["IconFont"] = oldFont;
          WindowThemes.Apply(WindowThemeKind.Light);
          WpfAppHost.Pump();
        }
      });
    }

    private static void Save(FrameworkElement element, string path) {
      double width = element.ActualWidth, height = element.ActualHeight;
      RenderTargetBitmap bitmap = new RenderTargetBitmap((int)Math.Ceiling(width), (int)Math.Ceiling(height), 96, 96, PixelFormats.Pbgra32);
      DrawingVisual visual = new DrawingVisual();
      using (DrawingContext dc = visual.RenderOpen()) {
        dc.DrawRectangle(new VisualBrush(element), null, new Rect(0, 0, width, height));
      }
      bitmap.Render(visual);
      PngBitmapEncoder encoder = new PngBitmapEncoder();
      encoder.Frames.Add(BitmapFrame.Create(bitmap));
      using (FileStream fs = File.Create(path)) { encoder.Save(fs); }
    }
  }
}
