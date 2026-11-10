using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace MDSHO.Controls {

  /// <summary>
  /// In-place edit control: shows a TextBlock (Text) or, while IsEditing, a TextBox (EditText).
  /// Enter runs CommitCommand, Esc runs CancelCommand, leaving the box runs CommitCommand.
  /// While editing it swallows its own mouse down/up/double-click so a parent Button never sees them.
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
      if (IsEditing) { e.Handled = true; }
    }

    private static void OnIsEditingChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) {
      EditableTextBlock control = (EditableTextBlock)d;
      if ((bool)e.NewValue) {
        // Deferred so it runs after a closing context menu has given focus back.
        control.Dispatcher.BeginInvoke(DispatcherPriority.Input, new System.Action(() => {
          if (control.IsEditing && control.editBox != null) {
            control.editBox.Focus();
            control.editBox.SelectAll();
          }
        }));
      }
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
