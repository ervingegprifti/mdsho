using System;
using System.Collections.Generic;

namespace MDSHO.Common {

  /// <summary>The modifier keys of a <see cref="Hotkey"/>. The values are Win32's MOD_ALT, MOD_CONTROL, MOD_SHIFT and MOD_WIN.</summary>
  [Flags]
  public enum HotkeyModifiers {
    /// <summary>No modifier.</summary>
    None = 0,
    /// <summary>Alt (MOD_ALT).</summary>
    Alt = 1,
    /// <summary>Ctrl (MOD_CONTROL).</summary>
    Control = 2,
    /// <summary>Shift (MOD_SHIFT).</summary>
    Shift = 4,
    /// <summary>The Windows key (MOD_WIN).</summary>
    Win = 8
  }

  /// <summary>What a pressed key combination is worth as a <see cref="Hotkey"/>.</summary>
  public enum HotkeyCheck {
    /// <summary>A key with a name, with Ctrl, Alt or Win.</summary>
    Valid,
    /// <summary>A key with a name, alone or with only Shift: it needs Ctrl, Alt or Win.</summary>
    NeedsModifier,
    /// <summary>A modifier key on its own, or a key without a name (keep waiting for another key).</summary>
    Ignored
  }

  /// <summary>
  /// A global keyboard shortcut: modifier keys plus one Win32 virtual key. Only keys whose name doesn't depend on the
  /// keyboard layout can be used (letters, digits, F1 to F24, the number pad digits, arrows and the editing keys). Saved as
  /// its <see cref="MenuText"/> ("Ctrl+Shift+Alt+M"), or "Off" for no shortcut.
  /// </summary>
  public sealed class Hotkey : IEquatable<Hotkey> {
    /// <summary>The setting text of "no shortcut".</summary>
    public const string OffSetting = "Off";

    private static readonly Dictionary<int, string> names = KeyNames();
    private static readonly (HotkeyModifiers Modifier, string Name)[] modifierNames = {
      (HotkeyModifiers.Control, "Ctrl"), (HotkeyModifiers.Shift, "Shift"), (HotkeyModifiers.Alt, "Alt"), (HotkeyModifiers.Win, "Win")
    };

    /// <summary>A shortcut of these modifiers and this virtual key.</summary>
    public Hotkey(HotkeyModifiers modifiers, int key) {
      Modifiers = modifiers;
      Key = key;
    }

    /// <summary>The modifier keys.</summary>
    public HotkeyModifiers Modifiers { get; }
    /// <summary>The Win32 virtual key code.</summary>
    public int Key { get; }

    /// <summary>Ctrl+Shift+Alt+M: clashes with no app shortcut, menu key or AltGr character.</summary>
    public static Hotkey Default { get; } = new Hotkey(HotkeyModifiers.Control | HotkeyModifiers.Shift | HotkeyModifiers.Alt, 'M');

    /// <summary>As the Settings box shows it: "Ctrl + Shift + Alt + M".</summary>
    public string DisplayText => string.Join(" + ", Parts());
    /// <summary>As the tray menu shows it, and as it is saved: "Ctrl+Shift+Alt+M".</summary>
    public string MenuText => string.Join("+", Parts());

    private List<string> Parts() {
      List<string> parts = new List<string>();
      foreach ((HotkeyModifiers modifier, string name) in modifierNames) {
        if ((Modifiers & modifier) != 0) {
          parts.Add(name);
        }
      }
      parts.Add(KeyName(Key) ?? ("Key " + Key));
      return parts;
    }

    /// <summary>The name of a virtual key, or null for modifier keys, Esc and keys whose name depends on the keyboard layout.</summary>
    public static string KeyName(int key) {
      return names.TryGetValue(key, out string name) ? name : null;
    }

    /// <summary>Whether this key with these modifiers makes a shortcut.</summary>
    public static HotkeyCheck Check(HotkeyModifiers modifiers, int key) {
      if (KeyName(key) == null) {
        return HotkeyCheck.Ignored;
      }
      return (modifiers & (HotkeyModifiers.Control | HotkeyModifiers.Alt | HotkeyModifiers.Win)) != 0 ? HotkeyCheck.Valid : HotkeyCheck.NeedsModifier;
    }

