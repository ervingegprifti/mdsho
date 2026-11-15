using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using MDSHO.ViewModels;

namespace MDSHO.Helpers {

  /// <summary>
  /// Swaps the window theme at runtime. The theme brushes (Themes/Light.xaml or Themes/Dark.xaml) sit in the app's
  /// resources and the windows use them through DynamicResource, so open windows change at once. Windows marked with
  /// <see cref="IsThemedProperty"/> (MDSHO's own windows, never panels or notes) also get the dark control styles
  /// (Themes/DarkControls.xaml) while Dark is on. The tray menu listens to <see cref="Applied"/>.
  /// </summary>
  public static class WindowThemes {
    public static readonly Uri LightUri = new Uri("pack://application:,,,/MDSHO;component/Themes/Light.xaml");
    public static readonly Uri DarkUri = new Uri("pack://application:,,,/MDSHO;component/Themes/Dark.xaml");
    public static readonly Uri DarkControlsUri = new Uri("pack://application:,,,/MDSHO;component/Themes/DarkControls.xaml");

    private static ResourceDictionary light, dark, darkControls;
    private static WindowThemeSetting followed;

    /// <summary>The theme in use. Light until <see cref="Apply"/> says otherwise (App.xaml starts with Light).</summary>
    public static WindowThemeKind Current { get; private set; } = WindowThemeKind.Light;

    /// <summary>The theme dictionary now in the app's resources.</summary>
    public static ResourceDictionary ActiveDictionary {
      get {
        Collection<ResourceDictionary> merged = Application.Current?.Resources.MergedDictionaries;
        int index = merged == null ? -1 : IndexOfTheme(merged);
        return index < 0 ? null : merged[index];
      }
    }

    /// <summary>Raised on the UI thread after a theme was applied.</summary>
    public static event Action Applied;

    /// <summary>Puts <paramref name="kind"/> in the app's resources and in every open themed window. Call it on the UI thread.</summary>
    public static void Apply(WindowThemeKind kind) {
      Application app = Application.Current;
      if (app == null) {
        return;
      }
      ResourceDictionary next = kind == WindowThemeKind.Dark
        ? dark ??= new ResourceDictionary { Source = DarkUri }
        : light ??= new ResourceDictionary { Source = LightUri };
      Collection<ResourceDictionary> merged = app.Resources.MergedDictionaries;
      int index = IndexOfTheme(merged);
      if (index < 0) {
        merged.Insert(0, next);
      } else if (!ReferenceEquals(merged[index], next)) {
        merged[index] = next;
      }
      Current = kind;
      foreach (Window window in app.Windows.Cast<Window>().Where(GetIsThemed).ToList()) {
        UpdateWindow(window);
      }
      Applied?.Invoke();
    }

    /// <summary>Applies the setting now and again whenever it changes; null stops following.</summary>
    public static void Follow(WindowThemeSetting setting) {
      if (followed != null) {
        followed.Changed -= SettingChanged;
      }
      followed = setting;
      if (setting == null) {
        return;
      }
      setting.Changed += SettingChanged;
      setting.Start();
      Apply(setting.Current);
    }

    // Windows' app mode can change on another thread.
    private static void SettingChanged() {
      System.Windows.Threading.Dispatcher dispatcher = Application.Current?.Dispatcher;
      if (dispatcher == null) {
        return;
      }
      Action apply = () => {
        WindowThemeSetting setting = followed;
        if (setting != null) {
          Apply(setting.Current);
        }
      };
      if (dispatcher.CheckAccess()) {
        apply();
      } else {
        dispatcher.BeginInvoke(apply);
      }
    }

    /// <summary>Marks a window as one of MDSHO's own windows (without user colours): it follows the theme.</summary>
    public static readonly DependencyProperty IsThemedProperty = DependencyProperty.RegisterAttached(
      "IsThemed", typeof(bool), typeof(WindowThemes), new PropertyMetadata(false, IsThemedChanged));

    public static bool GetIsThemed(DependencyObject element) {
      return (bool)element.GetValue(IsThemedProperty);
    }

    public static void SetIsThemed(DependencyObject element, bool value) {
      element.SetValue(IsThemedProperty, value);
    }

    private static void IsThemedChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) {
      if (!(d is FrameworkElement element)) {
        return;
      }
      // Set from XAML before the window's own resources: wait until the window is initialised.
      if (element.IsInitialized) {
        UpdateWindow(element);
      } else {
        EventHandler initialized = null;
        initialized = (s, args) => {
          element.Initialized -= initialized;
          UpdateWindow(element);
        };
        element.Initialized += initialized;
      }
    }

    // Adds the dark control styles to a themed window in Dark, and takes them away in Light.
    private static void UpdateWindow(FrameworkElement element) {
      Collection<ResourceDictionary> merged = element.Resources.MergedDictionaries;
      bool wanted = GetIsThemed(element) && Current == WindowThemeKind.Dark;
      ResourceDictionary present = merged.FirstOrDefault(m => m.Source == DarkControlsUri);
      if (wanted && present == null) {
        merged.Add(darkControls ??= new ResourceDictionary { Source = DarkControlsUri });
      } else if (!wanted && present != null) {
        merged.Remove(present);
      }
    }

    private static int IndexOfTheme(Collection<ResourceDictionary> merged) {
      for (int i = 0; i < merged.Count; i++) {
        string source = merged[i].Source?.OriginalString;
        if (source != null && (source.EndsWith("Themes/Light.xaml", StringComparison.OrdinalIgnoreCase) || source.EndsWith("Themes/Dark.xaml", StringComparison.OrdinalIgnoreCase))) {
          return i;
        }
      }
      return -1;
    }

