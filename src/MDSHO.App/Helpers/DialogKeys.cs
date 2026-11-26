using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace MDSHO.Helpers {

  /// <summary>
  /// Keyboard use shared by MDSHO's dialogs, like a Windows dialog: Esc closes (<see cref="EscClosesProperty"/>), the focus
  /// starts on a chosen control (<see cref="FirstFocusProperty"/>), and a radio group is one Tab stop on the chosen option
  /// whose arrow keys move and choose (<see cref="RadioGroupProperty"/>).
  /// </summary>
  public static class DialogKeys {

    /// <summary>On a window: Esc closes it, like its close button, unless the focused control used the key (an open list).</summary>
    public static readonly DependencyProperty EscClosesProperty = DependencyProperty.RegisterAttached(
      "EscCloses", typeof(bool), typeof(DialogKeys), new PropertyMetadata(false, OnEscClosesChanged));

    /// <summary>Gets <see cref="EscClosesProperty"/>.</summary>
    public static bool GetEscCloses(DependencyObject d) { return (bool)d.GetValue(EscClosesProperty); }

    /// <summary>Sets <see cref="EscClosesProperty"/>.</summary>
    public static void SetEscCloses(DependencyObject d, bool value) { d.SetValue(EscClosesProperty, value); }

    /// <summary>On a window: the control that has the focus when it opens. For a list, its chosen item.</summary>
    public static readonly DependencyProperty FirstFocusProperty = DependencyProperty.RegisterAttached(
      "FirstFocus", typeof(object), typeof(DialogKeys), new PropertyMetadata(null, OnFirstFocusChanged));

    /// <summary>Gets <see cref="FirstFocusProperty"/>.</summary>
    public static object GetFirstFocus(DependencyObject d) { return d.GetValue(FirstFocusProperty); }

    /// <summary>Sets <see cref="FirstFocusProperty"/>.</summary>
    public static void SetFirstFocus(DependencyObject d, object value) { d.SetValue(FirstFocusProperty, value); }

    /// <summary>On a panel of radio buttons: one Tab stop, on the chosen option; the arrow keys move, choose and wrap.</summary>
    public static readonly DependencyProperty RadioGroupProperty = DependencyProperty.RegisterAttached(
      "RadioGroup", typeof(bool), typeof(DialogKeys), new PropertyMetadata(false, OnRadioGroupChanged));

    /// <summary>Gets <see cref="RadioGroupProperty"/>.</summary>
    public static bool GetRadioGroup(DependencyObject d) { return (bool)d.GetValue(RadioGroupProperty); }

    /// <summary>Sets <see cref="RadioGroupProperty"/>.</summary>
    public static void SetRadioGroup(DependencyObject d, bool value) { d.SetValue(RadioGroupProperty, value); }

    // ---- Esc ----

    private static void OnEscClosesChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) {
      if (d is Window window) {
        window.KeyDown -= Window_KeyDown;
        if ((bool)e.NewValue) {
          window.KeyDown += Window_KeyDown;
        }
      }
    }

    // KeyDown bubbles up after the focused control: an open combo box list closes first and the window stays open.
    private static void Window_KeyDown(object sender, KeyEventArgs e) {
      if (!e.Handled && e.Key == Key.Escape && sender is Window window) {
        e.Handled = true;
        window.Close();
      }
    }

    // ---- First focus ----

    private static void OnFirstFocusChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) {
      if (!(d is Window window)) {
        return;
      }
      window.Loaded -= Window_Loaded;
      window.Loaded += Window_Loaded;
      if (window.IsLoaded) {
        Focus(window);
      }
    }

    private static void Window_Loaded(object sender, RoutedEventArgs e) {
      Focus((Window)sender);
    }

    private static void Focus(Window window) {
      object first = GetFirstFocus(window);
      if (first is ListBox list && list.SelectedItem != null && list.ItemContainerGenerator.ContainerFromItem(list.SelectedItem) is IInputElement item) {
        first = item;
      }
      if (first is IInputElement element) {
        // The window's focused element: it gets the keyboard focus whenever the window is active.
        FocusManager.SetFocusedElement(window, element);
        element.Focus();
      }
    }

    // ---- Radio groups ----

    private static void OnRadioGroupChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) {
      if (!(d is Panel panel) || !(bool)e.NewValue) {
        return;
      }
      panel.PreviewKeyDown += Group_PreviewKeyDown;
      panel.Loaded += (s, a) => {
        foreach (RadioButton radio in Radios(panel)) {
          radio.Checked -= Radio_CheckedChanged;
          radio.Checked += Radio_CheckedChanged;
          radio.Unchecked -= Radio_CheckedChanged;
          radio.Unchecked += Radio_CheckedChanged;
        }
        ShowTabStop(panel);
      };
    }

    private static void Radio_CheckedChanged(object sender, RoutedEventArgs e) {
      if (((FrameworkElement)sender).Parent is Panel panel) {
        ShowTabStop(panel);
      }
    }

    private static List<RadioButton> Radios(Panel panel) {
      return panel.Children.OfType<RadioButton>().Where(r => r.IsEnabled).ToList();
    }

    // Only the chosen option is a Tab stop (the first one when none is chosen), so Tab enters the group once.
    private static void ShowTabStop(Panel panel) {
      List<RadioButton> radios = Radios(panel);
      RadioButton chosen = radios.FirstOrDefault(r => r.IsChecked == true) ?? radios.FirstOrDefault();
      foreach (RadioButton radio in radios) {
        radio.IsTabStop = radio == chosen;
      }
    }

    private static void Group_PreviewKeyDown(object sender, KeyEventArgs e) {
      Panel panel = (Panel)sender;
      int step = e.Key == Key.Down || e.Key == Key.Right ? 1 : e.Key == Key.Up || e.Key == Key.Left ? -1 : 0;
      List<RadioButton> radios = Radios(panel);
      int now = radios.IndexOf(e.OriginalSource as RadioButton);
      if (step == 0 || now < 0) {
        return;
      }
      e.Handled = true;
      RadioButton next = radios[(now + step + radios.Count) % radios.Count];
      next.IsChecked = true;
      ShowTabStop(panel);
      FocusManager.SetFocusedElement(FocusManager.GetFocusScope(next), next);
      next.Focus();
    }
  }
}
