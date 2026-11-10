using System;
using System.Globalization;
using System.Windows.Data;

namespace MDSHO.Converters {

  /// <summary>Turns the tooltip text of a shortcut (its target, or for a run command the command line that will run) into its tooltip; empty text shows none.</summary>
  public class ShortcutToolTipConverter : IValueConverter {
    /// <summary>Returns the tooltip text.</summary>
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
      if (value == null) {
        return null;
      }
      string toolTip = (string)value;
      return string.IsNullOrEmpty(toolTip) ? null : toolTip;
    }

    /// <summary>Not supported.</summary>
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
      throw new NotImplementedException();
    }
  }

}