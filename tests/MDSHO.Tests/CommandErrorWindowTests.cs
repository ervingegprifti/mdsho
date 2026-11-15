using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using MDSHO.Helpers;
using MDSHO.ViewModels;
using Xunit;

namespace MDSHO.Tests {

  /// <summary>
  /// The small MDSHO window that says a run command did not run because of a syntax error: the command's name as the
  /// title, the error in red, a line saying how to fix it, and OK. Shown off screen.
  /// </summary>
  public class CommandErrorWindowTests {

    [Fact]
    public void Shows_the_title_the_error_in_red_the_hint_and_OK() {
      WpfAppHost.Run(() => {
        CommandErrorWindow window = new CommandErrorWindow("Ping", "The group has no value 4.", ShortcutViewModel.RunCommandNotRunText);
        window.WindowStartupLocation = WindowStartupLocation.Manual;
        window.Left = -10000;
        window.Top = -10000;
        window.ShowActivated = false;
        window.ShowInTaskbar = false;
        bool closed = false;
        window.Closed += (s, e) => closed = true;
        window.Show();
        try {
          WpfAppHost.Pump();
          Assert.StartsWith("Ping", window.Title);
          TextBlock error = Assert.IsType<TextBlock>(window.FindName("TextError"));
          Assert.Equal("The group has no value 4.", error.Text);
          Color c = Assert.IsType<SolidColorBrush>(error.Foreground).Color;
          Assert.True(c.R >= 150 && c.G <= 80 && c.B <= 80, "The error is red.");
          Assert.Equal("The command did not run. Right-click it and choose Edit run command... to fix it.",
            Assert.IsType<TextBlock>(window.FindName("TextDetail")).Text);
          Button ok = Assert.IsType<Button>(window.FindName("ButtonOK"));
          Assert.Equal("OK", ok.Content);
          Assert.True(ok.IsDefault);
          Assert.True(ok.IsCancel);
          Assert.Null(window.FindName("ButtonCopyDetails"));
          ok.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent, ok));
          WpfAppHost.Pump();
          Assert.True(closed);
        } finally {
          if (!closed) { window.Close(); }
          WpfAppHost.Pump();
        }
      });
    }

    [Fact]
    public void The_app_shows_run_command_errors_in_this_window() {
      WpfAppHost.Run(() => {
        Action<CommandErrorWindow> oldShow = CommandErrorWindow.ShowWindow;
        Action<string, string, string> oldError = Dialogs.ShowError;
        Func<string, string, bool> oldConfirm = Dialogs.Confirm;
        Action<string, string> oldMessage = Dialogs.ShowMessage;
        List<CommandErrorWindow> shown = new List<CommandErrorWindow>();
        CommandErrorWindow.ShowWindow = w => shown.Add(w);
        try {
          WpfBridge.Connect();
          Dialogs.ShowError("Ping", "The group has no value 4.", "Detail");
          CommandErrorWindow window = Assert.Single(shown);
          Assert.Equal("The group has no value 4.", ((TextBlock)window.FindName("TextError")).Text);
          window.Close();
        } finally {
          CommandErrorWindow.ShowWindow = oldShow;
          Dialogs.ShowError = oldError;
          Dialogs.Confirm = oldConfirm;
          Dialogs.ShowMessage = oldMessage;
        }
      });
    }
  }
}
