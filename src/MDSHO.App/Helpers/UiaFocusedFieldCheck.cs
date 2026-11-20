using System;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Automation;
using MDSHO.Common;

namespace MDSHO.Helpers {

  /// <summary>
  /// Looks at the focused element of a window: the desktop and the taskbar are never text fields; otherwise UI Automation
  /// tells the control type, the Value pattern (read-only or not) and the Text pattern, and the window's thread tells
  /// whether a caret shows. UI Automation runs off the UI thread with a time limit, since some apps answer slowly. Unknown
  /// only when UI Automation fails, times out or the focus is not in that window. One log line per check, facts only.
  /// </summary>
  public sealed class UiaFocusedFieldCheck : IFocusedFieldCheck {
    /// <summary>How long it waits for UI Automation.</summary>
    public static readonly TimeSpan TimeLimit = TimeSpan.FromMilliseconds(1500);

    private const uint GUI_CARETBLINKING = 0x0001;
    private const uint GA_ROOT = 2;

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT { public int left, top, right, bottom; }

    [StructLayout(LayoutKind.Sequential)]
    private struct GUITHREADINFO {
      public int cbSize;
      public uint flags;
      public IntPtr hwndActive;
      public IntPtr hwndFocus;
      public IntPtr hwndCapture;
      public IntPtr hwndMenuOwner;
      public IntPtr hwndMoveSize;
      public IntPtr hwndCaret;
      public RECT rcCaret;
    }

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);
    [DllImport("user32.dll")]
    private static extern bool GetGUIThreadInfo(uint threadId, ref GUITHREADINFO info);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetClassName(IntPtr hWnd, StringBuilder className, int maxCount);
    [DllImport("user32.dll")]
    private static extern IntPtr GetAncestor(IntPtr hWnd, uint flags);

    /// <summary>Whether the focused element of the window takes text.</summary>
    public async Task<FieldCheck> CheckAsync(IntPtr window) {
      uint threadId = GetWindowThreadProcessId(window, out uint processId);
      GUITHREADINFO info = new GUITHREADINFO { cbSize = Marshal.SizeOf<GUITHREADINFO>() };
      bool hasInfo = threadId != 0 && GetGUIThreadInfo(threadId, ref info);
      bool hasCaret = hasInfo && (info.hwndCaret != IntPtr.Zero || (info.flags & GUI_CARETBLINKING) != 0);
      IntPtr focus = hasInfo ? info.hwndFocus : IntPtr.Zero;
      string windowClass = ClassOf(window);
      string focusClass = ClassOf(focus);
      if (FocusedFieldRules.IsDesktopOrTaskbar(windowClass, focusClass)
          || (focus != IntPtr.Zero && FocusedFieldRules.IsDesktopOrTaskbar(ClassOf(GetAncestor(focus, GA_ROOT)), focusClass))) {
        AppLog.Information("Paste field check: desktop or taskbar (window class {WindowClass}, focus class {FocusClass}): {Decision}",
          windowClass, focusClass, FieldCheck.NotEditable.ToString());
        return FieldCheck.NotEditable;
      }
      Task<FieldCheck> check = Task.Run(() => Check((int)processId, hasCaret));
      Task done = await Task.WhenAny(check, Task.Delay(TimeLimit));
      if (done != check) {
        AppLog.Information("Paste field check: UI Automation did not answer in time: {Decision}", FieldCheck.Unknown.ToString());
        return FieldCheck.Unknown;
      }
      return await check;
    }

    private static FieldCheck Check(int processId, bool hasCaret) {
      try {
        AutomationElement element = AutomationElement.FocusedElement;
        // The focus is not in that window (yet): can't tell.
        if (element == null || element.Current.ProcessId != processId) {
          AppLog.Information("Paste field check: the focus is not in the window: {Decision}", FieldCheck.Unknown.ToString());
          return FieldCheck.Unknown;
        }
        ControlType type = element.Current.ControlType;
        bool hasValue = element.TryGetCurrentPattern(ValuePattern.Pattern, out object value);
        bool readOnly = hasValue && ((ValuePattern)value).Current.IsReadOnly;
        bool hasText = element.TryGetCurrentPattern(TextPattern.Pattern, out _);
        FieldCheck decision = FocusedFieldRules.Decide(KindOf(type), hasValue, readOnly, hasText, hasCaret);
        // Facts only: the control type and class name, never the element's name or value.
        string typeName = type?.ProgrammaticName?.Replace("ControlType.", "") ?? "unknown";
        AppLog.Information("Paste field check: {Facts}",
          FocusedFieldRules.Describe(typeName, element.Current.ClassName ?? "", hasValue, readOnly, hasText, hasCaret, decision));
        return decision;
      } catch (Exception ex) {
        AppLog.Information("Paste field check: UI Automation failed ({ErrorType}): {Decision}", ex.GetType().FullName, FieldCheck.Unknown.ToString());
        return FieldCheck.Unknown;
      }
    }

    private static string ClassOf(IntPtr hwnd) {
      if (hwnd == IntPtr.Zero) {
        return null;
      }
      StringBuilder name = new StringBuilder(256);
      return GetClassName(hwnd, name, name.Capacity) > 0 ? name.ToString() : null;
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
