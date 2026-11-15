using MDSHO.Common;
using Xunit;

namespace MDSHO.Tests {

  /// <summary>
  /// The "Bring panels to front" keyboard shortcut as plain values: which key combinations are allowed, how they are shown
  /// in the Settings box and the tray menu, and how they are saved.
  /// </summary>
  public class HotkeyTests {
    private const int M = 0x4D, K = 0x4B, F1 = 0x70, F24 = 0x87, Ctrl = 0x11, LeftShift = 0xA0, LeftWin = 0x5B, Semicolon = 0xBA, Esc = 0x1B;

    [Fact]
    public void The_default_is_Ctrl_Shift_Alt_M() {
      Hotkey d = Hotkey.Default;
      Assert.Equal(HotkeyModifiers.Control | HotkeyModifiers.Shift | HotkeyModifiers.Alt, d.Modifiers);
      Assert.Equal(M, d.Key);
      Assert.Equal("Ctrl + Shift + Alt + M", d.DisplayText);
      Assert.Equal("Ctrl+Shift+Alt+M", d.MenuText);
    }

    [Fact]
    public void The_modifier_flags_are_the_RegisterHotKey_values() {
      // MOD_ALT, MOD_CONTROL, MOD_SHIFT, MOD_WIN: passed to RegisterHotKey as they are.
      Assert.Equal(1, (int)HotkeyModifiers.Alt);
      Assert.Equal(2, (int)HotkeyModifiers.Control);
      Assert.Equal(4, (int)HotkeyModifiers.Shift);
      Assert.Equal(8, (int)HotkeyModifiers.Win);
    }

    [Fact]
    public void Modifiers_are_shown_as_Ctrl_Shift_Alt_Win_in_that_order() {
      Hotkey all = new Hotkey(HotkeyModifiers.Win | HotkeyModifiers.Alt | HotkeyModifiers.Shift | HotkeyModifiers.Control, F1);
      Assert.Equal("Ctrl + Shift + Alt + Win + F1", all.DisplayText);
      Assert.Equal("Ctrl+Shift+Alt+Win+F1", all.MenuText);
      Assert.Equal("Alt + Win + F24", new Hotkey(HotkeyModifiers.Win | HotkeyModifiers.Alt, F24).DisplayText);
    }

    [Theory]
    [InlineData(0x41, "A")]
    [InlineData(0x5A, "Z")]
    [InlineData(0x30, "0")]
    [InlineData(0x39, "9")]
    [InlineData(0x70, "F1")]
    [InlineData(0x87, "F24")]
    [InlineData(0x60, "Num 0")]
    [InlineData(0x69, "Num 9")]
    [InlineData(0x20, "Space")]
    [InlineData(0x21, "Page Up")]
    [InlineData(0x22, "Page Down")]
    [InlineData(0x23, "End")]
    [InlineData(0x24, "Home")]
    [InlineData(0x25, "Left")]
    [InlineData(0x26, "Up")]
    [InlineData(0x27, "Right")]
    [InlineData(0x28, "Down")]
    [InlineData(0x2D, "Insert")]
    [InlineData(0x2E, "Delete")]
    [InlineData(0x08, "Backspace")]
    [InlineData(0x09, "Tab")]
    [InlineData(0x0D, "Enter")]
    [InlineData(0x13, "Pause")]
    public void Keys_have_plain_names(int key, string name) {
      Assert.Equal(name, Hotkey.KeyName(key));
    }

    [Theory]
    [InlineData(Ctrl)]
    [InlineData(LeftShift)]
    [InlineData(LeftWin)]
    [InlineData(Semicolon)]
    [InlineData(Esc)]
    public void Modifier_keys_Esc_and_keys_whose_name_depends_on_the_keyboard_layout_have_no_name(int key) {
      Assert.Null(Hotkey.KeyName(key));
    }

