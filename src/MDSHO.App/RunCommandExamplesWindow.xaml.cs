using System;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using MDSHO.Core;
using MDSHO.ViewModels;

namespace MDSHO {

  /// <summary>
  /// The Examples popup of the Run Command window: a grouped list of ready-made commands. Use (or a double-click) fills
  /// the Run Command window; X and Esc close the popup without changes.
  /// </summary>
  public partial class RunCommandExamplesWindow : Window {
    private readonly RunCommandViewModel viewModel;

    /// <summary>Creates the popup for the Run Command window's view model.</summary>
    public RunCommandExamplesWindow(RunCommandViewModel viewModel) {
      InitializeComponent();
      // Custom title bar: ask Windows 11 for rounded corners, like the Settings window gets automatically.
      Helpers.WindowCorners.UseRoundedCorners(this);
      this.viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
      this.DataContext = viewModel;

      ListCollectionView view = new ListCollectionView(new System.Collections.Generic.List<RunCommandExample>(viewModel.Examples));
      view.GroupDescriptions.Add(new PropertyGroupDescription(nameof(RunCommandExample.Category)));
      this.ListExamples.ItemsSource = view;

      this.ListExamples.SelectionChanged += (s, e) => ShowSelected();
      this.ListExamples.MouseDoubleClick += (s, e) => { if (this.ListExamples.SelectedItem != null) { UseSelected(); } };
      this.ButtonUse.Click += (s, e) => UseSelected();
      this.ButtonClose.Click += (s, e) => Close();
      this.PreviewKeyDown += (s, e) => {
        if (e.Key == Key.Escape) {
          e.Handled = true;
          Close();
        }
      };
      ShowSelected();
    }

    private void ShowSelected() {
      RunCommandExample example = this.ListExamples.SelectedItem as RunCommandExample;
      this.ButtonUse.IsEnabled = example != null;
      this.TextDescription.Text = example == null ? "Pick an example to see what it does." : example.Description;
      this.TextRuns.Text = example == null ? "" : example.RunsText;
    }

    private void UseSelected() {
      if (this.ListExamples.SelectedItem is RunCommandExample example && this.viewModel.UseExample(example)) {
        Close();
      }
    }
  }
}
