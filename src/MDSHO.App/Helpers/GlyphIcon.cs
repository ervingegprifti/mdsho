using System;
using System.Globalization;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace MDSHO.Helpers {

  /// <summary>
  /// Turns a glyph of the MDSHOFont icon font into an image: a <see cref="DrawingImage"/> for WPF (window icons) or a
  /// small bitmap for WinForms menus (the tray menu). The glyph is drawn as a vector shape in a square em box, so it stays
  /// crisp at 16 and 32 pixels.
  /// </summary>
  public static class GlyphIcon {
    /// <summary>The dark colour of window icons, like the title text of the app's dialogs.</summary>
    public static readonly Color DefaultColor = Color.FromRgb(0x33, 0x33, 0x33);

    /// <summary>The colour of menu icons (see StyleMenuItemFontIcon in App.xaml).</summary>
    public static readonly Color MenuColor = Color.FromRgb(0x42, 0x42, 0x42);

    /// <summary>Size of the em box the glyph is drawn in, in DIPs; the image scales from it.</summary>
    private const double BoxSize = 32;

    // Which glyph each image was made from, so tests (and callers) can tell without reading pixels.
    private static readonly ConditionalWeakTable<object, string> glyphs = new ConditionalWeakTable<object, string>();

    /// <summary>A frozen square image of <paramref name="glyph"/> in the icon font, in <paramref name="color"/> (dark by default).</summary>
    public static ImageSource Create(string glyph, Color? color = null) {
      Brush brush = new SolidColorBrush(color ?? DefaultColor);
      brush.Freeze();
      FormattedText text = new FormattedText(glyph ?? "", CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
        new Typeface(IconFontFamily(), FontStyles.Normal, FontWeights.Normal, FontStretches.Normal), BoxSize, brush, 1.0);
      // Icon fonts put the glyph in the em box (ascent + descent = em), so the line's top-left is the box's top-left;
      // only the advance width may differ from the em, so centre it horizontally.
      Geometry shape = text.BuildGeometry(new Point(Math.Round((BoxSize - text.WidthIncludingTrailingWhitespace) / 2), 0));
      DrawingGroup drawing = new DrawingGroup();
      // A transparent square fixes the image's bounds to the whole box, whatever the glyph's shape.
      drawing.Children.Add(new GeometryDrawing(Brushes.Transparent, null, new RectangleGeometry(new Rect(0, 0, BoxSize, BoxSize))));
      drawing.Children.Add(new GeometryDrawing(brush, null, shape));
      drawing.ClipGeometry = new RectangleGeometry(new Rect(0, 0, BoxSize, BoxSize));
      DrawingImage image = new DrawingImage(drawing);
      image.Freeze();
      glyphs.AddOrUpdate(image, glyph);
      return image;
    }

    /// <summary>
    /// A <paramref name="size"/> x <paramref name="size"/> pixel bitmap of <paramref name="glyph"/>, for WinForms menu
    /// items. Uses the menu icon colour by default. The caller owns (and disposes) the bitmap.
    /// </summary>
    public static System.Drawing.Bitmap ToBitmap(string glyph, int size, Color? color = null) {
      ImageSource image = Create(glyph, color ?? MenuColor);
      DrawingVisual visual = new DrawingVisual();
      using (DrawingContext dc = visual.RenderOpen()) {
        dc.DrawImage(image, new Rect(0, 0, size, size));
      }
      RenderTargetBitmap rendered = new RenderTargetBitmap(size, size, 96, 96, PixelFormats.Pbgra32);
      rendered.Render(visual);
      PngBitmapEncoder encoder = new PngBitmapEncoder();
      encoder.Frames.Add(BitmapFrame.Create(rendered));
      using (MemoryStream stream = new MemoryStream()) {
        encoder.Save(stream);
        stream.Position = 0;
        // A Bitmap read from a stream needs the stream for its whole life: copy it into one that doesn't.
        using (System.Drawing.Bitmap fromStream = new System.Drawing.Bitmap(stream)) {
          System.Drawing.Bitmap bitmap = new System.Drawing.Bitmap(fromStream);
          glyphs.AddOrUpdate(bitmap, glyph);
          return bitmap;
        }
      }
    }

    /// <summary>The glyph an image made here was drawn from; null for any other image.</summary>
    public static string GlyphOf(object image) {
      return image != null && glyphs.TryGetValue(image, out string glyph) ? glyph : null;
    }

    /// <summary>The app's IconFont resource, or the same font from the app's resources when it can't be looked up here.</summary>
    private static FontFamily IconFontFamily() {
      Application app = Application.Current;
      if (app != null && app.Dispatcher.CheckAccess() && app.TryFindResource("IconFont") is FontFamily font) {
        return font;
      }
      return new FontFamily(new Uri("pack://application:,,,/"), "./Assets/Fonts/#MDSHOFont");
    }
  }
}
