using System;
using System.Collections.Generic;
using System.Linq;
using MDSHO.Common;
using MDSHO.Core;

namespace MDSHO.Data {

  internal sealed class ShortcutRepository : RepositoryBase, IShortcutRepository {
    /// <summary>Column list for SELECTs: the database column ParentMDSHOWindowID is exposed as the property ParentWindowID.</summary>
    internal const string SelectColumns = @"ID, ParentShortcutID, ParentMDSHOWindowID AS ParentWindowID, Name, Target, Ext, Guid, IsGroup, IsGroupExpanded,
      Comment, IsMarked, MarkColor, IsProcessCommand, ProcessFileName, ProcessArguments, ProcessRunAsAdmin, ProcessWorkingFolder, IsNote, NoteText,
      NoteBorderColor, NoteTitleBackgroundColor, NoteTitleTextColor, NoteBackgroundColor, NoteTextColor, IsSnippet, SnippetText, TextFont,
      CommentFont, CommentFontSize, TextFontSize, NoteHighlights,
      TextCurrentLine, TextWholeWords, TextMatchCase, TextWrapText, CommentCurrentLine, CommentWholeWords, CommentMatchCase, CommentWrapText,
      SyncId, SortKey, ChangedAt, ChangedBy";

    /// <summary>The order of siblings: by sort key, then SyncId.</summary>
    internal const string Order = "SortKey ASC, SyncId ASC";

    private const string InsertSql = @"
      INSERT INTO Shortcuts (
        ID, ParentShortcutID, ParentMDSHOWindowID, Name, Target, Ext, Guid, IsGroup, IsGroupExpanded, Comment, IsMarked,
        MarkColor, IsProcessCommand, ProcessFileName, ProcessArguments, ProcessRunAsAdmin, ProcessWorkingFolder, IsNote, NoteText,
        NoteBorderColor, NoteTitleBackgroundColor, NoteTitleTextColor, NoteBackgroundColor, NoteTextColor, IsSnippet, SnippetText, TextFont,
        CommentFont, CommentFontSize, TextFontSize, NoteHighlights,
        TextCurrentLine, TextWholeWords, TextMatchCase, TextWrapText, CommentCurrentLine, CommentWholeWords, CommentMatchCase, CommentWrapText,
        SyncId, SortKey, ChangedAt, ChangedBy)
      VALUES (
        NULLIF(@ID, 0), @ParentShortcutID, @ParentWindowID, @Name, @Target, @Ext, @Guid, @IsGroup, @IsGroupExpanded, @Comment, @IsMarked,
        @MarkColor, @IsProcessCommand, @ProcessFileName, @ProcessArguments, @ProcessRunAsAdmin, COALESCE(@ProcessWorkingFolder, ''), @IsNote, COALESCE(@NoteText, ''),
        COALESCE(@NoteBorderColor, ''), COALESCE(@NoteTitleBackgroundColor, ''), COALESCE(@NoteTitleTextColor, ''), COALESCE(@NoteBackgroundColor, ''), COALESCE(@NoteTextColor, ''), @IsSnippet, COALESCE(@SnippetText, ''), COALESCE(@TextFont, 'Courier New'),
        COALESCE(@CommentFont, 'Courier New'), @CommentFontSize, @TextFontSize, COALESCE(@NoteHighlights, ''),
        @TextCurrentLine, @TextWholeWords, @TextMatchCase, @TextWrapText, @CommentCurrentLine, @CommentWholeWords, @CommentMatchCase, @CommentWrapText,
        @SyncId, @SortKey, @ChangedAt, COALESCE(@ChangedBy, ''))";

