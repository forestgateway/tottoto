namespace todochart.Models;

/// <summary>
/// タスクの状態。元 Delphi 版の stsXxx 定数に対応。
/// </summary>
public enum ItemStatus
{
    None     = -1,  // 非表示・対象外
    Skip     = 0,   // 休日スキップ
    Complete = 1,   // 完了
    Wait     = 2,   // 待機（開始前）
    Progress = 3,   // 進行中
    Warning  = 4,   // 警告（もうすぐ期限）
    Error    = 5,   // エラー（期限超過）
    Over     = 6,   // 超過（期限切れ後もチャートに表示）
}
