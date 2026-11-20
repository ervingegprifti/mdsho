using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using MDSHO.Common;

namespace MDSHO.Controls {

  /// <summary>
  /// In-place edit control: shows a TextBlock (Text) or, while IsEditing, a TextBox (EditText).
  /// Enter runs CommitCommand, Esc runs CancelCommand, leaving the box runs CommitCommand.
  /// While editing it swallows its own mouse down/up/double-click so a parent Button never sees them.
  /// Clones of a panel share their buttons, so IsEditing turns on in every clone: only the control in the active window
  /// shows and focuses the edit box (see <see cref="EditBoxRules"/>); the others keep showing the text (ShowsEditBox).
  /// Lookless control (default style in Themes/Generic.xaml) so each use can restyle through the normal Control properties.
  /// </summary>
  [TemplatePart(Name = "PART_Display", Type = typeof(TextBlock))]
  [TemplatePart(Name = "PART_EditBox", Type = typeof(TextBox))]
  public class EditableTextBlock : Control {
    /// <summary>Dependency property of Text.</summary>
    public static readonly DependencyProperty TextProperty =
      DependencyProperty.Register(nameof(Text), typeof(string), typeof(EditableTextBlock), new PropertyMetadata(string.Empty));
    /// <summary>Dependency property of EditText.</summary>
    public static readonly DependencyProperty EditTextProperty =
      DependencyProperty.Register(nameof(EditText), typeof(string), typeof(EditableTextBlock),
        new FrameworkPropertyMetadata(string.Empty, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));
    /// <summary>Dependency property of IsEditing.</summary>
    public static readonly DependencyProperty IsEditingProperty =
      DependencyProperty.Register(nameof(IsEditing), typeof(bool), typeof(EditableTextBlock), new PropertyMetadata(false, OnIsEditingChanged));
    /// <summary>Dependency property of CommitCommand.</summary>
    public static readonly DependencyProperty CommitCommandProperty =
      DependencyProperty.Register(nameof(CommitCommand), typeof(ICommand), typeof(EditableTextBlock));
    /// <summary>Dependency property of CancelCommand.</summary>
    public static readonly DependencyProperty CancelCommandProperty =
      DependencyProperty.Register(nameof(CancelCommand), typeof(ICommand), typeof(EditableTextBlock));
    /// <summary>Padding of the edit box (Padding is the display text's).</summary>
    public static readonly DependencyProperty EditPaddingProperty =
      DependencyProperty.Register(nameof(EditPadding), typeof(Thickness), typeof(EditableTextBlock), new PropertyMetadata(new Thickness(0)));

    static EditableTextBlock() {
      DefaultStyleKeyProperty.OverrideMetadata(typeof(EditableTextBlock), new FrameworkPropertyMetadata(typeof(EditableTextBlock)));
    }

    /// <summary>The text shown while not editing.</summary>
    public string Text { get { return (string)GetValue(TextProperty); } set { SetValue(TextProperty, value); } }
    /// <summary>The text in the edit box while editing.</summary>
    public string EditText { get { return (string)GetValue(EditTextProperty); } set { SetValue(EditTextProperty, value); } }
    /// <summary>True while the edit box is open instead of the plain text.</summary>
    public bool IsEditing { get { return (bool)GetValue(IsEditingProperty); } set { SetValue(IsEditingProperty, value); } }
    /// <summary>Run when the user presses Enter or leaves the edit box.</summary>
    public ICommand CommitCommand { get { return (ICommand)GetValue(CommitCommandProperty); } set { SetValue(CommitCommandProperty, value); } }
    /// <summary>Run when the user presses Escape.</summary>
    public ICommand CancelCommand { get { return (ICommand)GetValue(CancelCommandProperty); } set { SetValue(CancelCommandProperty, value); } }
    /// <summary>Space around the text inside the edit box.</summary>
    public Thickness EditPadding { get { return (Thickness)GetValue(EditPaddingProperty); } set { SetValue(EditPaddingProperty, value); } }

    private static readonly DependencyPropertyKey ShowsEditBoxPropertyKey =
      DependencyProperty.RegisterReadOnly(nameof(ShowsEditBox), typeof(bool), typeof(EditableTextBlock), new PropertyMetadata(false));
    /// <summary>Dependency property of ShowsEditBox.</summary>
    public static readonly DependencyProperty ShowsEditBoxProperty = ShowsEditBoxPropertyKey.DependencyProperty;
    /// <summary>True while this control shows the edit box: editing, and not a clone of the window where the edit started.</summary>
    public bool ShowsEditBox { get { return (bool)GetValue(ShowsEditBoxProperty); } private set { SetValue(ShowsEditBoxPropertyKey, value); } }

