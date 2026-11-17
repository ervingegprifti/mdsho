using System;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using MDSHO.Core;
using Xunit;

namespace MDSHO.Tests {

  /// <summary>
  /// The colour presets of the Colours window: eight themes (four light, then four dark), each the five colours of a panel
  /// or a note. The title text and the body text must stay easy to read on their backgrounds.
  /// </summary>
  public class ColorPresetsTests {

    private static readonly (string Name, string Border, string TitleBackground, string TitleText, string Background, string Text)[] Expected = {
      ("Solarized Light", "#FF93A1A1", "#FF268BD2", "#FFFDF6E3", "#FFFDF6E3", "#FF586E75"),
      ("Catppuccin Latte", "#FFBCC0CC", "#FF8839EF", "#FFEFF1F5", "#FFEFF1F5", "#FF4C4F69"),
      ("Rosé Pine Dawn", "#FFDFDAD9", "#FF286983", "#FFFAF4ED", "#FFFAF4ED", "#FF575279"),
      ("Gruvbox Light", "#FFD5C4A1", "#FF458588", "#FFFBF1C7", "#FFFBF1C7", "#FF3C3836"),
      ("Nord", "#FF4C566A", "#FF5E81AC", "#FFECEFF4", "#FF2E3440", "#FFD8DEE9"),
      ("Dracula", "#FF44475A", "#FFBD93F9", "#FF282A36", "#FF282A36", "#FFF8F8F2"),
      ("Gruvbox Dark", "#FF504945", "#FFD65D0E", "#FFFBF1C7", "#FF282828", "#FFEBDBB2"),
      ("Catppuccin Mocha", "#FF45475A", "#FF89B4FA", "#FF1E1E2E", "#FF1E1E2E", "#FFCDD6F4"),
    };

    private static string[] ColoursOf(ColorPreset p) {
      return new[] { p.BorderColor, p.TitleBackgroundColor, p.TitleTextColor, p.BackgroundColor, p.TextColor };
    }

    // WCAG relative luminance and contrast ratio of two "#AARRGGBB" colours.
    private static double Luminance(string hex) {
      static double Channel(string part) {
        double s = int.Parse(part, NumberStyles.HexNumber) / 255.0;
        return s <= 0.03928 ? s / 12.92 : Math.Pow((s + 0.055) / 1.055, 2.4);
      }
      return 0.2126 * Channel(hex.Substring(3, 2)) + 0.7152 * Channel(hex.Substring(5, 2)) + 0.0722 * Channel(hex.Substring(7, 2));
    }

    private static double Contrast(string a, string b) {
      double la = Luminance(a), lb = Luminance(b);
      return (Math.Max(la, lb) + 0.05) / (Math.Min(la, lb) + 0.05);
    }

    [Fact]
    public void There_are_eight_presets_four_light_then_four_dark_with_these_names() {
      Assert.Equal(Expected.Select(e => e.Name).ToArray(), ColorPresets.All.Select(p => p.Name).ToArray());
    }

    [Fact]
    public void Each_preset_has_its_five_colours_border_title_background_title_text_background_text() {
      Assert.Equal(Expected.Length, ColorPresets.All.Count);
      for (int i = 0; i < Expected.Length; i++) {
        (string name, string border, string titleBackground, string titleText, string background, string text) = Expected[i];
        Assert.Equal(new[] { border, titleBackground, titleText, background, text }, ColoursOf(ColorPresets.All[i]));
      }
    }

    [Fact]
    public void Every_colour_is_an_opaque_AARRGGBB_hex() {
      Regex hex = new Regex("^#FF[0-9A-F]{6}$");
      Assert.All(ColorPresets.All.SelectMany(ColoursOf), c => Assert.Matches(hex, c));
    }

    [Fact]
    public void Body_text_is_easy_to_read_on_the_background() {
      Assert.All(ColorPresets.All, p => Assert.True(Contrast(p.TextColor, p.BackgroundColor) >= 4.5,
        $"{p.Name}: text on background is {Contrast(p.TextColor, p.BackgroundColor):0.00}:1"));
    }

    // The title is short and SemiBold: 3:1, WCAG's level for large text (the owner's choice, 1.15.0).
    [Fact]
    public void Title_text_is_easy_to_read_on_the_title_background() {
      Assert.All(ColorPresets.All, p => Assert.True(Contrast(p.TitleTextColor, p.TitleBackgroundColor) >= 3,
        $"{p.Name}: title text on title background is {Contrast(p.TitleTextColor, p.TitleBackgroundColor):0.00}:1"));
    }

    [Fact]
    public void Match_finds_the_preset_whose_five_colours_are_on_screen_ignoring_case() {
      ColorPreset nord = ColorPresets.All[4];
      Assert.Same(nord, ColorPresets.Match(nord.BorderColor, nord.TitleBackgroundColor, nord.TitleTextColor, nord.BackgroundColor, nord.TextColor));
      Assert.Same(nord, ColorPresets.Match("#ff4c566a", "#ff5e81ac", "#ffeceff4", "#ff2e3440", "#ffd8dee9"));
      for (int i = 0; i < ColorPresets.All.Count; i++) {
        string[] c = ColoursOf(ColorPresets.All[i]);
        Assert.Same(ColorPresets.All[i], ColorPresets.Match(c[0], c[1], c[2], c[3], c[4]));
      }
    }

    [Fact]
    public void Match_is_null_when_any_colour_differs() {
      ColorPreset nord = ColorPresets.All[4];
      string[] c = ColoursOf(nord);
      for (int i = 0; i < 5; i++) {
        string[] changed = (string[])c.Clone();
        changed[i] = "#FF123456";
        Assert.Null(ColorPresets.Match(changed[0], changed[1], changed[2], changed[3], changed[4]));
      }
      Assert.Null(ColorPresets.Match("#804C566A", c[1], c[2], c[3], c[4])); // alpha counts too
      Assert.Null(ColorPresets.Match(null, null, null, null, null));
    }

    [Fact]
    public void A_preset_gives_its_colours_as_a_colour_set() {
      ColorPreset nord = ColorPresets.All.Single(p => p.Name == "Nord");
      ColorSet set = nord.ToColorSet();
      Assert.Equal(ColoursOf(nord), new[] { set.BorderColor, set.TitleBackgroundColor, set.TitleTextColor, set.BackgroundColor, set.TextColor });
    }
  }
}
