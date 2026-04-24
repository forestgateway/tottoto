using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using todochart.ViewModels;

namespace todochart.Views;

public partial class ArchiveListWindow : Window
{
    private readonly ArchiveListViewModel _vm;

    public ArchiveListWindow(ArchiveListViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        DataContext = vm;
    }

    public bool Restored => _vm.Restored;

    private void OnCloseClick(object sender, RoutedEventArgs e)
    {
        DialogResult = _vm.Restored;
    }

    private void OnColumnHeaderClick(object sender, RoutedEventArgs e)
    {
        if (e.OriginalSource is not GridViewColumnHeader header) return;
        if (header.Column is null) return;

        // DisplayMemberBinding のパスからソート列名を取得
        var binding = header.Column.DisplayMemberBinding as System.Windows.Data.Binding;
        var column = binding?.Path?.Path;
        if (!string.IsNullOrEmpty(column))
            _vm.SortBy(column);
    }

    private void OnListViewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is not DependencyObject source) return;

        // クリック位置の ListViewItem を探す
        var item = ItemsControl.ContainerFromElement((ListView)sender, source) as ListViewItem;
        if (item?.DataContext is ArchivedItemRow row)
            row.IsChecked = !row.IsChecked;
    }
}
