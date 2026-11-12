using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using MDSHO.Core;
using MDSHO.Data;
using MDSHO.ViewModels;
using Xunit;

namespace MDSHO.Tests {

  /// <summary>
  /// The real Run Command window and its Examples popup, shown off screen (never activated). Process starts go to a
  /// recording seam, so no real program, UAC prompt or visible window is ever opened.
  /// </summary>
  public class RunCommandWindowTests {

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

    private sealed class Fixture {
      public AppViewModel App;
      public DesktopWindowViewModel Win;
      public ShortcutViewModel Command;
      public List<ProcessStartInfo> Started = new List<ProcessStartInfo>();
    }

    /// <summary>Temp database, a group "T {SRV01}" with a cmd command, process starts recorded; runs <paramref name="body"/> on the WPF thread.</summary>
    private static void WithCommand(string fileName, string arguments, Action<Fixture> body) {
      WpfAppHost.Run(() => {
        Action<ProcessStartInfo> oldStarter = ShortcutViewModel.ProcessStarter;
        Action<ShortcutViewModel, bool> oldEditor = ShortcutViewModel.OpenRunCommandEditor;
        Fixture f = new Fixture();
        ShortcutViewModel.ProcessStarter = psi => f.Started.Add(psi);
        ShortcutViewModel.OpenRunCommandEditor = (s, isNew) => { };
        try {
          using (TempDb db = new TempDb()) {
            f.App = new AppViewModel();
            f.Win = f.App.AddWindow("TEST", 0, 0);
            ShortcutViewModel group = ViewModelFactory.NewGroupShortcut(f.App, null, f.Win);
            f.Win.RootShortcuts.Add(group);
            group.Name = "T {SRV01}";
            f.Command = ViewModelFactory.NewProcessCommandShortcut(f.App, group, null);
            group.Children.Add(f.Command);
            f.Command.ProcessFileName = fileName;
            f.Command.ProcessArguments = arguments;
            body(f);
          }
        } finally {
          ShortcutViewModel.ProcessStarter = oldStarter;
          ShortcutViewModel.OpenRunCommandEditor = oldEditor;
        }
      });
    }

    private static void OffScreen(Window window) {
      window.WindowStartupLocation = WindowStartupLocation.Manual;
      window.Left = -10000;
      window.Top = -10000;
      window.ShowActivated = false;
      window.ShowInTaskbar = false;
    }

    private static void Shown(Window window, Action check) {
      OffScreen(window);
      window.Show();
      try {
        WpfAppHost.Pump();
        check();
      } finally {
        window.Close();
        WpfAppHost.Pump();
      }
    }

    private static T Named<T>(Window window, string name) where T : class {
      return Assert.IsAssignableFrom<T>(window.FindName(name));
    }

