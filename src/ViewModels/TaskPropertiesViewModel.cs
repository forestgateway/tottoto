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

            HasBeginDate = todo.BeginDate.HasValue;
            BeginDate    = todo.BeginDate ?? DateTime.Today;
            HasEndDate   = todo.EndDate.HasValue;
            EndDate      = todo.EndDate ?? DateTime.Today.AddDays(7);
        }
    }

    // ── プロパティ ────────────────────────────────────────
    public bool IsFolder { get; }

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
        set => SetField(ref _link, value);
    }

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
            todo.IsWait   = IsWait;
            todo.BeginDate = HasBeginDate ? BeginDate : null;
            todo.EndDate   = HasEndDate   ? EndDate   : null;
        }
    }
}
