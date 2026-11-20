namespace MDSHO.ViewModels {

  /// <summary>The keys <see cref="DesktopWindowViewModel.HandleKey"/> acts on; the view maps its keyboard keys to these.</summary>
  public enum ShortcutKey {
    /// <summary>Any other key.</summary>
    Other,
    /// <summary>V (paste with Ctrl).</summary>
    V,
    /// <summary>C (copy with Ctrl).</summary>
    C,
    /// <summary>X (cut with Ctrl).</summary>
    X,
    /// <summary>F2 (rename).</summary>
    F2,
    /// <summary>Delete.</summary>
    Delete,
    /// <summary>Enter (run, or expand/collapse a group; nothing on a snippet).</summary>
    Enter,
    /// <summary>Space (nothing on a snippet, so its button does not click itself).</summary>
    Space
  }

  /// <summary>The modifier keys held with a <see cref="ShortcutKey"/>.</summary>
  public enum ShortcutModifiers {
    /// <summary>No modifier.</summary>
    None,
    /// <summary>Ctrl only.</summary>
    Control,
    /// <summary>Any other combination.</summary>
    Other
  }

  /// <summary>
  /// The key texts panel menus show next to Rename, Cut, Copy, Paste and Delete. They name keys HandleKey acts on;
  /// no spaces, like the tray's shortcut text.
  /// </summary>
  public static class ShortcutKeyTexts {
    /// <summary>F2.</summary>
    public const string Rename = "F2";
    /// <summary>Ctrl+X.</summary>
    public const string Cut = "Ctrl+X";
    /// <summary>Ctrl+C.</summary>
    public const string Copy = "Ctrl+C";
    /// <summary>Ctrl+V.</summary>
    public const string Paste = "Ctrl+V";
    /// <summary>Delete.</summary>
    public const string Delete = "Delete";
  }
}
