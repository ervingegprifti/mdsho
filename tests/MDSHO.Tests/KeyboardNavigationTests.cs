using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using MDSHO.Common;
using MDSHO.Helpers;
using MDSHO.ViewModels;
using Xunit;

namespace MDSHO.Tests {

  /// <summary>
  /// Keyboard use in MDSHO's dialogs, like a Windows message box and dialog: focus starts on the default button, the arrow
  /// keys move between buttons, plain Y/N/C/O (or Alt with the letter) answer, Esc closes, Enter presses Apply, radio
  /// groups are one Tab stop chosen with the arrows, and a visible focus ring. Windows are shown off screen, never activated.
  /// </summary>
  public class KeyboardNavigationTests {

    private static T Named<T>(FrameworkElement root, string name) where T : class {
      return Assert.IsAssignableFrom<T>(root.FindName(name));
    }

    private static T ShowOffScreen<T>(T window) where T : Window {
      window.WindowStartupLocation = WindowStartupLocation.Manual;
      window.Left = -10000;
      window.Top = -10000;
      window.ShowActivated = false;
      window.ShowInTaskbar = false;
      window.Show();
      WpfAppHost.Pump();
      return window;
    }

    private static void CloseIfOpen(Window window) {
      if (PresentationSource.FromVisual(window) != null) {
        window.Close();
      }
      WpfAppHost.Pump();
    }

    // A key press sent to an element, as the keyboard would send it to the focused element.
    private static void Press(Window window, IInputElement target, Key key, RoutedEvent routedEvent) {
      KeyEventArgs args = new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(window), 0, key) { RoutedEvent = routedEvent };
      target.RaiseEvent(args);
      WpfAppHost.Pump();
    }

