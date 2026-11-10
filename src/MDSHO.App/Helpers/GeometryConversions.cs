using System.Windows;
using MDSHO.Common;

namespace MDSHO.Helpers {

  /// <summary>Between the plain screen geometry types of MDSHO.Common and WPF's Point and Rect.</summary>
  public static class GeometryConversions {
    /// <summary>The WPF point.</summary>
    public static Point ToPoint(this ScreenPoint point) {
      return new Point(point.X, point.Y);
    }
    /// <summary>The plain point.</summary>
    public static ScreenPoint ToScreenPoint(this Point point) {
      return new ScreenPoint(point.X, point.Y);
    }
    /// <summary>The plain rectangle; WPF's empty rectangle becomes <see cref="ScreenRect.Empty"/>.</summary>
    public static ScreenRect ToScreenRect(this Rect rect) {
      return rect.IsEmpty ? ScreenRect.Empty : new ScreenRect(rect.X, rect.Y, rect.Width, rect.Height);
    }
  }
}
