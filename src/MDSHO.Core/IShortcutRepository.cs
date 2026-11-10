using System.Collections.Generic;

namespace MDSHO.Core {

  /// <summary>
  /// Persistence of <see cref="Shortcut"/> (Shortcuts table). A shortcut belongs either to a window (root shortcut:
  /// ParentWindowID set) or to a group (ParentShortcutID set), never both. Positions count from 0 per parent.
  /// Returned entities have an empty <see cref="Shortcut.Shortcuts"/> list; whole trees come from <see cref="IDesktopWindowRepository.LoadAll"/>.
  /// </summary>
  public interface IShortcutRepository {
    /// <summary>The shortcut row or null.</summary>
    Shortcut Get(long id);
    /// <summary>All shortcut rows (flat, without children).</summary>
    List<Shortcut> GetAll();
    /// <summary>Root shortcuts of a window ordered by Position.</summary>
    List<Shortcut> GetRoots(long parentWindowID);
    /// <summary>Children of a group ordered by Position.</summary>
    List<Shortcut> GetChildren(long parentShortcutID);
    /// <summary>The highest Position among the siblings under the given parent (exactly one parent id must be set), -1 when there are none.</summary>
    long GetMaxPosition(long? parentShortcutID, long? parentWindowID);

    /// <summary>Inserts the shortcut exactly as given (its Position included; its ID too when not 0, e.g. on import, otherwise a new one is assigned), sets <see cref="Shortcut.ID"/> and returns it.</summary>
    long Add(Shortcut shortcut);
    /// <summary>Inserts a copy of all the fields of the template as the last sibling under its parent (the template's ID and Position are ignored).</summary>
    Shortcut Append(Shortcut template);
    /// <summary>Appends a new empty group named "New Group".</summary>
    Shortcut CreateGroup(long? parentShortcutID, long? parentWindowID);
    /// <summary>Appends a new shortcut.</summary>
    Shortcut Create(long? parentShortcutID, long? parentWindowID, string name, string target, string ext, string guid);
    /// <summary>Appends a new run-command shortcut named "New Command".</summary>
    Shortcut CreateProcessCommand(long? parentShortcutID, long? parentWindowID);
    /// <summary>Writes every column of the row.</summary>
    void Update(Shortcut shortcut);
    /// <summary>Deletes the shortcut; its descendants are deleted by the database (cascade).</summary>
    void Delete(long id);

    /// <summary>Moves the shortcut into a group (null = not in a group).</summary>
    void UpdateParentShortcutID(long id, long? parentShortcutID);
    /// <summary>Moves the shortcut to the root of a window (null = not at a window root).</summary>
    void UpdateParentWindowID(long id, long? parentWindowID);
    /// <summary>Moves the shortcut in one statement; exactly one of the two parents must be set.</summary>
    void UpdateParentShortcutIDAndParentWindowID(long id, long? parentShortcutID, long? parentWindowID);
    /// <summary>Saves the name.</summary>
    void UpdateName(long id, string name);
    /// <summary>Saves whether the group is open.</summary>
    void UpdateIsGroupExpanded(long id, bool isGroupExpanded);
    /// <summary>Saves whether the shortcut is marked.</summary>
    void UpdateIsMarked(long id, bool isMarked);
    /// <summary>Saves the mark colour.</summary>
    void UpdateMarkColor(long id, string markColor);
    /// <summary>Saves the order among the siblings.</summary>
    void UpdatePosition(long id, long position);
    /// <summary>Sets Position to the index in the list (0, 1, 2, ...) in one transaction: either all positions change or none.</summary>
    void UpdatePositions(IEnumerable<long> orderedShortcutIDs);
    /// <summary>Saves the program of a command shortcut.</summary>
    void UpdateProcessFileName(long id, string processFileName);
    /// <summary>Saves the arguments of a command shortcut.</summary>
    void UpdateProcessArguments(long id, string processArguments);
    /// <summary>Saves whether a command shortcut runs as administrator.</summary>
    void UpdateProcessRunAsAdmin(long id, bool processRunAsAdmin);
    /// <summary>Saves the working folder of a command shortcut (empty = none).</summary>
    void UpdateProcessWorkingFolder(long id, string processWorkingFolder);
  }

}
