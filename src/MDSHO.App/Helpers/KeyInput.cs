using System.Windows.Input;
using MDSHO.ViewModels;

namespace MDSHO.Helpers {

  /// <summary>Maps WPF keyboard keys to the plain keys the view models act on.</summary>
  public static class KeyInput {

    /// <summary>The shortcut key for a WPF key (<see cref="ShortcutKey.Other"/> for keys without a shortcut).</summary>
    public static ShortcutKey ToShortcutKey(Key key) {
      switch (key) {
        case Key.V: return ShortcutKey.V;
        case Key.C: return ShortcutKey.C;
        case Key.X: return ShortcutKey.X;
        case Key.F2: return ShortcutKey.F2;
        case Key.Delete: return ShortcutKey.Delete;
        case Key.Enter: return ShortcutKey.Enter;
        default: return ShortcutKey.Other;
      }
    }

    /// <summary>
    /// A pressed key as a Win32 virtual key and modifiers.
    /// With Alt held, WPF reports Key.System and the real key is in <paramref name="systemKey"/>.
    /// </summary>
    public static (Common.HotkeyModifiers Modifiers, int Key) ToHotkey(Key key, Key systemKey, ModifierKeys modifiers) {
      Key real = key == Key.System ? systemKey : key;
      int virtualKey = real == Key.None ? 0 : KeyInterop.VirtualKeyFromKey(real);
      Common.HotkeyModifiers hotkeyModifiers = Common.HotkeyModifiers.None;
      if ((modifiers & ModifierKeys.Control) != 0) { hotkeyModifiers |= Common.HotkeyModifiers.Control; }
      if ((modifiers & ModifierKeys.Shift) != 0) { hotkeyModifiers |= Common.HotkeyModifiers.Shift; }
      if ((modifiers & ModifierKeys.Alt) != 0) { hotkeyModifiers |= Common.HotkeyModifiers.Alt; }
      if ((modifiers & ModifierKeys.Windows) != 0) { hotkeyModifiers |= Common.HotkeyModifiers.Win; }
      return (hotkeyModifiers, virtualKey);
    }

    /// <summary>None, Ctrl alone, or any other combination.</summary>
    public static ShortcutModifiers ToShortcutModifiers(ModifierKeys modifiers) {
      if (modifiers == ModifierKeys.None) {
        return ShortcutModifiers.None;
      }
      return modifiers == ModifierKeys.Control ? ShortcutModifiers.Control : ShortcutModifiers.Other;
    }
  }
}
