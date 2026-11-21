using System;
using System.Collections.Generic;

namespace MDSHO.Core {

  /// <summary>Where a note's window was last, how big and in which state, and whether it was open (one row per note).</summary>
  public sealed class NoteWindowPlace {
    /// <summary>The window was normal (not minimized or maximized).</summary>
    public const long Normal = 0;
    /// <summary>The window was minimized.</summary>
    public const long Minimized = 1;
    /// <summary>The window was maximized.</summary>
    public const long Maximized = 2;

    /// <summary>Database ID.</summary>
    public long ID { get; set; }
    /// <summary>The note (Shortcuts.ID) the window belongs to.</summary>
    public long ShortcutID { get; set; }
    /// <summary>Left edge of the normal (not maximized) window.</summary>
    public long Left { get; set; }
    /// <summary>Top edge of the normal window.</summary>
    public long Top { get; set; }
    /// <summary>Width of the normal window.</summary>
    public long Width { get; set; }
    /// <summary>Height of the normal window.</summary>
    public long Height { get; set; }
    /// <summary><see cref="Normal"/>, <see cref="Minimized"/> or <see cref="Maximized"/>.</summary>
    public long State { get; set; }
    /// <summary>True when the window was open (also when MDSHO or Windows closed it at exit).</summary>
    public bool IsOpen { get; set; }
    /// <summary>True when the window is pinned to stay on top of other apps (off for new notes).</summary>
    public bool IsTopmost { get; set; }
    /// <summary>True when the note's text wraps at the window's edge (off for new notes).</summary>
    public bool IsWrapText { get; set; }
    /// <summary>Size of the note's text in whole points (DIPs up to database version 20); 0 = <see cref="DefaultFontSize"/>.</summary>
    public long FontSize { get; set; }

    /// <summary>The text size of a new note or comment and of the snippet editor, in points.</summary>
    public const long DefaultFontSize = 12;
    /// <summary>The sizes in the text size menu and the steps of the wheel, in points (Word's list).</summary>
    public static readonly IReadOnlyList<long> SizePresets = new long[] { 8, 9, 10, 11, 12, 14, 16, 18, 20, 22, 24, 26, 28, 36, 48, 72 };
    /// <summary>The smallest text size in points; a smaller saved size loads as this.</summary>
    public const long MinFontSize = 8;
    /// <summary>The biggest text size in points; a bigger saved size loads as this.</summary>
    public const long MaxFontSize = 72;
    /// <summary>The database version from which FontSize is in points (before, DIPs). Imports of older data convert it.</summary>
    public const int FontSizeInPointsFromVersion = 21;

    /// <summary>
    /// The size after one turn of the wheel: the next bigger preset for up (positive), the next smaller one for down. It
    /// stops at the first and last preset; a size outside them stays when there is no preset in that direction.
    /// </summary>
    public static long NextFontSize(long size, int wheelDelta) {
      if (wheelDelta > 0) {
        foreach (long preset in SizePresets) {
          if (preset > size) { return preset; }
        }
      } else if (wheelDelta < 0) {
        for (int i = SizePresets.Count - 1; i >= 0; i--) {
          if (SizePresets[i] < size) { return SizePresets[i]; }
        }
      }
      return size;
    }

    /// <summary>A size as shown: "12 pt".</summary>
    public static string SizeLabel(long points) {
      return points.ToString(System.Globalization.CultureInfo.InvariantCulture) + " pt";
    }

    /// <summary>WPF's size (DIPs, 96 per inch) of a size in points (72 per inch).</summary>
    public static double ToDips(long points) {
      return points * 96.0 / 72.0;
    }

    /// <summary>
    /// A size saved in DIPs (database version 20 or older) in whole points: 0 or less stays 0 (the default), others are
    /// rounded (half away from zero, like SQLite's ROUND) and kept within the limits. Database version 21 converts the same way.
    /// </summary>
    public static long FontSizeFromDips(long dips) {
      if (dips <= 0) {
        return 0;
      }
      return Math.Clamp((long)Math.Round(dips * 0.75, MidpointRounding.AwayFromZero), MinFontSize, MaxFontSize);
    }

    /// <summary>A saved size to use: 0 or less is the default, others are kept within the limits.</summary>
    public static long FontSizeOrDefault(long size) {
      return size <= 0 ? DefaultFontSize : Math.Clamp(size, MinFontSize, MaxFontSize);
    }

    /// <summary>Parameterless constructor for the data layer (row mapping).</summary>
    public NoteWindowPlace() { }

    /// <summary>Creates a place for a note's window.</summary>
    public NoteWindowPlace(long shortcutID, long left, long top, long width, long height, long state, bool isOpen) {
      ShortcutID = shortcutID;
      Left = left;
      Top = top;
      Width = width;
      Height = height;
      State = state;
      IsOpen = isOpen;
    }
  }

  /// <summary>Reads and writes the places of note windows.</summary>
  public interface INoteWindowRepository {
    /// <summary>The place of a note's window, or null when it was never opened.</summary>
    NoteWindowPlace Get(long shortcutID);
    /// <summary>The places of the note windows that were open, oldest note first.</summary>
    List<NoteWindowPlace> GetOpen();
    /// <summary>The places of all note windows, by note (exports carry them).</summary>
    List<NoteWindowPlace> GetAll();
    /// <summary>Saves the place of a note's window (adds the row the first time, updates it after that).</summary>
    void Save(NoteWindowPlace place);
    /// <summary>Marks a note's window as open or closed, keeping its place.</summary>
    void SetOpen(long shortcutID, bool isOpen);
  }
}
