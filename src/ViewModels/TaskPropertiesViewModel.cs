using todochart.Models;
using todochart.Services;

namespace todochart.ViewModels;

/// <summary>タスクプロパティダイアログの ViewModel。</summary>
public class TaskPropertiesViewModel : ViewModelBase
{
    private readonly ScheduleItemBase _item;
    private readonly MainViewModel    _main;

    public TaskPropertiesViewModel(ScheduleItemBase item, MainViewModel main)
    {
        _item = item;
        _main = main;

        // 初期値
        Name           = item.Name;
        Memo           = item.Memo;
        Link           = item.Link;
        DateCountLevel = item.DateCountLevel;

        IsFolder = item is ScheduleFolder;

        if (item is ScheduleToDo todo)
        {
            Completed = todo.Completed;
            IsWait    = todo.IsWait;
            Progress  = todo.Progress;

            HasBeginDate = todo.BeginDate.HasValue;
            BeginDate    = todo.BeginDate ?? DateTime.Today;
            HasEndDate   = todo.EndDate.HasValue;
            EndDate      = todo.EndDate ?? DateTime.Today.AddDays(7);

            var rule = todo.Recurrence;
            HasRecurrence        = rule is not null;
            IsRecurrenceExpanded = rule is not null;
            if (rule is not null)
            {
                RecurrenceFrequency        = rule.Frequency;
                RecurrenceInterval         = rule.Interval;
                RecurrenceMonthDay         = rule.MonthDay ?? BeginDate.Day;
                RecurrenceUseLastDayOfMonth = rule.UseLastDayOfMonth;
                RecurrenceHolidayShift     = rule.HolidayShift;
                foreach (var opt in RecurrenceDayOptions)
                    opt.IsSelected = rule.DaysOfWeek.Contains(opt.Day);
            }
            else
            {
                foreach (var opt in RecurrenceDayOptions)
                    opt.IsSelected = opt.Day == BeginDate.DayOfWeek;
            }
        }
    }

    // ── プロパティ ────────────────────────────────────────
    public bool IsFolder    { get; }
    public bool IsNotFolder => !IsFolder;

    private string _name = string.Empty;
    public string Name
    {
        get => _name;
        set => SetField(ref _name, value);
    }

    private bool _completed;
    public bool Completed
    {
        get => _completed;
        set => SetField(ref _completed, value);
    }

    private bool _isWait;
    public bool IsWait
    {
        get => _isWait;
        set => SetField(ref _isWait, value);
    }

    // ── 進捗率 ───────────────────────────────────────
    /// <summary>進捗率プルダウンの選択肢（0, 10, 20, ... 100）。</summary>
    public IReadOnlyList<int> ProgressOptions { get; } =
        Enumerable.Range(0, 11).Select(i => i * 10).ToArray();

    private int _progress;
    public int Progress
    {
        get => _progress;
        set => SetField(ref _progress, value);
    }

    private bool _hasBeginDate;
    public bool HasBeginDate
    {
        get => _hasBeginDate;
        set
        {
            if (SetField(ref _hasBeginDate, value))
                OnPropertyChanged(nameof(BeginDateEnabled));
        }
    }

    private DateTime _beginDate = DateTime.Today;
    public DateTime BeginDate
    {
        get => _beginDate;
        set
        {
            if (SetField(ref _beginDate, value))
            {
                if (EndDate < value) EndDate = value;
                UpdateDaysLabel();
            }
        }
    }

    private bool _hasEndDate;
    public bool HasEndDate
    {
        get => _hasEndDate;
        set
        {
            if (SetField(ref _hasEndDate, value))
                OnPropertyChanged(nameof(EndDateEnabled));
        }
    }

    private DateTime _endDate = DateTime.Today.AddDays(7);
    public DateTime EndDate
    {
        get => _endDate;
        set
        {
            if (SetField(ref _endDate, value))
            {
                if (value < BeginDate) BeginDate = value;
                UpdateDaysLabel();
            }
        }
    }

    private string _memo = string.Empty;
    public string Memo
    {
        get => _memo;
        set => SetField(ref _memo, value);
    }

    private string _link = string.Empty;
    public string Link
    {
        get => _link;
        set
        {
            if (SetField(ref _link, value))
                OnPropertyChanged(nameof(CanApplyDesktopAppPrefix));
        }
    }

    public bool CanApplyDesktopAppPrefix => IsHttpUrl(Link);

    private int _dateCountLevel;
    public int DateCountLevel
    {
        get => _dateCountLevel;
        set => SetField(ref _dateCountLevel, value);
    }

    public bool BeginDateEnabled => HasBeginDate;
    public bool EndDateEnabled   => HasEndDate;

    private string _daysLabel = string.Empty;
    public string DaysLabel
    {
        get => _daysLabel;
        set => SetField(ref _daysLabel, value);
    }