    private static void Click(Button button) {
      button.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent, button));
      WpfAppHost.Pump();
    }

    private static void PressEscape(Window window) {
      KeyEventArgs args = new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(window), 0, Key.Escape) { RoutedEvent = Keyboard.PreviewKeyDownEvent };
      window.RaiseEvent(args);
      WpfAppHost.Pump();
    }

    private static Color BackgroundOf(Window window, string name) {
      object element = window.FindName(name);
      Brush brush = element is Panel p ? p.Background : element is Border b ? b.Background : element is Control c ? c.Background : null;
      return Assert.IsType<SolidColorBrush>(brush).Color;
    }

    private static Point Position(FrameworkElement element, Window window) {
      return element.TranslatePoint(new Point(0, 0), (UIElement)VisualTreeHelper.GetChild(window, 0));
    }

    // ---- Roomy margins and the run icon (like the About window) ----

    [Fact]
    public void Every_outer_margin_of_the_fields_is_the_button_height() {
      WithCommand("cmd.exe", "", f => {
        RunCommandWindow window = new RunCommandWindow(new RunCommandViewModel(f.Command, false));
        Shown(window, () => {
          FrameworkElement body = Named<FrameworkElement>(window, "Body");
          ComboBox program = Named<ComboBox>(window, "ComboProgram");
          Button browse = Named<Button>(window, "ButtonBrowseProgram");
          TextBox preview = Named<TextBox>(window, "TextPreview");
          double h = Named<Button>(window, "ButtonSave").ActualHeight;
          Assert.True(h > 10, "Button height " + h);
          Assert.Equal(new Thickness(h), Named<FrameworkElement>(window, "BodyContent").Margin);
          Assert.Equal(h, program.TranslatePoint(new Point(0, 0), body).X, 1);
          Assert.Equal(body.ActualWidth - h, browse.TranslatePoint(new Point(browse.ActualWidth, 0), body).X, 1);
          Assert.Equal(body.ActualWidth - h, preview.TranslatePoint(new Point(preview.ActualWidth, 0), body).X, 1);
          Assert.Equal(h, body.ActualHeight - preview.TranslatePoint(new Point(0, preview.ActualHeight), body).Y, 1);
        });
      });
    }

    [Fact]
    public void Every_outer_margin_of_the_bottom_bar_is_the_button_height() {
      WithCommand("cmd.exe", "", f => {
        RunCommandWindow window = new RunCommandWindow(new RunCommandViewModel(f.Command, false));
        Shown(window, () => {
          FrameworkElement footer = Named<FrameworkElement>(window, "Footer");
          Button test = Named<Button>(window, "ButtonTestRun");
          Button cancel = Named<Button>(window, "ButtonCancel");
          double h = Named<Button>(window, "ButtonSave").ActualHeight;
          Point testTopLeft = test.TranslatePoint(new Point(0, 0), footer);
          Assert.Equal(h, testTopLeft.X, 1);
          Assert.Equal(h, testTopLeft.Y, 1);
          Assert.Equal(footer.ActualHeight - h, testTopLeft.Y + test.ActualHeight, 1);
          Assert.Equal(footer.ActualWidth - h, cancel.TranslatePoint(new Point(cancel.ActualWidth, 0), footer).X, 1);
        });
      });
    }

    [Fact]
    public void Title_bar_shows_the_run_icon_left_of_the_title_styled_like_it() {
      WithCommand("cmd.exe", "", f => {
        RunCommandWindow window = new RunCommandWindow(new RunCommandViewModel(f.Command, false));
        Shown(window, () => {
          TextBlock icon = Named<TextBlock>(window, "TextTitleIcon");
          TextBlock title = Named<TextBlock>(window, "TextTitle");
          Assert.Equal(f.Win.Appearance.Icons.Run, icon.Text);
          Assert.Same(Application.Current.FindResource("IconFont"), icon.FontFamily);
          Assert.Equal(((SolidColorBrush)title.Foreground).Color, ((SolidColorBrush)icon.Foreground).Color);
          Assert.True(Position(icon, window).X + icon.ActualWidth <= Position(title, window).X, "The run icon is not left of the title.");
          Assert.Equal(Position(icon, window).Y + icon.ActualHeight / 2, Position(title, window).Y + title.ActualHeight / 2, 1.5);
        });
      });
    }

    // ---- Rounded corners (Windows 11) ----

    [Fact]
    public void Both_windows_ask_for_rounded_corners_once_their_handle_exists() {
      WithCommand("cmd.exe", "", f => {
        Action<IntPtr> old = MDSHO.Helpers.WindowCorners.RequestRounded;
        List<IntPtr> requested = new List<IntPtr>();
        MDSHO.Helpers.WindowCorners.RequestRounded = hwnd => requested.Add(hwnd);
        try {
          RunCommandViewModel vm = new RunCommandViewModel(f.Command, false);
          RunCommandWindow window = new RunCommandWindow(vm);
          Assert.Empty(requested); // no handle yet
          Shown(window, () => {
            IntPtr hwnd = new System.Windows.Interop.WindowInteropHelper(window).Handle;
            Assert.NotEqual(IntPtr.Zero, hwnd);
            Assert.Equal(new[] { hwnd }, requested);
          });
          requested.Clear();
          RunCommandExamplesWindow examples = new RunCommandExamplesWindow(vm);
          Shown(examples, () => {
            Assert.Equal(new[] { new System.Windows.Interop.WindowInteropHelper(examples).Handle }, requested);
          });
        } finally {
          MDSHO.Helpers.WindowCorners.RequestRounded = old;
        }
      });
    }

    [Fact]
    public void Windows_11_asks_DWM_for_round_corners() {
      Func<bool> oldIs11 = MDSHO.Helpers.WindowCorners.IsWindows11;
      Func<IntPtr, int, int, int> oldSet = MDSHO.Helpers.WindowCorners.SetDwmAttribute;
      List<(IntPtr, int, int)> calls = new List<(IntPtr, int, int)>();
      try {
        MDSHO.Helpers.WindowCorners.IsWindows11 = () => true;
        MDSHO.Helpers.WindowCorners.SetDwmAttribute = (h, a, v) => { calls.Add((h, a, v)); return 0; };
        MDSHO.Helpers.WindowCorners.RequestRoundedFromDwm(new IntPtr(1234));
        Assert.Equal(new[] { (new IntPtr(1234), 33, 2) }, calls); // DWMWA_WINDOW_CORNER_PREFERENCE = DWMWCP_ROUND
      } finally {
        MDSHO.Helpers.WindowCorners.IsWindows11 = oldIs11;
        MDSHO.Helpers.WindowCorners.SetDwmAttribute = oldSet;
      }
    }

    [Fact]
    public void Windows_10_does_not_ask_DWM() {
      Func<bool> oldIs11 = MDSHO.Helpers.WindowCorners.IsWindows11;
      Func<IntPtr, int, int, int> oldSet = MDSHO.Helpers.WindowCorners.SetDwmAttribute;
      int calls = 0;
      try {
        MDSHO.Helpers.WindowCorners.IsWindows11 = () => false;
        MDSHO.Helpers.WindowCorners.SetDwmAttribute = (h, a, v) => { calls++; return 0; };
        MDSHO.Helpers.WindowCorners.RequestRoundedFromDwm(new IntPtr(1234));
        Assert.Equal(0, calls);
      } finally {
        MDSHO.Helpers.WindowCorners.IsWindows11 = oldIs11;
        MDSHO.Helpers.WindowCorners.SetDwmAttribute = oldSet;
      }
    }

    [Theory]
    [InlineData(10, 0, 19045, false)] // Windows 10 22H2
    [InlineData(10, 0, 21999, false)]
    [InlineData(10, 0, 22000, true)]  // Windows 11 21H2
    [InlineData(10, 0, 26100, true)]
    public void Windows_11_is_build_22000_or_later(int major, int minor, int build, bool expected) {
      Assert.Equal(expected, MDSHO.Helpers.WindowCorners.IsWindows11Version(new Version(major, minor, build)));
    }

    [Fact]
    public void A_real_DWM_request_never_throws_even_where_it_is_not_supported() {
      // On Windows 10 the attribute is unknown and DWM only returns an error code; a bad handle is also just an error code.
      Func<bool> oldIs11 = MDSHO.Helpers.WindowCorners.IsWindows11;
      try {
        MDSHO.Helpers.WindowCorners.IsWindows11 = () => true;
        MDSHO.Helpers.WindowCorners.RequestRoundedFromDwm(IntPtr.Zero);
        MDSHO.Helpers.WindowCorners.IsWindows11 = () => false;
        MDSHO.Helpers.WindowCorners.RequestRoundedFromDwm(IntPtr.Zero);
      } finally {
        MDSHO.Helpers.WindowCorners.IsWindows11 = oldIs11;
      }
    }

    // ---- Run Command window ----

    [Fact]
    public void Window_opens_without_binding_errors() {
      WithCommand("cmd.exe", "/K ping {}", f => {
        BindingErrorListener listener = new BindingErrorListener();
        SourceLevels oldLevel = PresentationTraceSources.DataBindingSource.Switch.Level;
        PresentationTraceSources.Refresh();
        PresentationTraceSources.DataBindingSource.Listeners.Add(listener);
        PresentationTraceSources.DataBindingSource.Switch.Level = SourceLevels.Warning;
        try {
          RunCommandViewModel vm = new RunCommandViewModel(f.Command, false);
          RunCommandWindow window = new RunCommandWindow(vm);
          Shown(window, () => {
            Named<ComboBox>(window, "ComboProgram").IsDropDownOpen = true;
            WpfAppHost.Pump();
            Named<ComboBox>(window, "ComboProgram").IsDropDownOpen = false;
          });
          RunCommandExamplesWindow examples = new RunCommandExamplesWindow(vm);
          Shown(examples, () => { });
        } finally {
          PresentationTraceSources.DataBindingSource.Listeners.Remove(listener);
          PresentationTraceSources.DataBindingSource.Switch.Level = oldLevel;
        }
        Assert.True(listener.Messages.Count == 0, "Binding errors:\n" + string.Join("\n", listener.Messages));
      });
    }

    [Fact]
    public void Title_bar_has_title_star_and_close_from_left_to_right_and_no_maximise_button() {
      WithCommand("cmd.exe", "/K ping {}", f => {
        RunCommandViewModel vm = new RunCommandViewModel(f.Command, false);
        RunCommandWindow window = new RunCommandWindow(vm);
        Shown(window, () => {
          Assert.Equal(WindowStyle.None, window.WindowStyle);
          Assert.NotNull(System.Windows.Shell.WindowChrome.GetWindowChrome(window));
          TextBlock title = Named<TextBlock>(window, "TextTitle");
          Button star = Named<Button>(window, "ButtonExamples");
          Assert.Null(window.FindName("ButtonMaximize"));
          Button close = Named<Button>(window, "ButtonClose");
          Assert.Equal("Run command", title.Text);
          Assert.Equal("Examples", star.ToolTip);
          Assert.Equal("Examples", System.Windows.Automation.AutomationProperties.GetName(star));
          Assert.Equal(f.Win.Appearance.Icons.Wizard, star.Content);
          Assert.Equal(f.Win.Appearance.Icons.SolidIcons ? "" : "", star.Content);
          f.Win.Appearance.Icons.SolidIcons = !f.Win.Appearance.Icons.SolidIcons;
          WpfAppHost.Pump();
          Assert.Equal(f.Win.Appearance.Icons.Wizard, star.Content);
          Assert.Equal(f.Win.Appearance.Icons.SolidIcons ? "" : "", star.Content);
          double[] x = new[] { Position(title, window).X, Position(star, window).X, Position(close, window).X };
          Assert.Equal(x.OrderBy(v => v).ToArray(), x);
        });
      });
    }

    [System.Runtime.InteropServices.DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
    private static extern IntPtr GetWindowLongPtr(IntPtr hwnd, int index);

    [Fact]
    public void Double_clicking_the_title_bar_maximises_and_the_content_stays_inside_the_screen() {
      WithCommand("cmd.exe", "", f => {
        RunCommandWindow window = new RunCommandWindow(new RunCommandViewModel(f.Command, false));
        Shown(window, () => {
          // Windows maximises a window on a double-click of its caption when it has a maximise box: the whole title bar is
          // the caption, and the window keeps the maximise box although it draws no maximise button.
          System.Windows.Shell.WindowChrome chrome = System.Windows.Shell.WindowChrome.GetWindowChrome(window);
          Assert.Equal(Named<Border>(window, "TitleBar").ActualHeight, chrome.CaptionHeight, 1);
          Assert.Equal(ResizeMode.CanResize, window.ResizeMode);
          IntPtr hwnd = new System.Windows.Interop.WindowInteropHelper(window).Handle;
          const long WS_MAXIMIZEBOX = 0x00010000;
          Assert.NotEqual(0, GetWindowLongPtr(hwnd, -16).ToInt64() & WS_MAXIMIZEBOX);
          Grid root = Named<Grid>(window, "RootGrid");
          Assert.Equal(new Thickness(0), root.Margin);
          window.WindowState = WindowState.Maximized;
          WpfAppHost.Pump();
          Assert.Equal(new Thickness(7), root.Margin);
          window.WindowState = WindowState.Normal;
          WpfAppHost.Pump();
          Assert.Equal(new Thickness(0), root.Margin);
        });
      });
    }

    [Fact]
    public void Colours_are_the_neutral_settings_colours() {
      WithCommand("cmd.exe", "", f => {
        RunCommandWindow window = new RunCommandWindow(new RunCommandViewModel(f.Command, false));
        Shown(window, () => {
          Color bar = (Color)ColorConverter.ConvertFromString("#DFDACB");
          Color body = (Color)ColorConverter.ConvertFromString("#F4F3ED");
          Assert.Equal(bar, BackgroundOf(window, "TitleBar"));
          Assert.Equal(bar, BackgroundOf(window, "Footer"));
          Assert.Equal(body, BackgroundOf(window, "Body"));
        });
      });
    }

    [Fact]
    public void Body_controls_are_set_up_as_agreed() {
      WithCommand("cmd.exe", "/K ping {}", f => {
        RunCommandWindow window = new RunCommandWindow(new RunCommandViewModel(f.Command, false));
        Shown(window, () => {
          ComboBox program = Named<ComboBox>(window, "ComboProgram");
          Assert.True(program.IsEditable);
          Assert.Equal("cmd.exe", program.Text);
          Assert.Equal(6, program.Items.Count);
          Named<Button>(window, "ButtonBrowseProgram");
          Named<TextBox>(window, "TextWorkingFolder");
          Named<Button>(window, "ButtonBrowseFolder");
          TextBox args = Named<TextBox>(window, "TextArguments");
          Assert.True(args.AcceptsReturn);
          Assert.Equal("Consolas", args.FontFamily.Source);
          Assert.Equal("ping {}", args.Text);
          Assert.True(Named<CheckBox>(window, "CheckKeepOpen").IsChecked);
          Assert.False(Named<CheckBox>(window, "CheckRunAsAdmin").IsChecked);
          TextBox preview = Named<TextBox>(window, "TextPreview");
          Assert.True(preview.IsReadOnly);
          Assert.Equal(TextWrapping.Wrap, preview.TextWrapping);
          Assert.Equal("cmd.exe /K ping SRV01", preview.Text);
          Assert.True(Named<Button>(window, "ButtonSave").IsDefault);
          Assert.True(Named<Button>(window, "ButtonCancel").IsCancel);
          Assert.True(Position(Named<Button>(window, "ButtonTestRun"), window).X < Position(Named<Button>(window, "ButtonSave"), window).X);
          Assert.True(window.ResizeMode == ResizeMode.CanResize || window.ResizeMode == ResizeMode.CanResizeWithGrip);
          Assert.True(window.MinWidth >= 300 && window.MinHeight >= 300);
        });
      });
    }

    [Fact]
    public void Keep_open_checkbox_is_disabled_for_other_programs() {
      WithCommand("explorer.exe", "shell:startup", f => {
        RunCommandWindow window = new RunCommandWindow(new RunCommandViewModel(f.Command, false));
        Shown(window, () => {
          Assert.False(Named<CheckBox>(window, "CheckKeepOpen").IsEnabled);
        });
      });
    }

    [Fact]
    public void Typing_updates_the_preview_live() {
      WithCommand("cmd.exe", "/K ping {}", f => {
        RunCommandWindow window = new RunCommandWindow(new RunCommandViewModel(f.Command, false));
        Shown(window, () => {
          Named<TextBox>(window, "TextArguments").Text = "ping {}\r\nipconfig";
          WpfAppHost.Pump();
          Assert.Equal("cmd.exe /K ping SRV01 & ipconfig", Named<TextBox>(window, "TextPreview").Text);
          Named<ComboBox>(window, "ComboProgram").Text = "notepad.exe";
          WpfAppHost.Pump();
          Assert.False(Named<CheckBox>(window, "CheckKeepOpen").IsEnabled);
        });
      });
    }

    [Fact]
    public void Arguments_box_takes_the_extra_height() {
      WithCommand("cmd.exe", "/K ping {}", f => {
        RunCommandWindow window = new RunCommandWindow(new RunCommandViewModel(f.Command, false));
        Shown(window, () => {
          TextBox args = Named<TextBox>(window, "TextArguments");
          TextBox preview = Named<TextBox>(window, "TextPreview");
          window.Height = window.MinHeight;
          window.UpdateLayout();
          double argsBefore = args.ActualHeight, previewBefore = preview.ActualHeight;
          window.Height = window.MinHeight + 200;
          window.UpdateLayout();
          Assert.True(args.ActualHeight >= argsBefore + 150, $"Arguments grew from {argsBefore:0} to {args.ActualHeight:0}.");
          Assert.Equal(previewBefore, preview.ActualHeight, 1);
        });
      });
    }

    [Fact]
    public void Preview_grows_to_about_four_lines_then_scrolls() {
      WithCommand("cmd.exe", "/K ping {}", f => {
        RunCommandWindow window = new RunCommandWindow(new RunCommandViewModel(f.Command, false));
        Shown(window, () => {
          TextBox preview = Named<TextBox>(window, "TextPreview");
          double one = preview.ActualHeight;
          Named<TextBox>(window, "TextArguments").Text = string.Join("\r\n", Enumerable.Repeat("ping a-very-long-computer-name-for-wrapping", 30));
          WpfAppHost.Pump();
          window.UpdateLayout();
          Assert.True(preview.ActualHeight > one * 2, "The preview grows for longer text.");
          Assert.True(preview.ActualHeight < one * 6, $"The preview stops growing at about 4 lines (one line {one:0}, now {preview.ActualHeight:0}).");
          Assert.Equal(ScrollBarVisibility.Auto, preview.VerticalScrollBarVisibility);
        });
      });
    }

    [Fact]
    public void Cancel_X_and_Escape_close_without_saving() {
      WithCommand("cmd.exe", "/K ping {}", f => {
        foreach (string how in new[] { "cancel", "x", "esc" }) {
          RunCommandWindow window = new RunCommandWindow(new RunCommandViewModel(f.Command, false));
          OffScreen(window);
          bool closed = false;
          window.Closed += (s, e) => closed = true;
          window.Show();
          WpfAppHost.Pump();
          Named<TextBox>(window, "TextArguments").Text = "changed";
          Named<CheckBox>(window, "CheckRunAsAdmin").IsChecked = true;
          WpfAppHost.Pump();
          if (how == "cancel") { Click(Named<Button>(window, "ButtonCancel")); }
          else if (how == "x") { Click(Named<Button>(window, "ButtonClose")); }
          else { PressEscape(window); }
          if (!closed) { window.Close(); }
          Assert.True(closed, how + " should close the window");
          Assert.False(window.Saved);
          Assert.Equal("/K ping {}", f.Command.ProcessArguments);
          Assert.False(f.Command.ProcessRunAsAdmin);
          Assert.Equal("/K ping {}", DataStore.Shortcuts.Get(f.Command.ID).ProcessArguments);
        }
      });
    }

    [Fact]
    public void Save_writes_and_closes() {
      WithCommand("cmd.exe", "/K ping {}", f => {
        RunCommandWindow window = new RunCommandWindow(new RunCommandViewModel(f.Command, false));
        OffScreen(window);
        bool closed = false;
        window.Closed += (s, e) => closed = true;
        window.Show();
        WpfAppHost.Pump();
        Named<TextBox>(window, "TextArguments").Text = "tracert {}";
        Named<CheckBox>(window, "CheckKeepOpen").IsChecked = false;
        Named<TextBox>(window, "TextWorkingFolder").Text = @"C:\Work";
        WpfAppHost.Pump();
        Click(Named<Button>(window, "ButtonSave"));
        if (!closed) { window.Close(); }
        Assert.True(closed);
        Assert.True(window.Saved);
        Shortcut row = DataStore.Shortcuts.Get(f.Command.ID);
        Assert.Equal(("/C tracert {}", @"C:\Work"), (row.ProcessArguments, row.ProcessWorkingFolder));
      });
    }

    [Fact]
    public void Test_run_button_starts_through_the_seam_only() {
      WithCommand("cmd.exe", "/K ping {}", f => {
        RunCommandWindow window = new RunCommandWindow(new RunCommandViewModel(f.Command, false));
        Shown(window, () => {
          Named<TextBox>(window, "TextArguments").Text = "ipconfig";
          WpfAppHost.Pump();
          Click(Named<Button>(window, "ButtonTestRun"));
          ProcessStartInfo psi = Assert.Single(f.Started);
          Assert.Equal(("cmd.exe", "/K ipconfig"), (psi.FileName, psi.Arguments));
          Assert.Equal("/K ping {}", DataStore.Shortcuts.Get(f.Command.ID).ProcessArguments);
        });
      });
    }

    // ---- Examples popup ----

    [Fact]
    public void Examples_popup_has_title_grouped_list_and_no_cancel_button() {
      WithCommand("cmd.exe", "", f => {
        RunCommandExamplesWindow window = new RunCommandExamplesWindow(new RunCommandViewModel(f.Command, true));
        Shown(window, () => {
          Assert.Equal("Examples", Named<TextBlock>(window, "TextTitle").Text);
          Assert.Equal(WindowStyle.None, window.WindowStyle);
          Color bar = (Color)ColorConverter.ConvertFromString("#DFDACB");
          Assert.Equal(bar, BackgroundOf(window, "TitleBar"));
          Assert.Equal((Color)ColorConverter.ConvertFromString("#F4F3ED"), BackgroundOf(window, "Body"));
          ListBox list = Named<ListBox>(window, "ListExamples");
          Assert.Equal(15, list.Items.Count);
          Assert.Equal(new[] { "Command Prompt (cmd)", "PowerShell", "Explorer", "Other" },
            list.Items.Groups.Cast<CollectionViewGroup>().Select(g => (string)g.Name).ToArray());
          Assert.True(Named<Button>(window, "ButtonUse").IsDefault);
          Button[] buttons = WpfAppHost.VisualDescendants<Button>(window).ToArray();
          Assert.DoesNotContain(buttons, b => b.IsCancel);
          Assert.DoesNotContain(buttons, b => "Cancel".Equals(b.Content as string));
        });
      });
    }

    /// <summary>The texts of an example's tooltip (what it does, then the command), read from its list item.</summary>
    internal static string[] ExampleToolTipTexts(ListBox list, RunCommandExample example) {
      list.ScrollIntoView(example);
      list.UpdateLayout();
      WpfAppHost.Pump();
      ListBoxItem item = (ListBoxItem)list.ItemContainerGenerator.ContainerFromItem(example);
      ToolTip tip = Assert.IsType<ToolTip>(item.ToolTip);
      StackPanel panel = Assert.IsType<StackPanel>(tip.Content);
      panel.DataContext = example;
      return panel.Children.OfType<TextBlock>().Select(t => t.Text).ToArray();
    }

    [Fact]
    public void Hovering_an_example_shows_its_description_and_command() {
      WithCommand("cmd.exe", "", f => {
        RunCommandExamplesWindow window = new RunCommandExamplesWindow(new RunCommandViewModel(f.Command, true));
        Shown(window, () => {
          ListBox list = Named<ListBox>(window, "ListExamples");
          RunCommandExample ping = RunCommandExamples.All.Single(e => e.Name == "Ping a computer");
          Assert.Equal(new[] { ping.Description, "Runs: cmd.exe /K ping {}" }, ExampleToolTipTexts(list, ping));
          // The bottom bar has no description and no hint any more: only Use.
          Assert.Null(window.FindName("TextDescription"));
          Assert.Null(window.FindName("TextRuns"));
        });
      });
    }

    [Fact]
    public void The_examples_list_has_the_note_look_and_the_use_bar_the_button_height_as_margins() {
      WithCommand("cmd.exe", "", f => {
        RunCommandExamplesWindow window = new RunCommandExamplesWindow(new RunCommandViewModel(f.Command, true));
        Shown(window, () => {
          window.UpdateLayout();
          ListBox list = Named<ListBox>(window, "ListExamples");
          FrameworkElement body = Named<FrameworkElement>(window, "Body");
          Rect l = list.TransformToAncestor(body).TransformBounds(new Rect(0, 0, list.ActualWidth, list.ActualHeight));
          Assert.Equal(0, l.X, 1);
          Assert.Equal(0, l.Y, 1);
          Assert.Equal(body.ActualWidth, l.Width, 1);
          Assert.Equal(body.ActualHeight, l.Height, 1);
          Assert.Equal(((SolidColorBrush)window.Background).Color, Assert.IsType<SolidColorBrush>(list.Background).Color);
          Assert.Equal(new Thickness(0), list.BorderThickness);
          Assert.Equal(new Thickness(10), list.Padding);
          FrameworkElement footer = Named<FrameworkElement>(window, "Footer");
          Button use = Named<Button>(window, "ButtonUse");
          double h = use.ActualHeight;
          Assert.True(h > 10, "Use height " + h);
          Point useTopLeft = use.TranslatePoint(new Point(0, 0), footer);
          Assert.Equal(h, useTopLeft.Y, 1);
          Assert.Equal(footer.ActualHeight - h, useTopLeft.Y + use.ActualHeight, 1);
          Assert.Equal(footer.ActualWidth - h, useTopLeft.X + use.ActualWidth, 1);
        });
      });
    }

    [Fact]
    public void Use_fills_the_run_command_window_and_closes() {
      WithCommand("cmd.exe", "", f => {
        RunCommandViewModel vm = new RunCommandViewModel(f.Command, true);
        RunCommandExamplesWindow window = new RunCommandExamplesWindow(vm);
        OffScreen(window);
        bool closed = false;
        window.Closed += (s, e) => closed = true;
        window.Show();
        WpfAppHost.Pump();
        Named<ListBox>(window, "ListExamples").SelectedItem = RunCommandExamples.All.Single(e => e.Name == "Open the Startup folder");
        WpfAppHost.Pump();
        Click(Named<Button>(window, "ButtonUse"));
        if (!closed) { window.Close(); }
        Assert.True(closed);
        Assert.Equal(("explorer.exe", "shell:startup"), (vm.Program, vm.Arguments));
        Shortcut row = DataStore.Shortcuts.Get(f.Command.ID); // still nothing saved
        Assert.Equal(("cmd.exe", ""), (row.ProcessFileName, row.ProcessArguments));
      });
    }

    [Fact]
    public void Escape_and_X_close_the_examples_popup_without_changes() {
      WithCommand("cmd.exe", "/K mine", f => {
        foreach (bool escape in new[] { true, false }) {
          RunCommandViewModel vm = new RunCommandViewModel(f.Command, false);
          RunCommandExamplesWindow window = new RunCommandExamplesWindow(vm);
          OffScreen(window);
          bool closed = false;
          window.Closed += (s, e) => closed = true;
          window.Show();
          WpfAppHost.Pump();
          Named<ListBox>(window, "ListExamples").SelectedIndex = 0;
          if (escape) { PressEscape(window); } else { Click(Named<Button>(window, "ButtonClose")); }
          if (!closed) { window.Close(); }
          Assert.True(closed);
          Assert.Equal("mine", vm.Arguments);
        }
      });
    }

    // ---- Where the windows open ----

    // A 150% screen to the left of the primary with the taskbar at its top: work area 2000x1160 DIPs at (-2000, 40).
    private static readonly MDSHO.Common.ScreenInfo LeftScreen150 = new MDSHO.Common.ScreenInfo(
      "\\\\.\\DISPLAY2", new MDSHO.Common.ScreenRect(-3000, 0, 3000, 1800), new MDSHO.Common.ScreenRect(-3000, 60, 3000, 1740), 1.5, false);

    private static void WithScreen(MDSHO.Common.ScreenInfo screen, List<Window> asked, Action body) {
      Func<Window, MDSHO.Common.ScreenInfo> old = MDSHO.Helpers.DialogPlacement.ScreenOf;
      MDSHO.Helpers.DialogPlacement.ScreenOf = w => { asked.Add(w); return screen; };
      try {
        body();
      } finally {
        MDSHO.Helpers.DialogPlacement.ScreenOf = old;
      }
    }

    [Fact]
    public void Run_command_window_finds_the_view_of_the_commands_own_window_as_owner() {
      WithCommand("cmd.exe", "/K ping {}", f => {
        DesktopWindowView view = new DesktopWindowView(f.Win) { ShowActivated = false, Left = -10000, Top = -10000 };
        try {
          view.Show();
          WpfAppHost.Pump();
          Assert.Same(view, RunCommandWindow.FindOwner(f.Command));
        } finally {
          view.Close();
          WpfAppHost.Pump();
        }
      });
    }

    private static DesktopWindowView OffScreenView(DesktopWindowViewModel vm) {
      DesktopWindowView view = new DesktopWindowView(vm) { ShowActivated = false, ShowInTaskbar = false, Left = -10000, Top = -10000 };
      view.Show();
      return view;
    }

    /// <summary>The command's window and a clone of it, both open off screen; <paramref name="active"/> picks which one counts as active.</summary>
    private static void WithParentAndClone(Fixture f, Func<DesktopWindowView, DesktopWindowView, Window> active, Action<DesktopWindowView, DesktopWindowView> body) {
      f.Win.CloneWindow(@"\\.\DISPLAY2", -10000, -10000);
      DesktopWindowViewModel clone = f.Win.Clones.Single();
      DesktopWindowView parentView = OffScreenView(f.Win);
      DesktopWindowView cloneView = OffScreenView(clone);
      Func<Window, bool> old = RunCommandWindow.IsActiveWindow;
      Window activeWindow = active(parentView, cloneView);
      RunCommandWindow.IsActiveWindow = w => w == activeWindow;
      try {
        WpfAppHost.Pump();
        body(parentView, cloneView);
      } finally {
        RunCommandWindow.IsActiveWindow = old;
        cloneView.Close();
        parentView.Close();
        WpfAppHost.Pump();
      }
    }

    [Fact]
    public void Run_command_window_opens_over_the_active_clone_where_the_command_was_clicked() {
      WithCommand("cmd.exe", "/K ping {}", f => {
        WithParentAndClone(f, (parent, clone) => clone, (parent, clone) => {
          Assert.Same(clone, RunCommandWindow.FindOwner(f.Command));
        });
      });
    }

    [Fact]
    public void Run_command_window_opens_over_the_parent_when_the_parent_is_active() {
      WithCommand("cmd.exe", "/K ping {}", f => {
        WithParentAndClone(f, (parent, clone) => parent, (parent, clone) => {
          Assert.Same(parent, RunCommandWindow.FindOwner(f.Command));
        });
      });
    }

    [Fact]
    public void Run_command_window_ignores_an_active_window_of_another_panel_when_its_own_panel_is_open() {
      WithCommand("cmd.exe", "/K ping {}", f => {
        DesktopWindowView other = OffScreenView(f.App.AddWindow("OTHER", 0, 0));
        try {
          WithParentAndClone(f, (parent, clone) => other, (parent, clone) => {
            Assert.Same(parent, RunCommandWindow.FindOwner(f.Command));
          });
        } finally {
          other.Close();
        }
      });
    }

    [Fact]
    public void New_run_command_from_a_clones_menu_opens_over_that_clone() {
      WithCommand("cmd.exe", "/K ping {}", f => {
        WithParentAndClone(f, (parent, clone) => clone, (parent, clone) => {
          ShortcutViewModel created = null;
          ShortcutViewModel.OpenRunCommandEditor = (s, isNew) => created = s;
          ((DesktopWindowViewModel)clone.DataContext).NewProcessCommandShortcutCommand.Execute(null);
          Assert.NotNull(created);
          Assert.Same(clone, RunCommandWindow.FindOwner(created));
        });
      });
    }

    [Fact]
    public void Run_command_window_opens_centred_in_the_work_area_of_the_owners_screen() {
      WithCommand("cmd.exe", "/K ping {}", f => {
        DesktopWindowView view = new DesktopWindowView(f.Win) { ShowActivated = false, Left = -10000, Top = -10000 };
        List<Window> asked = new List<Window>();
        try {
          view.Show();
          WpfAppHost.Pump();
          WithScreen(LeftScreen150, asked, () => {
            RunCommandWindow window = RunCommandWindow.Create(f.Command, false, view);
            Assert.Same(view, window.Owner);
            Assert.Equal(WindowStartupLocation.Manual, window.WindowStartupLocation);
            Assert.Equal(-2000 + (2000 - window.Width) / 2, window.Left, 6);
            Assert.Equal(40 + (1160 - window.Height) / 2, window.Top, 6);
            window.Close();
          });
          Assert.Equal(new Window[] { view }, asked);
        } finally {
          view.Close();
          WpfAppHost.Pump();
        }
      });
    }

    [Fact]
    public void Run_command_window_without_an_owner_centres_on_the_screen() {
      WithCommand("cmd.exe", "/K ping {}", f => {
        RunCommandWindow window = RunCommandWindow.Create(f.Command, false, null);
        Assert.Null(window.Owner);
        Assert.Equal(WindowStartupLocation.CenterScreen, window.WindowStartupLocation);
        window.Close();
      });
    }

    [Fact]
    public void Examples_window_opens_centred_on_the_run_command_window_at_80_percent_of_its_size() {
      WithCommand("cmd.exe", "/K ping {}", f => {
        RunCommandWindow window = new RunCommandWindow(new RunCommandViewModel(f.Command, false));
        Shown(window, () => {
          window.Left = -9000;
          window.Top = -8000;
          window.Width = 560;
          window.Height = 580;
          window.UpdateLayout();
          RunCommandExamplesWindow examples = window.CreateExamples();
          Assert.Same(window, examples.Owner);
          Assert.Equal(WindowStartupLocation.Manual, examples.WindowStartupLocation);
          Assert.Equal(560 * 0.8, examples.Width, 1);
          Assert.Equal(580 * 0.8, examples.Height, 1);
          Assert.Equal(-9000 + (560 - examples.Width) / 2, examples.Left, 1);
          Assert.Equal(-8000 + (580 - examples.Height) / 2, examples.Top, 1);
          examples.Close();
        });
      });
    }

    [Fact]
    public void Examples_window_never_gets_smaller_than_its_minimum() {
      WithCommand("cmd.exe", "/K ping {}", f => {
        RunCommandWindow window = new RunCommandWindow(new RunCommandViewModel(f.Command, false));
        Shown(window, () => {
          window.Left = -9000;
          window.Top = -8000;
          window.Width = window.MinWidth;
          window.Height = window.MinHeight;
          window.UpdateLayout();
          RunCommandExamplesWindow examples = window.CreateExamples();
          Assert.True(examples.Width >= examples.MinWidth, "Width " + examples.Width);
          Assert.True(examples.Height >= examples.MinHeight, "Height " + examples.Height);
          examples.Close();
        });
      });
    }

    /// <summary>
    /// Fake window handles: their physical rects live in a dictionary instead of on real monitors. The first move of a
    /// window simulates Windows rescaling it for the target screen's DPI (size x 1.5 plus a few pixels).
    /// </summary>
    private sealed class FakeHandles {
      public readonly Dictionary<Window, Rect> Rects = new Dictionary<Window, Rect>();
      public readonly List<Point> Moves = new List<Point>();
      public Rect BoundsOf(Window w) {
        if (!Rects.ContainsKey(w)) { Rects[w] = new Rect(w.Left, w.Top, w.Width, w.Height); }
        return Rects[w];
      }
      public void Move(Window w, Point p) {
        Rect r = BoundsOf(w);
        Size size = Moves.Count == 0 ? new Size(r.Width * 1.5 + 3, r.Height * 1.5 + 5) : r.Size;
        Moves.Add(p);
        Rects[w] = new Rect(p, size);
      }
    }

    private static void WithFakeHandles(Action<FakeHandles> body) {
      FakeHandles fake = new FakeHandles();
      Func<Window, Rect> oldBounds = MDSHO.Helpers.DialogPlacement.PhysicalBoundsOf;
      Action<Window, Point> oldMove = MDSHO.Helpers.DialogPlacement.MoveToPhysical;
      MDSHO.Helpers.DialogPlacement.PhysicalBoundsOf = fake.BoundsOf;
      MDSHO.Helpers.DialogPlacement.MoveToPhysical = fake.Move;
      try {
        body(fake);
      } finally {
        MDSHO.Helpers.DialogPlacement.PhysicalBoundsOf = oldBounds;
        MDSHO.Helpers.DialogPlacement.MoveToPhysical = oldMove;
      }
    }

    /// <summary>Asserts the dialog's handle was moved onto the 150% screen and ends up centred there for its final size.</summary>
    private static void AssertPlacedInPhysicalPixels(FakeHandles fake, Window dialog) {
      // First move: the dialog's DIP size times the target screen's scale, centred in the physical work area.
      Assert.True(fake.Moves.Count >= 1, "The dialog was not moved through its window handle.");
      Point first = MDSHO.Helpers.GeometryConversions.ToPoint(MDSHO.Common.ScreenGeometry.CenterInWorkAreaPhysical(LeftScreen150, dialog.Width * 1.5, dialog.Height * 1.5));
      Assert.Equal(first, fake.Moves[0]);
      // Final rect: centred again for the size Windows gave it on the new screen.
      Rect final = fake.Rects[dialog];
      Assert.Equal(MDSHO.Common.ScreenGeometry.CenterInWorkAreaPhysical(LeftScreen150, final.Width, final.Height), MDSHO.Helpers.GeometryConversions.ToScreenPoint(final.TopLeft));
      Assert.True(LeftScreen150.WorkingArea.Contains(MDSHO.Helpers.GeometryConversions.ToScreenRect(final)));
    }

    [Fact]
    public void Run_command_window_is_placed_in_physical_pixels_on_a_screen_with_another_scale() {
      WithCommand("cmd.exe", "/K ping {}", f => {
        DesktopWindowView view = OffScreenView(f.Win);
        try {
          WpfAppHost.Pump();
          WithFakeHandles(fake => WithScreen(LeftScreen150, new List<Window>(), () => {
            RunCommandWindow window = RunCommandWindow.Create(f.Command, false, view);
            Assert.Empty(fake.Moves);
            // Creating the handle (as ShowDialog does, before the window appears) places it.
            new System.Windows.Interop.WindowInteropHelper(window).EnsureHandle();
            AssertPlacedInPhysicalPixels(fake, window);
            window.Close();
          }));
        } finally {
          view.Close();
          WpfAppHost.Pump();
        }
      });
    }

    [Fact]
    public void Examples_window_is_placed_in_physical_pixels_centred_on_the_run_command_window() {
      WithCommand("cmd.exe", "/K ping {}", f => {
        RunCommandWindow window = new RunCommandWindow(new RunCommandViewModel(f.Command, false));
        Shown(window, () => WithFakeHandles(fake => WithScreen(LeftScreen150, new List<Window>(), () => {
          // The owner's handle is on the 150 % screen, in physical pixels.
          fake.Rects[window] = new Rect(-1800, 200, 840, 870);
          RunCommandExamplesWindow examples = window.CreateExamples();
          Assert.Empty(fake.Moves);
          // Creating the handle (as ShowDialog does, before the window appears) moves it through the handle.
          new System.Windows.Interop.WindowInteropHelper(examples).EnsureHandle();
          Assert.NotEmpty(fake.Moves);
          Rect final = fake.Rects[examples];
          Assert.InRange(final.X + final.Width / 2 - (-1800 + 840 / 2.0), -1, 1); // handles move in whole pixels
          Assert.InRange(final.Y + final.Height / 2 - (200 + 870 / 2.0), -1, 1);
          examples.Close();
        })));
      });
    }

    // ---- Context menu in the desktop window ----

    [Fact]
    public void Context_menus_have_Set_Run_Command_and_no_Sample_Commands() {
      WithCommand("cmd.exe", "/K ping {}", f => {
        DesktopWindowView view = new DesktopWindowView(f.Win) { ShowActivated = false, Left = -10000, Top = -10000 };
        try {
          view.Show();
          WpfAppHost.Pump();
          f.Win.RootShortcuts[0].IsGroupExpanded = true;
          WpfAppHost.Pump();
          TreeView tree = (TreeView)view.FindName("TreeViewShortcuts");
          List<string> treeHeaders = tree.ContextMenu.Items.OfType<MenuItem>().Select(m => m.Header as string).ToList();
          Assert.DoesNotContain("Sample Commands", treeHeaders);

          TreeViewItem item = WpfAppHost.VisualDescendants<TreeViewItem>(tree).Single(i => i.DataContext == f.Command);
          Button button = WpfAppHost.VisualDescendants<Button>(item).First(b => b.DataContext == f.Command);
          List<MenuItem> items = button.ContextMenu.Items.OfType<MenuItem>().ToList();
          List<string> headers = items.Select(m => m.Header as string).ToList();
          Assert.DoesNotContain("Sample Commands", headers);
          Assert.DoesNotContain("Run as administrator", headers);
          Assert.DoesNotContain("Set Process File Name", headers);
          Assert.DoesNotContain("Set Process Arguments", headers);
          MenuItem setRun = items.Single(m => (m.Header as string) == "Set run command...");
          // Context menu bindings resolve when the menu opens: check what they bind to.
          Assert.Equal(nameof(ShortcutViewModel.SetRunCommandCommand), BindingOperations.GetBinding(setRun, MenuItem.CommandProperty).Path.Path);
          Assert.Equal(nameof(ShortcutViewModel.ShowCommand), BindingOperations.GetBinding(setRun, UIElement.VisibilityProperty).Path.Path);
          Assert.Equal("OwningWindow.Appearance.Icons.Run", BindingOperations.GetBinding((TextBlock)setRun.Icon, TextBlock.TextProperty).Path.Path);
        } finally {
          view.Close();
          WpfAppHost.Pump();
        }
      });
    }

    [Fact]
    public void Run_command_button_in_the_panel_shows_the_run_icon() {
      WithCommand("cmd.exe", "/K ping {}", f => {
        DesktopWindowView view = new DesktopWindowView(f.Win) { ShowActivated = false, Left = -10000, Top = -10000 };
        try {
          view.Show();
          WpfAppHost.Pump();
          f.Win.RootShortcuts[0].IsGroupExpanded = true;
          WpfAppHost.Pump();
          TreeView tree = (TreeView)view.FindName("TreeViewShortcuts");
          TreeViewItem item = WpfAppHost.VisualDescendants<TreeViewItem>(tree).Single(i => i.DataContext == f.Command);
          TextBlock icon = WpfAppHost.VisualDescendants<TextBlock>(item).Single(t => t.DataContext == f.Command && t.IsVisible
            && BindingOperations.GetBinding(t, UIElement.VisibilityProperty)?.Path.Path == nameof(ShortcutViewModel.ShowCommand));
          Assert.Equal(f.Win.Appearance.Icons.Run, icon.Text);
          Assert.Equal(f.Win.Appearance.Icons.SolidIcons ? "" : "", icon.Text);
          f.Win.Appearance.Icons.SolidIcons = !f.Win.Appearance.Icons.SolidIcons;
          WpfAppHost.Pump();
          Assert.Equal(f.Win.Appearance.Icons.SolidIcons ? "" : "", icon.Text);
        } finally {
          view.Close();
          WpfAppHost.Pump();
        }
      });
    }

    [Fact]
    public void Removed_run_command_menu_commands_are_gone_from_the_view_model() {
      Assert.Null(typeof(ShortcutViewModel).GetProperty("RunAsAdministratorCommand"));
      Assert.Null(typeof(ShortcutViewModel).GetProperty("SetProcessFileNameCommand"));
      Assert.Null(typeof(ShortcutViewModel).GetProperty("SetProcessArgumentsCommand"));
    }

    private static string Describe(object item) {
      return item is Separator ? "-" : item is MenuItem m ? m.Header as string ?? "(item)" : item.ToString();
    }

    [Fact]
    public void Shortcut_context_menu_has_no_stray_separators_for_a_command_or_a_group() {
      WithCommand("cmd.exe", "/K ping {}", f => {
        DesktopWindowView view = new DesktopWindowView(f.Win) { ShowActivated = false, Left = -10000, Top = -10000 };
        try {
          view.Show();
          WpfAppHost.Pump();
          ShortcutViewModel group = f.Win.RootShortcuts[0];
          group.IsGroupExpanded = true;
          WpfAppHost.Pump();
          TreeView tree = (TreeView)view.FindName("TreeViewShortcuts");
          foreach (ShortcutViewModel shortcut in new[] { f.Command, group }) {
            TreeViewItem item = WpfAppHost.VisualDescendants<TreeViewItem>(tree).Single(i => i.DataContext == shortcut);
            ContextMenu menu = WpfAppHost.VisualDescendants<Button>(item).First(b => b.DataContext == shortcut).ContextMenu;
            menu.DataContext = shortcut; // what opening the menu does: its items bind to the shortcut
            WpfAppHost.Pump();
            List<string> visible = menu.Items.Cast<object>()
              .Where(i => !(i is FrameworkElement e) || e.Visibility != Visibility.Collapsed)
              .Select(Describe).ToList();
            string all = shortcut.Name + ": " + string.Join(" | ", visible);
            Assert.NotEqual("-", visible.First());
            Assert.NotEqual("-", visible.Last());
            for (int i = 1; i < visible.Count; i++) {
              Assert.False(visible[i] == "-" && visible[i - 1] == "-", "Two separators in a row: " + all);
            }
            Assert.Equal(shortcut == f.Command, visible.Contains("Set run command..."));
          }
        } finally {
          view.Close();
          WpfAppHost.Pump();
        }
      });
    }

    // ---- Pictures for review (only when asked for) ----

    /// <summary>
    /// Saves PNGs of both windows rendered off screen. Does nothing unless the MDSHO_SCREENSHOT_DIR environment
    /// variable names an existing folder.
    /// </summary>
    [Fact]
    public void Render_pictures_when_asked() {
      string folder = Environment.GetEnvironmentVariable("MDSHO_SCREENSHOT_DIR");
      if (string.IsNullOrEmpty(folder) || !Directory.Exists(folder)) { return; }
      WithCommand("cmd.exe", "/K ping {}\r\ntracert {}", f => {
        // Outside MDSHO.exe the icon font's application pack URI finds nothing; point it at the MDSHO assembly for the pictures only.
        object oldFont = System.Windows.Application.Current.Resources["IconFont"];
        System.Windows.Application.Current.Resources["IconFont"] = new FontFamily(new Uri("pack://application:,,,/MDSHO;component/"), "./Assets/Fonts/#MDSHOFont");
        try {
          RenderBoth(f, folder);
        } finally {
          System.Windows.Application.Current.Resources["IconFont"] = oldFont;
        }
      });
    }

    private static void RenderBoth(Fixture f, string folder) {
      f.Command.ProcessWorkingFolder = @"C:\Logs\{}";
      RunCommandViewModel vm = new RunCommandViewModel(f.Command, false);
      RunCommandWindow window = new RunCommandWindow(vm);
      Shown(window, () => Save(window, Path.Combine(folder, "run-command-window.png")));
      RunCommandExamplesWindow examples = new RunCommandExamplesWindow(vm);
      Shown(examples, () => {
        Named<ListBox>(examples, "ListExamples").SelectedItem = RunCommandExamples.All.Single(e => e.Name == "Ping a computer");
        WpfAppHost.Pump();
        Save(examples, Path.Combine(folder, "run-command-examples.png"));
      });
    }

    private static void Save(Window window, string path) {
      FrameworkElement root = (FrameworkElement)VisualTreeHelper.GetChild(window, 0);
      root.UpdateLayout();
      RenderTargetBitmap bitmap = new RenderTargetBitmap((int)Math.Ceiling(root.ActualWidth), (int)Math.Ceiling(root.ActualHeight), 96, 96, PixelFormats.Pbgra32);
      DrawingVisual visual = new DrawingVisual();
      using (DrawingContext dc = visual.RenderOpen()) {
        dc.DrawRectangle(Brushes.White, null, new Rect(0, 0, root.ActualWidth, root.ActualHeight));
        dc.DrawRectangle(new VisualBrush(root), null, new Rect(0, 0, root.ActualWidth, root.ActualHeight));
      }
      bitmap.Render(visual);
      PngBitmapEncoder encoder = new PngBitmapEncoder();
      encoder.Frames.Add(BitmapFrame.Create(bitmap));
      using (FileStream fs = File.Create(path)) { encoder.Save(fs); }
    }
  }
}
