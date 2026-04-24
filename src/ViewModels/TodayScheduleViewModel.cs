using System.Collections.ObjectModel;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Windows.Input;
using todochart.Models;

namespace todochart.ViewModels;

public class TodayScheduleViewModel : ViewModelBase
{
    private readonly MainViewModel _main;

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

        // 全タブの全アイテムを走査（HideCompleted フィルターなし）
        var allRows = _main.BuildAllScheduleRows();

        // 今日が期間内のタスクと、その祖先フォルダを収集
        var needed = new HashSet<ScheduleItemBase>();
        foreach (var row in allRows)
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

        // allRows の順序を保持して TodayItems を再構築し、横縞用に RowIndex を振り直す
        TodayItems.Clear();
        int rowIndex = 0;
        foreach (var row in allRows)
        {
            if (!needed.Contains(row.Item)) continue;
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