    private void UpdateDaysLabel()
    {
        if (!HasBeginDate || !HasEndDate)
        {
            DaysLabel = string.Empty;
            return;
        }
        int days  = _main.Holidays.CountWorkingDays(BeginDate, EndDate, DateCountLevel);
        int total = (int)(EndDate - BeginDate).TotalDays + 1;
        DaysLabel = $"期間: {total}日 (実働{days}日)";
    }

    private static bool IsHttpUrl(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return false;
        if (!Uri.TryCreate(value.Trim(), UriKind.Absolute, out var uri)) return false;
        return uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps;
    }

    // ── 繰り返し予定 ───────────────────────────────────────
    public IReadOnlyList<RecurrenceFrequency> RecurrenceFrequencyOptions { get; } =
        Enum.GetValues<RecurrenceFrequency>();

    public IReadOnlyList<RecurrenceHolidayShift> RecurrenceHolidayShiftOptions { get; } =
        Enum.GetValues<RecurrenceHolidayShift>();

    public List<RecurrenceDayOption> RecurrenceDayOptions { get; } = new()
    {
        new(DayOfWeek.Monday,    "月"),
        new(DayOfWeek.Tuesday,   "火"),
        new(DayOfWeek.Wednesday, "水"),
        new(DayOfWeek.Thursday,  "木"),
        new(DayOfWeek.Friday,    "金"),
        new(DayOfWeek.Saturday,  "土"),
        new(DayOfWeek.Sunday,    "日"),
    };

    private bool _hasRecurrence;
    public bool HasRecurrence
    {
        get => _hasRecurrence;
        set => SetField(ref _hasRecurrence, value);
    }

    private bool _isRecurrenceExpanded;
    /// <summary>「繰り返し予定」GroupBox の展開状態。既存の繰り返し設定がある場合のみ既定で展開する。</summary>
    public bool IsRecurrenceExpanded
    {
        get => _isRecurrenceExpanded;
        set => SetField(ref _isRecurrenceExpanded, value);
    }

    private RecurrenceFrequency _recurrenceFrequency = RecurrenceFrequency.Weekly;
    public RecurrenceFrequency RecurrenceFrequency
    {
        get => _recurrenceFrequency;
        set
        {
            if (SetField(ref _recurrenceFrequency, value))
            {
                OnPropertyChanged(nameof(IsWeekly));
                OnPropertyChanged(nameof(IsMonthly));
            }
        }
    }

    public bool IsWeekly  => RecurrenceFrequency == RecurrenceFrequency.Weekly;
    public bool IsMonthly => RecurrenceFrequency == RecurrenceFrequency.Monthly;

    private int _recurrenceInterval = 1;
    public int RecurrenceInterval
    {
        get => _recurrenceInterval;
        set => SetField(ref _recurrenceInterval, value < 1 ? 1 : value);
    }

    private int _recurrenceMonthDay = 1;
    public int RecurrenceMonthDay
    {
        get => _recurrenceMonthDay;
        set => SetField(ref _recurrenceMonthDay, value);
    }

    private bool _recurrenceUseLastDayOfMonth;
    public bool RecurrenceUseLastDayOfMonth
    {
        get => _recurrenceUseLastDayOfMonth;
        set => SetField(ref _recurrenceUseLastDayOfMonth, value);
    }

    private RecurrenceHolidayShift _recurrenceHolidayShift = RecurrenceHolidayShift.None;
    public RecurrenceHolidayShift RecurrenceHolidayShift
    {
        get => _recurrenceHolidayShift;
        set => SetField(ref _recurrenceHolidayShift, value);
    }

    private RecurrenceRule BuildRecurrenceRule()
    {
        var rule = new RecurrenceRule
        {
            Frequency         = RecurrenceFrequency,
            Interval          = RecurrenceInterval,
            MonthDay          = RecurrenceMonthDay,
            UseLastDayOfMonth = RecurrenceUseLastDayOfMonth,
            HolidayShift      = RecurrenceHolidayShift,
        };
        rule.DaysOfWeek.AddRange(RecurrenceDayOptions.Where(o => o.IsSelected).Select(o => o.Day));
        return rule;
    }

    // ── 確定 ─────────────────────────────────────────────
    public void Apply()
    {
        _item.Name           = Name;
        _item.Memo           = Memo;
        _item.Link           = Link;
        _item.DateCountLevel = DateCountLevel;

        if (_item is ScheduleToDo todo)
        {
            todo.Completed = Completed;
            todo.IsWait    = IsWait;
            todo.Progress  = Progress;
            todo.BeginDate = HasBeginDate ? BeginDate : null;
            todo.EndDate   = HasEndDate   ? EndDate   : null;
            todo.Recurrence = HasRecurrence ? BuildRecurrenceRule() : null;
        }
    }
}
