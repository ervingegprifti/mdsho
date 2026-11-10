using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using MDSHO.Common;

namespace MDSHO.Converters {

  /// <summary>Chooses the background of a shortcut button from its selected and hover state.</summary>
  public class ShortcutBackgroundConverter : IMultiValueConverter {
    /// <summary>Returns the background brush for the current state.</summary>
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture) {
      if (values == null || values.Length != 2) {
        return null;
      }
      // The window's text colour ("#AARRGGBB").
      SolidColorBrush background = ((string)values[0]).ToSolidColorBrush();
      bool IsSelected = (bool)values[1];
      if (IsSelected) {
        return background.WithAlpha(40);
      } else {
        return new SolidColorBrush(Colors.Transparent);
      }
    }

    /// <summary>Not supported.</summary>
    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) {
      throw new NotImplementedException();
    }
  }

}