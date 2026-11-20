using System.Collections.Generic;

namespace MDSHO.Core {

  /// <summary>One colour preset of the Colours window: a theme name and its five colours, each "#AARRGGBB".</summary>
  public sealed class ColorPreset {
    /// <summary>The preset's name, such as "Ocean".</summary>
    public string Name { get; init; }
    /// <summary>The season it belongs to.</summary>
    public string Season { get; init; }
    /// <summary>"Season: Name", the tooltip and accessible name.</summary>
    public string Label => this.Season + ": " + this.Name;
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
  /// The colour presets of the Colours window: three per season, in the order Winter, Spring, Summer, Autumn, each in
  /// that season's colours. A click on one puts its five colours on screen; Apply saves them. Title text is at least 3:1
  /// on its background and body text at least 4.5:1, over any desktop where a background lets it through (ColorPresetsTests).
  /// </summary>
  public static class ColorPresets {
    /// <summary>All presets, in the order shown: season, name, border, title background, title text, background, text.</summary>
    public static IReadOnlyList<ColorPreset> All { get; } = new[] {
      Preset("Winter", "Frost", "#FFA9C4D9", "#FF3E6F96", "#FFFFFFFF", "#FFEEF4F9", "#FF1F3A52"),
      Preset("Winter", "Snowfall", "#FF55544F", "#FF55544F", "#FFFFFFFF", "#FFFFFEF5", "#FF000000"),
      Preset("Winter", "Midnight", "#FF3A4A63", "#FF1B2A41", "#FFE8EEF5", "#FF26344A", "#FFDCE6F0"),
      Preset("Spring", "Blossom", "#FFE8B4C3", "#FFB8496E", "#FFFFFFFF", "#FFFFF4F7", "#FF5A2A3A"),
      Preset("Spring", "Fresh leaf", "#FFA8D5A2", "#FF3F8A31", "#FFFFFFFF", "#FFF3FAEF", "#FF2C4A24"),
      Preset("Spring", "Lilac", "#FFC9B8E3", "#FF7452A6", "#FFFFFFFF", "#FFF7F3FC", "#FF3E2D57"),
      Preset("Summer", "Ocean", "#FF7CC6D6", "#FF0B6F8C", "#FFFFFFFF", "#FFEAF8FB", "#FF134552"),
      Preset("Summer", "Sunshine", "#FFE9C46A", "#FFF4B400", "#FF3A2A00", "#FFFFFBEA", "#FF4A3B10"),
      Preset("Summer", "Seaside", "#FF75B2A1", "#FF75B2A1", "#FF000000", "#FFE9E2BA", "#FF000000"),
      Preset("Autumn", "Maple", "#FFD9A07A", "#FFA63F17", "#FFFFF5EC", "#FFFFF6EE", "#FF4A2412"),
      Preset("Autumn", "Harvest", "#FFD6B25E", "#FF7A5B16", "#FFFFF8E6", "#FFFBF4E2", "#FF4A3A15"),
      // Also MDSHO's own panel colours (AppConstants.DefaultWindow*Color; ColorPresetsTests keeps them equal). Its body is
      // 60 % opaque black, so the desktop shows through.
      Preset("Autumn", "Halloween", "#FF000000", "#FF000000", "#FFFFFFFF", "#99000000", "#FFFFFFFF"),
    };

    /// <summary>Autumn: Halloween, the last preset: the colours of a new panel and of Default colours without saved defaults.</summary>
    public static ColorPreset Halloween => All[All.Count - 1];

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

    private static ColorPreset Preset(string season, string name, string border, string titleBackground, string titleText, string background, string text) {
      return new ColorPreset {
        Season = season, Name = name, BorderColor = border, TitleBackgroundColor = titleBackground, TitleTextColor = titleText,
        BackgroundColor = background, TextColor = text
      };
    }
  }
}
