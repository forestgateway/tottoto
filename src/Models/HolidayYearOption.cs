namespace todochart.Models;

/// <summary>
/// 祝日反映対象の年選択オプション。
/// </summary>
public sealed class HolidayYearOption
{
    public int Year { get; init; }
    public bool IsChecked { get; set; }
}
