using System;

namespace MDSHO.Common {

  /// <summary>What an in-place edit box does when editing starts.</summary>
  public enum EditBoxMode {
    /// <summary>Its window is the active one: open the box and give it keyboard focus.</summary>
    ShowAndFocus,
    /// <summary>Another window of the app is active (where the rename started): keep showing the name.</summary>
    ShowName,
    /// <summary>No window of the app is active: open the box but leave focus alone.</summary>
    ShowWithoutFocus,
  }

  /// <summary>
  /// Clones of a panel show the same buttons, so a rename turns on edit mode in every clone. Giving keyboard focus to a
  /// box in a window that is not active activates that window and brings it in front of other apps, so only the active
  /// window (where the rename started) opens and focuses its box; clones keep showing the name.
  /// </summary>
  public static class EditBoxRules {
    /// <summary>Decides from the control's own window and the active window of the UI thread (IntPtr.Zero when none).</summary>
    public static EditBoxMode Decide(IntPtr ownWindow, IntPtr activeWindow) {
      if (ownWindow == IntPtr.Zero || activeWindow == IntPtr.Zero) {
        return EditBoxMode.ShowWithoutFocus;
      }
      return ownWindow == activeWindow ? EditBoxMode.ShowAndFocus : EditBoxMode.ShowName;
    }
  }
}
