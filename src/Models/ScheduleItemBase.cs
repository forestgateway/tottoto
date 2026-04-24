using todochart.Services;

namespace todochart.Models;

/// <summary>
/// スケジュールアイテムの基底クラス。ToDo とフォルダ共通ロジック。
/// </summary>
public abstract class ScheduleItemBase
{
        // ユーザーが付けるマーク（0=なし,1=黄色,2=黒）
        public int MarkLevel { get; set; } = 0;
    // WAIT フラグ: ユーザーが明示的に待機状態にするためのフラグ
    public bool IsWait { get; set; } = false;

    public string      Name           { get; set; } = string.Empty;
    public DateTime?   BeginDate      { get; set; }
    public DateTime?   EndDate        { get; set; }
    public string      Memo           { get; set; } = string.Empty;
    public string      Link           { get; set; } = string.Empty;
    public int         DateCountLevel { get; set; } = 0;   // 0=全日, 1=土日スキップ, 2=休日スキップ

    public ItemStatus  Status  { get; protected set; } = ItemStatus.Wait;
    public bool        IsEmpty { get; protected set; } = false;

    public abstract bool IsFolder { get; }

    public List<ScheduleItemBase> Children { get; } = new();
    public ScheduleItemBase?      Parent   { get; set; }

    /// <summary>
    /// ステータスを今日の日付・アラートカウント・休日設定から再計算する。
    /// </summary>
    public abstract void UpdateStatus(DateTime today, int alertCount, HolidayService holidays);

    /// <summary>自身のデータを浅くコピーする（Children は含まない）。</summary>
    public abstract ScheduleItemBase CloneShallow();

    /// <summary>開始〜終了の実働日数を数える（DateCountLevel を参照）。</summary>
    public int CountWorkingDays(DateTime from, DateTime to, HolidayService holidays)
    {
        if (from.Date > to.Date) return 0;
        int count = 0;
        for (var d = from.Date; d <= to.Date; d = d.AddDays(1))
        {
            if (holidays.GetLevel(d) <= DateCountLevel)
                count++;
        }
        return count;
    }

    /// <summary>期限状態を日付から計算する（ToDo / フォルダ共通）。</summary>
    protected void ComputeStatusFromDates(DateTime today, int alertCount, HolidayService holidays)
    {
        if (!EndDate.HasValue)
        {
            Status = (!BeginDate.HasValue || today.Date >= BeginDate.Value.Date)
                     ? ItemStatus.Progress
                     : ItemStatus.Wait;
            return;
        }

        // 残日数: 今日が開始前なら開始〜終了, それ以外は今日〜終了
        DateTime from = (BeginDate.HasValue && today.Date < BeginDate.Value.Date)
                        ? BeginDate.Value
                        : today;
        int daysLeft = CountWorkingDays(from, EndDate.Value, holidays);

        if (daysLeft < 1)
        {
            Status = ItemStatus.Error;
        }
        else if (daysLeft            <= alertCount + 1
              && CountWorkingDays(today, EndDate.Value, holidays) <= alertCount + 1)
        {
            Status = ItemStatus.Warning;
        }
        else
        {
            Status = (!BeginDate.HasValue || today.Date >= BeginDate.Value.Date)
                     ? ItemStatus.Progress
                     : ItemStatus.Wait;
        }
    }
}
