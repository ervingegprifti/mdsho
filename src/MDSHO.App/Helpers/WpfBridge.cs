using System.Windows;
using MDSHO.ViewModels;

namespace MDSHO.Helpers {

  /// <summary>
  /// Gives the WPF-free view models what they need from WPF: prompts are message boxes. Connected once, when the App
  /// type is first used. (Commands are CommunityToolkit.Mvvm commands; the view models tell them when to ask again.)
  /// </summary>
  public static class WpfBridge {

    /// <summary>Connects the view model seams to WPF. Safe to call more than once.</summary>
    public static void Connect() {
      Dialogs.Confirm = (message, caption) =>
        MessageBox.Show(message, caption, MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No) == MessageBoxResult.Yes;
      Dialogs.ShowMessage = (message, caption) =>
        MessageBox.Show(message, caption, MessageBoxButton.OK, MessageBoxImage.Exclamation);
    }
  }
}
