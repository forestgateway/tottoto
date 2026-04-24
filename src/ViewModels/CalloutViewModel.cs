using System.Windows.Input;
using todochart.Models;

namespace todochart.ViewModels;

/// <summary>
/// 1 件の吹き出しを表示・編集するための ViewModel。
/// </summary>
public class CalloutViewModel : ViewModelBase
{
    private readonly Callout          _callout;
    private readonly ScheduleItemBase _task;
    private readonly MainViewModel    _main;
    private readonly ScheduleEntry    _entry;

    public Callout            Model => _callout;
    public ScheduleItemBase   Task  => _task;

    public CalloutViewModel(Callout callout, ScheduleItemBase task,
                            MainViewModel main, ScheduleEntry entry)
    {
        _callout = callout;
        _task    = task;
        _main    = main;
        _entry   = entry;

        BeginEditCommand  = new RelayCommand(BeginEdit);
        CommitEditCommand = new RelayCommand(CommitEdit);
        CancelEditCommand = new RelayCommand(CancelEdit);
        DeleteCommand     = new RelayCommand(Delete);

        SetPositionModeAbsoluteCommand = new RelayCommand(() => PositionMode = CalloutPositionMode.AbsoluteDateTime);
        SetPositionModeStartCommand    = new RelayCommand(() => PositionMode = CalloutPositionMode.StartDate);
        SetPositionModeEndCommand      = new RelayCommand(() => PositionMode = CalloutPositionMode.EndDate);

        ToggleVisibilityModeCommand = new RelayCommand(ToggleVisibilityMode);
    }

    // ── テキスト ───────────────────────────────────────────────
    public string Text
    {
        get => _callout.Text;
        set
        {
            if (_callout.Text == value) return;
            _callout.Text = value;
            _callout.UpdatedAt = DateTime.Now;
            OnPropertyChanged();
            OnPropertyChanged(nameof(TextLineCount));
            OnPropertyChanged(nameof(DynBubbleH));
            OnPropertyChanged(nameof(TailRootRelY));
            OnPropertyChanged(nameof(TipRelY));
            OnPropertyChanged(nameof(TipDotTop));
            OnPropertyChanged(nameof(DynCanvasH));
            OnPropertyChanged(nameof(AbsoluteTop));
            _entry.IsModified = true;
        }
    }

    // ── 編集モード ─────────────────────────────────────────────
    private bool _isEditing;
    public bool IsEditing
    {
        get => _isEditing;
        private set => SetField(ref _isEditing, value);
    }

    private string _editingText = string.Empty;
    public string EditingText
    {
        get => _editingText;
        set => SetField(ref _editingText, value);
    }

    public void BeginEdit()
    {
        _editingText = _callout.Text;
        OnPropertyChanged(nameof(EditingText));
        IsEditing = true;
    }

    public void CommitEdit()
    {
        if (!IsEditing) return;
        IsEditing = false;
        Text = EditingText;
    }

    public void CancelEdit()
    {
        if (!IsEditing) return;
        IsEditing = false;
    }

    // ── 表示位置モード ─────────────────────────────────────────
    public CalloutPositionMode PositionMode
    {
        get => _callout.PositionMode;
        set
        {
            if (_callout.PositionMode == value) return;

            // モード変更前の指し示す日付を保持する
            DateTime? anchorDate = _callout.ComputeAnchorDate(_task);

            _callout.PositionMode = value;

            // 指し示す日付が変わらないようにオフセット／絶対日付を再計算する
            if (anchorDate.HasValue)
            {
                switch (value)
                {
                    case CalloutPositionMode.AbsoluteDateTime:
                        _callout.AbsoluteDateTime = anchorDate.Value;
                        break;
                    case CalloutPositionMode.StartDate:
                        _callout.OffsetDays = _task.BeginDate.HasValue
                            ? (int)(anchorDate.Value.Date - _task.BeginDate.Value.Date).TotalDays
                            : 0;
                        break;
                    case CalloutPositionMode.EndDate:
                        _callout.OffsetDays = _task.EndDate.HasValue
                            ? (int)(anchorDate.Value.Date - _task.EndDate.Value.Date).TotalDays
                            : 0;
                        break;
                }
            }

            _callout.UpdatedAt = DateTime.Now;
            OnPropertyChanged();
            OnPropertyChanged(nameof(PositionModeLabel));
            OnPropertyChanged(nameof(IsPositionModeAbsolute));
            OnPropertyChanged(nameof(IsPositionModeStart));
            OnPropertyChanged(nameof(IsPositionModeEnd));
            RefreshAnchorColumn();
            _entry.IsModified = true;
        }
    }

