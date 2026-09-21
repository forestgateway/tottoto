using todochart.Services;

namespace todochart.Models;

/// <summary>繰り返し予定の設定。</summary>
public class RecurrenceRule
{
    /// <summary>頻度（毎日 / 毎週 / 毎月）。</summary>
    public RecurrenceFrequency Frequency { get; set; } = RecurrenceFrequency.Weekly;

    /// <summary>間隔（〇日ごと / 〇週ごと / 〇ヶ月ごと）。1 以上。</summary>
    public int Interval { get; set; } = 1;

    /// <summary>毎週の場合の対象曜日一覧。</summary>
    public List<DayOfWeek> DaysOfWeek { get; } = new();

    /// <summary>毎月の場合の日付指定（1〜31）。末日指定時は無視される。</summary>
    public int? MonthDay { get; set; }

    /// <summary>毎月の場合、月末日を対象とするかどうか。</summary>
    public bool UseLastDayOfMonth { get; set; } = false;

    /// <summary>休日と重なった場合の挙動。</summary>
    public RecurrenceHolidayShift HolidayShift { get; set; } = RecurrenceHolidayShift.None;

    public RecurrenceRule CloneShallow()
    {
        var clone = new RecurrenceRule
        {
            Frequency         = Frequency,
            Interval          = Interval,
            MonthDay          = MonthDay,
            UseLastDayOfMonth = UseLastDayOfMonth,
            HolidayShift      = HolidayShift,
        };
        clone.DaysOfWeek.AddRange(DaysOfWeek);
        return clone;
    }

    /// <summary>
    /// 起点日（anchor）を基準に、繰り返し条件に合致する発生日を範囲内で列挙する（休日シフト適用前）。
    /// </summary>
    private IEnumerable<DateTime> EnumerateRawOccurrences(DateTime anchor, DateTime rangeStart, DateTime rangeEnd)
    {
        int interval = Math.Max(1, Interval);

        switch (Frequency)
        {
            case RecurrenceFrequency.Daily:
                for (var d = anchor.Date; d <= rangeEnd.Date; d = d.AddDays(interval))
                {
                    if (d >= rangeStart.Date)
                        yield return d;
                }
                break;

            case RecurrenceFrequency.Weekly:
            {
                var days = DaysOfWeek.Count > 0 ? DaysOfWeek : new List<DayOfWeek> { anchor.DayOfWeek };
                var weekStart = anchor.Date.AddDays(-(int)anchor.DayOfWeek);
                for (var week = weekStart; week <= rangeEnd.Date; week = week.AddDays(7 * interval))
                {
                    foreach (var day in days)
                    {
                        var d = week.AddDays((int)day);
                        if (d >= anchor.Date && d >= rangeStart.Date && d <= rangeEnd.Date)
                            yield return d;
                    }
                }
                break;
            }

            case RecurrenceFrequency.Monthly:
            {
                var month = new DateTime(anchor.Year, anchor.Month, 1);
                while (month <= rangeEnd.Date)
                {
                    int day = UseLastDayOfMonth
                        ? DateTime.DaysInMonth(month.Year, month.Month)
                        : Math.Min(MonthDay ?? anchor.Day, DateTime.DaysInMonth(month.Year, month.Month));
                    var d = new DateTime(month.Year, month.Month, day);
                    if (d >= anchor.Date && d >= rangeStart.Date && d <= rangeEnd.Date)
                        yield return d;
                    month = month.AddMonths(interval);
                }
                break;
            }
        }
    }

    private DateTime ApplyHolidayShift(DateTime date, HolidayService holidays)
    {
        if (HolidayShift == RecurrenceHolidayShift.None) return date;
        var d = date;
        while (holidays.GetLevel(d) > 0)
            d = HolidayShift == RecurrenceHolidayShift.Before ? d.AddDays(-1) : d.AddDays(1);
        return d;
    }

    /// <summary>
    /// 起点日（anchor）以降で、指定日（from）以降最初に到来する予定日（休日シフト適用後）を求める。
    /// </summary>
    public DateTime? GetNextOccurrence(DateTime anchor, DateTime from, HolidayService holidays, DateTime? limit = null)
    {
        var rangeEnd = limit ?? from.AddYears(2);
        foreach (var raw in EnumerateRawOccurrences(anchor, anchor, rangeEnd))
        {
            var shifted = ApplyHolidayShift(raw, holidays);
            if (shifted.Date >= from.Date)
                return shifted;
        }
        return null;
    }

    /// <summary>
    /// 起点日（anchor）以降で、指定日（from）以前の直近の予定日（休日シフト適用後）を求める。
    /// </summary>
    public DateTime? GetPreviousOccurrence(DateTime anchor, DateTime from, HolidayService holidays)
    {
        DateTime? result = null;
        foreach (var raw in EnumerateRawOccurrences(anchor, anchor, from))
        {
            var shifted = ApplyHolidayShift(raw, holidays);
            if (shifted.Date <= from.Date)
                result = shifted;
        }
        return result;
    }

    /// <summary>
    /// 指定範囲内 [rangeStart, rangeEnd] に含まれる全ての予定日（休日シフト適用後）を列挙する。
    /// </summary>
    public IEnumerable<DateTime> GetOccurrencesInRange(DateTime anchor, DateTime rangeStart, DateTime rangeEnd, HolidayService holidays)
    {
        foreach (var raw in EnumerateRawOccurrences(anchor, rangeStart.AddYears(-1), rangeEnd))
        {
            var shifted = ApplyHolidayShift(raw, holidays);
            if (shifted.Date >= rangeStart.Date && shifted.Date <= rangeEnd.Date)
                yield return shifted;
        }
    }
}