    /// <summary>The text saved for a shortcut; null (no shortcut) is "Off".</summary>
    public static string ToSetting(Hotkey hotkey) {
      return hotkey == null ? OffSetting : hotkey.MenuText;
    }

    /// <summary>
    /// Reads a saved setting: "Off" gives true and null; a valid shortcut gives true and the shortcut. Anything else
    /// (empty, unknown names, a modifier twice, no Ctrl, Alt or Win) gives false and null.
    /// </summary>
    public static bool TryParseSetting(string text, out Hotkey hotkey) {
      hotkey = null;
      if (string.IsNullOrWhiteSpace(text)) {
        return false;
      }
      if (string.Equals(text.Trim(), OffSetting, StringComparison.OrdinalIgnoreCase)) {
        return true;
      }
      string[] parts = text.Split('+');
      HotkeyModifiers modifiers = HotkeyModifiers.None;
      for (int i = 0; i < parts.Length - 1; i++) {
        HotkeyModifiers modifier = ModifierByName(parts[i].Trim());
        if (modifier == HotkeyModifiers.None || (modifiers & modifier) != 0) {
          return false;
        }
        modifiers |= modifier;
      }
      int? key = KeyByName(parts[parts.Length - 1].Trim());
      if (key == null || Check(modifiers, key.Value) != HotkeyCheck.Valid) {
        return false;
      }
      hotkey = new Hotkey(modifiers, key.Value);
      return true;
    }

    private static HotkeyModifiers ModifierByName(string name) {
      foreach ((HotkeyModifiers modifier, string modifierName) in modifierNames) {
        if (string.Equals(name, modifierName, StringComparison.OrdinalIgnoreCase)) {
          return modifier;
        }
      }
      return HotkeyModifiers.None;
    }

    private static int? KeyByName(string name) {
      foreach (KeyValuePair<int, string> pair in names) {
        if (string.Equals(name, pair.Value, StringComparison.OrdinalIgnoreCase)) {
          return pair.Key;
        }
      }
      return null;
    }

    // Virtual key codes: https://learn.microsoft.com/en-us/windows/win32/inputdev/virtual-key-codes
    private static Dictionary<int, string> KeyNames() {
      Dictionary<int, string> keys = new Dictionary<int, string> {
        [0x08] = "Backspace", [0x09] = "Tab", [0x0D] = "Enter", [0x13] = "Pause", [0x20] = "Space",
        [0x21] = "Page Up", [0x22] = "Page Down", [0x23] = "End", [0x24] = "Home",
        [0x25] = "Left", [0x26] = "Up", [0x27] = "Right", [0x28] = "Down", [0x2D] = "Insert", [0x2E] = "Delete"
      };
      for (char c = 'A'; c <= 'Z'; c++) {
        keys[c] = c.ToString();
      }
      for (int d = 0; d <= 9; d++) {
        keys['0' + d] = d.ToString();
        keys[0x60 + d] = "Num " + d;
      }
      for (int f = 1; f <= 24; f++) {
        keys[0x6F + f] = "F" + f;
      }
      return keys;
    }

    /// <inheritdoc/>
    public bool Equals(Hotkey other) {
      return other != null && other.Modifiers == Modifiers && other.Key == Key;
    }
    /// <inheritdoc/>
    public override bool Equals(object obj) { return Equals(obj as Hotkey); }
    /// <inheritdoc/>
    public override int GetHashCode() { return ((int)Modifiers << 16) ^ Key; }
    /// <summary>The <see cref="MenuText"/>.</summary>
    public override string ToString() { return MenuText; }
  }

  /// <summary>
  /// One global hotkey of the app (RegisterHotKey). <see cref="Register"/> replaces the current registration; it returns
  /// false, with nothing registered, when Windows refuses the keys (usually because another app holds them).
  /// </summary>
  public interface IGlobalHotkeyService : IDisposable {
    /// <summary>Raised on the UI thread when the registered keys are pressed.</summary>
    event Action Pressed;
    /// <summary>Registers these keys instead of the current ones. False = refused (nothing is registered then).</summary>
    bool Register(Hotkey hotkey);
    /// <summary>Releases the keys (nothing happens when none are registered).</summary>
    void Unregister();
  }
}