    public string PositionModeLabel => PositionMode switch
    {
        CalloutPositionMode.AbsoluteDateTime => "日時固定",
        CalloutPositionMode.StartDate        => "開始日",
        CalloutPositionMode.EndDate          => "終了日",
        _                                    => string.Empty,
    };

    public bool IsPositionModeAbsolute => PositionMode == CalloutPositionMode.AbsoluteDateTime;
    public bool IsPositionModeStart    => PositionMode == CalloutPositionMode.StartDate;
    public bool IsPositionModeEnd      => PositionMode == CalloutPositionMode.EndDate;

    // ── 表示モード ─────────────────────────────────────────────
    /// <summary>VisibilityMode 変更時に親 TaskRowViewModel へ通知するコールバック。</summary>
    internal Action? VisibilityModeChanged { get; set; }

    public CalloutVisibilityMode VisibilityMode
    {
        get => _callout.VisibilityMode;
        set
        {
            if (_callout.VisibilityMode == value) return;
            _callout.VisibilityMode = value;
            _callout.UpdatedAt = DateTime.Now;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsHoverOnly));
            OnPropertyChanged(nameof(VisibilityModeToggleHeader));
            _entry.IsModified = true;
            VisibilityModeChanged?.Invoke();
        }
    }

    public bool IsHoverOnly => VisibilityMode == CalloutVisibilityMode.HoverOnly;

    public string VisibilityModeToggleHeader =>
        IsHoverOnly ? "常に表示（AlwaysVisible）" : "自動的に隠す（HoverOnly）";

    private void ToggleVisibilityMode()
    {
        VisibilityMode = IsHoverOnly
            ? CalloutVisibilityMode.AlwaysVisible
            : CalloutVisibilityMode.HoverOnly;
    }

    // ── チャート列インデックス（表示位置） ─────────────────────
    public const double CellWidth          = 22.0;
    public const double RowHeight          = 24.0;
    public const double CalloutBubbleWidth = 160.0;

    // ── BubbleLine レイアウト定数 ──────────────────────────────
    // ●(先端) = セル中心。●から 45°右上に TailLength px が尻尾根元。
    // 根元から左に TailOffset px が吹き出し左下端。
    public  const double TailLength  = 25.0;
    public  const double TailOffset  = 10.0;
    private const double FontLineH   = 16.0; // FontSize=11 の行高近似
    private const double BubblePadV  = 6.0;  // 吹き出し上下パディング合計
    private static readonly double Sqrt2 = Math.Sqrt(2);

    /// <summary>テキストの行数（\n で分割）。</summary>
    public int TextLineCount =>
        string.IsNullOrEmpty(_callout.Text) ? 1
        : Math.Max(1, _callout.Text.Split('\n').Length);

    /// <summary>テキスト行数に応じた吹き出し本体の高さ。</summary>
    public double DynBubbleH => TextLineCount * FontLineH + BubblePadV;

    /// <summary>尻尾根元の Canvas 相対 X（= 吹き出し左端 + TailOffset）。</summary>
    public double TailRootRelX => TailOffset;

    /// <summary>尻尾先端(● 中心)の Canvas 相対 X。</summary>
    public double TipRelX => TailOffset - TailLength / Sqrt2;

    /// <summary>Ellipse の Canvas.Left（中心 X - 半径 4px）。</summary>
    public double TipDotLeft => TipRelX - 4.0;

    /// <summary>尻尾根元の Canvas 相対 Y（= 吹き出し下辺）。</summary>
    public double TailRootRelY => DynBubbleH;

    /// <summary>尻尾先端(● 中心)の Canvas 相対 Y。</summary>
    public double TipRelY => DynBubbleH + TailLength / Sqrt2;

    /// <summary>Ellipse の Canvas.Top（中心 Y - 半径 4px）。</summary>
    public double TipDotTop => TipRelY - 4.0;

    /// <summary>Canvas 全体の高さ。</summary>
    public double DynCanvasH => TipRelY + 6.0;

    /// <summary>尻尾領域の高さ（Grid 2行目用）。</summary>
    public double TailExtraH => TailLength / Sqrt2 + 6.0;

    public const double StackStep = TailLength + 6.0;

    private int _anchorColumnIndex = -1;
    public int AnchorColumnIndex
    {
        get => _anchorColumnIndex;
        private set
        {
            if (SetField(ref _anchorColumnIndex, value))
                OnPropertyChanged(nameof(AnchorColumnLeft));
        }
    }

    private double _stackOffsetY;
    /// <summary>同一列で重なる吹き出しを上方向に積む際の追加オフセット（正 = より上へ）。</summary>
    public double StackOffsetY
    {
        get => _stackOffsetY;
        set
        {
            if (SetField(ref _stackOffsetY, value))
                OnPropertyChanged(nameof(AbsoluteTop));
        }
    }

    private int _rowIndex;
    public int RowIndex
    {
        get => _rowIndex;
        set
        {
            if (SetField(ref _rowIndex, value))
                OnPropertyChanged(nameof(AbsoluteTop));
        }
    }

    /// <summary>
    /// 吹き出しの Canvas.Left。
    /// tipX + L/√2 = 尻尾根元X、吹き出し左端 = 根元X − TailOffset。
    /// </summary>
    public double AnchorColumnLeft
    {
        get
        {
            if (_anchorColumnIndex < 0) return -9999;
            double tipX = _anchorColumnIndex * CellWidth + CellWidth / 2.0;
            return tipX + TailLength / Sqrt2 - TailOffset;
        }
    }

    /// <summary>
    /// 吹き出しの Canvas.Top。
    /// tipY − L/√2 = 尻尾根元Y、吹き出し上端 = 根元Y − DynBubbleH。
    /// </summary>
    public double AbsoluteTop
    {
        get
        {
            double tipY = _rowIndex * RowHeight + RowHeight / 2.0;
            return tipY - TailLength / Sqrt2 - DynBubbleH - _stackOffsetY;
        }
    }

    /// <summary>
    /// チャートの表示開始日を基にアンカー列インデックスを再計算する。
    /// </summary>
    public void RefreshAnchorColumn(DateTime? chartStart = null, int cellCount = 0)
    {
        if (chartStart is null)
        {
            chartStart = _main.ChartStart;
            cellCount  = MainViewModel.CellCount;
        }

        var anchorDate = _callout.ComputeAnchorDate(_task);
        if (anchorDate is null)
        {
            AnchorColumnIndex = -1;
            return;
        }

        int col = (int)(anchorDate.Value.Date - chartStart.Value.Date).TotalDays;
        AnchorColumnIndex = (col >= 0 && col < cellCount) ? col : -1;
    }

    // ── ホバー状態（HoverOnly の表示制御） ────────────────────
    private bool _isTaskHovered;
    public bool IsTaskHovered
    {
        get => _isTaskHovered;
        set
        {
            if (SetField(ref _isTaskHovered, value))
                OnPropertyChanged(nameof(IsVisible));
        }
    }

    private bool _isTaskSelected;
    public bool IsTaskSelected
    {
        get => _isTaskSelected;
        set
        {
            if (SetField(ref _isTaskSelected, value))
                OnPropertyChanged(nameof(IsVisible));
        }
    }

    public bool IsVisible =>
        VisibilityMode == CalloutVisibilityMode.AlwaysVisible || _isTaskHovered || _isTaskSelected;

    // ── 日付移動（ドラッグ用） ────────────────────────────────
    /// <summary>
    /// アンカー日付を <paramref name="days"/> 日分ずらす。
    /// PositionMode に応じて AbsoluteDateTime または OffsetDays を更新する。
    /// </summary>
    public void MoveByDays(int days)
    {
        if (days == 0) return;
        switch (_callout.PositionMode)
        {
            case CalloutPositionMode.AbsoluteDateTime:
                if (_callout.AbsoluteDateTime.HasValue)
                    _callout.AbsoluteDateTime = _callout.AbsoluteDateTime.Value.AddDays(days);
                break;
            case CalloutPositionMode.StartDate:
            case CalloutPositionMode.EndDate:
                _callout.OffsetDays += days;
                break;
        }
        _callout.UpdatedAt = DateTime.Now;
        _entry.IsModified  = true;
        RefreshAnchorColumn();
    }

    // ── 削除 ───────────────────────────────────────────────────
    private void Delete()
    {
        if (_task is ScheduleToDo todo)
        {
            todo.Callouts.Remove(_callout);
            _entry.IsModified = true;
            _main.RefreshCalloutsForTask(todo);
        }
    }

    // ── コマンド ───────────────────────────────────────────────
    public ICommand BeginEditCommand  { get; }
    public ICommand CommitEditCommand { get; }
    public ICommand CancelEditCommand { get; }
    public ICommand DeleteCommand     { get; }

    public ICommand SetPositionModeAbsoluteCommand { get; }
    public ICommand SetPositionModeStartCommand    { get; }
    public ICommand SetPositionModeEndCommand      { get; }

    public ICommand ToggleVisibilityModeCommand { get; }
}
