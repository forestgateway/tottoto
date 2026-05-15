using System.Windows;
using todochart.ViewModels;

namespace todochart.Views;

public partial class HolidaySettingsWindow : Window
{
    private readonly HolidaySettingsViewModel _vm;

    public HolidaySettingsWindow(HolidaySettingsViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        DataContext = vm;
    }

    private async void OnFetchJapanHolidaysClick(object sender, RoutedEventArgs e)
    {
        try
        {
            var years = await _vm.FetchJapanHolidayYearsAsync();
            if (years.Count == 0)
            {
                MessageBox.Show("反映対象の年が見つかりませんでした。", "休日取得", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var yearVm = new HolidayYearSelectionViewModel(years);
            var yearWin = new HolidayYearSelectionWindow(yearVm)
            {
                Owner = this,
            };

            if (yearWin.ShowDialog() == true)
            {
                var selectedYears = yearVm.GetSelectedYears();
                _ = _vm.ApplyFetchedJapanHolidaysByYears(selectedYears);
            }
        }
        catch (Exception ex)
        {
            _vm.StatusMessage = $"エラー: {ex.Message}";
            MessageBox.Show(_vm.StatusMessage, "休日取得エラー", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void OnOkClick(object sender, RoutedEventArgs e)
    {
        // ViewModel の設定を HolidayService に適用
        _vm.ApplyToService();
        DialogResult = true;
        Close();
    }
}
