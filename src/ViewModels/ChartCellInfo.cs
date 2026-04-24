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

    /// <summary>タスク期間内のセルに描画する小正方形の色。null = 描画しない。</summary>
    public Brush? BarBrush   { get; init; }
}
