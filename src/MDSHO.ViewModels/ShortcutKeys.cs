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
    /// <summary>Enter (run, or expand/collapse a group).</summary>
    Enter
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
}
