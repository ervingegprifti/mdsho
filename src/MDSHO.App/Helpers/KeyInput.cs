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

    /// <summary>None, Ctrl alone, or any other combination.</summary>
    public static ShortcutModifiers ToShortcutModifiers(ModifierKeys modifiers) {
      if (modifiers == ModifierKeys.None) {
        return ShortcutModifiers.None;
      }
      return modifiers == ModifierKeys.Control ? ShortcutModifiers.Control : ShortcutModifiers.Other;
    }
  }
}
