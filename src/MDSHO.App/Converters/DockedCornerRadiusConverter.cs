using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace MDSHO.Converters {

  /// <summary>
  /// Turns a window's IsDocked into a corner radius: square (0) when docked, otherwise the radius given as the
  /// parameter ("topLeft,topRight,bottomRight,bottomLeft", e.g. "5,5,0,0").
  /// </summary>
  public sealed class DockedCornerRadiusConverter : IValueConverter {
    private static readonly CornerRadiusConverter parser = new CornerRadiusConverter();

    /// <summary>Square when docked, otherwise the parameter's radius.</summary>
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
      if (value is bool docked && docked) {
        return new CornerRadius(0);
      }
      return (CornerRadius)parser.ConvertFromInvariantString((string)parameter);
    }

    /// <summary>Not used: the binding is one way.</summary>
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
      throw new NotSupportedException();
    }
  }
}
