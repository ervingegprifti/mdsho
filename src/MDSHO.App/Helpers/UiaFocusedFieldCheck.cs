using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Automation;
using MDSHO.Common;

namespace MDSHO.Helpers {

  /// <summary>
  /// Asks UI Automation about the focused element of a window: edit box or document, Value pattern (read-only or not),
  /// Text pattern. Runs off the UI thread with a time limit, since some apps answer slowly; Unknown when it can't tell.
  /// </summary>
  public sealed class UiaFocusedFieldCheck : IFocusedFieldCheck {
    /// <summary>How long it waits for UI Automation.</summary>
    public static readonly TimeSpan TimeLimit = TimeSpan.FromMilliseconds(1500);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

    /// <summary>Whether the focused element of the window takes text.</summary>
    public async Task<FieldCheck> CheckAsync(IntPtr window) {
      GetWindowThreadProcessId(window, out uint processId);
      Task<FieldCheck> check = Task.Run(() => Check((int)processId));
      Task done = await Task.WhenAny(check, Task.Delay(TimeLimit));
      if (done != check) {
        AppLog.Information("UI Automation did not answer in time; pasting anyway");
        return FieldCheck.Unknown;
      }
      return await check;
    }

    private static FieldCheck Check(int processId) {
      try {
        AutomationElement element = AutomationElement.FocusedElement;
        // The focus is not in that window (yet): can't tell.
        if (element == null || element.Current.ProcessId != processId) {
          return FieldCheck.Unknown;
        }
        bool hasValue = element.TryGetCurrentPattern(ValuePattern.Pattern, out object value);
        bool readOnly = hasValue && ((ValuePattern)value).Current.IsReadOnly;
        bool hasText = element.TryGetCurrentPattern(TextPattern.Pattern, out _);
        return FocusedFieldRules.Decide(KindOf(element.Current.ControlType), hasValue, readOnly, hasText);
      } catch (Exception ex) {
        AppLog.Information("UI Automation could not check the focused field: {ErrorType}", ex.GetType().FullName);
        return FieldCheck.Unknown;
      }
    }

    private static FocusedControlKind KindOf(ControlType type) {
      if (type == ControlType.Edit) { return FocusedControlKind.Edit; }
      if (type == ControlType.Document) { return FocusedControlKind.Document; }
      if (type == null || type == ControlType.Window || type == ControlType.Pane || type == ControlType.Group || type == ControlType.Custom) {
        return FocusedControlKind.Container;
      }
      return FocusedControlKind.Other;
    }
  }
}
