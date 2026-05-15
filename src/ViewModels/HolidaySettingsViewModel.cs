using System.Collections.ObjectModel;
using System.Windows.Input;
using todochart.Models;
using todochart.Services;

namespace todochart.ViewModels;

/// <summary>
/// 休日設定ウィンドウの ViewModel。
/// 曜日レベル設定、個別休日リスト、祝日取得機能を提供。
/// </summary>
public class HolidaySettingsViewModel : ViewModelBase
{
    private readonly HolidayService _holidayService;

    // ── 曜日レベル（0=日, 1=月, ..., 6=土） ──────────────────────
    private int _sundayLevel;
    private int _mondayLevel;
    private int _tuesdayLevel;
    private int _wednesdayLevel;
    private int _thursdayLevel;
    private int _fridayLevel;
    private int _saturdayLevel;

    public int SundayLevel    { get => _sundayLevel;    set => SetField(ref _sundayLevel, value); }
    public int MondayLevel    { get => _mondayLevel;    set => SetField(ref _mondayLevel, value); }
    public int TuesdayLevel   { get => _tuesdayLevel;   set => SetField(ref _tuesdayLevel, value); }
    public int WednesdayLevel { get => _wednesdayLevel; set => SetField(ref _wednesdayLevel, value); }
    public int ThursdayLevel  { get => _thursdayLevel;  set => SetField(ref _thursdayLevel, value); }
    public int FridayLevel    { get => _fridayLevel;    set => SetField(ref _fridayLevel, value); }
    public int SaturdayLevel  { get => _saturdayLevel;  set => SetField(ref _saturdayLevel, value); }

    // ── 個別休日リスト ────────────────────────────────────────
    public ObservableCollection<HolidayData> SpecialHolidays { get; } = new();

    private HolidayData? _selectedHoliday;
    public HolidayData? SelectedHoliday
    {
        get => _selectedHoliday;
        set => SetField(ref _selectedHoliday, value);
    }

    // ── 新規追加用フィールド ──────────────────────────────────
    private DateOnly _newDate = DateOnly.FromDateTime(DateTime.Today);
    private string _newName = "休日";
    private int _newLevel = 2;

    public DateOnly NewDate  { get => _newDate;  set => SetField(ref _newDate, value); }
    public string NewName    { get => _newName;  set => SetField(ref _newName, value); }
    public int NewLevel      { get => _newLevel; set => SetField(ref _newLevel, value); }

    // ── ステータスメッセージ ──────────────────────────────────
    private string _statusMessage = string.Empty;
    public string StatusMessage
    {
        get => _statusMessage;
        set => SetField(ref _statusMessage, value);
    }

    // ── 取得済み祝日データ（年選択ダイアログ用） ─────────────────────
    private List<HolidayData> _fetchedJapanHolidays = new();

    // ── コマンド ──────────────────────────────────────────────
    public ICommand AddHolidayCommand { get; }
    public ICommand RemoveHolidayCommand { get; }

    public HolidaySettingsViewModel(HolidayService holidayService)
    {
        _holidayService = holidayService;

        // 既存の設定をロード
        var levels = holidayService.GetWeekdayLevels();
        _sundayLevel    = levels[0];
        _mondayLevel    = levels[1];
        _tuesdayLevel   = levels[2];
        _wednesdayLevel = levels[3];
        _thursdayLevel  = levels[4];
        _fridayLevel    = levels[5];
        _saturdayLevel  = levels[6];

        // 個別休日をロード
        foreach (var holiday in holidayService.GetSpecialHolidayData())
        {
            SpecialHolidays.Add(holiday);
        }

        // コマンド初期化
        AddHolidayCommand = new RelayCommand(AddHoliday, CanAddHoliday);
        RemoveHolidayCommand = new RelayCommand(RemoveHoliday, () => SelectedHoliday is not null);
    }

    public async Task<IReadOnlyList<int>> FetchJapanHolidayYearsAsync()
    {
        StatusMessage = "祝日データを取得中...";

        var holidays = await _holidayService.FetchJapanHolidaysAsync();
        _fetchedJapanHolidays = holidays
            .OrderBy(h => h.Date)
            .ToList();

        var years = _fetchedJapanHolidays
            .Select(h => h.Date.Year)
            .Distinct()
            .OrderBy(y => y)
            .ToList();

        StatusMessage = years.Count == 0
            ? "祝日データが見つかりませんでした。"
            : $"{_fetchedJapanHolidays.Count} 件の祝日データを取得しました。反映する年を選択してください。";

        return years;
    }

    public int ApplyFetchedJapanHolidaysByYears(IEnumerable<int> years)
    {
        var selectedYears = years.Distinct().ToHashSet();
        if (selectedYears.Count == 0) return 0;

        int added = 0;
        foreach (var h in _fetchedJapanHolidays.Where(x => selectedYears.Contains(x.Date.Year)))
        {
            if (SpecialHolidays.Any(x => x.Date == h.Date)) continue;
            SpecialHolidays.Add(h);
            added++;
        }

        StatusMessage = $"{added} 件の休日を反映しました。";
        return added;
    }

    private bool CanAddHoliday()
    {
        return !string.IsNullOrWhiteSpace(NewName);
    }

    private void AddHoliday()
    {
        var existing = SpecialHolidays.FirstOrDefault(x => x.Date == NewDate);
        if (existing is not null)
        {
            var index = SpecialHolidays.IndexOf(existing);
            SpecialHolidays[index] = new HolidayData(NewDate, NewName, NewLevel);
            StatusMessage = "同じ日付の休日を上書きしました。";
            return;
        }

        SpecialHolidays.Add(new HolidayData(NewDate, NewName, NewLevel));
        StatusMessage = "休日を追加しました。";
    }

    private void RemoveHoliday()
    {
        if (SelectedHoliday is not null)
        {
            SpecialHolidays.Remove(SelectedHoliday);
            StatusMessage = "休日を削除しました。";
        }
    }

    /// <summary>
    /// 設定を HolidayService に適用する。
    /// </summary>
    public void ApplyToService()
    {
        // 曜日レベルを適用
        var levels = new[]
        {
            SundayLevel, MondayLevel, TuesdayLevel, WednesdayLevel,
            ThursdayLevel, FridayLevel, SaturdayLevel
        };
        _holidayService.SetWeekdayLevels(levels);

        // 個別休日をクリアして再登録
        var specials = _holidayService.SpecialHolidays.Keys.ToList();
        foreach (var key in specials)
        {
            _holidayService.RemoveSpecialHoliday(key); // クリア
        }

        foreach (var h in SpecialHolidays)
        {
            _holidayService.SetSpecialHoliday(h.Date, h.Level, h.Name);
        }
    }
}
