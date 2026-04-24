namespace todochart.Models;

/// <summary>
/// アーカイブファイルに保存される 1 タスク分のデータ。
/// ツリー構造は持たず、元のパス文字列で復元先を特定する。
/// </summary>
public class ArchivedItem
{
    /// <summary>タスク名。</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>元のツリーパス（例: "Root/ProjectA/Subtask1"）。</summary>
    public string Path { get; set; } = string.Empty;

    /// <summary>メモ。</summary>
    public string Memo { get; set; } = string.Empty;

    /// <summary>リンク。</summary>
    public string Link { get; set; } = string.Empty;

    /// <summary>開始日。</summary>
    public DateTime? BeginDate { get; set; }

    /// <summary>終了日。</summary>
    public DateTime? EndDate { get; set; }

    /// <summary>日数カウントレベル。</summary>
    public int DateCountLevel { get; set; }

    /// <summary>完了済みか。</summary>
    public bool Completed { get; set; }

    /// <summary>WAIT フラグ。</summary>
    public bool IsWait { get; set; }

    /// <summary>マークレベル。</summary>
    public int MarkLevel { get; set; }

    /// <summary>アーカイブ実行日時。</summary>
    public DateTime ArchivedAt { get; set; }
}
