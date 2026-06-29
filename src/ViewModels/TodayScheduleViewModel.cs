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

        // Today ウィンドウ専用の ★ フィルタ状態とコマンド
        ToggleStarFilterCommand = new RelayCommand(() =>
        {
            StarFilterState = (StarFilterState + 1) % 3;
            RebuildTodayItems();
        });

        RebuildTodayItems();
    }

    // Today ウィンドウ専用の ★フィルタ（0=なし,1=黄色のみ,2=黄色+黒）
    private int _starFilterState = 0;
    public int StarFilterState
    {
        get => _starFilterState;
        private set
        {
            if (SetField(ref _starFilterState, value))
            {
                OnPropertyChanged(nameof(StarFilterBrush));
                OnPropertyChanged(nameof(StarFilterTooltip));
                OnPropertyChanged(nameof(StarFilterGlyph));
            }
        }
    }

    public System.Windows.Input.ICommand ToggleStarFilterCommand { get; private set; }

    public System.Windows.Media.Brush StarFilterBrush
    {
        get
        {
            return StarFilterState switch
            {
                1 => new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0xFF, 0xD7, 0x00)),
                2 => (System.Windows.Media.Brush)System.Windows.Application.Current.FindResource("MarkBlackBrush"),
                _ => (System.Windows.Media.Brush)System.Windows.Application.Current.FindResource("SubTextBrush")
            };
        }
    }

    public string StarFilterTooltip => StarFilterState switch
    {
        0 => "★フィルタ: すべて表示 (クリックで黄色のみ表示)",
        1 => "★フィルタ: 黄色のみ (クリックで黄色＋黒を表示)",
        2 => "★フィルタ: 黄色＋黒 (クリックで全て表示)",
        _ => string.Empty,
    };

    public string StarFilterGlyph => StarFilterState == 0 ? "∀" : "★";

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
    public ICommand EditOnMemoCommand     => _main.EditOnMemoCommand;
    public ICommand ArchiveCommand        => _main.ArchiveCommand;
    public ICommand CopyItemCommand       => _main.CopyItemCommand;
    public ICommand CutItemCommand        => _main.CutItemCommand;
    public ICommand PasteItemCommand      => _main.PasteItemCommand;
    public ICommand SelectNextCommand     => _main.SelectNextCommand;
    public ICommand SelectPreviousCommand => _main.SelectPreviousCommand;
    // Progress change commands proxy to MainViewModel
    public ICommand IncreaseProgressCommand => _main.IncreaseProgressCommand;
    public ICommand DecreaseProgressCommand => _main.DecreaseProgressCommand;
    // Shift end date commands proxy to MainViewModel
    public ICommand ShiftEndPlusCommand     => _main.ShiftEndPlusCommand;
    public ICommand ShiftEndMinusCommand    => _main.ShiftEndMinusCommand;

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

            // StarFilterState を考慮: 0=なし,1=黄色のみ,2=黄色+黒
            bool matchesStar = StarFilterState == 0
                || row.Item.MarkLevel == 1
                || (StarFilterState == 2 && row.Item.MarkLevel == 2);
            if (!matchesStar) continue;

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
