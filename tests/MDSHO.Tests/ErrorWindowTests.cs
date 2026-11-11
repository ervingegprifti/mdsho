using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using MDSHO.Helpers;
using MDSHO.Infrastructure.Logging;
using Xunit;

namespace MDSHO.Tests {

  /// <summary>
  /// The error window shows a short plain-English message and the exception's message, never the stack trace; Copy details
  /// and Open logs folder go through seams (no clipboard, no Explorer in tests). Windows are shown off screen.
  /// </summary>
  public class ErrorWindowTests : IDisposable {
    private readonly Action<string> oldCopy = ErrorWindow.CopyText;
    private readonly Action oldOpenLogs = ErrorWindow.OpenLogs;
    private readonly FakeAppServices services = new FakeAppServices();
    private readonly List<string> copied = new List<string>();
    private int openLogsCalls;

    public ErrorWindowTests() {
      ErrorWindow.CopyText = text => copied.Add(text);
      ErrorWindow.OpenLogs = () => openLogsCalls++;
    }

    public void Dispose() {
      ErrorWindow.CopyText = oldCopy;
      ErrorWindow.OpenLogs = oldOpenLogs;
      services.Dispose();
    }

    private static Exception Thrown() {
      try {
        throw new InvalidOperationException("The file is locked.", new IOException("inner cause"));
      } catch (Exception ex) {
        return ex;
      }
    }

    private static void Shown(ErrorWindow window, Action check) {
      window.WindowStartupLocation = WindowStartupLocation.Manual;
      window.Left = -10000;
      window.Top = -10000;
      window.ShowActivated = false;
      window.ShowInTaskbar = false;
      window.Show();
      try {
        WpfAppHost.Pump();
        check();
      } finally {
        window.Close();
        WpfAppHost.Pump();
      }
    }

    private static T Named<T>(Window window, string name) where T : class {
      return Assert.IsAssignableFrom<T>(window.FindName(name));
    }

    private static void Click(Button button) {
      button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    }

    private static string AllVisibleText(Window window) {
      IEnumerable<string> blocks = WpfAppHost.VisualDescendants<TextBlock>(window).Select(t => t.Text);
      IEnumerable<string> boxes = WpfAppHost.VisualDescendants<TextBox>(window).Select(t => t.Text);
      return string.Join("\n", blocks.Concat(boxes));
    }

    [Fact]
    public void Shows_the_given_message_and_the_exception_message_but_no_stack_trace() {
      Exception ex = Thrown();
      WpfAppHost.Run(() => {
        ErrorWindow window = new ErrorWindow("The data could not be exported.", ex);
        Shown(window, () => {
          Assert.Equal("The data could not be exported.", Named<TextBlock>(window, "TextMessage").Text);
          Assert.Equal("The file is locked.", Named<TextBlock>(window, "TextDetail").Text);
          string visible = AllVisibleText(window);
          Assert.DoesNotContain(" at ", visible);
          Assert.DoesNotContain("inner cause", visible);
          Assert.DoesNotContain(nameof(Thrown), visible);
        });
      });
    }

    [Fact]
    public void Message_and_button_bar_have_the_same_26_pixel_outer_margin_as_the_other_windows() {
      WpfAppHost.Run(() => {
        ErrorWindow window = new ErrorWindow("The data could not be exported.", Thrown());
        Shown(window, () => {
          window.UpdateLayout();
          double m = AboutWindow.OuterMargin;
          FrameworkElement message = (FrameworkElement)window.FindName("PanelMessage");
          Assert.Equal(new Thickness(m), message.Margin);
          System.Windows.Controls.Border bar = (System.Windows.Controls.Border)window.FindName("ButtonBar");
          Assert.Equal(new Thickness(m), bar.Padding);
          FrameworkElement ok = (FrameworkElement)window.FindName("ButtonOK");
          Point okTopLeft = ok.TranslatePoint(new Point(0, 0), bar);
          Assert.Equal(m, okTopLeft.Y, 1);
          Assert.Equal(bar.ActualWidth - m, okTopLeft.X + ok.ActualWidth, 1);
          Assert.Equal(bar.ActualHeight - m, okTopLeft.Y + ok.ActualHeight, 1);
        });
      });
    }

    [Fact]
    public void Without_a_message_shows_something_went_wrong() {
      WpfAppHost.Run(() => {
        ErrorWindow window = new ErrorWindow(Thrown());
        Shown(window, () => {
          Assert.Equal("Something went wrong.", Named<TextBlock>(window, "TextMessage").Text);
          Assert.Equal("The file is locked.", Named<TextBlock>(window, "TextDetail").Text);
        });
      });
    }

    [Fact]
    public void Copy_details_copies_the_full_exception_text() {
      Exception ex = Thrown();
      WpfAppHost.Run(() => {
        ErrorWindow window = new ErrorWindow("The data could not be exported.", ex);
        Shown(window, () => {
          Button copy = Named<Button>(window, "ButtonCopyDetails");
          Assert.Equal("Copy details", copy.Content);
          Click(copy);
        });
      });
      string text = Assert.Single(copied);
      Assert.Contains("The data could not be exported.", text);
      Assert.Contains(ex.ToString(), text);
      Assert.Contains("inner cause", text);
      Assert.Contains(nameof(Thrown), text);
    }

    [Fact]
    public void Open_logs_folder_uses_the_seam() {
      WpfAppHost.Run(() => {
        ErrorWindow window = new ErrorWindow(Thrown());
        Shown(window, () => {
          Button open = Named<Button>(window, "ButtonOpenLogs");
          Assert.Equal("Open logs folder", open.Content);
          Click(open);
        });
      });
      Assert.Equal(1, openLogsCalls);
    }

    [Fact]
    public void OK_closes_the_window() {
      WpfAppHost.Run(() => {
        ErrorWindow window = new ErrorWindow(Thrown());
        bool closed = false;
        window.Closed += (s, e) => closed = true;
        Shown(window, () => {
          Button ok = Named<Button>(window, "ButtonOK");
          Assert.True(ok.IsDefault);
          Assert.True(ok.IsCancel);
          Click(ok);
          WpfAppHost.Pump();
          Assert.True(closed);
        });
      });
    }

    [Fact]
    public void Open_logs_folder_opens_the_logs_folder_when_it_exists_else_the_app_data_folder() {
      List<string> opened = services.OpenedFolders;
      string appData;
      using (TempDb db = new TempDb(false)) {
        appData = AppServices.Current.AppData.Locate();
        oldOpenLogs();
        Directory.CreateDirectory(AppLogging.LogsFolder(appData));
        oldOpenLogs();
        opened = services.OpenedFolders;
      }
      Assert.Equal(2, opened.Count);
      Assert.Equal(appData, opened[0]);
      Assert.Equal(Path.Combine(appData, "logs"), opened[1]);
    }
  }
}
