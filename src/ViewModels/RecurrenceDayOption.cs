namespace todochart.ViewModels;

/// <summary>繰り返し設定の曜日選択チェックボックス 1 つ分の表示用 ViewModel。</summary>
public class RecurrenceDayOption : ViewModelBase
{
    public DayOfWeek Day { get; }
    public string Label { get; }

    public RecurrenceDayOption(DayOfWeek day, string label)
    {
        Day   = day;
        Label = label;
    }

    private bool _isSelected;
    public bool IsSelected
    {
        get => _isSelected;
        set => SetField(ref _isSelected, value);
    }
}
