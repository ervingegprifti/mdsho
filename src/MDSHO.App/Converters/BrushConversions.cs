using System;
using System.Windows.Media;
using MDSHO.Common;

namespace MDSHO.Converters {

  /// <summary>Conversions between WPF colours/brushes and the "#AARRGGBB" text of the database and view models.</summary>
  public static class BrushConversions {
    /// <summary>
    /// Convert a Media Color (WPF) to "#AARRGGBB" text.
    /// </summary>
    public static string ToHexAARRGGBB(this System.Windows.Media.Color color) {
      string hexAARRGGBB = "#" + color.A.ToString("X2") + color.R.ToString("X2") + color.G.ToString("X2") + color.B.ToString("X2");
      return hexAARRGGBB;
    }
    /// <summary>
    /// Convert a HexAARRGGBB string to Media Color (WPF).
    /// </summary>
    public static System.Windows.Media.Color ToColor(this string hexAARRGGBB) {
      if ((hexAARRGGBB == null) || (hexAARRGGBB.Length == 0)) {
        throw new ArgumentException("ColorConversions.ToColor 'hexAARRGGBB' is null or empty.");
      }
      if (hexAARRGGBB[0] != '#' || hexAARRGGBB.Length != 9) {
        throw new ArgumentException("ColorConversions.ToColor 'hexAARRGGBB' is not in the form of '#AARRGGBB'.");
      }
      // 0 12 34 56 78
      // # AA RR GG BB
      if (!uint.TryParse(hexAARRGGBB.Substring(1), System.Globalization.NumberStyles.AllowHexSpecifier, System.Globalization.CultureInfo.InvariantCulture, out uint argb)) {
        throw new ArgumentException("ColorConversions.ToColor 'hexAARRGGBB' contains invalid hexadecimal digits.");
      }
      return System.Windows.Media.Color.FromArgb((byte)(argb >> 24), (byte)(argb >> 16), (byte)(argb >> 8), (byte)argb);
    }
    /// <summary>
    /// Convert a Media Color (WPF) to Media SolidColorBrush (WPF).
    /// </summary>
    public static System.Windows.Media.SolidColorBrush ToSolidColorBrush(this System.Windows.Media.Color color) {
      SolidColorBrush solidColorBrush = new System.Windows.Media.SolidColorBrush(color);
      return solidColorBrush;
    }
    /// <summary>
    /// Convert a HexAARRGGBB string to Media SolidColorBrush (WPF).
    /// An invalid (e.g. corrupted stored) color falls back to opaque white instead of throwing.
    /// </summary>
    public static System.Windows.Media.SolidColorBrush ToSolidColorBrush(this string hexAARRGGBB) {
      System.Windows.Media.Color color;
      try {
        color = hexAARRGGBB.ToColor();
      } catch (ArgumentException) {
        color = System.Windows.Media.Colors.White;
      }
      SolidColorBrush solidColorBrush = new System.Windows.Media.SolidColorBrush(color);
      return solidColorBrush;
    }
    /// <summary>A new brush of the same colour with the given alpha (0 = transparent, 255 = opaque).</summary>
    public static SolidColorBrush WithAlpha(this SolidColorBrush brush, byte alpha) {
      Color color = brush.Color;
      color.A = alpha;
      return new SolidColorBrush(color);
    }
    /// <summary>
    /// Convert a Media SolidColorBrush (WPF) to "#AARRGGBB" text.
    /// </summary>
    public static string ToHexAARRGGBB(this System.Windows.Media.SolidColorBrush solidColorBrush) {
      System.Windows.Media.Color color = solidColorBrush.Color;
      string hexAARRGGBB = color.ToHexAARRGGBB();
      return hexAARRGGBB;
    }
  }

}
