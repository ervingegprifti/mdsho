using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using MDSHO.Common;

namespace MDSHO.Converters {

  /// <summary>Between a view model colour ("#AARRGGBB") and a WPF colour, for the colour pickers (two way).</summary>
  public sealed class HexToColorConverter : IValueConverter {
    /// <summary>The colour; an invalid one gives white.</summary>
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
      return (value as string).ToSolidColorBrush().Color;
    }

    /// <summary>The picked colour as "#AARRGGBB".</summary>
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
      return ((Color)value).ToHexAARRGGBB();
    }
  }
}
