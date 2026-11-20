using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace MDSHO.Core {

  /// <summary>The fonts a note or snippet can use (the font menu in its title bar), and its text size in points.</summary>
  public static class NoteFonts {
    /// <summary>The font of new notes and snippets, and of a stored font that is not in the list.</summary>
    public const string Default = "Courier New";

    /// <summary>The label above the fonts that Windows has.</summary>
    public const string FromWindowsLabel = "From Windows";

    /// <summary>The label above the fonts that ship inside MDSHO.</summary>
    public const string ComesWithMdshoLabel = "Comes with MDSHO";

    /// <summary>Fonts every Windows PC has.</summary>
    public static readonly IReadOnlyList<string> FromWindows = new[] { "Courier New", "Consolas", "Arial" };

    /// <summary>Fonts that ship inside MDSHO (embedded font files).</summary>
    public static readonly IReadOnlyList<string> ComesWithMdsho = new[] {
      "JetBrains Mono", "Source Code Pro", "IBM Plex Mono", "Latin Modern Roman", "Latin Modern Mono" };

    /// <summary>Every font in menu order.</summary>
    public static readonly IReadOnlyList<string> All = FromWindows.Concat(ComesWithMdsho).ToArray();

    /// <summary>The font as the list spells it, or <see cref="Default"/> when it is not in the list.</summary>
    public static string OrDefault(string name) {
      return All.FirstOrDefault(f => string.Equals(f, name?.Trim(), StringComparison.OrdinalIgnoreCase)) ?? Default;
    }

    /// <summary>True for a font that ships inside MDSHO.</summary>
    public static bool IsShipped(string name) {
      return ComesWithMdsho.Contains(name, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>A size in points, rounded to the nearest half point. WPF sizes are DIPs (96 per inch); points are 72 per inch.</summary>
    public static double Points(double dips) {
      return Math.Round(dips * 72.0 / 96.0 * 2.0, MidpointRounding.AwayFromZero) / 2.0;
    }

    /// <summary>The size label in the title bar, for example "10.5 pt".</summary>
    public static string PointsLabel(double dips) {
      return Points(dips).ToString("0.#", CultureInfo.InvariantCulture) + " pt";
    }
  }
}
