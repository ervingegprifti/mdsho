using System.Collections.Generic;

namespace MDSHO.Core {

  /// <summary>The two kinds of text a button keeps a history of (History.Field).</summary>
  public static class HistoryFields {
    /// <summary>A note's or snippet's text.</summary>
    public const string Text = "Text";
    /// <summary>Any button's comment.</summary>
    public const string Comment = "Comment";

    /// <summary>True for "Text" and "Comment".</summary>
    public static bool IsKnown(string field) => field == Text || field == Comment;
  }

  /// <summary>One earlier copy of a text (a row of the History table).</summary>
  public sealed class HistoryEntry {
    /// <summary>The row ID (not exported).</summary>
    public long ID { get; set; }
    /// <summary>The button it belongs to.</summary>
    public long ShortcutID { get; set; }
    /// <summary><see cref="HistoryFields.Text"/> or <see cref="HistoryFields.Comment"/>.</summary>
    public string Field { get; set; } = HistoryFields.Text;
    /// <summary>The text as it was.</summary>
    public string Text { get; set; } = "";
    /// <summary>When it was kept: local time, "yyyy-MM-ddTHH:mm:ss".</summary>
    public string KeptAt { get; set; } = "";
    /// <summary>The PC a synced text came from when it lost a clash; "" for this PC's own copies.</summary>
    public string FromPc { get; set; } = "";
  }

  /// <summary>The History table: earlier copies of each button's text and comment.</summary>
  public interface IHistoryRepository {
    /// <summary>The copies of one field of one button, newest first.</summary>
    List<HistoryEntry> Get(long shortcutId, string field);
    /// <summary>Every row, oldest first (for exports).</summary>
    List<HistoryEntry> GetAll();
    /// <summary>Adds the row and sets its ID.</summary>
    void Add(HistoryEntry entry);
    /// <summary>Deletes one row.</summary>
    void Delete(long id);
    /// <summary>Deletes every copy of one field of one button.</summary>
    void Clear(long shortcutId, string field);
    /// <summary>Keeps only the newest <paramref name="keep"/> copies of one field of one button.</summary>
    void Trim(long shortcutId, string field, int keep);
    /// <summary>Keeps only the newest <paramref name="keep"/> copies of every field of every button.</summary>
    void TrimAll(int keep);
  }
}
