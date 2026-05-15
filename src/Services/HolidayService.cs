using System.Net.Http;
using System.Text;
using todochart.Models;

namespace todochart.Services;

/// <summary>
/// 休日・祝日の管理と日付レベルの計算。
/// レベル 0 = 平日, 1 = 半休日（土曜等）, 2 = 全休日（日曜・祝日）
/// </summary>
public class HolidayService
{
    static HolidayService()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }
    // インデックス: 0=日, 1=月, 2=火, 3=水, 4=木, 5=金, 6=土
    private readonly int[] _weekdayLevels = new int[7];

    // 個別指定の祝日: key=DateOnly, value=level
    private readonly Dictionary<DateOnly, int> _specials = new();
    // 個別指定の祝日名: key=DateOnly, value=name
    private readonly Dictionary<DateOnly, string> _specialNames = new();

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

    public void SetSpecialHoliday(DateOnly date, int level, string? name = null)
    {
        if (level < 0)
        {
            _specials.Remove(date);
            _specialNames.Remove(date);
            return;
        }

        _specials[date] = level;
        _specialNames[date] = string.IsNullOrWhiteSpace(name) ? "休日" : name.Trim();
    }

    public void RemoveSpecialHoliday(DateOnly date)
    {
        _specials.Remove(date);
        _specialNames.Remove(date);
    }

    public IReadOnlyDictionary<DateOnly, int> SpecialHolidays => _specials;

    public string GetSpecialHolidayName(DateOnly date)
        => _specialNames.TryGetValue(date, out var name) && !string.IsNullOrWhiteSpace(name)
            ? name
            : "休日";

    public List<HolidayData> GetSpecialHolidayData()
    {
        return _specials
            .Select(kvp => new HolidayData(kvp.Key, GetSpecialHolidayName(kvp.Key), kvp.Value))
            .OrderBy(x => x.Date)
            .ToList();
    }

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
        {
            var name = GetSpecialHolidayName(date).Replace("\t", " ");
            list.Add($"{date:yyyyMMdd}\t{level}\t{name}");
        }
        list.Sort();
        return list;
    }

    public void ImportSpecialHolidays(IEnumerable<string> lines)
    {
        _specials.Clear();
        _specialNames.Clear();
        foreach (var line in lines)
        {
            if (line.StartsWith('#') || string.IsNullOrWhiteSpace(line)) continue;
            var parts = line.Split('\t');
            if (parts.Length >= 2
                && DateOnly.TryParseExact(parts[0], "yyyyMMdd", null,
                       System.Globalization.DateTimeStyles.None, out var date)
                && int.TryParse(parts[1], out int level))
            {
                if (level < 0) continue;
                _specials[date] = level;
                _specialNames[date] = parts.Length >= 3 && !string.IsNullOrWhiteSpace(parts[2])
                    ? parts[2].Trim()
                    : "休日";
            }
        }
    }

    // ── 内閣府祝日データ取得 ──────────────────────────────────────
    /// <summary>
    /// 内閣府のサイトから祝日 CSV を取得し、HolidayData のリストとして返す。
    /// URL: https://www8.cao.go.jp/chosei/shukujitsu/syukujitsu.csv
    /// </summary>
    public async Task<List<HolidayData>> FetchJapanHolidaysAsync()
    {
        const string url = "https://www8.cao.go.jp/chosei/shukujitsu/syukujitsu.csv";
        var result = new List<HolidayData>();

        try
        {
            using var client = new HttpClient();
            client.DefaultRequestHeaders.UserAgent.ParseAdd("tottoto/1.0");

            var bytes = await client.GetByteArrayAsync(url);
            // 内閣府の CSV は Shift_JIS のため、UTF-8 に変換してから解析する
            var shiftJis = Encoding.GetEncoding(932);
            var utf8Bytes = Encoding.Convert(shiftJis, Encoding.UTF8, bytes);
            var text = Encoding.UTF8.GetString(utf8Bytes);
            var lines = text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

            foreach (var line in lines.Skip(1)) // ヘッダー行をスキップ
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                var parts = line.Split(',');
                if (parts.Length >= 2
                    && DateOnly.TryParseExact(parts[0], "yyyy/M/d", null,
                           System.Globalization.DateTimeStyles.None, out var date))
                {
                    var name = parts[1].Trim();
                    result.Add(new HolidayData(date, name, 2)); // レベル2（全休日）
                }
            }
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"祝日データの取得に失敗しました: {ex.Message}", ex);
        }

        return result;
    }

    /// <summary>
    /// HolidayData のリストを _specials 辞書にマージする。
    /// </summary>
    public void MergeHolidays(IEnumerable<HolidayData> holidays)
    {
        foreach (var h in holidays)
        {
            SetSpecialHoliday(h.Date, h.Level, h.Name);
        }
    }
}
