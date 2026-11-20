using System;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using MDSHO.Core;
using Xunit;

namespace MDSHO.Tests {

  /// <summary>
  /// The colour presets of the Colours window: twelve, three per season (Winter, Spring, Summer, Autumn), each the five
  /// colours of a panel or a note. The title text and the body text must stay easy to read on their backgrounds.
  /// </summary>
  public class ColorPresetsTests {

    private static readonly (string Season, string Name, string Border, string TitleBackground, string TitleText, string Background, string Text)[] Expected = {
      ("Winter", "Frost", "#FFA9C4D9", "#FF3E6F96", "#FFFFFFFF", "#FFEEF4F9", "#FF1F3A52"),
      ("Winter", "Snowfall", "#FF55544F", "#FF55544F", "#FFFFFFFF", "#FFFFFEF5", "#FF000000"),
      ("Winter", "Midnight", "#FF3A4A63", "#FF1B2A41", "#FFE8EEF5", "#FF26344A", "#FFDCE6F0"),
      ("Spring", "Blossom", "#FFE8B4C3", "#FFB8496E", "#FFFFFFFF", "#FFFFF4F7", "#FF5A2A3A"),
      ("Spring", "Fresh leaf", "#FFA8D5A2", "#FF3F8A31", "#FFFFFFFF", "#FFF3FAEF", "#FF2C4A24"),
      ("Spring", "Lilac", "#FFC9B8E3", "#FF7452A6", "#FFFFFFFF", "#FFF7F3FC", "#FF3E2D57"),
      ("Summer", "Ocean", "#FF7CC6D6", "#FF0B6F8C", "#FFFFFFFF", "#FFEAF8FB", "#FF134552"),
      ("Summer", "Sunshine", "#FFE9C46A", "#FFF4B400", "#FF3A2A00", "#FFFFFBEA", "#FF4A3B10"),
      ("Summer", "Seaside", "#FF75B2A1", "#FF75B2A1", "#FF000000", "#FFE9E2BA", "#FF000000"),
      ("Autumn", "Maple", "#FFD9A07A", "#FFA63F17", "#FFFFF5EC", "#FFFFF6EE", "#FF4A2412"),
      ("Autumn", "Harvest", "#FFD6B25E", "#FF7A5B16", "#FFFFF8E6", "#FFFBF4E2", "#FF4A3A15"),
      ("Autumn", "Halloween", "#FF000000", "#FF000000", "#FFFFFFFF", "#99000000", "#FFFFFFFF"),
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

    // A translucent colour over a backdrop ("#FFRRGGBB"): what the eye sees.
    private static string Over(string hex, string backdrop) {
      double a = int.Parse(hex.Substring(1, 2), NumberStyles.HexNumber) / 255.0;
      string result = "#FF";
      for (int i = 3; i < 9; i += 2) {
        double c = int.Parse(hex.Substring(i, 2), NumberStyles.HexNumber), b = int.Parse(backdrop.Substring(i, 2), NumberStyles.HexNumber);
        result += ((int)Math.Round(c * a + b * (1 - a))).ToString("X2");
      }
      return result;
    }

    // The worst contrast of a text colour on a background that may let the desktop through: over white and over black.
    private static double ContrastOnAnyDesktop(string text, string background) {
      return Math.Min(Contrast(text, Over(background, "#FFFFFFFF")), Contrast(text, Over(background, "#FF000000")));
    }

    private static double Contrast(string a, string b) {
      double la = Luminance(a), lb = Luminance(b);
      return (Math.Max(la, lb) + 0.05) / (Math.Min(la, lb) + 0.05);
    }

    [Fact]
    public void There_are_twelve_presets_with_these_seasons_and_names() {
      Assert.Equal(Expected.Select(e => e.Season + "/" + e.Name).ToArray(), ColorPresets.All.Select(p => p.Season + "/" + p.Name).ToArray());
    }

    [Fact]
    public void There_are_three_presets_per_season_in_the_order_Winter_Spring_Summer_Autumn() {
      string[] seasons = { "Winter", "Spring", "Summer", "Autumn" };
      Assert.Equal(12, ColorPresets.All.Count);
      for (int i = 0; i < ColorPresets.All.Count; i++) {
        Assert.Equal(seasons[i / 3], ColorPresets.All[i].Season);
      }
    }

    [Fact]
    public void The_label_is_season_and_name() {
      Assert.Equal("Summer: Ocean", ColorPresets.All[6].Label);
      Assert.Equal("Spring: Fresh leaf", ColorPresets.All[4].Label);
      Assert.All(ColorPresets.All, p => Assert.Equal(p.Season + ": " + p.Name, p.Label));
    }

    [Fact]
    public void Each_preset_has_its_five_colours_border_title_background_title_text_background_text() {
      Assert.Equal(Expected.Length, ColorPresets.All.Count);
      for (int i = 0; i < Expected.Length; i++) {
        (string season, string name, string border, string titleBackground, string titleText, string background, string text) = Expected[i];
        Assert.Equal(new[] { border, titleBackground, titleText, background, text }, ColoursOf(ColorPresets.All[i]));
      }
    }

    [Fact]
    public void Every_colour_is_an_AARRGGBB_hex_and_only_Halloween_has_a_see_through_background() {
      Assert.All(ColorPresets.All.SelectMany(ColoursOf), c => Assert.Matches(new Regex("^#[0-9A-F]{8}$"), c));
      Assert.All(ColorPresets.All.Where(p => p.Name != "Halloween").SelectMany(ColoursOf), c => Assert.StartsWith("#FF", c));
      ColorPreset halloween = ColorPresets.All.Single(p => p.Name == "Halloween");
      Assert.Equal("#99000000", halloween.BackgroundColor); // 60 % opaque black
      Assert.All(new[] { halloween.BorderColor, halloween.TitleBackgroundColor, halloween.TitleTextColor, halloween.TextColor },
        c => Assert.StartsWith("#FF", c));
    }

    [Fact]
    public void Halloween_is_the_last_preset_Autumn_Halloween() {
      Assert.Equal("Autumn: Halloween", ColorPresets.All[11].Label);
      Assert.Same(ColorPresets.All[11], ColorPresets.Halloween);
    }

    // One source: a new panel, the welcome panel and Default colours (without saved defaults) use Halloween's colours.
    [Fact]
    public void The_default_panel_colours_are_the_Halloween_preset() {
      ColorPreset h = ColorPresets.Halloween;
      string[] defaults = {
        MDSHO.Common.AppConstants.DefaultWindowBorderColor, MDSHO.Common.AppConstants.DefaultWindowTitleBackgroundColor,
        MDSHO.Common.AppConstants.DefaultWindowTitleTextColor, MDSHO.Common.AppConstants.DefaultWindowBackgroundColor,
        MDSHO.Common.AppConstants.DefaultWindowTextColor };
      Assert.Equal(ColoursOf(h), defaults);
      Assert.Same(h, ColorPresets.Match(defaults[0], defaults[1], defaults[2], defaults[3], defaults[4]));
    }

    [Fact]
    public void Match_counts_alpha_so_Halloween_needs_its_60_percent_background() {
      ColorPreset h = ColorPresets.All[11];
      Assert.Same(h, ColorPresets.Match("#FF000000", "#FF000000", "#FFFFFFFF", "#99000000", "#FFFFFFFF"));
      Assert.Null(ColorPresets.Match("#FF000000", "#FF000000", "#FFFFFFFF", "#cc000000", "#FFFFFFFF")); // 80 %, Halloween before 1.19.2
      Assert.Null(ColorPresets.Match("#FF000000", "#FF000000", "#FFFFFFFF", "#FF000000", "#FFFFFFFF")); // solid
    }

    [Fact]
    public void Body_text_is_easy_to_read_on_the_background() {
      Assert.All(ColorPresets.All, p => Assert.True(ContrastOnAnyDesktop(p.TextColor, p.BackgroundColor) >= 4.5,
        $"{p.Name}: text on background is {ContrastOnAnyDesktop(p.TextColor, p.BackgroundColor):0.00}:1"));
    }

    // The title is short and SemiBold: 3:1, WCAG's level for large text (the owner's choice, 1.15.0).
    [Fact]
    public void Title_text_is_easy_to_read_on_the_title_background() {
      Assert.All(ColorPresets.All, p => Assert.True(ContrastOnAnyDesktop(p.TitleTextColor, p.TitleBackgroundColor) >= 3,
        $"{p.Name}: title text on title background is {ContrastOnAnyDesktop(p.TitleTextColor, p.TitleBackgroundColor):0.00}:1"));
    }

    [Fact]
    public void Match_finds_the_preset_whose_five_colours_are_on_screen_ignoring_case() {
      ColorPreset ocean = ColorPresets.All[6];
      Assert.Same(ocean, ColorPresets.Match(ocean.BorderColor, ocean.TitleBackgroundColor, ocean.TitleTextColor, ocean.BackgroundColor, ocean.TextColor));
      Assert.Same(ocean, ColorPresets.Match("#ff7cc6d6", "#ff0b6f8c", "#ffffffff", "#ffeaf8fb", "#ff134552"));
      for (int i = 0; i < ColorPresets.All.Count; i++) {
        string[] c = ColoursOf(ColorPresets.All[i]);
        Assert.Same(ColorPresets.All[i], ColorPresets.Match(c[0], c[1], c[2], c[3], c[4]));
      }
    }

    [Fact]
    public void Match_is_null_when_any_colour_differs() {
      string[] c = ColoursOf(ColorPresets.All[6]);
      for (int i = 0; i < 5; i++) {
        string[] changed = (string[])c.Clone();
        changed[i] = "#FF123456";
        Assert.Null(ColorPresets.Match(changed[0], changed[1], changed[2], changed[3], changed[4]));
      }
      Assert.Null(ColorPresets.Match("#807CC6D6", c[1], c[2], c[3], c[4])); // alpha counts too
      Assert.Null(ColorPresets.Match(null, null, null, null, null));
    }

    [Fact]
    public void A_preset_gives_its_colours_as_a_colour_set() {
      ColorPreset ocean = ColorPresets.All.Single(p => p.Name == "Ocean");
      ColorSet set = ocean.ToColorSet();
      Assert.Equal(ColoursOf(ocean), new[] { set.BorderColor, set.TitleBackgroundColor, set.TitleTextColor, set.BackgroundColor, set.TextColor });
    }
  }
}
