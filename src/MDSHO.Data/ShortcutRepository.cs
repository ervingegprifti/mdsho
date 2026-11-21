using System.Collections.Generic;
using MDSHO.Common;
using MDSHO.Core;

namespace MDSHO.Data {

  internal sealed class ShortcutRepository : RepositoryBase, IShortcutRepository {
    /// <summary>Column list for SELECTs: the database column ParentMDSHOWindowID is exposed as the property ParentWindowID.</summary>
    internal const string SelectColumns = @"ID, ParentShortcutID, ParentMDSHOWindowID AS ParentWindowID, Name, Target, Ext, Guid, IsGroup, IsGroupExpanded,
      Comment, IsMarked, MarkColor, Position, IsProcessCommand, ProcessFileName, ProcessArguments, ProcessRunAsAdmin, ProcessWorkingFolder, IsNote, NoteText,
      NoteBorderColor, NoteTitleBackgroundColor, NoteTitleTextColor, NoteBackgroundColor, NoteTextColor, IsSnippet, SnippetText, TextFont,
      CommentFont, CommentFontSize, TextPrevious1, TextPrevious1Time, TextPrevious2, TextPrevious2Time, CommentPrevious1, CommentPrevious1Time, CommentPrevious2, CommentPrevious2Time";

    private const string InsertSql = @"
      INSERT INTO Shortcuts (
        ID, ParentShortcutID, ParentMDSHOWindowID, Name, Target, Ext, Guid, IsGroup, IsGroupExpanded, Comment, IsMarked,
        MarkColor, Position, IsProcessCommand, ProcessFileName, ProcessArguments, ProcessRunAsAdmin, ProcessWorkingFolder, IsNote, NoteText,
        NoteBorderColor, NoteTitleBackgroundColor, NoteTitleTextColor, NoteBackgroundColor, NoteTextColor, IsSnippet, SnippetText, TextFont,
        CommentFont, CommentFontSize, TextPrevious1, TextPrevious1Time, TextPrevious2, TextPrevious2Time, CommentPrevious1, CommentPrevious1Time, CommentPrevious2, CommentPrevious2Time)
      VALUES (
        NULLIF(@ID, 0), @ParentShortcutID, @ParentWindowID, @Name, @Target, @Ext, @Guid, @IsGroup, @IsGroupExpanded, @Comment, @IsMarked,
        @MarkColor, @Position, @IsProcessCommand, @ProcessFileName, @ProcessArguments, @ProcessRunAsAdmin, COALESCE(@ProcessWorkingFolder, ''), @IsNote, COALESCE(@NoteText, ''),
        COALESCE(@NoteBorderColor, ''), COALESCE(@NoteTitleBackgroundColor, ''), COALESCE(@NoteTitleTextColor, ''), COALESCE(@NoteBackgroundColor, ''), COALESCE(@NoteTextColor, ''), @IsSnippet, COALESCE(@SnippetText, ''), COALESCE(@TextFont, 'Courier New'),
        COALESCE(@CommentFont, 'Courier New'), @CommentFontSize,
        COALESCE(@TextPrevious1, ''), COALESCE(@TextPrevious1Time, ''), COALESCE(@TextPrevious2, ''), COALESCE(@TextPrevious2Time, ''),
        COALESCE(@CommentPrevious1, ''), COALESCE(@CommentPrevious1Time, ''), COALESCE(@CommentPrevious2, ''), COALESCE(@CommentPrevious2Time, ''))";