    // Both halves of a key press: the preview (tunnel) and then, if nobody took it, the key down (bubble).
    private static void PressKey(Window window, IInputElement target, Key key) {
      KeyEventArgs preview = new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(window), 0, key) { RoutedEvent = Keyboard.PreviewKeyDownEvent };
      target.RaiseEvent(preview);
      if (!preview.Handled) {
        Press(window, target, key, Keyboard.KeyDownEvent);
      } else {
        WpfAppHost.Pump();
      }
    }

    private static IInputElement Focused(Window window) {
      return FocusManager.GetFocusedElement(window);
    }

    private static bool IsOpen(Window window) {
      return PresentationSource.FromVisual(window) != null;
    }

    // ---- The message window ----

    private static void WithMessage(MessageButtons buttons, MessageResult defaultResult, Action<MessageWindow> check) {
      WpfAppHost.Run(() => {
        MessageWindow window = ShowOffScreen(new MessageWindow("T", "Question?", "", MessageIcon.Warning, buttons, defaultResult));
        try {
          check(window);
        } finally {
          CloseIfOpen(window);
        }
      });
    }

    [Theory]
    [InlineData(MessageButtons.YesNo, MessageResult.No, "ButtonNo")]
    [InlineData(MessageButtons.YesNoCancel, MessageResult.Yes, "ButtonYes")]
    [InlineData(MessageButtons.OKCancel, MessageResult.OK, "ButtonOK")]
    [InlineData(MessageButtons.OK, MessageResult.None, "ButtonOK")]
    public void Focus_starts_on_the_default_button(MessageButtons buttons, MessageResult defaultResult, string expected) {
      WithMessage(buttons, defaultResult, window => Assert.Same(window.FindName(expected), Focused(window)));
    }

    [Fact]
    public void Right_and_left_move_between_the_buttons_and_wrap_around() {
      WithMessage(MessageButtons.YesNoCancel, MessageResult.Yes, window => {
        string Now() => ((Button)Focused(window)).Name;
        Press(window, Focused(window), Key.Right, Keyboard.PreviewKeyDownEvent);
        Assert.Equal("ButtonNo", Now());
        Press(window, Focused(window), Key.Right, Keyboard.PreviewKeyDownEvent);
        Assert.Equal("ButtonCancel", Now());
        Press(window, Focused(window), Key.Right, Keyboard.PreviewKeyDownEvent);
        Assert.Equal("ButtonYes", Now());
        Press(window, Focused(window), Key.Left, Keyboard.PreviewKeyDownEvent);
        Assert.Equal("ButtonCancel", Now());
        Assert.True(IsOpen(window));
      });
    }

    [Fact]
    public void In_the_delete_prompt_left_from_no_goes_to_yes() {
      WithMessage(MessageButtons.YesNo, MessageResult.No, window => {
        Press(window, Focused(window), Key.Left, Keyboard.PreviewKeyDownEvent);
        Assert.Same(window.FindName("ButtonYes"), Focused(window));
      });
    }

    [Theory]
    [InlineData(MessageButtons.YesNo, Key.Y, MessageResult.Yes)]
    [InlineData(MessageButtons.YesNo, Key.N, MessageResult.No)]
    [InlineData(MessageButtons.YesNoCancel, Key.C, MessageResult.Cancel)]
    [InlineData(MessageButtons.OKCancel, Key.O, MessageResult.OK)]
    [InlineData(MessageButtons.OK, Key.O, MessageResult.OK)]
    public void A_plain_letter_answers_and_closes(MessageButtons buttons, Key key, MessageResult expected) {
      WithMessage(buttons, MessageResult.None, window => {
        Press(window, Focused(window), key, Keyboard.PreviewKeyDownEvent);
        Assert.Equal(expected, window.Result);
        Assert.False(IsOpen(window));
      });
    }

    [Fact]
    public void A_letter_without_a_button_or_with_ctrl_does_nothing() {
      Func<ModifierKeys> original = MessageWindow.KeyboardModifiers;
      try {
        WithMessage(MessageButtons.YesNo, MessageResult.No, window => {
          Press(window, Focused(window), Key.C, Keyboard.PreviewKeyDownEvent);
          Assert.True(IsOpen(window));
          MessageWindow.KeyboardModifiers = () => ModifierKeys.Control;
          Press(window, Focused(window), Key.Y, Keyboard.PreviewKeyDownEvent);
          Assert.True(IsOpen(window));
          Assert.Equal(MessageResult.None, window.Result);
        });
      } finally {
        MessageWindow.KeyboardModifiers = original;
      }
    }

    [Fact]
    public void The_buttons_show_their_letter_underlined_and_name_it_for_screen_readers() {
      WithMessage(MessageButtons.YesNoCancel, MessageResult.Yes, window => {
        foreach ((string name, string letter, string rest) in new[] { ("ButtonYes", "Y", "es"), ("ButtonNo", "N", "o"), ("ButtonCancel", "C", "ancel") }) {
          Button button = Named<Button>(window, name);
          Assert.Equal(letter + rest, button.Content);
          Assert.Equal(letter, System.Windows.Automation.AutomationProperties.GetAccessKey(button));
          List<TextBlock> blocks = WpfAppHost.VisualDescendants<TextBlock>(button).ToList();
          TextBlock underlined = Assert.Single(blocks, b => b.TextDecorations != null && b.TextDecorations.Any(d => d.Location == TextDecorationLocation.Underline));
          Assert.Equal(letter, underlined.Text);
          Assert.Equal(letter + rest, string.Concat(blocks.Select(b => b.Text)));
        }
      });
    }

    [Fact]
    public void The_rules_give_each_button_its_letter() {
      Assert.Equal("Y", MessageRules.AccessKey(MessageResult.Yes));
      Assert.Equal("N", MessageRules.AccessKey(MessageResult.No));
      Assert.Equal("C", MessageRules.AccessKey(MessageResult.Cancel));
      Assert.Equal("O", MessageRules.AccessKey(MessageResult.OK));
      Assert.Equal("", MessageRules.AccessKey(MessageResult.None));
      Assert.Equal(MessageResult.No, MessageRules.ResultOfKey(MessageButtons.YesNo, 'n'));
      Assert.Equal(MessageResult.Yes, MessageRules.ResultOfKey(MessageButtons.YesNoCancel, 'Y'));
      Assert.Equal(MessageResult.None, MessageRules.ResultOfKey(MessageButtons.YesNo, 'C'));
      Assert.Equal(MessageResult.None, MessageRules.ResultOfKey(MessageButtons.OK, 'Y'));
    }

    // ---- The focus ring ----

    private static Style FocusVisualOf(Style style) {
      for (Style s = style; s != null; s = s.BasedOn) {
        Setter setter = s.Setters.OfType<Setter>().FirstOrDefault(x => x.Property == FrameworkElement.FocusVisualStyleProperty);
        if (setter != null) {
          return setter.Value as Style;
        }
      }
      return null;
    }

    [Fact]
    public void Mdsho_buttons_show_a_2_pixel_focus_ring_in_the_theme_focus_colour() {
      WpfAppHost.Run(() => {
        MessageWindow window = ShowOffScreen(new MessageWindow("T", "Q?", "", MessageIcon.Info, MessageButtons.YesNo, MessageResult.No));
        try {
          Style ring = Assert.IsType<Style>(window.FindResource("StyleFocusRing"));
          Assert.Same(ring, FocusVisualOf((Style)window.FindResource("StyleDarkButton")));
          Assert.Same(ring, FocusVisualOf((Style)window.FindResource("StyleLightButton")));
          ControlTemplate template = Assert.IsType<ControlTemplate>(ring.Setters.OfType<Setter>().Single(s => s.Property == Control.TemplateProperty).Value);
          Control host = new Control { Template = template };
          host.ApplyTemplate();
          Border border = Assert.IsType<Border>(System.Windows.Media.VisualTreeHelper.GetChild(host, 0));
          Assert.Equal(new Thickness(2), border.BorderThickness);
        } finally {
          CloseIfOpen(window);
        }
      });
    }

    [Fact]
    public void The_focus_ring_follows_the_theme() {
      WpfAppHost.Run(() => {
        Style ring = (Style)Application.Current.FindResource("StyleFocusRing");
        ControlTemplate template = (ControlTemplate)ring.Setters.OfType<Setter>().Single(s => s.Property == Control.TemplateProperty).Value;
        Border border = (Border)template.LoadContent();
        Assert.Equal("Theme.FocusBorder", DynamicKeyOf(border, Border.BorderBrushProperty));
      });
    }

    private static object DynamicKeyOf(DependencyObject d, DependencyProperty property) {
      object local = d.ReadLocalValue(property);
      System.Reflection.PropertyInfo key = local?.GetType().GetProperty("ResourceKey", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public);
      return key?.GetValue(local);
    }

    // ---- Settings ----

    private static void WithSettings(Action<SettingsWindow> check) {
      using (TempDb db = new TempDb())
      using (FakeAppServices fake = new FakeAppServices()) {
        WpfAppHost.Run(() => {
          DesktopWindowViewModel w = new AppViewModel().AddWindow("TEST", 100, 100);
          SettingsWindow window = ShowOffScreen(new SettingsWindow(w));
          try {
            check(window);
          } finally {
            CloseIfOpen(window);
            WindowThemes.Apply(WindowThemeKind.Light);
            WpfAppHost.Pump();
          }
        });
      }
    }

    [Fact]
    public void Settings_starts_on_the_chosen_page_in_the_list() {
      WithSettings(window => {
        ListBox pages = Named<ListBox>(window, "ListSettingsPages");
        Assert.Same(pages.SelectedItem, Focused(window));
      });
    }

    [Fact]
    public void Settings_esc_closes_and_enter_is_apply() {
      WithSettings(window => {
        Assert.True(Named<Button>(window, "ButtonApply").IsDefault);
        PressKey(window, Focused(window), Key.Escape);
        Assert.False(IsOpen(window));
      });
    }

    [Theory]
    [InlineData("PanelIconsAppearance")]
    [InlineData("PanelWindowTheme")]
    public void Settings_radio_groups_are_one_tab_stop_on_the_chosen_option(string panelName) {
      WithSettings(window => {
        Named<ListBoxItem>(window, "PageItemAppearance").IsSelected = true;
        WpfAppHost.Pump();
        Panel panel = Named<Panel>(window, panelName);
        // Only the chosen option is a Tab stop, so Tab enters the group once, on it.
        List<RadioButton> radios = panel.Children.OfType<RadioButton>().ToList();
        Assert.True(radios.Count > 1);
        Assert.All(radios, r => Assert.Equal(r.IsChecked == true, r.IsTabStop));
      });
    }

    [Fact]
    public void Settings_arrows_in_a_radio_group_move_and_choose_and_wrap() {
      WithSettings(window => {
        Named<ListBoxItem>(window, "PageItemAppearance").IsSelected = true;
        WpfAppHost.Pump();
        Panel panel = Named<Panel>(window, "PanelWindowTheme");
        RadioButton same = Named<RadioButton>(window, "RadioThemeSameAsWindows");
        RadioButton light = Named<RadioButton>(window, "RadioThemeLight");
        RadioButton dark = Named<RadioButton>(window, "RadioThemeDark");
        same.IsChecked = true;
        WpfAppHost.Pump();
        Press(window, same, Key.Down, Keyboard.PreviewKeyDownEvent);
        Assert.True(light.IsChecked);
        Assert.Same(light, Focused(window));
        Assert.True(light.IsTabStop);
        Assert.False(same.IsTabStop);
        Assert.False(dark.IsTabStop);
        Press(window, light, Key.Right, Keyboard.PreviewKeyDownEvent);
        Assert.True(dark.IsChecked);
        Press(window, dark, Key.Down, Keyboard.PreviewKeyDownEvent);
        Assert.True(same.IsChecked);
        Press(window, same, Key.Up, Keyboard.PreviewKeyDownEvent);
        Assert.True(dark.IsChecked);
        Press(window, dark, Key.Left, Keyboard.PreviewKeyDownEvent);
        Assert.True(light.IsChecked);
      });
    }

    // ---- Colours ----

    [Fact]
    public void Colours_starts_on_the_colour_list_esc_closes_and_enter_is_apply() {
      using (TempDb db = new TempDb())
      using (FakeAppServices fake = new FakeAppServices()) {
        WpfAppHost.Run(() => {
          DesktopWindowViewModel w = new AppViewModel().AddWindow("TEST", 100, 100);
          ColorsWindow window = ShowOffScreen(new ColorsWindow(w));
          try {
            ListBox targets = Named<ListBox>(window, "ListColorTargets");
            Assert.Same(targets.SelectedItem, Focused(window));
            Assert.True(Named<Button>(window, "ButtonApply").IsDefault);
            PressKey(window, Focused(window), Key.Escape);
            Assert.False(IsOpen(window));
          } finally {
            CloseIfOpen(window);
          }
        });
      }
    }

    // ---- About ----

    [Fact]
    public void About_starts_on_the_first_link_and_esc_closes() {
      using (FakeAppServices fake = new FakeAppServices()) {
        WpfAppHost.Run(() => {
          AboutWindow window = ShowOffScreen(new AboutWindow());
          try {
            Assert.Same(window.FindName("HyperlinkProject"), Focused(window));
            PressKey(window, Focused(window), Key.Escape);
            Assert.False(IsOpen(window));
          } finally {
            CloseIfOpen(window);
          }
        });
      }
    }

    // ---- Review ----

    [Fact]
    public void Review_starts_on_rate_mdsho() {
      using (TempDb db = new TempDb())
      using (FakeAppServices fake = new FakeAppServices()) {
        WpfAppHost.Run(() => {
          ReviewPrompt prompt = new ReviewPrompt(new FakeClock(), new ScreenSetupTests.FakeTimer(), () => false, p => { }, fake.Services.StoreActions, false);
          ReviewWindow window = ShowOffScreen(new ReviewWindow(prompt));
          try {
            Assert.Same(window.FindName("ButtonRate"), Focused(window));
          } finally {
            CloseIfOpen(window);
          }
        });
      }
    }

    // ---- Focus back after a prompt with no owner ----

    [Fact]
    public void A_prompt_without_an_owner_gives_the_focus_back_to_the_window_active_before() {
      List<IntPtr> activated = new List<IntPtr>();
      (Func<IntPtr, bool> visible, Action<IntPtr> activate) original = (DialogFocus.IsVisible, DialogFocus.Activate);
      try {
        DialogFocus.IsVisible = h => h == (IntPtr)42;
        DialogFocus.Activate = h => activated.Add(h);
        WpfAppHost.Run(() => {
          MessageWindow dialog = new MessageWindow("T", "Q?", "", MessageIcon.Info, MessageButtons.OK, MessageResult.None);
          DialogFocus.GiveBack((IntPtr)42, dialog);
          DialogFocus.GiveBack((IntPtr)7, dialog);
          DialogFocus.GiveBack(IntPtr.Zero, dialog);
          dialog.Close();
        });
        Assert.Equal(new[] { (IntPtr)42 }, activated);
      } finally {
        DialogFocus.IsVisible = original.visible;
        DialogFocus.Activate = original.activate;
      }
    }

    [Fact]
    public void A_prompt_with_an_owner_leaves_the_focus_to_windows() {
      List<IntPtr> activated = new List<IntPtr>();
      (Func<IntPtr, bool> visible, Action<IntPtr> activate) original = (DialogFocus.IsVisible, DialogFocus.Activate);
      try {
        DialogFocus.IsVisible = h => true;
        DialogFocus.Activate = h => activated.Add(h);
        WpfAppHost.Run(() => {
          Window owner = ShowOffScreen(new Window { Width = 100, Height = 100 });
          try {
            MessageWindow dialog = new MessageWindow("T", "Q?", "", MessageIcon.Info, MessageButtons.OK, MessageResult.None) { Owner = owner };
            DialogFocus.GiveBack((IntPtr)42, dialog);
            dialog.Close();
          } finally {
            CloseIfOpen(owner);
          }
        });
        Assert.Empty(activated);
      } finally {
        DialogFocus.IsVisible = original.visible;
        DialogFocus.Activate = original.activate;
      }
    }
  }
}