    private const string UpdateSql = @"
      UPDATE Shortcuts SET
        ParentShortcutID = @ParentShortcutID, ParentMDSHOWindowID = @ParentWindowID, Name = @Name, Target = @Target,
        Ext = @Ext, Guid = @Guid, IsGroup = @IsGroup, IsGroupExpanded = @IsGroupExpanded, Comment = @Comment, IsMarked = @IsMarked,
        MarkColor = @MarkColor, SortKey = CASE WHEN COALESCE(@SortKey, '') = '' THEN SortKey ELSE @SortKey END, IsProcessCommand = @IsProcessCommand, ProcessFileName = @ProcessFileName,
        ProcessArguments = @ProcessArguments, ProcessRunAsAdmin = @ProcessRunAsAdmin, IsNote = @IsNote, NoteText = COALESCE(@NoteText, ''),
        NoteBorderColor = COALESCE(@NoteBorderColor, ''), NoteTitleBackgroundColor = COALESCE(@NoteTitleBackgroundColor, ''), NoteTitleTextColor = COALESCE(@NoteTitleTextColor, ''), NoteBackgroundColor = COALESCE(@NoteBackgroundColor, ''), NoteTextColor = COALESCE(@NoteTextColor, ''),
        ProcessWorkingFolder = COALESCE(@ProcessWorkingFolder, ''), IsSnippet = @IsSnippet, SnippetText = COALESCE(@SnippetText, ''),
        TextFont = COALESCE(@TextFont, 'Courier New'), CommentFont = COALESCE(@CommentFont, 'Courier New'), CommentFontSize = @CommentFontSize,
        TextFontSize = @TextFontSize, NoteHighlights = COALESCE(@NoteHighlights, ''),
        TextCurrentLine = @TextCurrentLine, TextWholeWords = @TextWholeWords, TextMatchCase = @TextMatchCase, TextWrapText = @TextWrapText,
        CommentCurrentLine = @CommentCurrentLine, CommentWholeWords = @CommentWholeWords, CommentMatchCase = @CommentMatchCase, CommentWrapText = @CommentWrapText
      WHERE ID = @ID";

    #region QUERIES
    public Shortcut Get(long id) {
      return One<Shortcut>("SELECT " + SelectColumns + " FROM Shortcuts WHERE ID = @id", new { id });
    }

    public List<Shortcut> GetAll() {
      return Many<Shortcut>("SELECT " + SelectColumns + " FROM Shortcuts ORDER BY ID ASC");
    }

    public List<Shortcut> GetRoots(long parentWindowID) {
      return Many<Shortcut>("SELECT " + SelectColumns + " FROM Shortcuts WHERE ParentShortcutID IS NULL AND ParentMDSHOWindowID = @parentWindowID ORDER BY " + Order, new { parentWindowID });
    }

    public List<Shortcut> GetChildren(long parentShortcutID) {
      return Many<Shortcut>("SELECT " + SelectColumns + " FROM Shortcuts WHERE ParentShortcutID = @parentShortcutID ORDER BY " + Order, new { parentShortcutID });
    }

    // The highest valid sort key among the siblings under the given parent, or null when there is none.
    private static string LastKey(long? parentShortcutID, long? parentWindowID) {
      List<string> keys = parentShortcutID != null
        ? Many<string>("SELECT SortKey FROM Shortcuts WHERE ParentShortcutID = @parentShortcutID", new { parentShortcutID })
        : Many<string>("SELECT SortKey FROM Shortcuts WHERE ParentShortcutID IS NULL AND ParentMDSHOWindowID = @parentWindowID", new { parentWindowID });
      string last = null;
      foreach (string key in keys) {
        if (SortKeys.IsValid(key) && (last == null || string.CompareOrdinal(key, last) > 0)) {
          last = key;
        }
      }
      return last;
    }
    #endregion QUERIES

    #region CREATE
    public long Add(Shortcut shortcut) {
      return InTransaction(() => {
        if (string.IsNullOrEmpty(shortcut.SyncId)) {
          shortcut.SyncId = Guid.NewGuid().ToString();
        }
        if (!SortKeys.IsValid(shortcut.SortKey)) {
          shortcut.SortKey = SortKeys.Between(LastKey(shortcut.ParentShortcutID, shortcut.ParentWindowID), null);
        }
        shortcut.ID = Insert(InsertSql, shortcut);
        return shortcut.ID;
      });
    }

    /// <summary>Puts back a change record an import keeps (writing ChangedAt itself, so the triggers leave it).</summary>
    internal static void SetChangeRecord(long id, long changedAt, string changedBy) {
      Run("UPDATE Shortcuts SET ChangedAt = @changedAt, ChangedBy = @changedBy WHERE ID = @id", new { id, changedAt, changedBy = changedBy ?? "" });
    }

