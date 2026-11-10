using System;

namespace MDSHO.Common {

  /// <summary>The "#AARRGGBB" colour text stored in the database and used by the view models (WPF brushes: MDSHO.App BrushConversions).</summary>
  public static class ColorConversions {
    /// <summary>The colour used when a stored colour is invalid: opaque white.</summary>
    public const string FallbackHex = "#FFFFFFFF";

    /// <summary>
    /// A stored "#AARRGGBB" colour in upper case. An invalid one (wrong length, missing '#', bad hex digits, null)
    /// becomes opaque white, as the app's brush conversion does.
    /// </summary>
    public static string NormalizeHex(string hexAARRGGBB) {
      if (hexAARRGGBB == null || hexAARRGGBB.Length != 9 || hexAARRGGBB[0] != '#'
        || !uint.TryParse(hexAARRGGBB.Substring(1), System.Globalization.NumberStyles.AllowHexSpecifier, System.Globalization.CultureInfo.InvariantCulture, out uint argb)) {
        return FallbackHex;
      }
      return "#" + argb.ToString("X8", System.Globalization.CultureInfo.InvariantCulture);
    }
  }

}
