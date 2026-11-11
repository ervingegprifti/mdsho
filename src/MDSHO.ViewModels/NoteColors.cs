using CommunityToolkit.Mvvm.ComponentModel;

namespace MDSHO.ViewModels {

  /// <summary>
  /// The five colours of a note's window (#AARRGGBB). They always hold a real colour: a colour the note does not set is the
  /// default. Stored with the note, where a default colour is saved as empty.
  /// </summary>
  public partial class NoteColors : ObservableObject {
    /// <summary>The default border colour (the beige look of MDSHO's own windows).</summary>
    public const string DefaultBorder = "#FFB9B39F";
    /// <summary>The default title bar background colour.</summary>
    public const string DefaultTitleBackground = "#FFDFDACB";
    /// <summary>The default title text colour.</summary>
    public const string DefaultTitleText = "#FF333333";
    /// <summary>The default background colour.</summary>
    public const string DefaultBackground = "#FFF4F3ED";
    /// <summary>The default text colour.</summary>
    public const string DefaultText = "#FF000000";

    /// <summary>Border colour.</summary>
    [ObservableProperty]
    private string borderColor = DefaultBorder;
    /// <summary>Title bar background colour.</summary>
    [ObservableProperty]
    private string titleBackgroundColor = DefaultTitleBackground;
    /// <summary>Title text colour (also the title bar buttons).</summary>
    [ObservableProperty]
    private string titleTextColor = DefaultTitleText;
    /// <summary>Background colour.</summary>
    [ObservableProperty]
    private string backgroundColor = DefaultBackground;
    /// <summary>Text colour.</summary>
    [ObservableProperty]
    private string textColor = DefaultText;

    /// <summary>Creates the colours from their stored values (empty or null = the default).</summary>
    public NoteColors(string border = "", string titleBackground = "", string titleText = "", string background = "", string text = "") {
      Load(border, titleBackground, titleText, background, text);
    }

    /// <summary>Sets all five from stored values (empty or null = the default).</summary>
    public void Load(string border, string titleBackground, string titleText, string background, string text) {
      this.BorderColor = OrDefault(border, DefaultBorder);
      this.TitleBackgroundColor = OrDefault(titleBackground, DefaultTitleBackground);
      this.TitleTextColor = OrDefault(titleText, DefaultTitleText);
      this.BackgroundColor = OrDefault(background, DefaultBackground);
      this.TextColor = OrDefault(text, DefaultText);
    }

    /// <summary>Puts all five back to the default colours.</summary>
    public void ResetToDefaults() {
      Load("", "", "", "", "");
    }

    /// <summary>The value to store for a colour: empty when it is the default.</summary>
    public static string ToStored(string value, string defaultValue) {
      return string.IsNullOrEmpty(value) || string.Equals(value, defaultValue, System.StringComparison.OrdinalIgnoreCase) ? "" : value;
    }

    /// <summary>The stored values of the five colours: border, title background, title text, background, text.</summary>
    public (string Border, string TitleBackground, string TitleText, string Background, string Text) Stored =>
      (ToStored(this.BorderColor, DefaultBorder), ToStored(this.TitleBackgroundColor, DefaultTitleBackground),
       ToStored(this.TitleTextColor, DefaultTitleText), ToStored(this.BackgroundColor, DefaultBackground), ToStored(this.TextColor, DefaultText));

    private static string OrDefault(string value, string defaultValue) {
      return string.IsNullOrEmpty(value) ? defaultValue : value;
    }
  }

  /// <summary>What the Settings window edits for a note: its colours (as "Appearance", like a panel's) and the note.</summary>
  public sealed class NoteColorsTarget {
    /// <summary>The note's colours; the Settings window binds to Appearance.BorderColor and so on.</summary>
    public NoteColors Appearance { get; }
    /// <summary>The note.</summary>
    public ShortcutViewModel Note { get; }

    /// <summary>Creates the target for a note.</summary>
    public NoteColorsTarget(ShortcutViewModel note) {
      this.Note = note;
      this.Appearance = note.NoteColors;
    }
  }
}
