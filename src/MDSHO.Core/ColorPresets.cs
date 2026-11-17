using System.Collections.Generic;

namespace MDSHO.Core {

  /// <summary>One colour preset of the Colours window: a theme name and its five colours, each "#AARRGGBB".</summary>
  public sealed class ColorPreset {
    /// <summary>The theme name, shown as the tooltip.</summary>
    public string Name { get; init; }
    /// <summary>Border colour.</summary>
    public string BorderColor { get; init; }
    /// <summary>Title bar background colour.</summary>
    public string TitleBackgroundColor { get; init; }
    /// <summary>Title text colour.</summary>
    public string TitleTextColor { get; init; }
    /// <summary>Background colour.</summary>
    public string BackgroundColor { get; init; }
    /// <summary>Text colour.</summary>
    public string TextColor { get; init; }

    /// <summary>The five colours as a colour set.</summary>
    public ColorSet ToColorSet() {
      return new ColorSet {
        BorderColor = this.BorderColor, TitleBackgroundColor = this.TitleBackgroundColor, TitleTextColor = this.TitleTextColor,
        BackgroundColor = this.BackgroundColor, TextColor = this.TextColor
      };
    }
  }

  /// <summary>
  /// The colour presets of the Colours window, from the published palettes of popular editor themes: four light, then
  /// four dark. A click on one puts its five colours on screen; Apply saves them. Title text is at least 3:1 on its
  /// background and body text at least 4.5:1 (ColorPresetsTests).
  /// </summary>
  public static class ColorPresets {
    /// <summary>All presets, in the order shown: name, border, title background, title text, background, text.</summary>
    public static IReadOnlyList<ColorPreset> All { get; } = new[] {
      // Light themes
      Preset("Solarized Light", "#FF93A1A1", "#FF268BD2", "#FFFDF6E3", "#FFFDF6E3", "#FF586E75"),
      Preset("Catppuccin Latte", "#FFBCC0CC", "#FF8839EF", "#FFEFF1F5", "#FFEFF1F5", "#FF4C4F69"),
      // Rosé Pine Dawn's title bar uses the theme's pine (its rose was too light for the title text).
      Preset("Rosé Pine Dawn", "#FFDFDAD9", "#FF286983", "#FFFAF4ED", "#FFFAF4ED", "#FF575279"),
      Preset("Gruvbox Light", "#FFD5C4A1", "#FF458588", "#FFFBF1C7", "#FFFBF1C7", "#FF3C3836"),
      // Dark themes
      Preset("Nord", "#FF4C566A", "#FF5E81AC", "#FFECEFF4", "#FF2E3440", "#FFD8DEE9"),
      Preset("Dracula", "#FF44475A", "#FFBD93F9", "#FF282A36", "#FF282A36", "#FFF8F8F2"),
      Preset("Gruvbox Dark", "#FF504945", "#FFD65D0E", "#FFFBF1C7", "#FF282828", "#FFEBDBB2"),
      Preset("Catppuccin Mocha", "#FF45475A", "#FF89B4FA", "#FF1E1E2E", "#FF1E1E2E", "#FFCDD6F4"),
    };

    /// <summary>The preset with exactly these five colours (ignoring case), or null.</summary>
    public static ColorPreset Match(string border, string titleBackground, string titleText, string background, string text) {
      foreach (ColorPreset p in All) {
        if (Same(p.BorderColor, border) && Same(p.TitleBackgroundColor, titleBackground) && Same(p.TitleTextColor, titleText)
            && Same(p.BackgroundColor, background) && Same(p.TextColor, text)) {
          return p;
        }
      }
      return null;
    }

    private static bool Same(string a, string b) {
      return string.Equals(a, b, System.StringComparison.OrdinalIgnoreCase);
    }

    private static ColorPreset Preset(string name, string border, string titleBackground, string titleText, string background, string text) {
      return new ColorPreset {
        Name = name, BorderColor = border, TitleBackgroundColor = titleBackground, TitleTextColor = titleText,
        BackgroundColor = background, TextColor = text
      };
    }
  }
}
