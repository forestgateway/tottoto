using System.Windows;
using todochart.ViewModels;

namespace todochart.Views;

/// <summary>
/// バージョン更新確認ダイアログ。
/// </summary>
public partial class UpdateCheckWindow : Window
{
    private readonly UpdateCheckViewModel _vm;

    public UpdateCheckWindow(UpdateCheckViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        DataContext = vm;
    }

    /// <summary>ウィンドウ表示後に非同期でバージョンチェックを開始する。</summary>
    protected override async void OnContentRendered(EventArgs e)
    {
        base.OnContentRendered(e);
        // Ensure the Toggle visual reflects the persisted value immediately
        try
        {
            if (StartupCheckToggle is not null)
            {
                StartupCheckToggle.IsChecked = _vm.CheckForUpdatesOnStartup;
            }
        }
        catch { }

        await _vm.LoadAsync();
    }

    private void OnCloseClick(object sender, RoutedEventArgs e) => Close();
}