    private const string UpdateSql = @"
      UPDATE Shortcuts SET
        ParentShortcutID = @ParentShortcutID, ParentMDSHOWindowID = @ParentWindowID, Name = @Name, Target = @Target,
        Ext = @Ext, Guid = @Guid, IsGroup = @IsGroup, IsGroupExpanded = @IsGroupExpanded, Comment = @Comment, IsMarked = @IsMarked,
        MarkColor = @MarkColor, Position = @Position, IsProcessCommand = @IsProcessCommand, ProcessFileName = @ProcessFileName,
        ProcessArguments = @ProcessArguments, ProcessRunAsAdmin = @ProcessRunAsAdmin, IsNote = @IsNote, NoteText = COALESCE(@NoteText, ''),
        NoteBorderColor = COALESCE(@NoteBorderColor, ''), NoteTitleBackgroundColor = COALESCE(@NoteTitleBackgroundColor, ''), NoteTitleTextColor = COALESCE(@NoteTitleTextColor, ''), NoteBackgroundColor = COALESCE(@NoteBackgroundColor, ''), NoteTextColor = COALESCE(@NoteTextColor, ''),
        ProcessWorkingFolder = COALESCE(@ProcessWorkingFolder, ''), IsSnippet = @IsSnippet, SnippetText = COALESCE(@SnippetText, ''),
        TextFont = COALESCE(@TextFont, 'Courier New'), CommentFont = COALESCE(@CommentFont, 'Courier New'), CommentFontSize = @CommentFontSize,
        TextPrevious1 = COALESCE(@TextPrevious1, ''), TextPrevious1Time = COALESCE(@TextPrevious1Time, ''),
        TextPrevious2 = COALESCE(@TextPrevious2, ''), TextPrevious2Time = COALESCE(@TextPrevious2Time, ''),
        CommentPrevious1 = COALESCE(@CommentPrevious1, ''), CommentPrevious1Time = COALESCE(@CommentPrevious1Time, ''),
        CommentPrevious2 = COALESCE(@CommentPrevious2, ''), CommentPrevious2Time = COALESCE(@CommentPrevious2Time, '')
      WHERE ID = @ID";

    #region QUERIES
    public Shortcut Get(long id) {
      return One<Shortcut>("SELECT " + SelectColumns + " FROM Shortcuts WHERE ID = @id", new { id });
    }

    public List<Shortcut> GetAll() {
      return Many<Shortcut>("SELECT " + SelectColumns + " FROM Shortcuts ORDER BY ID ASC");
    }

    public List<Shortcut> GetRoots(long parentWindowID) {
      return Many<Shortcut>("SELECT " + SelectColumns + " FROM Shortcuts WHERE ParentShortcutID IS NULL AND ParentMDSHOWindowID = @parentWindowID ORDER BY Position ASC, ID ASC", new { parentWindowID });
    }

    public List<Shortcut> GetChildren(long parentShortcutID) {
      return Many<Shortcut>("SELECT " + SelectColumns + " FROM Shortcuts WHERE ParentShortcutID = @parentShortcutID ORDER BY Position ASC, ID ASC", new { parentShortcutID });
    }

    public long GetMaxPosition(long? parentShortcutID, long? parentWindowID) {
      // Both parentShortcutID and parentWindowID cannot be null and cannot be both NOT null.
      // The set is empty if -1. Since we start positioning from 0 then -1 + 1 = 0 is the first position element.
      long? max = null;
      if (parentShortcutID != null) {
        max = One<long?>("SELECT MAX(Position) FROM Shortcuts WHERE ParentShortcutID = @parentShortcutID AND ParentMDSHOWindowID IS NULL", new { parentShortcutID });
      }
      if (parentWindowID != null) {
        max = One<long?>("SELECT MAX(Position) FROM Shortcuts WHERE ParentShortcutID IS NULL AND ParentMDSHOWindowID = @parentWindowID", new { parentWindowID });
      }
      return max ?? -1;
    }
    #endregion QUERIES

    #region CREATE
    public long Add(Shortcut shortcut) {
      shortcut.ID = Insert(InsertSql, shortcut);
      return shortcut.ID;
    }

    public Shortcut Append(Shortcut template) {
      return InTransaction(() => {
        Shortcut copy = new Shortcut(
          template.ParentShortcutID, template.ParentWindowID, template.Name, template.Target, template.Ext, template.Guid,
          template.IsGroup, template.IsGroupExpanded, template.Comment, template.IsMarked, template.MarkColor,
          GetMaxPosition(template.ParentShortcutID, template.ParentWindowID) + 1,
          template.IsProcessCommand, template.ProcessFileName, template.ProcessArguments,
          template.ProcessRunAsAdmin, template.ProcessWorkingFolder, template.IsNote, template.NoteText);
        copy.NoteBorderColor = template.NoteBorderColor;
        copy.NoteTitleBackgroundColor = template.NoteTitleBackgroundColor;
        copy.NoteTitleTextColor = template.NoteTitleTextColor;
        copy.NoteBackgroundColor = template.NoteBackgroundColor;
        copy.NoteTextColor = template.NoteTextColor;
        copy.IsSnippet = template.IsSnippet;
        copy.SnippetText = template.SnippetText ?? "";
        copy.TextFont = template.TextFont ?? NoteFonts.Default;
        copy.CommentFont = template.CommentFont ?? NoteFonts.Default;
        copy.CommentFontSize = template.CommentFontSize;
        copy.TextPrevious1 = template.TextPrevious1 ?? "";
        copy.TextPrevious1Time = template.TextPrevious1Time ?? "";
        copy.TextPrevious2 = template.TextPrevious2 ?? "";
        copy.TextPrevious2Time = template.TextPrevious2Time ?? "";
        copy.CommentPrevious1 = template.CommentPrevious1 ?? "";
        copy.CommentPrevious1Time = template.CommentPrevious1Time ?? "";
        copy.CommentPrevious2 = template.CommentPrevious2 ?? "";
        copy.CommentPrevious2Time = template.CommentPrevious2Time ?? "";
        return Get(Add(copy));
      });
    }

