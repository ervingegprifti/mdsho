using System.Globalization;

namespace MDSHO.Common {

  /// <summary>Text helpers for Unicode code points.</summary>
  public static class UnicodeText {
    /// <summary>Turns a hexadecimal code point such as "E900" into the one character (or surrogate pair) it stands for; used for the icon font glyphs.</summary>
    public static string FromHex(string hex) {
      int code = int.Parse(hex, NumberStyles.HexNumber); // If hex is null then ArgumentNullException is thrown.
      return char.ConvertFromUtf32(code);
    }
  }

}
