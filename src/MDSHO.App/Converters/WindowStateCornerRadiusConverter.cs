using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using MDSHO.Controls;

namespace MDSHO.Converters {

  /// <summary>Turns a window's state into its frame's corner radius: square while maximised, otherwise rounded.</summary>
  public sealed class WindowStateCornerRadiusConverter : IValueConverter {
    /// <summary>The corners for the state (see <see cref="ClippingBorder.CornersFor"/>).</summary>
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
      return ClippingBorder.CornersFor(value is WindowState state ? state : WindowState.Normal);
    }

    /// <summary>Not used: the binding is one way.</summary>
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
      throw new NotSupportedException();
    }
  }
}