    public Shortcut CreateGroup(long? parentShortcutID, long? parentWindowID) {
      return Append(new Shortcut(parentShortcutID, parentWindowID, "New group", "", "", Identifiers.NewGuid(),
        isGroup: 1, isGroupExpanded: 1, comment: "", isMarked: 0, markColor: "#FFFFFFFF", position: 0,
        isProcessCommand: 0, processFileName: "", processArguments: ""));
    }

    public Shortcut Create(long? parentShortcutID, long? parentWindowID, string name, string target, string ext, string guid) {
      return Append(new Shortcut(parentShortcutID, parentWindowID, name, target, ext, guid,
        isGroup: 0, isGroupExpanded: 0, comment: "", isMarked: 0, markColor: "#FFFFFFFF", position: 0,
        isProcessCommand: 0, processFileName: "", processArguments: ""));
    }

    public Shortcut CreateProcessCommand(long? parentShortcutID, long? parentWindowID) {
      return Append(new Shortcut(parentShortcutID, parentWindowID, "New command", "", "", Identifiers.NewGuid(),
        isGroup: 0, isGroupExpanded: 0, comment: "", isMarked: 0, markColor: "#FFFFFFFF", position: 0,
        isProcessCommand: 1, processFileName: "", processArguments: ""));
    }

    public Shortcut CreateNote(long? parentShortcutID, long? parentWindowID) {
      return Append(new Shortcut(parentShortcutID, parentWindowID, "New note", "", "", Identifiers.NewGuid(),
        isGroup: 0, isGroupExpanded: 0, comment: "", isMarked: 0, markColor: "#FFFFFFFF", position: 0,
        isProcessCommand: 0, processFileName: "", processArguments: "", isNote: 1, noteText: ""));
    }
    public Shortcut CreateSnippet(long? parentShortcutID, long? parentWindowID) {
      return Append(new Shortcut(parentShortcutID, parentWindowID, "New snippet", "", "", Identifiers.NewGuid(),
        isGroup: 0, isGroupExpanded: 0, comment: "", isMarked: 0, markColor: "#FFFFFFFF", position: 0,
        isProcessCommand: 0, processFileName: "", processArguments: "") { IsSnippet = 1, SnippetText = "" });
    }
    #endregion CREATE

    #region UPDATE / DELETE
    public void Update(Shortcut shortcut) {
      Run(UpdateSql, shortcut);
    }

    public void Delete(long id) {
      Run("DELETE FROM Shortcuts WHERE ID = @id", new { id });
    }