    public Shortcut Append(Shortcut template) {
      return InTransaction(() => {
        Shortcut copy = new Shortcut(
          template.ParentShortcutID, template.ParentWindowID, template.Name, template.Target, template.Ext, template.Guid,
          template.IsGroup, template.IsGroupExpanded, template.Comment, template.IsMarked, template.MarkColor,
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
        copy.TextFontSize = template.TextFontSize;
        copy.NoteHighlights = template.NoteHighlights ?? "";
        copy.TextCurrentLine = template.TextCurrentLine;
        copy.TextWholeWords = template.TextWholeWords;
        copy.TextMatchCase = template.TextMatchCase;
        copy.TextWrapText = template.TextWrapText;
        copy.CommentCurrentLine = template.CommentCurrentLine;
        copy.CommentWholeWords = template.CommentWholeWords;
        copy.CommentMatchCase = template.CommentMatchCase;
        copy.CommentWrapText = template.CommentWrapText;
        return Get(Add(copy));
      });
    }

    public Shortcut CreateGroup(long? parentShortcutID, long? parentWindowID) {
      return Append(new Shortcut(parentShortcutID, parentWindowID, "New group", "", "", Identifiers.NewGuid(),
        isGroup: 1, isGroupExpanded: 1, comment: "", isMarked: 0, markColor: "#FFFFFFFF",
        isProcessCommand: 0, processFileName: "", processArguments: ""));
    }

    public Shortcut Create(long? parentShortcutID, long? parentWindowID, string name, string target, string ext, string guid) {
      return Append(new Shortcut(parentShortcutID, parentWindowID, name, target, ext, guid,
        isGroup: 0, isGroupExpanded: 0, comment: "", isMarked: 0, markColor: "#FFFFFFFF",
        isProcessCommand: 0, processFileName: "", processArguments: ""));
    }

    public Shortcut CreateProcessCommand(long? parentShortcutID, long? parentWindowID) {
      return Append(new Shortcut(parentShortcutID, parentWindowID, "New command", "", "", Identifiers.NewGuid(),
        isGroup: 0, isGroupExpanded: 0, comment: "", isMarked: 0, markColor: "#FFFFFFFF",
        isProcessCommand: 1, processFileName: "", processArguments: ""));
    }

    public Shortcut CreateNote(long? parentShortcutID, long? parentWindowID) {
      return Append(new Shortcut(parentShortcutID, parentWindowID, "New note", "", "", Identifiers.NewGuid(),
        isGroup: 0, isGroupExpanded: 0, comment: "", isMarked: 0, markColor: "#FFFFFFFF",
        isProcessCommand: 0, processFileName: "", processArguments: "", isNote: 1, noteText: ""));
    }
    public Shortcut CreateSnippet(long? parentShortcutID, long? parentWindowID) {
      return Append(new Shortcut(parentShortcutID, parentWindowID, "New snippet", "", "", Identifiers.NewGuid(),
        isGroup: 0, isGroupExpanded: 0, comment: "", isMarked: 0, markColor: "#FFFFFFFF",
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

    public void UpdateTextFontSize(long id, long points) {
      Run("UPDATE Shortcuts SET TextFontSize = @points WHERE ID = @id;", new { id, points });
    }

    public void UpdateNoteHighlights(long id, string highlights) {
      Run("UPDATE Shortcuts SET NoteHighlights = @highlights WHERE ID = @id;", new { id, highlights = highlights ?? "" });
    }

    public void UpdateViewOptions(long id, bool comment, TextViewOptions options) {
      string prefix = comment ? "Comment" : "Text";
      Run("UPDATE Shortcuts SET " + prefix + "CurrentLine = @currentLine, " + prefix + "WholeWords = @wholeWords, "
        + prefix + "MatchCase = @matchCase, " + prefix + "WrapText = @wrapText WHERE ID = @id;",
        new { id, currentLine = options.CurrentLine, wholeWords = options.WholeWords, matchCase = options.MatchCase, wrapText = options.WrapText });
    }

    public void UpdatePositions(IEnumerable<long> orderedShortcutIDs) {
      List<long> ids = orderedShortcutIDs.ToList();
      if (ids.Count == 0) {
        return;
      }
      InTransaction(() => {
        Dictionary<long, string> current = new Dictionary<long, string>();
        foreach (Shortcut row in Many<Shortcut>("SELECT ID, SortKey FROM Shortcuts WHERE ID IN @ids", new { ids })) {
          current[row.ID] = row.SortKey;
        }
        string[] old = ids.Select(id => current.TryGetValue(id, out string key) ? key : null).ToArray();
        string[] keys = SortKeys.Reorder(old);
        for (int i = 0; i < ids.Count; i++) {
          if (keys[i] != old[i]) {
            Run("UPDATE Shortcuts SET SortKey = @key WHERE ID = @id;", new { id = ids[i], key = keys[i] });
          }
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
