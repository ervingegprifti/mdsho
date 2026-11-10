using System;
using System.Globalization;
using System.Windows.Data;

namespace MDSHO.Converters {

  /// <summary>Converts the Solid icons setting between a bool and the choice shown in the Settings window.</summary>
  public class SolidIconsConverter : IValueConverter {
    /// <summary>Converts the setting to its display value.</summary>
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
      return Convert(value, parameter);
    }

    /// <summary>Converts the display value back to the setting.</summary>
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
      return Convert(value, parameter);
    }

    private object Convert(object value, object parameter) {
      if (value == null | parameter == null) {
        return Binding.DoNothing;
      }
      bool isChecked = (bool)value;
      string parameterString = (string)parameter;
      if ("SolidIcons".Equals(parameterString)) {
        return isChecked;
      }
      if ("OutlinedIcons".Equals(parameterString)) {
        return !isChecked;
      }
      return Binding.DoNothing;
    }
  }

}