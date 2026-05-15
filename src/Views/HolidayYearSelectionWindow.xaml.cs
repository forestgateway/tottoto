using System.Windows;
using todochart.ViewModels;

namespace todochart.Views;

public partial class HolidayYearSelectionWindow : Window
{
    public HolidayYearSelectionWindow(HolidayYearSelectionViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;
    }

    private void OnOkClick(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
        Close();
    }
}
