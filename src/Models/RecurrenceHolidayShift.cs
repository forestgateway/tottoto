namespace todochart.Models;

/// <summary>繰り返し予定の各回が休日と重なった場合の挙動。</summary>
public enum RecurrenceHolidayShift
{
    /// <summary>そのまま（日付通り配置）。</summary>
    None,
    /// <summary>直前の営業日に前倒し。</summary>
    Before,
    /// <summary>直後の営業日に後ろ倒し。</summary>
    After,
}
