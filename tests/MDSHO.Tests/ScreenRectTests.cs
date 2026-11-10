using MDSHO.Common;
using Xunit;

namespace MDSHO.Tests {

  /// <summary>
  /// The plain rectangle and point of MDSHO.Common's screen geometry. They follow the WPF Rect rules the geometry relies
  /// on (edges included in Contains, touching rectangles intersect with zero size), so the geometry gives the same results.
  /// </summary>
  public class ScreenRectTests {

    [Fact]
    public void Edges_and_size() {
      ScreenRect r = new ScreenRect(-10, 20, 300, 400);
      Assert.Equal(-10, r.Left);
      Assert.Equal(20, r.Top);
      Assert.Equal(290, r.Right);
      Assert.Equal(420, r.Bottom);
      Assert.Equal(300, r.Width);
      Assert.Equal(400, r.Height);
      Assert.Equal(new ScreenPoint(-10, 20), r.TopLeft);
      Assert.False(r.IsEmpty);
      Assert.True(ScreenRect.Empty.IsEmpty);
    }

    [Fact]
    public void Equal_values_are_equal() {
      Assert.Equal(new ScreenRect(1, 2, 3, 4), new ScreenRect(1, 2, 3, 4));
      Assert.NotEqual(new ScreenRect(1, 2, 3, 4), new ScreenRect(1, 2, 3, 5));
      Assert.True(new ScreenRect(1, 2, 3, 4) == new ScreenRect(1, 2, 3, 4));
      Assert.Equal(new ScreenPoint(1, 2), new ScreenPoint(1, 2));
      Assert.True(new ScreenPoint(1, 2) != new ScreenPoint(2, 1));
    }

    [Fact]
    public void Intersect_gives_the_overlap_or_empty() {
      Assert.Equal(new ScreenRect(50, 60, 50, 40), ScreenRect.Intersect(new ScreenRect(0, 0, 100, 100), new ScreenRect(50, 60, 100, 100)));
      Assert.True(ScreenRect.Intersect(new ScreenRect(0, 0, 10, 10), new ScreenRect(20, 20, 10, 10)).IsEmpty);
      // Touching rectangles intersect with zero width, like WPF.
      ScreenRect touching = ScreenRect.Intersect(new ScreenRect(0, 0, 10, 10), new ScreenRect(10, 0, 10, 10));
      Assert.False(touching.IsEmpty);
      Assert.Equal(0, touching.Width);
      Assert.True(ScreenRect.Intersect(ScreenRect.Empty, new ScreenRect(0, 0, 10, 10)).IsEmpty);
    }

    [Fact]
    public void Contains_includes_the_edges() {
      ScreenRect r = new ScreenRect(0, 0, 10, 10);
      Assert.True(r.Contains(new ScreenPoint(0, 0)));
      Assert.True(r.Contains(new ScreenPoint(10, 10)));
      Assert.True(r.Contains(new ScreenPoint(5, 5)));
      Assert.False(r.Contains(new ScreenPoint(10.5, 5)));
      Assert.False(ScreenRect.Empty.Contains(new ScreenPoint(0, 0)));
    }

    [Fact]
    public void Contains_a_rectangle_when_it_lies_inside_or_on_the_edges() {
      ScreenRect r = new ScreenRect(0, 0, 10, 10);
      Assert.True(r.Contains(new ScreenRect(0, 0, 10, 10)));
      Assert.True(r.Contains(new ScreenRect(2, 2, 3, 3)));
      Assert.False(r.Contains(new ScreenRect(5, 5, 6, 3)));
      Assert.False(r.Contains(ScreenRect.Empty));
    }

    [Fact]
    public void Same_results_as_WPF_Rect() {
      System.Windows.Rect a = new System.Windows.Rect(-3000, 0, 3000, 1740);
      System.Windows.Rect b = new System.Windows.Rect(-150, 10, 300, 32);
      System.Windows.Rect wpf = System.Windows.Rect.Intersect(a, b);
      ScreenRect ours = ScreenRect.Intersect(new ScreenRect(a.X, a.Y, a.Width, a.Height), new ScreenRect(b.X, b.Y, b.Width, b.Height));
      Assert.Equal(new ScreenRect(wpf.X, wpf.Y, wpf.Width, wpf.Height), ours);
    }
  }
}
