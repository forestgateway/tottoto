using todochart.Services;

namespace todochart.Models;

/// <summary>フォルダノード。子アイテムの日付・状態を集約する。</summary>
public class ScheduleFolder : ScheduleItemBase
{
    public override bool IsFolder => true;
    public bool IsExpanded { get; set; } = true;

    public override void UpdateStatus(DateTime today, int alertCount, HolidayService holidays)
    {
        // 先に子を更新
        foreach (var child in Children)
            child.UpdateStatus(today, alertCount, holidays);

        if (Children.Count == 0)
        {
            IsEmpty = true;
            Status  = ItemStatus.Wait;
            return;
        }

        bool anyNonEmpty           = false;
        bool allComplete          = true;
        bool anyError             = false;
        int  nonCompleteCount     = 0;   // 完了以外の子の数
        int  nonCompleteWaitCount = 0;   // 完了以外の子のうち WAIT の数

        // -1 相当の初期値: null で「未初期化」を表現
        bool   beginIsNull = false;
        bool   beginInit   = false;
        DateTime? tempBegin = null;

        bool   endIsNull = false;
        long   endRaw    = -1;   // Delphi TDate 相当の整数比較用

        foreach (var child in Children)
        {
            if (child.IsEmpty) continue;
            anyNonEmpty = true;

            // 完了チェック
            if (child is ScheduleToDo td && !td.Completed)
                allComplete = false;
            else if (child is ScheduleFolder sf && sf.Status != ItemStatus.Complete)
                allComplete = false;

            // Error / WAIT カウント（完了以外の子のみ対象）
            if (child.Status != ItemStatus.Complete)
            {
                nonCompleteCount++;
                if (child.Status == ItemStatus.Error || child.Status == ItemStatus.Over)
                    anyError = true;
                if (child.Status == ItemStatus.Wait)
                    nonCompleteWaitCount++;
            }

            // BeginDate 集計: null = "開始日なし"
            if (!child.BeginDate.HasValue)
            {
                beginIsNull = true;
            }
            else if (!beginIsNull)
            {
                if (!beginInit || child.BeginDate.Value < tempBegin!.Value)
                {
                    tempBegin  = child.BeginDate.Value.Date;
                    beginInit  = true;
                }
            }

            // EndDate 集計: null = "終了日なし"、正数 = 日付（大きい方が優先）
            if (!child.EndDate.HasValue)
            {
                endIsNull = true;
            }
            else
            {
                long raw = child.EndDate.Value.Date.Ticks;
                if (endRaw < raw) endRaw = raw;   // 初期 -1 も含めて最大値を更新
            }
        }

        IsEmpty = !anyNonEmpty;

        if (IsEmpty)
        {
            Status = ItemStatus.Wait;
            return;
        }

        BeginDate = beginIsNull ? null : (beginInit ? tempBegin : null);
        EndDate   = endIsNull   ? null : (endRaw >= 0 ? new DateTime(endRaw) : null);

        if (allComplete)
        {
            Status = ItemStatus.Complete;
            return;
        }

        if (anyError)
        {
            Status = ItemStatus.Error;
            return;
        }

        // 完了以外の子が全て WAIT であればフォルダも WAIT
        if (nonCompleteCount > 0 && nonCompleteCount == nonCompleteWaitCount)
        {
            Status = ItemStatus.Wait;
            return;
        }

        ComputeStatusFromDates(today, alertCount, holidays);
    }

    public override ScheduleItemBase CloneShallow() => new ScheduleFolder
    {
        Name       = Name,
        Memo       = Memo,
        Link       = Link,
        IsExpanded = IsExpanded,
        MarkLevel  = MarkLevel,
        IsWait     = IsWait,
    };
}
