using System;
using MDSHO.Common;
using Xunit;

namespace MDSHO.Tests {

  /// <summary>
  /// Which in-place edit box opens and takes focus. Clones of a panel show the same buttons, so a rename turns on edit
  /// mode in every clone; only the window the rename started in (the active one) may take focus, since focusing a box in
  /// an inactive window activates that window and brings it in front of other apps.
  /// </summary>
  public class EditBoxRulesTests {
    private static readonly IntPtr Panel = new IntPtr(100);
    private static readonly IntPtr Clone = new IntPtr(200);

    [Fact]
    public void The_active_window_shows_the_edit_box_and_focuses_it() {
      Assert.Equal(EditBoxMode.ShowAndFocus, EditBoxRules.Decide(Panel, Panel));
    }

    [Fact]
    public void A_window_that_is_not_active_keeps_showing_the_name_and_never_takes_focus() {
      Assert.Equal(EditBoxMode.ShowName, EditBoxRules.Decide(Clone, Panel));
    }

    [Fact]
    public void With_no_active_window_the_box_shows_without_focus_so_no_window_jumps_to_the_front() {
      Assert.Equal(EditBoxMode.ShowWithoutFocus, EditBoxRules.Decide(Clone, IntPtr.Zero));
    }

    [Fact]
    public void A_control_without_a_window_shows_the_box_without_focus() {
      Assert.Equal(EditBoxMode.ShowWithoutFocus, EditBoxRules.Decide(IntPtr.Zero, Panel));
      Assert.Equal(EditBoxMode.ShowWithoutFocus, EditBoxRules.Decide(IntPtr.Zero, IntPtr.Zero));
    }
  }
}
