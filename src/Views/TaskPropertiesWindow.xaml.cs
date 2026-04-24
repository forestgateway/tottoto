using System.Windows;
using Microsoft.Win32;
using todochart.Models;
using todochart.ViewModels;

namespace todochart.Views;

public enum PropertiesInitialTab { Default, Memo, Link }

public partial class TaskPropertiesWindow : Window
{
    private readonly TaskPropertiesViewModel _vm;

    public TaskPropertiesWindow(ScheduleItemBase item, MainViewModel main,
                                PropertiesInitialTab initialTab = PropertiesInitialTab.Default)
    {
        InitializeComponent();
        _vm = new TaskPropertiesViewModel(item, main);
        DataContext = _vm;

        // フォルダは日程タブを非表示
        if (_vm.IsFolder)
            ScheduleTab.Visibility = Visibility.Collapsed;

        // 指定タブを初期選択
        MainTabControl.SelectedItem = initialTab switch
        {
            PropertiesInitialTab.Memo => MemoTab,
            PropertiesInitialTab.Link => LinkTab,
            _                         => MainTabControl.Items[0],
        };
    }

    private void OnOkClick(object sender, RoutedEventArgs e)
    {
        _vm.Apply();
        DialogResult = true;
    }

    private void OnCancelClick(object sender, RoutedEventArgs e) =>
        DialogResult = false;

    private void OnBrowseFileClick(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Title  = "ファイルを選択",
            Filter = "すべてのファイル (*.*)|*.*",
        };
        if (dlg.ShowDialog() == true)
            _vm.Link = dlg.FileName;
    }

    private void OnOpenLinkClick(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_vm.Link)) return;
        try
        {
            System.Diagnostics.Process.Start(
                new System.Diagnostics.ProcessStartInfo(_vm.Link) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            MessageBox.Show($"リンクを開けませんでした。\n{ex.Message}",
                            "エラー", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void OnClearLinkClick(object sender, RoutedEventArgs e) =>
        _vm.Link = string.Empty;
}
