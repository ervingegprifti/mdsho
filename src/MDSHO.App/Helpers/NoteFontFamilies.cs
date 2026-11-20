using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Resources;
using System.Windows.Media;
using MDSHO.Core;

namespace MDSHO.Helpers {

  /// <summary>
  /// Turns a note font name into a WPF font. Windows fonts load by name; the fonts that come with MDSHO load from
  /// embedded files in Assets/Fonts (like the icon font). A shipped font whose file is missing shows Courier New.
  /// </summary>
  public static class NoteFontFamilies {
    /// <summary>The folder of the embedded font files.</summary>
    public const string FolderUri = "pack://application:,,,/Assets/Fonts/";

    // The Regular file of each shipped font, and the family name inside it.
    // Check the family names when the files arrive (Latin Modern files name their family "LM Roman 10" / "LM Mono 10").
    private static readonly Dictionary<string, (string File, string Family)> shipped = new Dictionary<string, (string, string)>(StringComparer.OrdinalIgnoreCase) {
      ["JetBrains Mono"] = ("JetBrainsMono-Regular.ttf", "JetBrains Mono"),
      ["Source Code Pro"] = ("SourceCodePro-Regular.ttf", "Source Code Pro"),
      ["IBM Plex Mono"] = ("IBMPlexMono-Regular.ttf", "IBM Plex Mono"),
      ["Latin Modern Roman"] = ("lmroman10-regular.otf", "LM Roman 10"),
      ["Latin Modern Mono"] = ("lmmono10-regular.otf", "LM Mono 10"),
    };

    /// <summary>True when a font file is embedded in Assets/Fonts. Tests replace it.</summary>
    public static Func<string, bool> ResourceExists = EmbeddedFontFileExists;

    /// <summary>The embedded file of a shipped font (for example "IBMPlexMono-Regular.ttf"), or null for a Windows font.</summary>
    public static string FileOf(string name) {
      return name != null && shipped.TryGetValue(name, out var font) ? font.File : null;
    }

    /// <summary>The family name inside the file of a shipped font, or null for a Windows font.</summary>
    public static string FamilyInFileOf(string name) {
      return name != null && shipped.TryGetValue(name, out var font) ? font.Family : null;
    }

    /// <summary>True when the file of a shipped font is embedded; false for a Windows font.</summary>
    public static bool IsAvailable(string name) {
      string file = FileOf(name);
      return file != null && ResourceExists(file);
    }

    /// <summary>The WPF font for a note font name: a name not in the list, or a shipped font without its file, is Courier New.</summary>
    public static FontFamily Resolve(string name) {
      name = NoteFonts.OrDefault(name);
      if (!NoteFonts.IsShipped(name)) {
        return new FontFamily(name);
      }
      if (!IsAvailable(name)) {
        return new FontFamily(NoteFonts.Default);
      }
      return new FontFamily(new Uri(FolderUri), "./#" + FamilyInFileOf(name));
    }

    private static readonly Lazy<HashSet<string>> embedded = new Lazy<HashSet<string>>(ReadEmbeddedNames);

    /// <summary>True when Assets/Fonts/<paramref name="file"/> is embedded in the app (its WPF resources).</summary>
    public static bool EmbeddedFontFileExists(string file) {
      return !string.IsNullOrEmpty(file) && embedded.Value.Contains("assets/fonts/" + file.ToLowerInvariant());
    }

    // The names of the app's WPF resources (lower case paths such as "assets/fonts/mdshofont.ttf").
    private static HashSet<string> ReadEmbeddedNames() {
      HashSet<string> names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
      System.Reflection.Assembly app = typeof(NoteFontFamilies).Assembly;
      using (Stream stream = app.GetManifestResourceStream(app.GetName().Name + ".g.resources")) {
        if (stream == null) {
          return names;
        }
        using (ResourceReader reader = new ResourceReader(stream)) {
          foreach (DictionaryEntry entry in reader) {
            names.Add((string)entry.Key);
          }
        }
      }
      return names;
    }
  }
}
