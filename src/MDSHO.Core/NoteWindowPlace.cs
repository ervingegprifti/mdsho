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
    /// <summary>Size of the note's text (Ctrl + mouse wheel); 0 = <see cref="DefaultFontSize"/>.</summary>
    public long FontSize { get; set; }

    /// <summary>The text size of a new note or a comment.</summary>
    public const long DefaultFontSize = 16;
    /// <summary>The smallest text size.</summary>
    public const long MinFontSize = 8;
    /// <summary>The biggest text size.</summary>
    public const long MaxFontSize = 72;
    /// <summary>How much one turn of the wheel changes the size.</summary>
    public const long FontSizeStep = 2;

    /// <summary>The size after one turn of the wheel: bigger for up (positive), smaller for down, within the limits.</summary>
    public static long NextFontSize(long size, int wheelDelta) {
      long next = wheelDelta > 0 ? size + FontSizeStep : wheelDelta < 0 ? size - FontSizeStep : size;
      return Math.Clamp(next, MinFontSize, MaxFontSize);
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
    /// <summary>Saves the place of a note's window (adds the row the first time, updates it after that).</summary>
    void Save(NoteWindowPlace place);
    /// <summary>Marks a note's window as open or closed, keeping its place.</summary>
    void SetOpen(long shortcutID, bool isOpen);
  }
}
