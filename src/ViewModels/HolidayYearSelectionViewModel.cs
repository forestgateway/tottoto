using System.Collections.ObjectModel;
using todochart.Models;

namespace todochart.ViewModels;

/// <summary>
/// 祝日反映対象の年を選択するダイアログの ViewModel。
/// </summary>
public class HolidayYearSelectionViewModel : ViewModelBase
{
    public ObservableCollection<HolidayYearOption> Years { get; } = new();

    public HolidayYearSelectionViewModel(IEnumerable<int> years)
    {
        foreach (var year in years.OrderByDescending(y => y))
        {
            Years.Add(new HolidayYearOption
            {
                Year = year,
                IsChecked = false,
            });
        }
    }

    public IReadOnlyList<int> GetSelectedYears() =>
        Years.Where(y => y.IsChecked)
             .Select(y => y.Year)
             .ToList();
}
