using System.Collections.ObjectModel;

using System.Collections.Specialized;
using System.Windows.Input;
using todochart.Models;

namespace todochart.ViewModels;

public class TodayScheduleViewModel : ViewModelBase
{
    private readonly MainViewModel _main;

    // 今日の予定ウィンドウ専用の展開状態（フォルダモデルをキーとする）
    private readonly Dictionary<ScheduleItemBase, bool> _expandedStates = new();

    public ObservableCollection<TaskRowViewModel> TodayItems { get; } = new();

    public TodayScheduleViewModel(MainViewModel main)
    {
        _main = main;
        // FlatItems の変化（アクティブタブの編集・タブ切替）を監視
        _main.FlatItems.CollectionChanged += OnFlatItemsChanged;
        // Schedules 自体の追加・削除を監視
        _main.Schedules.CollectionChanged += OnSchedulesChanged;
        RebuildTodayItems();
    }

    // -- Selected: proxy to MainViewModel (bidirectional sync) ---------------
    public TaskRowViewModel? Selected
    {
        get => _main.Selected;
        set
        {
            if (_main.Selected == value) return;
            _main.SelectWithoutTabSwitch(value);
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsContextCompleteEnabled));
            OnPropertyChanged(nameof(ContextCompleteHeader));
        }
    }

    public bool IsContextCompleteEnabled =>
        Selected?.Item is ScheduleToDo;

    public string ContextCompleteHeader =>
        Selected?.Item is ScheduleToDo todo && todo.Completed ? "完了を外す(_C)" : "完了(_C)";

    public bool IsContextWaitEnabled =>
        Selected?.Item is ScheduleToDo td && !td.Completed;

    public string ContextWaitHeader =>
        Selected?.Item is ScheduleToDo todo2 && todo2.IsWait ? "WAITを外す(_W)" : "WAIT(_W)";

    // -- Commands: delegate to MainViewModel ---------------------------------
    public ICommand ToggleCompleteCommand => _main.ToggleCompleteCommand;
    public ICommand ToggleWaitCommand     => _main.ToggleWaitCommand;
    public ICommand NewToDoCommand        => _main.NewToDoCommand;
    public ICommand NewFolderCommand      => _main.NewFolderCommand;
    public ICommand DeleteCommand         => _main.DeleteCommand;
    public ICommand EditCommand           => _main.EditCommand;
    public ICommand ArchiveCommand        => _main.ArchiveCommand;
    public ICommand CopyItemCommand       => _main.CopyItemCommand;
    public ICommand CutItemCommand        => _main.CutItemCommand;
    public ICommand PasteItemCommand      => _main.PasteItemCommand;

    // -- Called by MainViewModel when its Selected changes -------------------
    public void NotifySelectedChanged()
    {
        OnPropertyChanged(nameof(Selected));
        OnPropertyChanged(nameof(IsContextCompleteEnabled));
        OnPropertyChanged(nameof(ContextCompleteHeader));
        OnPropertyChanged(nameof(IsContextWaitEnabled));
        OnPropertyChanged(nameof(ContextWaitHeader));
    }

    // -- Rebuild filter when FlatItems changes --------------------------------
    private void OnFlatItemsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        RebuildTodayItems();
        NotifySelectedChanged();
    }

    // -- Rebuild filter when Schedules list itself changes --------------------
    private void OnSchedulesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        RebuildTodayItems();
        NotifySelectedChanged();
    }

    private void RebuildTodayItems()
    {
        var today = _main.Today;

        // needed の判定は展開状態に関係なく全アイテムを走査する
        var allRowsForFilter = _main.BuildAllScheduleRowsFlat();

        // 今日が期間内のタスクと、その祖先フォルダを収集
        var needed = new HashSet<ScheduleItemBase>();
        foreach (var row in allRowsForFilter)
        {
            if (row.Item.IsFolder) continue;
            if (!IsTaskActiveToday(row.Item, today)) continue;

            needed.Add(row.Item);
            var ancestor = row.Item.Parent;
            while (ancestor is not null)
            {
                needed.Add(ancestor);
                ancestor = ancestor.Parent;
            }
        }

        // 表示リストは独自展開状態を使って構築（折りたたまれた子は含まれない）
        var allRows = _main.BuildAllScheduleRows(_expandedStates);

        TodayItems.Clear();
        int rowIndex = 0;
        foreach (var row in allRows)
        {
            if (!needed.Contains(row.Item)) continue;

            // フォルダ行に独立展開コールバックを設定
            if (row.Item.IsFolder)
            {
                var folder = row.Item;
                row.SetExpandedCallback(expanded =>
                {
                    _expandedStates[folder] = expanded;
                    RebuildTodayItems();
                });
            }

            row.RowIndex = rowIndex++;
            TodayItems.Add(row);
        }
    }

    private static bool IsTaskActiveToday(ScheduleItemBase item, DateTime today)
    {
        bool afterBegin = !item.BeginDate.HasValue || item.BeginDate.Value.Date <= today.Date;
        bool beforeEnd  = !item.EndDate.HasValue   || item.EndDate.Value.Date   >= today.Date;
        return afterBegin && beforeEnd;
    }

    public void Detach()
    {
        _main.FlatItems.CollectionChanged  -= OnFlatItemsChanged;
        _main.Schedules.CollectionChanged  -= OnSchedulesChanged;
    }
}
