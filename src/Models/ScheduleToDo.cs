using todochart.Services;

namespace todochart.Models;

/// <summary>葉ノード（ToDo タスク）。</summary>
public class ScheduleToDo : ScheduleItemBase
{
    public override bool IsFolder => false;
    public bool Completed { get; set; }

    /// <summary>このタスクに紐づく吹き出しのリスト。</summary>
    public List<Callout> Callouts { get; } = new();

    public override void UpdateStatus(DateTime today, int alertCount, HolidayService holidays)
    {
        IsEmpty = false;

        if (Completed)
        {
            Status = ItemStatus.Complete;
            return;
        }

        if (IsWait)
        {
            Status = ItemStatus.Wait;
            return;
        }

        ComputeStatusFromDates(today, alertCount, holidays);
    }

    public override ScheduleItemBase CloneShallow()
    {
        var clone = new ScheduleToDo
        {
            Name           = Name,
            BeginDate      = BeginDate,
            EndDate        = EndDate,
            Memo           = Memo,
            Link           = Link,
            DateCountLevel = DateCountLevel,
            Completed      = Completed,
            MarkLevel      = MarkLevel,
            IsWait         = IsWait,
        };
        foreach (var c in Callouts)
            clone.Callouts.Add(c);
        return clone;
    }
}
