using MDSHO.Common;
using MDSHO.ViewModels;
using Xunit;

namespace MDSHO.Tests {

  /// <summary>The glyphs added in MDSHOFont v1.15 (comment, note, the two thumbtacks of the note window's pin) and v1.16 (ok).</summary>
  public class NewGlyphsTests {

    [Fact]
    public void Comment_is_e94c_solid_and_e94d_outline() {
      Assert.Equal("\uE94C", new IconGlyphs(true).Comment);
      Assert.Equal("\uE94D", new IconGlyphs(false).Comment);
    }

    [Fact]
    public void Note_is_e94e_solid_and_e94f_outline() {
      Assert.Equal("\uE94E", new IconGlyphs(true).Note);
      Assert.Equal("\uE94F", new IconGlyphs(false).Note);
    }

    [Fact]
    public void Pin_off_is_the_thumbtack_pointing_left_e950_solid_and_e951_outline() {
      Assert.Equal("\uE950", new IconGlyphs(true).PinOff);
      Assert.Equal("\uE951", new IconGlyphs(false).PinOff);
    }

    [Fact]
    public void Pin_on_is_the_thumbtack_pointing_down_e952_solid_and_e953_outline() {
      Assert.Equal("\uE952", new IconGlyphs(true).PinOn);
      Assert.Equal("\uE953", new IconGlyphs(false).PinOn);
    }

    [Fact]
    public void Ok_is_e954_solid_and_e955_outline() {
      Assert.Equal("\uE954", new IconGlyphs(true).Ok);
      Assert.Equal("\uE955", new IconGlyphs(false).Ok);
    }

    [Theory]
    [InlineData(nameof(IconGlyphs.Ok))]
    [InlineData(nameof(IconGlyphs.Comment))]
    [InlineData(nameof(IconGlyphs.Note))]
    [InlineData(nameof(IconGlyphs.PinOff))]
    [InlineData(nameof(IconGlyphs.PinOn))]
    public void The_new_glyphs_follow_the_solid_icons_setting(string name) {
      IconGlyphs icons = new IconGlyphs(true);
      bool raised = false;
      icons.PropertyChanged += (s, e) => raised |= e.PropertyName == name;
      icons.SolidIcons = false;
      Assert.True(raised, name + " did not change with the setting.");
    }

    [Fact]
    public void A_note_button_uses_the_note_glyph() {
      Assert.Equal(new IconGlyphs(true).Note, new IconGlyphs(true).ForKind(ShortcutTargetKind.Note));
      Assert.Equal(new IconGlyphs(false).Note, new IconGlyphs(false).ForKind(ShortcutTargetKind.Note));
    }
  }
}