    public void UpdateParentShortcutID(long id, long? parentShortcutID) {
      Run("UPDATE Shortcuts SET ParentShortcutID = @parentShortcutID WHERE ID = @id;", new { id, parentShortcutID });
    }
    public void UpdateParentWindowID(long id, long? parentWindowID) {
      Run("UPDATE Shortcuts SET ParentMDSHOWindowID = @parentWindowID WHERE ID = @id;", new { id, parentWindowID });
    }
    public void UpdateParentShortcutIDAndParentWindowID(long id, long? parentShortcutID, long? parentWindowID) {
      Run("UPDATE Shortcuts SET ParentShortcutID = @parentShortcutID, ParentMDSHOWindowID = @parentWindowID WHERE ID = @id;", new { id, parentShortcutID, parentWindowID });
    }
    public void UpdateName(long id, string name) {
      Run("UPDATE Shortcuts SET Name = @name WHERE ID = @id;", new { id, name });
    }
    public void UpdateIsGroupExpanded(long id, bool isGroupExpanded) {
      Run("UPDATE Shortcuts SET IsGroupExpanded = @isGroupExpanded WHERE ID = @id;", new { id, isGroupExpanded });
    }
    public void UpdateIsMarked(long id, bool isMarked) {
      Run("UPDATE Shortcuts SET IsMarked = @isMarked WHERE ID = @id;", new { id, isMarked });
    }
    public void UpdateMarkColor(long id, string markColor) {
      Run("UPDATE Shortcuts SET MarkColor = @markColor WHERE ID = @id;", new { id, markColor });
    }
    public void UpdateComment(long id, string comment) {
      Run("UPDATE Shortcuts SET Comment = @comment WHERE ID = @id;", new { id, comment = comment ?? "" });
    }
    public void UpdateNoteColors(long id, string border, string titleBackground, string titleText, string background, string text) {
      Run(@"UPDATE Shortcuts SET NoteBorderColor = @border, NoteTitleBackgroundColor = @titleBackground, NoteTitleTextColor = @titleText,
        NoteBackgroundColor = @background, NoteTextColor = @text WHERE ID = @id;",
        new { id, border = border ?? "", titleBackground = titleBackground ?? "", titleText = titleText ?? "", background = background ?? "", text = text ?? "" });
    }
    public void UpdateNoteText(long id, string noteText) {
      Run("UPDATE Shortcuts SET NoteText = @noteText WHERE ID = @id;", new { id, noteText = noteText ?? "" });
    }
    public void UpdateSnippetText(long id, string snippetText) {
      Run("UPDATE Shortcuts SET SnippetText = @snippetText WHERE ID = @id;", new { id, snippetText = snippetText ?? "" });
    }
    public void UpdateTextFont(long id, string font) {
      Run("UPDATE Shortcuts SET TextFont = @font WHERE ID = @id;", new { id, font = font ?? NoteFonts.Default });
    }

    public void UpdateCommentFont(long id, string font) {
      Run("UPDATE Shortcuts SET CommentFont = @font WHERE ID = @id;", new { id, font = font ?? NoteFonts.Default });
    }

    public void UpdateCommentFontSize(long id, long points) {
      Run("UPDATE Shortcuts SET CommentFontSize = @points WHERE ID = @id;", new { id, points });
    }

    public void UpdateTextHistory(long id, TextHistorySlots history) {
      history ??= TextHistorySlots.Empty;
      Run("UPDATE Shortcuts SET TextPrevious1 = @p1, TextPrevious1Time = @t1, TextPrevious2 = @p2, TextPrevious2Time = @t2 WHERE ID = @id;",
        new { id, p1 = history.Previous1 ?? "", t1 = history.Previous1Time ?? "", p2 = history.Previous2 ?? "", t2 = history.Previous2Time ?? "" });
    }

    public void UpdateCommentHistory(long id, TextHistorySlots history) {
      history ??= TextHistorySlots.Empty;
      Run("UPDATE Shortcuts SET CommentPrevious1 = @p1, CommentPrevious1Time = @t1, CommentPrevious2 = @p2, CommentPrevious2Time = @t2 WHERE ID = @id;",
        new { id, p1 = history.Previous1 ?? "", t1 = history.Previous1Time ?? "", p2 = history.Previous2 ?? "", t2 = history.Previous2Time ?? "" });
    }
    public void UpdatePosition(long id, long position) {
      Run("UPDATE Shortcuts SET Position = @position WHERE ID = @id;", new { id, position });
    }
    public void UpdatePositions(IEnumerable<long> orderedShortcutIDs) {
      InTransaction(() => {
        long position = 0;
        foreach (long id in orderedShortcutIDs) {
          UpdatePosition(id, position++);
        }
      });
    }
    public void UpdateProcessFileName(long id, string processFileName) {
      Run("UPDATE Shortcuts SET ProcessFileName = @processFileName WHERE ID = @id;", new { id, processFileName });
    }
    public void UpdateProcessArguments(long id, string processArguments) {
      Run("UPDATE Shortcuts SET ProcessArguments = @processArguments WHERE ID = @id;", new { id, processArguments });
    }
    public void UpdateProcessRunAsAdmin(long id, bool processRunAsAdmin) {
      Run("UPDATE Shortcuts SET ProcessRunAsAdmin = @processRunAsAdmin WHERE ID = @id;", new { id, processRunAsAdmin });
    }
    public void UpdateProcessWorkingFolder(long id, string processWorkingFolder) {
      Run("UPDATE Shortcuts SET ProcessWorkingFolder = @processWorkingFolder WHERE ID = @id;", new { id, processWorkingFolder = processWorkingFolder ?? "" });
    }
    #endregion UPDATE / DELETE
  }

}
