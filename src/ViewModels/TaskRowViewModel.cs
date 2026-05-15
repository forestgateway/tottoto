using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using todochart.Models;
using todochart.Services;

namespace todochart.ViewModels;

/// <summary>ツリー罫線の種類</summary>
public enum TreeLineKind
{
    /// <summary>何も描かない（祖先で兄弟なし）</summary>
    None,
    /// <summary>縦線のみ（兄弟がまだ続く深さ）</summary>
    Vertical,
    /// <summary>└ 形（自分の深さ、最後の子）</summary>
    Corner,
    /// <summary>├ 形（自分の深さ、後続の兄弟あり）</summary>
    Tee,
}

/// <summary>1 行分の罫線セグメント（1 深さレベルにつき 1 つ）</summary>
public record TreeLineSegment(int Level, TreeLineKind Kind);

/// <summary>
/// ガントチャートの 1 行分（ツリーの 1 ノード相当）の表示 ViewModel。
/// フラットリストで管理し、Depth で字下げを表現する。
/// </summary>
public class TaskRowViewModel : ViewModelBase
{
    private readonly MainViewModel _main;

    public ScheduleItemBase Item { get; }
    public int Depth { get; }

    private int _rowIndex;
    public int RowIndex
    {
        get => _rowIndex;
        set { if (_rowIndex != value) { _rowIndex = value; OnPropertyChanged(nameof(RowBackground)); } }
    }

    /// <summary>
    /// 深さ 0 が [0]=自分の深さのセグメント。
    /// ancestorHasNext[d] は深さ d の祖先がまだ次の兄弟を持つかどうか。
    /// </summary>
    public IReadOnlyList<TreeLineSegment> TreeLineSegments { get; }

    public TaskRowViewModel(ScheduleItemBase item, int depth, MainViewModel main,
                            bool isLastChild = true,
                            IReadOnlyList<bool>? ancestorHasNext = null)
    {
        Item   = item;
        Depth  = depth;
        _main  = main;

        // ── ツリー罫線セグメントを構築 ──
        // ancestorHasNext[d] : 深さ d の祖先がまだ後続の兄弟を持つか（d < depth）
        var segs = new List<TreeLineSegment>(depth + 1);
        for (int d = 0; d < depth; d++)
        {
            bool hasNext = ancestorHasNext != null && d < ancestorHasNext.Count && ancestorHasNext[d];
            segs.Add(new TreeLineSegment(d, hasNext ? TreeLineKind.Vertical : TreeLineKind.None));
        }
        if (depth > 0)
            segs.Add(new TreeLineSegment(depth, isLastChild ? TreeLineKind.Corner : TreeLineKind.Tee));
        TreeLineSegments = segs;

        // フォルダの展開状態を同期
        _isExpanded = item is ScheduleFolder sf ? sf.IsExpanded : false;

        ToggleExpandCommand = new RelayCommand(
            () => IsExpanded = !IsExpanded,
            () => IsFolder);

        OpenMemoCommand = new RelayCommand(
            () => _main.EditItemOnMemoTab(this),
            () => HasMemo);

        OpenLinkCommand = new RelayCommand(
            () => OpenLink(),
            () => HasLink);

        EditCommand = new RelayCommand(() => _main.EditItem(this));
        ToggleMarkCommand = new RelayCommand(() => MarkLevel = (MarkLevel + 1) % 3);
    }

    // ── プロパティ ├───────────────────────────────────────
    public bool IsFolder => Item.IsFolder;

    public string Name
    {
        get => Item.Name;
        set
        {
            if (Item.Name == value) return;
            Item.Name = value;
            OnPropertyChanged();
            var entry = _main.FindEntryForItem(Item);
            if (entry is not null) entry.IsModified = true;
        }
    }

    /// <summary>モデル／コールバックを介さず内部フラグだけ書き換える（ビルド時の初期化用）。</summary>
    internal void SetIsExpandedDirect(bool value) => _isExpanded = value;

    // null の場合はモデルに書き戻し MainViewModel.RefreshFlatList を呼ぶ（通常モード）。
    // 非null の場合はモデルを変更せずコールバックだけ呼ぶ（今日の予定ウィンドウ用）。
    private Action<bool>? _expandedCallback;