    [DllImport("user32.dll")]
    private static extern IntPtr GetActiveWindow();
    [ThreadStatic]
    private static Func<IntPtr> activeWindow;
    /// <summary>The active window of the UI thread; tests replace it (per thread). Null restores GetActiveWindow.</summary>
    public static Func<IntPtr> ActiveWindow {
      get { return activeWindow ?? GetActiveWindow; }
      set { activeWindow = value; }
    }

    private TextBox editBox;
    private bool movingFocus;

    /// <summary>Creates the control.</summary>
    public EditableTextBlock() {
      // MouseLeftButtonDown/Up are direct events: a parent Button only sees them because the bubbling MouseDown/MouseUp is promoted on each ancestor,
      // so the bubbling events are the ones to stop. handledEventsToo: TextBox marks some mouse events handled itself.
      AddHandler(MouseDownEvent, new MouseButtonEventHandler(SwallowMouse), true);
      AddHandler(MouseUpEvent, new MouseButtonEventHandler(SwallowMouse), true);
      AddHandler(MouseLeftButtonDownEvent, new MouseButtonEventHandler(SwallowMouse), true);
      AddHandler(MouseLeftButtonUpEvent, new MouseButtonEventHandler(SwallowMouse), true);
      AddHandler(MouseDoubleClickEvent, new MouseButtonEventHandler(SwallowMouse), true);
    }

    /// <summary>Finds the display and edit parts of the template and wires up their events.</summary>
    public override void OnApplyTemplate() {
      base.OnApplyTemplate();
      if (editBox != null) {
        editBox.KeyDown -= EditBox_KeyDown;
        editBox.LostFocus -= EditBox_LostFocus;
      }
      editBox = GetTemplateChild("PART_EditBox") as TextBox;
      if (editBox != null) {
        editBox.KeyDown += EditBox_KeyDown;
        editBox.LostFocus += EditBox_LostFocus;
      }
    }

    private void SwallowMouse(object sender, MouseButtonEventArgs e) {
      if (ShowsEditBox) { e.Handled = true; }
    }

    private static void OnIsEditingChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) {
      EditableTextBlock control = (EditableTextBlock)d;
      if (!(bool)e.NewValue) {
        control.ShowsEditBox = false;
        return;
      }
      control.ApplyEditMode(false);
      // Deferred so it runs after a closing context menu has given focus back (and a new button is in its window).
      control.Dispatcher.BeginInvoke(DispatcherPriority.Input, new Action(() => control.ApplyEditMode(true)));
    }

    /// <summary>
    /// Opens the edit box unless the rename started in another window (a clone). Focus goes to the box only while its window
    /// is the active one: focusing a box in an inactive window would bring that window in front of other apps.
    /// </summary>
    private void ApplyEditMode(bool focus) {
      if (!IsEditing) {
        return;
      }
      HwndSource source = PresentationSource.FromVisual(this) as HwndSource;
      if (source == null && focus) {
        // Not in a window yet (a new button): decide once it is.
        Loaded -= EditableTextBlock_Loaded;
        Loaded += EditableTextBlock_Loaded;
      }
      EditBoxMode mode = EditBoxRules.Decide(source?.Handle ?? IntPtr.Zero, ActiveWindow());
      ShowsEditBox = mode != EditBoxMode.ShowName;
      if (focus && ShowsEditBox && editBox != null) {
        if (mode == EditBoxMode.ShowAndFocus) {
          editBox.Focus();
        }
        editBox.SelectAll();
      }
    }

    private void EditableTextBlock_Loaded(object sender, RoutedEventArgs e) {
      Loaded -= EditableTextBlock_Loaded;
      ApplyEditMode(true);
    }

    private void EditBox_KeyDown(object sender, KeyEventArgs e) {
      if (e.Key != Key.Return && e.Key != Key.Escape) { return; }
      ICommand command = e.Key == Key.Return ? CommitCommand : CancelCommand;
      e.Handled = true;
      if (command != null && command.CanExecute(null)) { command.Execute(null); }
      MoveFocusOut();
    }

    private void EditBox_LostFocus(object sender, RoutedEventArgs e) {
      if (movingFocus || !IsEditing) { return; }
      ICommand command = CommitCommand;
      if (command != null && command.CanExecute(null)) { command.Execute(null); }
    }

    /// <summary>After Enter/Esc the (now hidden) edit box must not keep keyboard focus: give it to the nearest focusable ancestor (so tree keyboard shortcuts keep working).</summary>
    private void MoveFocusOut() {
      movingFocus = true;
      try {
        for (DependencyObject d = VisualTreeHelper.GetParent(this); d != null; d = VisualTreeHelper.GetParent(d)) {
          if (d is UIElement element && element.Focusable && element.IsVisible) {
            Keyboard.Focus(element);
            break;
          }
        }
      } finally {
        movingFocus = false;
      }
    }
  }
}
