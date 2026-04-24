using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using todochart.Models;

namespace todochart.ViewModels;

/// <summary>アーカイブ一覧内の 1 行分。チェックボックス付き。</summary>
public class ArchivedItemRow : INotifyPropertyChanged
{
    public ArchivedItem Item { get; }

    public string Name       => Item.Name;
    public string Path       => Item.Path;
    public string ArchivedAt => Item.ArchivedAt.ToString("yyyy/MM/dd HH:mm");

    private bool _isChecked;
    public bool IsChecked
    {
        get => _isChecked;
        set { if (_isChecked != value) { _isChecked = value; OnPropertyChanged(); } }
    }

    public ArchivedItemRow(ArchivedItem item) => Item = item;

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

/// <summary>アーカイブ一覧（復元）ウィンドウの ViewModel。</summary>
public class ArchiveListViewModel : ViewModelBase
{
    private readonly MainViewModel _main;
    private readonly string _archivePath;

    public ObservableCollection<ArchivedItemRow> Items { get; } = new();

    public ICommand RestoreCommand { get; }
    public ICommand DeleteCommand { get; }
    public ICommand SelectAllCommand { get; }
    public ICommand DeselectAllCommand { get; }

    // ── ソート状態 ──
    private string _sortColumn = "ArchivedAt";
    private bool   _sortAscending = false; // 初期: アーカイブ日降順

    public ArchiveListViewModel(MainViewModel main, string archivePath, List<ArchivedItem> archived)
    {
        _main        = main;
        _archivePath = archivePath;

        foreach (var a in archived.OrderByDescending(x => x.ArchivedAt))
            Items.Add(new ArchivedItemRow(a));

        RestoreCommand     = new RelayCommand(Restore,     () => Items.Any(i => i.IsChecked));
        DeleteCommand      = new RelayCommand(Delete,      () => Items.Any(i => i.IsChecked));
        SelectAllCommand   = new RelayCommand(SelectAll);
        DeselectAllCommand = new RelayCommand(DeselectAll);
    }

    /// <summary>復元結果。true = 何か復元された → 呼び出し元でリフレッシュが必要。</summary>
    public bool Restored { get; private set; }

    private void Restore()
    {
        var selected = Items.Where(i => i.IsChecked).ToList();
        if (selected.Count == 0) return;

        _main.RestoreArchivedItems(
            selected.Select(r => r.Item).ToList(),
            _archivePath);

        // 一覧から除去
        foreach (var row in selected)
            Items.Remove(row);

        Restored = true;
    }

    private void Delete()
    {
        var selected = Items.Where(i => i.IsChecked).ToList();
        if (selected.Count == 0) return;

        var r = System.Windows.MessageBox.Show(
            $"選択された {selected.Count} 件をアーカイブから完全に削除します。\nこの操作は元に戻せません。",
            "削除確認", System.Windows.MessageBoxButton.YesNo, System.Windows.MessageBoxImage.Warning);
        if (r != System.Windows.MessageBoxResult.Yes) return;

        _main.DeleteArchivedItems(
            selected.Select(row => row.Item).ToList(),
            _archivePath);

        foreach (var row in selected)
            Items.Remove(row);
    }

    private void SelectAll()
    {
        foreach (var item in Items) item.IsChecked = true;
    }

    private void DeselectAll()
    {
        foreach (var item in Items) item.IsChecked = false;
    }

    /// <summary>指定列でソートする。同じ列なら昇降順を切り替える。</summary>
    public void SortBy(string column)
    {
        if (_sortColumn == column)
            _sortAscending = !_sortAscending;
        else
        {
            _sortColumn    = column;
            _sortAscending = true;
        }

        var sorted = _sortColumn switch
        {
            "Name"       => _sortAscending
                ? Items.OrderBy(r => r.Name, StringComparer.CurrentCultureIgnoreCase).ToList()
                : Items.OrderByDescending(r => r.Name, StringComparer.CurrentCultureIgnoreCase).ToList(),
            "Path"       => _sortAscending
                ? Items.OrderBy(r => r.Path, StringComparer.CurrentCultureIgnoreCase).ToList()
                : Items.OrderByDescending(r => r.Path, StringComparer.CurrentCultureIgnoreCase).ToList(),
            "ArchivedAt" => _sortAscending
                ? Items.OrderBy(r => r.Item.ArchivedAt).ToList()
                : Items.OrderByDescending(r => r.Item.ArchivedAt).ToList(),
            _ => Items.ToList(),
        };

        Items.Clear();
        foreach (var row in sorted)
            Items.Add(row);
    }
}
