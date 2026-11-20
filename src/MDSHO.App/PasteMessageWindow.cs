using System;
using MDSHO.Common;

namespace MDSHO {

  /// <summary>The one MDSHO dialog that says why a snippet could not paste: an info icon, a bold heading, the text and OK.</summary>
  public class PasteMessageWindow : MessageWindow {

    /// <summary>Shows the built window (modal). Tests replace it so no window opens.</summary>
    public static Action<PasteMessageWindow> ShowWindow = window => window.ShowDialog();

    /// <summary>Creates the window.</summary>
    /// <param name="caption">The title, "MDSHO".</param>
    /// <param name="heading">The bold first line.</param>
    /// <param name="text">The text below it.</param>
    /// <param name="solidIcons">The icon set of the snippet's panel.</param>
    public PasteMessageWindow(string caption, string heading, string text, bool solidIcons = true)
        : base(caption, heading, text, MessageIcon.Info, MessageButtons.OK, MessageResult.OK, solidIcons) {
    }
  }
}
