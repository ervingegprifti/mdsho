using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

namespace MDSHO.Helpers {

  /// <summary>
  /// Renaming from a window's title (the Note and Run command windows): the title text takes the mouse (the rest of the
  /// title bar is still the caption), a double click on it opens an edit box in its place, and dragging it moves the
  /// window. Enter or leaving the box keeps the new name, Esc the old one, a blank name is ignored; closing the window
  /// while renaming keeps the new name. A double click elsewhere on the title bar still maximises and restores.
  /// </summary>
  internal sealed class TitleRename {
    private readonly Window window;
    private readonly TextBlock title;
    private readonly TextBox edit;
    private readonly Func<string> getName;
    private readonly Action<string> setName;
    private readonly Action afterEnd;
    private Point? dragStart;

    /// <param name="window">The window whose title is renamed.</param>
    /// <param name="title">The title text.</param>
    /// <param name="edit">The edit box shown in place of the title (collapsed until a rename starts).</param>
    /// <param name="toolTip">The title's tooltip.</param>
    /// <param name="getName">The current name.</param>
    /// <param name="setName">Takes a changed, non-blank name.</param>
    /// <param name="afterEnd">Runs when the edit box closes, e.g. to give the focus back to the window's main field.</param>
    public TitleRename(Window window, TextBlock title, TextBox edit, string toolTip, Func<string> getName, Action<string> setName, Action afterEnd) {
      this.window = window;
      this.title = title;
      this.edit = edit;
      this.getName = getName;
      this.setName = setName;
      this.afterEnd = afterEnd;
      System.Windows.Shell.WindowChrome.SetIsHitTestVisibleInChrome(title, true);
      title.ToolTip = toolTip;
      title.MouseLeftButtonDown += Title_MouseLeftButtonDown;
      title.MouseMove += Title_MouseMove;
      title.MouseLeftButtonUp += (s, e) => this.dragStart = null;
      edit.LostKeyboardFocus += (s, e) => End(save: true);
      window.Closing += (s, e) => End(save: true);
    }

    /// <summary>True while the edit box is open.</summary>
    public bool IsRenaming => this.edit.Visibility == Visibility.Visible;

    /// <summary>Opens the edit box in place of the title, with the name selected.</summary>
    public void Begin() {
      if (IsRenaming) { return; }
      this.edit.Text = this.getName() ?? "";
      this.title.Visibility = Visibility.Collapsed;
      this.edit.Visibility = Visibility.Visible;
      // Deferred so it runs after the window has been shown or activated.
      this.window.Dispatcher.BeginInvoke(DispatcherPriority.Input, new Action(() => {
        if (IsRenaming) {
          this.edit.Focus();
          this.edit.SelectAll();
        }
      }));
    }

    /// <summary>Closes the edit box; with <paramref name="save"/> a changed, non-blank name is passed on.</summary>
    public void End(bool save) {
      if (!IsRenaming) { return; }
      string name = this.edit.Text ?? "";
      this.edit.Visibility = Visibility.Collapsed;
      this.title.Visibility = Visibility.Visible;
      if (save && !string.IsNullOrWhiteSpace(name) && name != this.getName()) {
        this.setName(name);
      }
      this.afterEnd?.Invoke();
    }

    /// <summary>
    /// While renaming, Enter keeps the new name and Esc the old one; neither reaches the window (no close, no save, no
    /// default button). Returns true when it handled the key.
    /// </summary>
    public bool HandleKey(KeyEventArgs e) {
      if (!IsRenaming || (e.Key != Key.Enter && e.Key != Key.Escape)) { return false; }
      e.Handled = true;
      End(save: e.Key == Key.Enter);
      return true;
    }

    private void Title_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) {
      e.Handled = true;
      if (e.ClickCount == 2) {
        this.dragStart = null;
        Begin();
      } else {
        this.dragStart = e.GetPosition(this.window);
      }
    }

    // Dragging starts only once the mouse has moved, so a double click still reaches the title.
    private void Title_MouseMove(object sender, MouseEventArgs e) {
      if (this.dragStart == null) { return; }
      if (e.LeftButton != MouseButtonState.Pressed) {
        this.dragStart = null;
        return;
      }
      Vector moved = e.GetPosition(this.window) - this.dragStart.Value;
      if (Math.Abs(moved.X) < SystemParameters.MinimumHorizontalDragDistance && Math.Abs(moved.Y) < SystemParameters.MinimumVerticalDragDistance) {
        return;
      }
      this.dragStart = null;
      Window w = this.window;
      if (w.WindowState == WindowState.Maximized) {
        // Like dragging a maximised window's title bar: restore it under the mouse, at the same place along the title.
        Point inWindow = e.GetPosition(w);
        double along = w.ActualWidth > 0 ? inWindow.X / w.ActualWidth : 0.5;
        Point onScreen = w.PointToScreen(inWindow);
        PresentationSource source = PresentationSource.FromVisual(w);
        if (source?.CompositionTarget != null) {
          onScreen = source.CompositionTarget.TransformFromDevice.Transform(onScreen);
        }
        w.WindowState = WindowState.Normal;
        w.Left = onScreen.X - w.Width * along;
        w.Top = onScreen.Y - inWindow.Y;
      }
      w.DragMove();
    }
  }
}
