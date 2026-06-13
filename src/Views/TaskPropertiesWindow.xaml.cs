using System.IO;
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

    private void OnApplyDesktopAppPrefixClick(object sender, RoutedEventArgs e)
    {
        var link = _vm.Link?.Trim();
        if (string.IsNullOrWhiteSpace(link)) return;

        if (!Uri.TryCreate(link, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            MessageBox.Show("URL（http/https）のときのみ変換できます。",
                            "未対応", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (link.StartsWith("ms-excel:", StringComparison.OrdinalIgnoreCase) ||
            link.StartsWith("ms-word:", StringComparison.OrdinalIgnoreCase) ||
            link.StartsWith("ms-powerpoint:", StringComparison.OrdinalIgnoreCase))
        {
            MessageBox.Show("既にデスクトップアプリ用のリンク形式です。",
                            "情報", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var ext = GetLinkExtension(link);
        var prefix = ext switch
        {
            ".xlsx" or ".xlsm" or ".xls" => "ms-excel:ofv|u|",
            ".docx" or ".docm" or ".doc" => "ms-word:ofv|u|",
            ".pptx" or ".pptm" or ".ppt" => "ms-powerpoint:ofv|u|",
            _ => null,
        };

        if (prefix is null)
        {
            MessageBox.Show("Excel / Word / PowerPoint の拡張子を持つリンクのみ変換できます。",
                            "未対応", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        _vm.Link = prefix + link;
    }

    private static string GetLinkExtension(string link)
    {
        if (Uri.TryCreate(link, UriKind.Absolute, out var uri))
            return Path.GetExtension(uri.AbsolutePath).ToLowerInvariant();

        return Path.GetExtension(link).ToLowerInvariant();
    }
}
