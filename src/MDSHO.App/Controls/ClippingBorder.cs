using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace MDSHO.Controls {

  /// <summary>
  /// A border that also clips what is inside it to its inner rounded corners, so a title bar or any other content can't
  /// poke out of the corners. Used as the frame of the rounded windows (see Themes/RoundedWindow.xaml).
  /// </summary>
  public class ClippingBorder : Border {
    /// <summary>The corner radius of a rounded window, the same as the panels.</summary>
    public const double Radius = 5;

    /// <summary>The frame's corners for a window state: square while maximised, otherwise rounded.</summary>
    public static CornerRadius CornersFor(WindowState state) {
      return state == WindowState.Maximized ? new CornerRadius(0) : new CornerRadius(Radius);
    }

    /// <summary>Arranges the content, then clips it to the inside of the border with the inner corner radius.</summary>
    protected override Size ArrangeOverride(Size finalSize) {
      Size arranged = base.ArrangeOverride(finalSize);
      if (this.Child is UIElement child) {
        double radius = Math.Max(0, this.CornerRadius.TopLeft - this.BorderThickness.Left);
        child.Clip = new RectangleGeometry(new Rect(child.RenderSize), radius, radius);
      }
      return arranged;
    }
  }
}
