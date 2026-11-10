using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using MDSHO.Common;

namespace MDSHO.Converters {

  /// <summary>Chooses the background of a shortcut button while the mouse is over it.</summary>
  public class HOverShortcutBackgroundConverter : IValueConverter {
    /// <summary>Returns the hover background brush.</summary>
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
      if (value == null) {
        return null;
      }
      // The window's text colour ("#AARRGGBB").
      return ((string)value).ToSolidColorBrush().WithAlpha(60);
    }

    /// <summary>Not supported.</summary>
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
      throw new NotImplementedException();
    }
  }

}