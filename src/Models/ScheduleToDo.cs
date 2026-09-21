using todochart.Services;

namespace todochart.Models;

/// <summary>葉ノード（ToDo タスク）。</summary>
public class ScheduleToDo : ScheduleItemBase
{
    public override bool IsFolder => false;
    public bool Completed { get; set; }

    /// <summary>進捗率（0〜100、10%単位）。</summary>
    public int Progress { get; set; } = 0;

    /// <summary>このタスクに紐づく吹き出しのリスト。</summary>
    public List<Callout> Callouts { get; } = new();

    /// <summary>繰り返し予定の設定。null の場合は繰り返しなし。</summary>
    public RecurrenceRule? Recurrence { get; set; }

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

        if (Recurrence is not null)
        {
            ComputeRecurrenceStatus(today, alertCount, holidays);
            return;
        }

        ComputeStatusFromDates(today, alertCount, holidays);
    }

    /// <summary>
    /// 繰り返し予定の場合のステータス計算。
    /// 予定日（次回発生日）を基準に、当日なら残1日、当日を過ぎれば次の予定日までのカウントとする。
    /// </summary>
    private void ComputeRecurrenceStatus(DateTime today, int alertCount, HolidayService holidays)
    {
        var anchor = BeginDate ?? today;

        if (!today.Date.Equals(default) && anchor.Date > today.Date)
        {
            // 起点日がまだ来ていない場合は待機
            Status = ItemStatus.Wait;
            return;
        }

        var next = Recurrence!.GetNextOccurrence(anchor, today, holidays, EndDate);
        if (next is null)
        {
            // 終了日を過ぎて次回発生日がない場合は完了扱いの範囲外 → エラー（期限超過）とする
            Status = ItemStatus.Error;
            return;
        }

        int daysLeft = CountWorkingDays(today, next.Value, holidays);
        if (daysLeft < 1)
            daysLeft = 1; // 予定が当日の場合は残1日として扱う

        if (daysLeft <= alertCount + 1)
        {
            Status = ItemStatus.Warning;
        }
        else
        {
            Status = ItemStatus.Progress;
        }
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
            Progress       = Progress,
            MarkLevel      = MarkLevel,
            IsWait         = IsWait,
            Recurrence     = Recurrence?.CloneShallow(),
        };
        foreach (var c in Callouts)
            clone.Callouts.Add(c);
        return clone;
    }
}
