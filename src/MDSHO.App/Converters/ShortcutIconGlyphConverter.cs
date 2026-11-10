using System;
using System.Globalization;
using System.Windows.Data;
using MDSHO.ViewModels;

namespace MDSHO.Converters {

  /// <summary>
  /// Picks the first icon of a shortcut from its kind of target and the window's icon set.
  /// Values: the shortcut's <see cref="ShortcutTargetKind"/>, the window's <see cref="IconGlyphs"/>, and its SolidIcons flag
  /// (only bound so the glyph is picked again when the setting changes).
  /// </summary>
  public class ShortcutIconGlyphConverter : IMultiValueConverter {
    /// <summary>Returns the glyph for the kind, or Binding.DoNothing while a value is not available yet.</summary>
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture) {
      if (values == null || values.Length < 2 || !(values[0] is ShortcutTargetKind kind) || !(values[1] is IconGlyphs icons)) {
        return Binding.DoNothing;
      }
      return icons.ForKind(kind);
    }

    /// <summary>Not supported.</summary>
    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) {
      throw new NotImplementedException();
    }
  }

}