    [Theory]
    [InlineData(HotkeyModifiers.Control)]
    [InlineData(HotkeyModifiers.Alt)]
    [InlineData(HotkeyModifiers.Win)]
    [InlineData(HotkeyModifiers.Control | HotkeyModifiers.Shift)]
    [InlineData(HotkeyModifiers.Alt | HotkeyModifiers.Shift)]
    [InlineData(HotkeyModifiers.Win | HotkeyModifiers.Shift)]
    public void A_key_with_Ctrl_Alt_or_Win_is_valid(HotkeyModifiers modifiers) {
      Assert.Equal(HotkeyCheck.Valid, Hotkey.Check(modifiers, K));
    }

    [Theory]
    [InlineData(HotkeyModifiers.None)]
    [InlineData(HotkeyModifiers.Shift)]
    public void A_key_alone_or_with_only_Shift_needs_Ctrl_Alt_or_Win(HotkeyModifiers modifiers) {
      Assert.Equal(HotkeyCheck.NeedsModifier, Hotkey.Check(modifiers, K));
    }

    [Theory]
    [InlineData(Ctrl)]
    [InlineData(LeftShift)]
    [InlineData(LeftWin)]
    [InlineData(Semicolon)]
    public void A_modifier_key_on_its_own_or_a_key_without_a_name_is_ignored(int key) {
      Assert.Equal(HotkeyCheck.Ignored, Hotkey.Check(HotkeyModifiers.Control, key));
      Assert.Equal(HotkeyCheck.Ignored, Hotkey.Check(HotkeyModifiers.None, key));
    }

    [Fact]
    public void Equal_combinations_are_equal() {
      Hotkey a = new Hotkey(HotkeyModifiers.Alt | HotkeyModifiers.Shift | HotkeyModifiers.Control, M);
      Assert.Equal(Hotkey.Default, a);
      Assert.Equal(Hotkey.Default.GetHashCode(), a.GetHashCode());
      Assert.NotEqual(Hotkey.Default, new Hotkey(HotkeyModifiers.Control | HotkeyModifiers.Alt, M));
      Assert.NotEqual(Hotkey.Default, new Hotkey(HotkeyModifiers.Control | HotkeyModifiers.Shift | HotkeyModifiers.Alt, K));
    }

    [Fact]
    public void The_setting_is_the_menu_text_or_Off() {
      Assert.Equal("Ctrl+Shift+Alt+M", Hotkey.ToSetting(Hotkey.Default));
      Assert.Equal("Off", Hotkey.ToSetting(null));
    }

    [Fact]
    public void A_saved_setting_reads_back() {
      Assert.True(Hotkey.TryParseSetting("Ctrl+Shift+Alt+M", out Hotkey d));
      Assert.Equal(Hotkey.Default, d);
      Assert.True(Hotkey.TryParseSetting("Off", out Hotkey off));
      Assert.Null(off);
      // Spaces and letter case don't matter, and names with a space work.
      Assert.True(Hotkey.TryParseSetting(" alt + win + page up ", out Hotkey pageUp));
      Assert.Equal(new Hotkey(HotkeyModifiers.Alt | HotkeyModifiers.Win, 0x21), pageUp);
      Assert.True(Hotkey.TryParseSetting("Ctrl+Num 5", out Hotkey num));
      Assert.Equal(new Hotkey(HotkeyModifiers.Control, 0x65), num);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("M")]
    [InlineData("Shift+M")]
    [InlineData("Ctrl+")]
    [InlineData("Ctrl+Alt")]
    [InlineData("Ctrl+Foo")]
    [InlineData("Ctrl+M+K")]
    [InlineData("Ctrl+Ctrl+M")]
    [InlineData("Hyper+M")]
    public void A_damaged_setting_does_not_read(string text) {
      Assert.False(Hotkey.TryParseSetting(text, out Hotkey hotkey));
      Assert.Null(hotkey);
    }

    [Fact]
    public void Every_named_key_round_trips_through_the_setting() {
      for (int key = 0; key < 256; key++) {
        if (Hotkey.KeyName(key) == null) {
          continue;
        }
        Hotkey h = new Hotkey(HotkeyModifiers.Control | HotkeyModifiers.Alt, key);
        Assert.True(Hotkey.TryParseSetting(Hotkey.ToSetting(h), out Hotkey back), Hotkey.ToSetting(h));
        Assert.Equal(h, back);
      }
    }
  }
}
