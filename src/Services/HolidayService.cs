namespace todochart.Services;

/// <summary>
/// 休日・祝日の管理と日付レベルの計算。
/// レベル 0 = 平日, 1 = 半休日（土曜等）, 2 = 全休日（日曜・祝日）
/// </summary>
public class HolidayService
{
    // インデックス: 0=日, 1=月, 2=火, 3=水, 4=木, 5=金, 6=土
    private readonly int[] _weekdayLevels = new int[7];

    // 個別指定の祝日: key=DateOnly, value=level
    private readonly Dictionary<DateOnly, int> _specials = new();

    /// <summary>警告を出す残日数しきい値（AlertCount+1日以内で Warning 状態）。</summary>
    public int AlertCount     { get; set; } = 3;

    /// <summary>アプリ全体の日数カウントレベルデフォルト値。</summary>
    public int DateCountLevel { get; set; } = 0;

    public HolidayService()
    {
        // デフォルト: 日曜=2, 土曜=1
        _weekdayLevels[(int)DayOfWeek.Sunday]   = 2;
        _weekdayLevels[(int)DayOfWeek.Saturday]  = 1;
    }

    // ── 設定 ─────────────────────────────────────────────
    public int  GetWeekdayLevel(DayOfWeek dow)           => _weekdayLevels[(int)dow];
    public void SetWeekdayLevel(DayOfWeek dow, int level) => _weekdayLevels[(int)dow] = level;

    public int[]   GetWeekdayLevels()             => (int[])_weekdayLevels.Clone();
    public void    SetWeekdayLevels(int[] levels) =>
        Array.Copy(levels, _weekdayLevels, Math.Min(7, levels.Length));

    public void SetSpecialHoliday(DateOnly date, int level)
    {
        if (level <= 0) _specials.Remove(date);
        else            _specials[date] = level;
    }

    public IReadOnlyDictionary<DateOnly, int> SpecialHolidays => _specials;

    // ── 照会 ─────────────────────────────────────────────
    public int GetLevel(DateTime date)
    {
        var key = DateOnly.FromDateTime(date.Date);
        if (_specials.TryGetValue(key, out int lv)) return lv;
        return _weekdayLevels[(int)date.DayOfWeek];
    }

    public bool IsHoliday(DateTime date, int threshold = 1) =>
        GetLevel(date) >= threshold;

    /// <summary>
    /// from〜to の実働日数を数える（requiredLevel 以下の日のみカウント）。
    /// </summary>
    public int CountWorkingDays(DateTime from, DateTime to, int requiredLevel = 0)
    {
        if (from.Date > to.Date) return 0;
        int count = 0;
        for (var d = from.Date; d <= to.Date; d = d.AddDays(1))
            if (GetLevel(d) <= requiredLevel) count++;
        return count;
    }

    // ── シリアライズ ──────────────────────────────────────
    public List<string> ExportSpecialHolidays()
    {
        var list = new List<string>();
        foreach (var (date, level) in _specials)
            list.Add($"{date:yyyyMMdd}\t{level}");
        list.Sort();
        return list;
    }

    public void ImportSpecialHolidays(IEnumerable<string> lines)
    {
        _specials.Clear();
        foreach (var line in lines)
        {
            if (line.StartsWith('#') || string.IsNullOrWhiteSpace(line)) continue;
            var parts = line.Split('\t');
            if (parts.Length >= 2
                && DateOnly.TryParseExact(parts[0], "yyyyMMdd", null,
                       System.Globalization.DateTimeStyles.None, out var date)
                && int.TryParse(parts[1], out int level))
            {
                _specials[date] = level;
            }
        }
    }
}
