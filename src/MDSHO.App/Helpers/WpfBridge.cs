using System.Windows;
using MDSHO.Common;
using MDSHO.ViewModels;

namespace MDSHO.Helpers {

  /// <summary>
  /// Gives the WPF-free view models what they need from WPF: prompts are MDSHO dialogs. Connected once, when the App
  /// type is first used. (Commands are CommunityToolkit.Mvvm commands; the view models tell them when to ask again.)
  /// </summary>
  public static class WpfBridge {

    /// <summary>Connects the view model seams to WPF. Safe to call more than once.</summary>
    public static void Connect() {
      Dialogs.Confirm = (message, caption) =>
        MdshoMessage.Show(null, caption, message, MessageIcon.Warning, MessageButtons.YesNo, MessageResult.No) == MessageResult.Yes;
      Dialogs.ShowMessage = (message, caption) =>
        MdshoMessage.Show(null, caption, message, MessageIcon.Warning);
      Dialogs.ShowError = (caption, error, detail) =>
        CommandErrorWindow.ShowWindow(new CommandErrorWindow(caption, error, detail));
      Dialogs.ShowInfo = (caption, heading, text) =>
        PasteMessageWindow.ShowWindow(new PasteMessageWindow(caption, heading, text));
    }
  }
}
