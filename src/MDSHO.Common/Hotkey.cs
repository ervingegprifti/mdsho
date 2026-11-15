using System;
using System.Collections.Generic;

namespace MDSHO.Common {

  /// <summary>Modifier keys. The values match Win32's MOD_ flags.</summary>
  [Flags]
  public enum HotkeyModifiers {
    None = 0,
    Alt = 1,
    Control = 2,
    Shift = 4,
    Win = 8
  }

  /// <summary>Whether a key combination can be a shortcut.</summary>
  public enum HotkeyCheck {
    /// <summary>Usable.</summary>
    Valid,
    /// <summary>Needs Ctrl, Alt or Win.</summary>
    NeedsModifier,
    /// <summary>A modifier alone or an unusable key: keep waiting.</summary>
    Ignored
  }

  /// <summary>A global keyboard shortcut: modifier keys plus one key. Only keys with the same name on every keyboard layout.</summary>
  public sealed class Hotkey : IEquatable<Hotkey> {
    /// <summary>The saved text for no shortcut.</summary>
    public const string OffSetting = "Off";

    private static readonly Dictionary<int, string> names = KeyNames();
    private static readonly (HotkeyModifiers Modifier, string Name)[] modifierNames = {
      (HotkeyModifiers.Control, "Ctrl"), (HotkeyModifiers.Shift, "Shift"), (HotkeyModifiers.Alt, "Alt"), (HotkeyModifiers.Win, "Win")
    };

    public Hotkey(HotkeyModifiers modifiers, int key) {
      Modifiers = modifiers;
      Key = key;
    }

    public HotkeyModifiers Modifiers { get; }
    /// <summary>The Win32 virtual key code.</summary>
    public int Key { get; }

    /// <summary>Ctrl+Shift+Alt+M. No app, menu key or AltGr character uses it.</summary>
    public static Hotkey Default { get; } = new Hotkey(HotkeyModifiers.Control | HotkeyModifiers.Shift | HotkeyModifiers.Alt, 'M');

    /// <summary>For the Settings box: "Ctrl + Shift + Alt + M".</summary>
    public string DisplayText => string.Join(" + ", Parts());
    /// <summary>For the tray menu and the database: "Ctrl+Shift+Alt+M".</summary>
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

    /// <summary>The key's name, or null when it can't be used.</summary>
    public static string KeyName(int key) {
      return names.TryGetValue(key, out string name) ? name : null;
    }

    public static HotkeyCheck Check(HotkeyModifiers modifiers, int key) {
      if (KeyName(key) == null) {
        return HotkeyCheck.Ignored;
      }
      return (modifiers & (HotkeyModifiers.Control | HotkeyModifiers.Alt | HotkeyModifiers.Win)) != 0 ? HotkeyCheck.Valid : HotkeyCheck.NeedsModifier;
    }

    /// <summary>The saved text; null is "Off".</summary>
    public static string ToSetting(Hotkey hotkey) {
      return hotkey == null ? OffSetting : hotkey.MenuText;
    }

    /// <summary>Reads the saved text. "Off" gives true and null; anything invalid gives false.</summary>
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

    public bool Equals(Hotkey other) {
      return other != null && other.Modifiers == Modifiers && other.Key == Key;
    }
    public override bool Equals(object obj) { return Equals(obj as Hotkey); }
    public override int GetHashCode() { return ((int)Modifiers << 16) ^ Key; }
    public override string ToString() { return MenuText; }
  }

  /// <summary>The app's one global hotkey (RegisterHotKey).</summary>
  public interface IGlobalHotkeyService : IDisposable {
    /// <summary>Raised on the UI thread when the keys are pressed.</summary>
    event Action Pressed;
    /// <summary>Replaces the registered keys. False when Windows refuses them; then nothing is registered.</summary>
    bool Register(Hotkey hotkey);
    /// <summary>Releases the keys.</summary>
    void Unregister();
  }
}
