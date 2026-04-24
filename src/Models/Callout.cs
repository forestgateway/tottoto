namespace todochart.Models;

/// <summary>タスクに紐づく吹き出しエンティティ。</summary>
public class Callout
{
    public string              Id             { get; set; } = Guid.NewGuid().ToString();
    public string              Text           { get; set; } = string.Empty;
    public CalloutPositionMode PositionMode   { get; set; } = CalloutPositionMode.EndDate;
    public DateTime?           AbsoluteDateTime { get; set; }
    public int                 OffsetDays     { get; set; } = 0;
    public CalloutStyle        Style          { get; set; } = CalloutStyle.BubbleLine;
    public CalloutVisibilityMode VisibilityMode { get; set; } = CalloutVisibilityMode.AlwaysVisible;
    public DateTime            CreatedAt      { get; set; } = DateTime.Now;
    public DateTime            UpdatedAt      { get; set; } = DateTime.Now;

    /// <summary>
    /// 表示すべき日付を計算する。
    /// <paramref name="task"/> の開始日・終了日が基準になる場合はそれを使用する。
    /// </summary>
    public DateTime? ComputeAnchorDate(ScheduleItemBase task)
    {
        return PositionMode switch
        {
            CalloutPositionMode.AbsoluteDateTime => AbsoluteDateTime,
            CalloutPositionMode.StartDate        => task.BeginDate?.AddDays(OffsetDays),
            CalloutPositionMode.EndDate          => task.EndDate?.AddDays(OffsetDays),
            _                                    => null,
        };
    }
}
