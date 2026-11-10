using System;
using System.Globalization;
using System.Windows.Data;
using MDSHO.Common;

namespace MDSHO.Converters {

  /// <summary>Turns a view model colour ("#AARRGGBB") into a brush; an invalid colour gives white.</summary>
  public sealed class HexToBrushConverter : IValueConverter {
    /// <summary>The brush of the colour.</summary>
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
      return (value as string).ToSolidColorBrush();
    }

    /// <summary>Not used: the binding is one way.</summary>
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
      throw new NotSupportedException();
    }
  }
}
