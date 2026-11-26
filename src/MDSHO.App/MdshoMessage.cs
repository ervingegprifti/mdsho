using System;
using System.Linq;
using System.Windows;
using MDSHO.Common;
using MDSHO.Helpers;

namespace MDSHO {

  /// <summary>
  /// Shows a message in the one MDSHO dialog (<see cref="MessageWindow"/>) instead of a Windows message box, and returns
  /// the answer. The dialog is centred on the screen of its owner (the given window, else the active MDSHO window), like
  /// MDSHO's other dialogs; without one it opens in the middle of the screen. A message from a panel or one of its
  /// buttons uses that panel's Solid icons setting; app-wide messages stay solid, like About and Settings.
  /// </summary>
  public static class MdshoMessage {

    /// <summary>Shows the built dialog (modal) and returns its answer. Tests replace it so no window opens.</summary>
    public static Func<MessageWindow, MessageResult> ShowWindow = window => {
      DialogFocus.ShowDialog(window);
      return window.Result;
    };

    /// <summary>Shows <paramref name="message"/>: its first line bold, the rest below it.</summary>
    public static MessageResult Show(Window owner, string title, string message, MessageIcon icon,
        MessageButtons buttons = MessageButtons.OK, MessageResult defaultResult = MessageResult.None, bool solidIcons = true) {
      (string heading, string text) = MessageRules.Split(message);
      return Show(owner, title, heading, text, icon, buttons, defaultResult, solidIcons);
    }

    /// <summary>Shows a bold <paramref name="heading"/> and the <paramref name="text"/> below it.</summary>
    public static MessageResult Show(Window owner, string title, string heading, string text, MessageIcon icon,
        MessageButtons buttons, MessageResult defaultResult, bool solidIcons = true) {
      MessageWindow window = new MessageWindow(title, heading, text, icon, buttons, defaultResult, solidIcons);
      Place(window, owner);
      return ShowWindow(window);
    }

    /// <summary>Centres the dialog on the screen of the owner (or the active MDSHO window) when there is one that is shown.</summary>
    public static void Place(Window dialog, Window owner) {
      try {
        Window over = owner ?? Application.Current?.Windows.OfType<Window>().FirstOrDefault(w => w != dialog && w.IsActive);
        if (over != null && over.IsVisible && PresentationSource.FromVisual(over) != null) {
          DialogPlacement.CenterOnScreenOf(dialog, over);
        }
      } catch (Exception ex) {
        // Placement is a nicety: the dialog still opens in the middle of the screen.
        AppLog.Warning(ex, "A message could not be placed on its owner's screen");
      }
    }
  }
}
