using System.Windows.Media;
using todochart.Models;

namespace todochart.ViewModels;

/// <summary>ガントチャートの 1 日分のセル情報（表示専用）。</summary>
public sealed class ChartCellInfo
{
    public DateTime   Date       { get; init; }
    public ItemStatus Status     { get; init; }
    public bool       IsToday    { get; init; }
    public int        HolidayLv  { get; init; }   // 0=平日, 1=半休, 2=全休

    public Brush  Background { get; init; } = Brushes.White;
    public Brush  Foreground { get; init; } = Brushes.Black;
    public string Symbol     { get; init; } = string.Empty;
    // Row base brush (striped background). OverlayBrush is used for weekend/holiday shading
    // which should be drawn on top of the row base to avoid being covered by other layers.
    public Brush RowBase { get; init; } = Brushes.Transparent;
    public Brush? OverlayBrush { get; init; }

    /// <summary>タスク期間内のセルに描画する小正方形の色。null = 描画しない。</summary>
    public Brush? BarBrush   { get; init; }
    public bool IsTaskStart { get; init; }
    public bool IsTaskEnd   { get; init; }

    // Note: Background/Foreground は生成側（TaskRowViewModel）で明示設定されるため
    // デフォルトは白/黒にしておく。
}
