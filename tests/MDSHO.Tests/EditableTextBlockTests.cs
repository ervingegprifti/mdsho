using System;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Threading;
using MDSHO.Controls;
using CommunityToolkit.Mvvm.Input;
using Xunit;

namespace MDSHO.Tests {

  /// <summary>EditableTextBlock behaviour, run on a dedicated STA thread with a hidden HwndSource as presentation source.</summary>
  public class EditableTextBlockTests {
    private static void Sta(Action<EditableTextBlock, TextBox, TextBlock, Border, HwndSource> body) {
      Exception error = null;
      var thread = new Thread(() => {
        HwndSource source = null;
        try {
          source = new HwndSource(new HwndSourceParameters("t") { Width = 200, Height = 50, ParentWindow = new IntPtr(-3) });
          var parent = new Border();
          var control = new EditableTextBlock { Text = "shown", EditText = "typed" };
          parent.Child = control;
          source.RootVisual = parent;
          control.ApplyTemplate();
          parent.Measure(new Size(200, 50)); parent.Arrange(new Rect(0, 0, 200, 50)); parent.UpdateLayout();
          var edit = (TextBox)control.Template.FindName("PART_EditBox", control);
          var display = (TextBlock)control.Template.FindName("PART_Display", control);
          Assert.NotNull(edit); Assert.NotNull(display);
          body(control, edit, display, parent, source);
        } catch (Exception ex) {
          error = ex;
        } finally {
          source?.Dispose();
          Dispatcher.CurrentDispatcher.InvokeShutdown();
        }
      });
      thread.SetApartmentState(ApartmentState.STA);
      thread.Start();
      thread.Join();
      if (error != null) { throw new Exception(error.Message, error); }
    }
    private static void Pump() {
      Dispatcher.CurrentDispatcher.Invoke(DispatcherPriority.ApplicationIdle, new Action(() => { }));
    }
    private static void Key(UIElement target, HwndSource source, Key key) {
      target.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, source, 0, key) { RoutedEvent = Keyboard.KeyDownEvent });
    }

    [Fact]
    public void Shows_text_when_not_editing_and_the_edit_box_when_editing() {
      Sta((c, edit, display, parent, src) => {
        Assert.Equal(Visibility.Visible, display.Visibility);
        Assert.NotEqual(Visibility.Visible, edit.Visibility);
        c.IsEditing = true;
        Assert.NotEqual(Visibility.Visible, display.Visibility);
        Assert.Equal(Visibility.Visible, edit.Visibility);
      });
    }

    [Fact]
    public void A_clone_in_a_window_that_is_not_active_keeps_showing_the_name_and_does_not_take_focus() {
      Sta((c, edit, display, parent, src) => {
        // The rename started in another panel (the active window); this control is in a clone on another screen.
        EditableTextBlock.ActiveWindow = () => new IntPtr(0x7FFF0001);
        try {
          c.IsEditing = true;
          parent.UpdateLayout();
          Pump();
          Assert.Equal(Visibility.Visible, display.Visibility);
          Assert.NotEqual(Visibility.Visible, edit.Visibility);
          Assert.False(c.ShowsEditBox);
          Assert.False(edit.IsKeyboardFocusWithin);
        } finally {
          EditableTextBlock.ActiveWindow = null;
        }
      });
    }

    [Fact]
    public void The_window_where_the_rename_started_shows_the_edit_box() {
      Sta((c, edit, display, parent, src) => {
        EditableTextBlock.ActiveWindow = () => src.Handle;
        try {
          c.IsEditing = true;
          Assert.True(c.ShowsEditBox);
          Assert.Equal(Visibility.Visible, edit.Visibility);
          Assert.NotEqual(Visibility.Visible, display.Visibility);
          c.IsEditing = false;
          Assert.False(c.ShowsEditBox);
          Assert.Equal(Visibility.Visible, display.Visibility);
        } finally {
          EditableTextBlock.ActiveWindow = null;
        }
      });
    }

    [Fact]
    public void A_clone_that_showed_the_name_opens_the_box_when_a_rename_starts_in_it() {
      Sta((c, edit, display, parent, src) => {
        IntPtr active = new IntPtr(0x7FFF0001);
        EditableTextBlock.ActiveWindow = () => active;
        try {
          c.IsEditing = true;
          Assert.False(c.ShowsEditBox);
          c.IsEditing = false;
          active = src.Handle;
          c.IsEditing = true;
          Assert.True(c.ShowsEditBox);
        } finally {
          EditableTextBlock.ActiveWindow = null;
        }
      });
    }

    [Fact]
    public void Mouse_events_reach_the_parent_of_a_clone_that_shows_the_name() {
      Sta((c, edit, display, parent, src) => {
        EditableTextBlock.ActiveWindow = () => new IntPtr(0x7FFF0001);
        try {
          int seen = 0;
          parent.MouseDown += (o, e) => seen++;
          c.IsEditing = true;
          RaiseMouse(display, src, UIElement.MouseDownEvent);
          Assert.Equal(1, seen);
        } finally {
          EditableTextBlock.ActiveWindow = null;
        }
      });
    }

    [Fact]
    public void Starting_an_edit_selects_all_the_text() {
      Sta((c, edit, display, parent, src) => {
        c.IsEditing = true;
        parent.UpdateLayout();
        Pump();
        Assert.Equal("typed".Length, edit.SelectionLength);
      });
    }

    [Fact]
    public void Enter_runs_CommitCommand_only() {
      Sta((c, edit, display, parent, src) => {
        int commit = 0, cancel = 0;
        c.CommitCommand = new RelayCommand(() => commit++);
        c.CancelCommand = new RelayCommand(() => cancel++);
        c.IsEditing = true;
        Key(edit, src, System.Windows.Input.Key.Enter);
        Assert.Equal(1, commit); Assert.Equal(0, cancel);
      });
    }

    [Fact]
    public void Escape_runs_CancelCommand_only() {
      Sta((c, edit, display, parent, src) => {
        int commit = 0, cancel = 0;
        c.CommitCommand = new RelayCommand(() => commit++);
        c.CancelCommand = new RelayCommand(() => { cancel++; c.IsEditing = false; });
        c.IsEditing = true;
        Key(edit, src, System.Windows.Input.Key.Escape);
        Pump();
        Assert.Equal(0, commit); Assert.Equal(1, cancel);
      });
    }

    [Fact]
    public void LostFocus_runs_CommitCommand_while_editing_but_not_after()  {
      Sta((c, edit, display, parent, src) => {
        int commit = 0;
        c.CommitCommand = new RelayCommand(() => commit++);
        c.IsEditing = true;
        edit.RaiseEvent(new RoutedEventArgs(UIElement.LostFocusEvent));
        Assert.Equal(1, commit);
        c.IsEditing = false;
        edit.RaiseEvent(new RoutedEventArgs(UIElement.LostFocusEvent));
        Assert.Equal(1, commit);
      });
    }

    [Fact]
    public void Mouse_events_while_editing_do_not_bubble_to_the_parent() {
      Sta((c, edit, display, parent, src) => {
        int seen = 0;
        parent.MouseUp += (o, e) => seen++;
        parent.MouseDown += (o, e) => seen++;
        parent.AddHandler(Control.MouseDoubleClickEvent, new MouseButtonEventHandler((o, e) => seen++), true);
        c.IsEditing = true;
        RaiseMouse(edit, src, UIElement.MouseDownEvent);
        RaiseMouse(edit, src, UIElement.MouseUpEvent);
        RaiseMouse(edit, src, UIElement.MouseDownEvent);
        RaiseMouse(edit, src, Control.MouseDoubleClickEvent);
        Assert.Equal(0, seen);
      });
    }

    [Fact]
    public void Mouse_events_when_not_editing_still_reach_the_parent() {
      Sta((c, edit, display, parent, src) => {
        int seen = 0;
        parent.MouseDown += (o, e) => seen++;
        parent.MouseUp += (o, e) => seen++;
        RaiseMouse(display, src, UIElement.MouseDownEvent);
        Assert.False(LastHandled);
        RaiseMouse(display, src, UIElement.MouseUpEvent);
        RaiseMouse(display, src, UIElement.MouseDownEvent);
        Assert.Equal(3, seen);
      });
    }

    private static bool LastHandled;
    private static void RaiseMouse(UIElement target, HwndSource src, RoutedEvent ev) {
      var args = new MouseButtonEventArgs(Mouse.PrimaryDevice, Environment.TickCount, MouseButton.Left) { RoutedEvent = ev, Source = target };
      target.RaiseEvent(args);
      LastHandled = args.Handled;
    }
  }
}
