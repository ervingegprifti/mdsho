namespace MDSHO.Core {

  /// <summary>Which windows a saved set of default colours is for.</summary>
  public enum ColorSetKind {
    /// <summary>Panels (MDSHO windows and their clones).</summary>
    Window,
    /// <summary>Note windows.</summary>
    Note
  }

  /// <summary>The five colours of a panel or a note window, each "#AARRGGBB".</summary>
  public sealed class ColorSet {
    /// <summary>Border colour.</summary>
    public string BorderColor { get; set; } = "";
    /// <summary>Title bar background colour.</summary>
    public string TitleBackgroundColor { get; set; } = "";
    /// <summary>Title text colour.</summary>
    public string TitleTextColor { get; set; } = "";
    /// <summary>Background colour.</summary>
    public string BackgroundColor { get; set; } = "";
    /// <summary>Text colour.</summary>
    public string TextColor { get; set; } = "";
  }

  /// <summary>
  /// The default colours saved with "Set as default" in the Colours window: one set for panels, one for notes. They are
  /// what Default colours puts back and what new panels and notes start with.
  /// </summary>
  public interface IDefaultColorsRepository {
    /// <summary>The saved defaults of that kind, or null when none were saved (then MDSHO's own colours apply).</summary>
    ColorSet Get(ColorSetKind kind);
    /// <summary>Saves the defaults of that kind (adds the row the first time, replaces it after that).</summary>
    void Save(ColorSetKind kind, ColorSet colors);
  }
}