    /// <summary>A theme brush's colour, for WinForms (the tray menu).</summary>
    public static System.Drawing.Color DrawingColor(string key) {
      Color c = ((ActiveDictionary?[key] as SolidColorBrush) ?? Brushes.Black).Color;
      return System.Drawing.Color.FromArgb(c.A, c.R, c.G, c.B);
    }

    /// <summary>A theme brush's colour.</summary>
    public static Color MediaColor(string key) {
      return ((ActiveDictionary?[key] as SolidColorBrush) ?? Brushes.Black).Color;
    }

    /// <summary>
    /// Gives a WinForms menu (the tray menu), its items and its submenus the theme in use: dark colours in Dark, the
    /// Windows look in Light. Items added later need another call (the tray menu calls it after it refills a submenu).
    /// </summary>
    public static void ApplyTo(System.Windows.Forms.ToolStrip strip) {
      bool dark = Current == WindowThemeKind.Dark;
      if (dark) {
        strip.Renderer = new DarkTrayMenuRenderer();
        strip.ForeColor = DrawingColor("Theme.MenuText");
        strip.BackColor = DrawingColor("Theme.MenuBackground");
      } else {
        strip.RenderMode = System.Windows.Forms.ToolStripRenderMode.ManagerRenderMode;
        strip.ResetForeColor();
        strip.ResetBackColor();
      }
      foreach (System.Windows.Forms.ToolStripItem item in strip.Items) {
        if (dark) {
          item.ForeColor = strip.ForeColor;
        } else {
          item.ResetForeColor();
        }
        if (item is System.Windows.Forms.ToolStripMenuItem menuItem && menuItem.HasDropDown) {
          ApplyTo(menuItem.DropDown);
        }
      }
    }
  }

  /// <summary>The tray menu in the Dark theme: the theme's menu colours instead of the Windows ones.</summary>
  public sealed class DarkTrayMenuRenderer : System.Windows.Forms.ToolStripProfessionalRenderer {
    public DarkTrayMenuRenderer() : base(new DarkColors()) {
      RoundedEdges = false;
    }

    protected override void OnRenderItemText(System.Windows.Forms.ToolStripItemTextRenderEventArgs e) {
      e.TextColor = WindowThemes.DrawingColor(e.Item.Enabled ? "Theme.MenuText" : "Theme.MenuDisabledText");
      base.OnRenderItemText(e);
    }

    protected override void OnRenderArrow(System.Windows.Forms.ToolStripArrowRenderEventArgs e) {
      e.ArrowColor = WindowThemes.DrawingColor("Theme.MenuText");
      base.OnRenderArrow(e);
    }

    // A light tick (Start with Windows, the current screen setup), drawn in the image margin.
    protected override void OnRenderItemCheck(System.Windows.Forms.ToolStripItemImageRenderEventArgs e) {
      System.Drawing.Rectangle r = e.ImageRectangle;
      System.Drawing.Graphics g = e.Graphics;
      System.Drawing.Drawing2D.SmoothingMode old = g.SmoothingMode;
      g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
      using (System.Drawing.Pen pen = new System.Drawing.Pen(WindowThemes.DrawingColor("Theme.MenuText"), Math.Max(1.5f, r.Height / 8f))) {
        g.DrawLines(pen, new[] {
          new System.Drawing.PointF(r.Left + r.Width * 0.2f, r.Top + r.Height * 0.5f),
          new System.Drawing.PointF(r.Left + r.Width * 0.42f, r.Top + r.Height * 0.72f),
          new System.Drawing.PointF(r.Left + r.Width * 0.8f, r.Top + r.Height * 0.28f),
        });
      }
      g.SmoothingMode = old;
    }

    private sealed class DarkColors : System.Windows.Forms.ProfessionalColorTable {
      private static System.Drawing.Color C(string key) { return WindowThemes.DrawingColor(key); }
      public override System.Drawing.Color ToolStripDropDownBackground => C("Theme.MenuBackground");
      public override System.Drawing.Color ImageMarginGradientBegin => C("Theme.MenuBackground");
      public override System.Drawing.Color ImageMarginGradientMiddle => C("Theme.MenuBackground");
      public override System.Drawing.Color ImageMarginGradientEnd => C("Theme.MenuBackground");
      public override System.Drawing.Color MenuBorder => C("Theme.MenuBorder");
      public override System.Drawing.Color MenuItemBorder => C("Theme.MenuHighlightBorder");
      public override System.Drawing.Color MenuItemSelected => C("Theme.MenuHighlight");
      public override System.Drawing.Color MenuItemSelectedGradientBegin => C("Theme.MenuHighlight");
      public override System.Drawing.Color MenuItemSelectedGradientEnd => C("Theme.MenuHighlight");
      public override System.Drawing.Color MenuItemPressedGradientBegin => C("Theme.MenuHighlight");
      public override System.Drawing.Color MenuItemPressedGradientEnd => C("Theme.MenuHighlight");
      public override System.Drawing.Color SeparatorDark => C("Theme.MenuSeparator");
      public override System.Drawing.Color SeparatorLight => C("Theme.MenuSeparator");
      public override System.Drawing.Color CheckBackground => C("Theme.MenuBackground");
      public override System.Drawing.Color CheckSelectedBackground => C("Theme.MenuHighlight");
      public override System.Drawing.Color CheckPressedBackground => C("Theme.MenuHighlight");
    }
  }
}
