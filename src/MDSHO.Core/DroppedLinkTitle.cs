using System;
using System.Text;
using System.Text.RegularExpressions;

namespace MDSHO.Core {

  /// <summary>
  /// The name of a link dropped from a browser: the page or link title found in the drop data, else the URL.
  /// Reads only the drop data; it never fetches the page.
  /// </summary>
  public static class DroppedLinkTitle {

    /// <summary>The longest name made from a title.</summary>
    public const int MaxNameLength = 100;

    // FILEGROUPDESCRIPTORW: a UINT count, then FILEDESCRIPTORW items of 592 bytes; the WCHAR[260] file name starts at byte 72.
    private const int DescriptorSize = 592;
    private const int FileNameOffset = 72;
    private const int FileNameChars = 260;
    private const string UrlEnding = ".url";

    // The characters Windows does not allow in file names; browsers write them as "_".
    private static readonly Regex FileNameReplaced = new Regex(@"[\\/:*?""<>|]", RegexOptions.CultureInvariant);
    private static readonly Regex Spaces = new Regex(@"\s+", RegexOptions.CultureInvariant);

    /// <summary>
    /// The raw title for <paramref name="url"/> in the drop data, or null. Chrome and Edge give a FileGroupDescriptorW
    /// with one "Title.url" file; Firefox gives text/x-moz-url, UTF-16 "url\ntitle". Firefox's title is used only when its
    /// URL is the dropped one.
    /// </summary>
    public static string Find(string url, byte[] fileGroupDescriptor, byte[] mozUrl) {
      string title = FromFileGroupDescriptor(fileGroupDescriptor);
      if (string.IsNullOrWhiteSpace(title)) {
        title = FromMozUrl(url, mozUrl);
      }
      return string.IsNullOrWhiteSpace(title) ? null : title;
    }

    /// <summary>The cleaned title, or <paramref name="url"/> when the title is empty or just the URL.</summary>
    public static string NameFor(string url, string title) {
      string name = Clean(title);
      if (name.Length == 0 || IsJustTheUrl(url, name)) {
        return url;
      }
      return name;
    }

    /// <summary>The title from a single "Title.url" virtual file, or null.</summary>
    private static string FromFileGroupDescriptor(byte[] data) {
      if (data == null || data.Length < 4) {
        return null;
      }
      uint count = BitConverter.ToUInt32(data, 0);
      // One link only, and the whole descriptor must be there.
      if (count != 1 || data.Length < 4 + DescriptorSize) {
        return null;
      }
      string fileName = Encoding.Unicode.GetString(data, 4 + FileNameOffset, FileNameChars * 2);
      int end = fileName.IndexOf('\0');
      if (end >= 0) {
        fileName = fileName.Substring(0, end);
      }
      if (!fileName.EndsWith(UrlEnding, StringComparison.OrdinalIgnoreCase)) {
        return null;
      }
      return fileName.Substring(0, fileName.Length - UrlEnding.Length);
    }

    /// <summary>The title from Firefox's "url\ntitle" when its first URL is <paramref name="url"/>, or null.</summary>
    private static string FromMozUrl(string url, byte[] data) {
      if (data == null || data.Length < 2) {
        return null;
      }
      string text = Encoding.Unicode.GetString(data, 0, data.Length - data.Length % 2);
      int end = text.IndexOf('\0');
      if (end >= 0) {
        text = text.Substring(0, end);
      }
      string[] lines = text.Replace("\r\n", "\n").Split('\n');
      if (lines.Length < 2 || !SameUrl(url, lines[0].Trim())) {
        return null;
      }
      return lines[1];
    }

    /// <summary>True when both are the same absolute address, however they are written.</summary>
    private static bool SameUrl(string a, string b) {
      return Uri.TryCreate(a, UriKind.Absolute, out Uri ua) && Uri.TryCreate(b, UriKind.Absolute, out Uri ub)
        && string.Equals(ua.AbsoluteUri, ub.AbsoluteUri, StringComparison.Ordinal);
    }

    /// <summary>
    /// Drops control characters, collapses spaces and line breaks, trims, and cuts at <see cref="MaxNameLength"/>.
    /// The "_" browsers put for characters a file name can't hold stays: it could be a real underscore.
    /// </summary>
    private static string Clean(string title) {
      if (string.IsNullOrEmpty(title)) {
        return "";
      }
      StringBuilder kept = new StringBuilder(title.Length);
      foreach (char c in title) {
        // Line breaks and tabs become spaces; other control characters go.
        if (char.IsWhiteSpace(c)) {
          kept.Append(' ');
        } else if (!char.IsControl(c)) {
          kept.Append(c);
        }
      }
      string name = Spaces.Replace(kept.ToString(), " ").Trim();
      if (name.Length > MaxNameLength) {
        int cut = MaxNameLength;
        // Don't split a character made of two UTF-16 parts.
        if (char.IsHighSurrogate(name[cut - 1])) {
          cut--;
        }
        name = name.Substring(0, cut).TrimEnd();
      }
      return name;
    }

    /// <summary>True when the title only repeats the address, also as browsers write it in a file name.</summary>
    private static bool IsJustTheUrl(string url, string title) {
      if (Plain(title) == Plain(url)) {
        return true;
      }
      // Chrome names an untitled page's file after its address, for example "https___example.org_a".
      string asFileName = FileNameReplaced.Replace(url.Trim(), "_");
      return string.Equals(title.TrimEnd('_'), asFileName.TrimEnd('_'), StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>An address without scheme, "www." and ending "/", in lower case, for comparing.</summary>
    private static string Plain(string address) {
      string a = address.Trim().ToLowerInvariant();
      int scheme = a.IndexOf("://", StringComparison.Ordinal);
      if (scheme >= 0) {
        a = a.Substring(scheme + 3);
      }
      if (a.StartsWith("www.", StringComparison.Ordinal)) {
        a = a.Substring(4);
      }
      return a.TrimEnd('/');
    }
  }
}
