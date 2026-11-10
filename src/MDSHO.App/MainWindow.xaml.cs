using System.Windows;

namespace MDSHO {

  /// <summary>
  /// Interaction logic for MainWindow.xaml
  /// </summary>
  public partial class MainWindow : Window {

    /// <summary>Creates the hidden main window that keeps the application alive.</summary>
    public MainWindow() {
      InitializeComponent();
      SetTitle();
    }

    private void SetTitle() {
#if DEBUG
      this.Title += " DEBUG";
#endif
    }

  }

}