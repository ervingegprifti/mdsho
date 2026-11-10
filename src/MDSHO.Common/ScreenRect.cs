using System;

namespace MDSHO.Common {

  /// <summary>A point of the screen geometry (DIPs or physical pixels, as the caller uses it).</summary>
  public readonly record struct ScreenPoint(double X, double Y);

  /// <summary>
  /// A rectangle of the screen geometry, without WPF. It follows WPF Rect's rules where the geometry needs them: edges
  /// count as inside, and rectangles that only touch intersect with zero size.
  /// </summary>
  public readonly record struct ScreenRect {
    /// <summary>The empty rectangle (no intersection).</summary>
    public static ScreenRect Empty { get; } = new ScreenRect(0, 0, 0, 0, true);

    /// <summary>Left edge.</summary>
    public double X { get; }
    /// <summary>Top edge.</summary>
    public double Y { get; }
    /// <summary>Width.</summary>
    public double Width { get; }
    /// <summary>Height.</summary>
    public double Height { get; }
    /// <summary>True for <see cref="Empty"/>.</summary>
    public bool IsEmpty { get; }

    /// <summary>Creates a rectangle from its top-left corner and size.</summary>
    public ScreenRect(double x, double y, double width, double height) : this(x, y, width, height, false) { }

    private ScreenRect(double x, double y, double width, double height, bool isEmpty) {
      X = x;
      Y = y;
      Width = width;
      Height = height;
      IsEmpty = isEmpty;
    }

    /// <summary>Left edge.</summary>
    public double Left => X;
    /// <summary>Top edge.</summary>
    public double Top => Y;
    /// <summary>Right edge (X + Width).</summary>
    public double Right => X + Width;
    /// <summary>Bottom edge (Y + Height).</summary>
    public double Bottom => Y + Height;
    /// <summary>Top-left corner.</summary>
    public ScreenPoint TopLeft => new ScreenPoint(X, Y);

    /// <summary>True when the point is inside or on an edge.</summary>
    public bool Contains(ScreenPoint point) {
      return !IsEmpty && point.X >= Left && point.X <= Right && point.Y >= Top && point.Y <= Bottom;
    }

    /// <summary>True when the other rectangle lies inside this one (edges included).</summary>
    public bool Contains(ScreenRect other) {
      return !IsEmpty && !other.IsEmpty && other.Left >= Left && other.Right <= Right && other.Top >= Top && other.Bottom <= Bottom;
    }

    /// <summary>The overlap of two rectangles, or <see cref="Empty"/> when they do not meet.</summary>
    public static ScreenRect Intersect(ScreenRect a, ScreenRect b) {
      if (a.IsEmpty || b.IsEmpty || a.Left > b.Right || b.Left > a.Right || a.Top > b.Bottom || b.Top > a.Bottom) {
        return Empty;
      }
      double left = Math.Max(a.Left, b.Left);
      double top = Math.Max(a.Top, b.Top);
      return new ScreenRect(left, top, Math.Max(Math.Min(a.Right, b.Right) - left, 0), Math.Max(Math.Min(a.Bottom, b.Bottom) - top, 0));
    }
  }
}