    /// <summary>
    /// 今日の予定ウィンドウ用の独立展開モードを設定する。
    /// コールバックが設定されると IsExpanded の変更がモデルに伝播しなくなる。
    /// </summary>
    internal void SetExpandedCallback(Action<bool> callback) => _expandedCallback = callback;

    private bool _isExpanded = true;
    public bool IsExpanded
    {
        get => _isExpanded;
        set
        {
            if (_isExpanded == value) return;
            _isExpanded = value;
            if (_expandedCallback is not null)
            {
                // 独立モード：モデルに書き戻さず、コールバックで TodayScheduleViewModel を更新
                OnPropertyChanged();
                _expandedCallback(value);
            }
            else
            {
                if (Item is ScheduleFolder sf) sf.IsExpanded = value;
                OnPropertyChanged();
                _main.RefreshFlatList();
            }
        }
    }

    private bool _isSelected;
    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (SetField(ref _isSelected, value))
                foreach (var c in _callouts)
                    c.IsTaskSelected = value;
        }
    }

    // マーク（三段階: 0=なし,1=黄色,2=黒）
    public int MarkLevel
    {
        get => Item.MarkLevel;
        set
        {
            if (Item.MarkLevel == value) return;
            Item.MarkLevel = Math.Clamp(value, 0, 2);
            OnPropertyChanged();
            OnPropertyChanged(nameof(MarkBrush));
            var entry = _main.FindEntryForItem(Item);
            if (entry is not null) entry.IsModified = true;
        }
    }

    private static readonly Brush s_markNone  = Brushes.Transparent;
    private static readonly Brush s_markYellow = Freeze(new SolidColorBrush(Color.FromRgb(0xFF, 0xD7, 0x00)));
    private static readonly Brush s_markBlack  = Freeze(new SolidColorBrush(Colors.Black));

    public Brush MarkBrush => MarkLevel == 0 ? s_markNone : (MarkLevel == 1 ? s_markYellow : s_markBlack);

    public ICommand ToggleMarkCommand { get; }

    // ── インライン名前編集 ────────────────────────────────
    private bool _isEditing;
    public bool IsEditing
    {
        get => _isEditing;
        private set
        {
            if (_isEditing == value) return;
            _isEditing = value;
            OnPropertyChanged();
        }
    }

    private string _editingName = string.Empty;
    public string EditingName
    {
        get => _editingName;
        set => SetField(ref _editingName, value);
    }

    public void BeginEdit()
    {
        _editingName = Item.Name;
        OnPropertyChanged(nameof(EditingName));
        IsEditing = true;
    }

    public void CommitEdit()
    {
        if (!IsEditing) return;
        IsEditing = false;
        var newName = EditingName.Trim();
        if (!string.IsNullOrEmpty(newName))
            Name = newName;
    }

    public void CancelEdit()
    {
        if (!IsEditing) return;
        IsEditing = false;
    }

    // ── 表示用ユーティリティ ──────────────────────────────
    public Thickness IndentMargin     => new(Depth * 16.0, 0, 0, 0);
    public double    ToggleLeftMargin => Depth * 16.0;
    public Visibility ToggleVisibility =>
        IsFolder ? Visibility.Visible : Visibility.Collapsed;

    public string ExpandGlyph => IsExpanded ? "▼" : "▶";

    public string BeginText =>
        Item.BeginDate.HasValue ? Item.BeginDate.Value.ToString("yyyy/MM/dd") : "－";
    public string EndText =>
        Item.EndDate.HasValue ? Item.EndDate.Value.ToString("yyyy/MM/dd") : "－";

    public string DaysText
    {
        get
        {
            if (Item is ScheduleFolder f && f.IsEmpty) return string.Empty;
            if (Item.Status == ItemStatus.Complete) return "完了";
            if (Item.Status == ItemStatus.Wait)
            {
                if (Item.BeginDate.HasValue && DateTime.Today < Item.BeginDate.Value)
                    return "予定";
                return "WAIT";
            }
            if (Item.Status == ItemStatus.Error)    return "遅延";
            if (!Item.EndDate.HasValue)             return "期限なし";
            var today = DateTime.Today;
            var from  = Item.BeginDate.HasValue && today < Item.BeginDate.Value
                        ? Item.BeginDate.Value : today;
            int days  = Item.CountWorkingDays(from, Item.EndDate.Value, _main.Holidays);
            return days < 1 ? "0日" : $"残{days}日";
        }
    }

    public bool HasMemo => !string.IsNullOrEmpty(Item.Memo);
    public bool HasLink => !string.IsNullOrEmpty(Item.Link);

    // ── ステータス別アイコン・色 ──────────────────────────
    public Brush StatusBrush => StatusToBrush(Item.Status);

    public static Brush StatusToBrush(ItemStatus status) => status switch
    {
        ItemStatus.Complete => new SolidColorBrush(Color.FromRgb(0xBB, 0x44, 0xBB)),
        ItemStatus.Wait     => new SolidColorBrush(Color.FromRgb(0x44, 0x88, 0xFF)),
        ItemStatus.Progress => new SolidColorBrush(Color.FromRgb(0x22, 0xBB, 0x22)),
        ItemStatus.Warning  => new SolidColorBrush(Color.FromRgb(0xFF, 0xAA, 0x00)),
        ItemStatus.Error    => new SolidColorBrush(Color.FromRgb(0xFF, 0x33, 0x33)),
        ItemStatus.Over     => new SolidColorBrush(Color.FromRgb(0x99, 0x00, 0x00)),
        _                   => new SolidColorBrush(Color.FromRgb(0xAA, 0xAA, 0xAA)),
    };

    public string StatusIcon
    {
        get
        {
            if (IsFolder)
            {
                return Item.Status switch
                {
                    ItemStatus.Complete => "📁",
                    ItemStatus.Error    => "📁!",
                    ItemStatus.Warning  => "📁~",
                    ItemStatus.Progress => "📁",
                    _                  => "📁",
                };
            }
            return ((ScheduleToDo)Item).Completed ? "⚬" : "○";
        }
    }

    /// <summary>フォルダかつ全タスク完了のとき true。アイコン背景色切り替えに使用。</summary>
    public bool IsCompletedFolder => IsFolder && Item.Status == ItemStatus.Complete;

    private static readonly Brush s_rowEven = Freeze(new SolidColorBrush(Colors.White));
    private static readonly Brush s_rowOdd  = Freeze(new SolidColorBrush(Color.FromRgb(0xF8, 0xF8, 0xF8)));

    public Brush RowBackground => _rowIndex % 2 == 0 ? s_rowEven : s_rowOdd;

    // ── チャートセル ─────────────────────────────────────
    private IReadOnlyList<ChartCellInfo> _chartCells = Array.Empty<ChartCellInfo>();
    public IReadOnlyList<ChartCellInfo> ChartCells => _chartCells;

    /// <summary>
    /// チャートセルを再計算する。MainViewModel がチャート開始日変更時に呼び出す。
    /// </summary>
    public void RefreshChartCells(DateTime chartStart, int cellCount,
                                  DateTime today, HolidayService holidays,
                                  int appDateCountLv)
    {
        var cells = new List<ChartCellInfo>(cellCount);
        var rowBase = _rowIndex % 2 == 0 ? s_rowEven : s_rowOdd;

        for (int i = 0; i < cellCount; i++)
        {
            var date    = chartStart.AddDays(i);
            var hlv     = holidays.GetLevel(date);
            var isToday = date.Date == today.Date;

            int cellStatus = ComputeCellStatus(date, today, appDateCountLv, hlv, holidays);

            cells.Add(new ChartCellInfo
            {
                Date       = date,
                Status     = (ItemStatus)cellStatus,
                IsToday    = isToday,
                HolidayLv  = hlv,
                Background = CellBackground(cellStatus, hlv, date, rowBase),
                Symbol     = CellSymbol(cellStatus, isToday),
                BarBrush   = cellStatus >= 0 ? BarBrush(cellStatus) : null,
            });
        }

        _chartCells = cells;
        OnPropertyChanged(nameof(ChartCells));
    }

    private int ComputeCellStatus(DateTime date, DateTime today,
                                  int appDateCountLv, int hlv,
                                  HolidayService holidays)
    {
        var status = (int)Item.Status;

        if (status == (int)ItemStatus.None || status < 0) return -1;

        if (Item is ScheduleFolder folder)
        {
            if (folder.IsEmpty) return -1;
            // 折りたたみ時に限り表示
            if (IsExpanded) return -1;
        }

        bool inRange = InChartRange(date);

        if (!inRange)
        {
            // 期限超過後も Over として表示
            if (status == (int)ItemStatus.Error
             && Item.EndDate.HasValue
             && date.Date > Item.EndDate.Value.Date
             && date.Date <= today.Date)
                return (int)ItemStatus.Over;
            return -1;
        }

        // 休日スキップ
        bool skipByHoliday = hlv > appDateCountLv && hlv > Item.DateCountLevel
                             && status is (int)ItemStatus.Wait
                                       or (int)ItemStatus.Progress
                                       or (int)ItemStatus.Warning;
        if (skipByHoliday) return (int)ItemStatus.Skip;

        return status;
    }

    private bool InChartRange(DateTime date)
    {
        bool afterBegin = !Item.BeginDate.HasValue || date.Date >= Item.BeginDate.Value.Date;
        bool beforeEnd  = !Item.EndDate.HasValue   || date.Date <= Item.EndDate.Value.Date;
        return afterBegin && beforeEnd;
    }

    // ── セル背景ブラシのキャッシュ ────────────────────────
    private static readonly Brush s_bgWeekend  = Freeze(new SolidColorBrush(Color.FromRgb(0xE8, 0xE8, 0xFF)));

    // ── バー（小正方形）ブラシのキャッシュ ───────────────
    private static readonly Brush s_barSkip     = Freeze(new SolidColorBrush(Color.FromRgb(0xCC, 0xCC, 0xCC)));
    private static readonly Brush s_barComplete = Freeze(new SolidColorBrush(Color.FromRgb(0xD4, 0xA0, 0xD4)));
    private static readonly Brush s_barWait     = Freeze(new SolidColorBrush(Color.FromRgb(0xFF, 0xB3, 0xB3)));
    private static readonly Brush s_barProgress = Freeze(new SolidColorBrush(Color.FromRgb(0x90, 0xEE, 0x90)));
    private static readonly Brush s_barWarning  = Freeze(new SolidColorBrush(Color.FromRgb(0x90, 0xE0, 0xE0)));
    private static readonly Brush s_barError    = Freeze(new SolidColorBrush(Color.FromRgb(0xA0, 0xA0, 0xFF)));
    private static readonly Brush s_barOver     = Freeze(new SolidColorBrush(Color.FromRgb(0x70, 0x70, 0xDD)));

    private static Brush Freeze(SolidColorBrush b) { b.Freeze(); return b; }

    private static Brush CellBackground(int cellStatus, int hlv, DateTime date, Brush rowBase)
    {
        if (hlv >= 1) return s_bgWeekend;
        return rowBase;
    }

    private static Brush? BarBrush(int cellStatus) => ((ItemStatus)cellStatus) switch
    {
        ItemStatus.Skip     => s_barSkip,
        ItemStatus.Complete => s_barComplete,
        ItemStatus.Wait     => s_barWait,
        ItemStatus.Progress => s_barProgress,
        ItemStatus.Warning  => s_barWarning,
        ItemStatus.Error    => s_barError,
        ItemStatus.Over     => s_barOver,
        _                   => null,
    };

    private static string CellSymbol(int cellStatus, bool isToday)
    {
        if (!isToday) return string.Empty;
        return ((ItemStatus)cellStatus) switch
        {
            ItemStatus.Warning  => "▲",
            ItemStatus.Error    => "■",
            ItemStatus.Over     => "■",
            _ => string.Empty,
        };
    }

    // ── コマンド ─────────────────────────────────────────
    public ICommand ToggleExpandCommand { get; }
    public ICommand OpenMemoCommand     { get; }
    public ICommand OpenLinkCommand     { get; }
    public ICommand EditCommand         { get; }

    private void OpenLink()
    {
        var link = Item.Link;
        if (string.IsNullOrWhiteSpace(link)) return;
        try
        {
            if (System.IO.Directory.Exists(link))
            {
                // フォルダ → Explorer で開く
                System.Diagnostics.Process.Start("explorer.exe", link);
            }
            else
            {
                // ファイル・URL → 関連付けアプリで開く
                System.Diagnostics.Process.Start(
                    new System.Diagnostics.ProcessStartInfo(link) { UseShellExecute = true });
            }
        }
        catch { }
    }

    // ── 完了トグル ────────────────────────────────────────
    public void ToggleComplete()
    {
        if (Item is not ScheduleToDo todo) return;
        todo.Completed = !todo.Completed;
        _main.UpdateItemStatusAfterToggle(this);
        var entry = _main.FindEntryForItem(Item);
        if (entry is not null) entry.IsModified = true;
    }

    // ── 表示更新 ─────────────────────────────────────────
    public void Refresh()
    {
        OnPropertyChanged(nameof(Name));
        OnPropertyChanged(nameof(BeginText));
        OnPropertyChanged(nameof(EndText));
        OnPropertyChanged(nameof(DaysText));
        OnPropertyChanged(nameof(StatusBrush));
        OnPropertyChanged(nameof(StatusIcon));
        OnPropertyChanged(nameof(IsCompletedFolder));
        OnPropertyChanged(nameof(RowBackground));
        OnPropertyChanged(nameof(HasMemo));
        OnPropertyChanged(nameof(HasLink));
        OnPropertyChanged(nameof(ExpandGlyph));
        OnPropertyChanged(nameof(ChartCells));
    }

    // ── 吹き出し ─────────────────────────────────────────
    private ObservableCollection<CalloutViewModel> _callouts = new();
    public ObservableCollection<CalloutViewModel> Callouts => _callouts;

    /// <summary>吹き出し ViewModel を再構築する。</summary>
    public void RefreshCallouts()
    {
        if (Item is not ScheduleToDo todo)
        {
            _callouts.Clear();
            return;
        }

        var entry = _main.FindEntryForItem(Item);
        if (entry is null) return;

        _callouts.Clear();
        var chartStart = _main.ChartStart;
        int cellCount  = MainViewModel.CellCount;

        foreach (var c in todo.Callouts)
        {
            var vm = new CalloutViewModel(c, Item, _main, entry);
            vm.RowIndex = _rowIndex;
            vm.RefreshAnchorColumn(chartStart, cellCount);
            vm.VisibilityModeChanged = RebuildCalloutTexts;
            _callouts.Add(vm);
        }

        AdjustCalloutStackOffsets();
        RebuildCalloutTexts();

        OnPropertyChanged(nameof(Callouts));
        OnPropertyChanged(nameof(HasCallouts));
    }

    /// <summary>同一列で重なる吹き出しに縦方向スタックオフセットを割り当てる。</summary>
    private void AdjustCalloutStackOffsets()
    {
        var groups = _callouts
            .Where(c => c.AnchorColumnIndex >= 0)
            .GroupBy(c => c.AnchorColumnIndex);

        foreach (var group in groups)
        {
            int i = 0;
            foreach (var vm in group)
            {
                // i=0 が最下段（タスクバーに最も近い）、i が増えるほど上へ積む
                vm.StackOffsetY = i * CalloutViewModel.StackStep;
                i++;
            }
        }
    }

    public bool HasCallouts => Item is ScheduleToDo td && td.Callouts.Count > 0;

    private IReadOnlyDictionary<int, string> _calloutTexts = new Dictionary<int, string>();
    /// <summary>列インデックスをキーに、その列の吸き出しテキストを値に持つ辭書。赤∆マーカーのツールチップ用。</summary>
    public IReadOnlyDictionary<int, string> CalloutTexts => _calloutTexts;

    /// <summary>HoverOnly な吸き出しの列インデックス→テキスト辭書を再構築する。</summary>
    private void RebuildCalloutTexts()
    {
        var dict = new Dictionary<int, string>();
        foreach (var cvm in _callouts)
        {
            if (cvm.AnchorColumnIndex < 0) continue;
            if (cvm.VisibilityMode != Models.CalloutVisibilityMode.HoverOnly) continue;
            var date  = _main.ColumnIndexToDate(cvm.AnchorColumnIndex);
            var label = date.ToString("M/d");
            var text  = string.IsNullOrWhiteSpace(cvm.Text) ? "(空)" : cvm.Text;
            if (dict.TryGetValue(cvm.AnchorColumnIndex, out var existing))
                dict[cvm.AnchorColumnIndex] = existing + "\n" + text;
            else
                dict[cvm.AnchorColumnIndex] = $"[{label}] {text}";
        }
        _calloutTexts = dict;
        OnPropertyChanged(nameof(CalloutTexts));
    }

    // ── ホバー通知 ────────────────────────────────────────
    private bool _isChartRowHovered;
    public bool IsChartRowHovered
    {
        get => _isChartRowHovered;
        set
        {
            if (_isChartRowHovered == value) return;
            _isChartRowHovered = value;
            foreach (var c in _callouts)
                c.IsTaskHovered = value;
        }
    }
}
