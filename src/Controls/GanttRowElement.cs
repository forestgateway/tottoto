using System.Globalization;
using System.Windows;
using System.Windows.Media;
using todochart.ViewModels;

namespace todochart.Controls;

/// <summary>
/// ガントチャートの 1 行分を DrawingContext で高速描画するカスタムコントロール。
/// 60 個の Border 要素を作る代わりにベクター描画する。
/// </summary>
public class GanttRowElement : FrameworkElement
{
    public static readonly DependencyProperty CellsProperty =
        DependencyProperty.Register(nameof(Cells),
            typeof(IReadOnlyList<ChartCellInfo>), typeof(GanttRowElement),
            new FrameworkPropertyMetadata(null,
                FrameworkPropertyMetadataOptions.AffectsRender |
                FrameworkPropertyMetadataOptions.AffectsMeasure));

    public static readonly DependencyProperty CellWidthProperty =
        DependencyProperty.Register(nameof(CellWidth), typeof(double), typeof(GanttRowElement),
            new FrameworkPropertyMetadata(16.0,
                FrameworkPropertyMetadataOptions.AffectsRender |
                FrameworkPropertyMetadataOptions.AffectsMeasure));

    public static readonly DependencyProperty HoveredColumnIndexProperty =
        DependencyProperty.Register(nameof(HoveredColumnIndex), typeof(int), typeof(GanttRowElement),
            new FrameworkPropertyMetadata(-1, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty IsSelectedProperty =
        DependencyProperty.Register(nameof(IsSelected), typeof(bool), typeof(GanttRowElement),
            new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty CalloutTextsProperty =
        DependencyProperty.Register(nameof(CalloutTexts),
            typeof(IReadOnlyDictionary<int, string>), typeof(GanttRowElement),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public IReadOnlyList<ChartCellInfo>? Cells
    {
        get => (IReadOnlyList<ChartCellInfo>?)GetValue(CellsProperty);
        set => SetValue(CellsProperty, value);
    }

    public double CellWidth
    {
        get => (double)GetValue(CellWidthProperty);
        set => SetValue(CellWidthProperty, value);
    }

    public int HoveredColumnIndex
    {
        get => (int)GetValue(HoveredColumnIndexProperty);
        set => SetValue(HoveredColumnIndexProperty, value);
    }

    public bool IsSelected
    {
        get => (bool)GetValue(IsSelectedProperty);
        set => SetValue(IsSelectedProperty, value);
    }

    public IReadOnlyDictionary<int, string>? CalloutTexts
    {
        get => (IReadOnlyDictionary<int, string>?)GetValue(CalloutTextsProperty);
        set => SetValue(CalloutTextsProperty, value);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        int count = Cells?.Count ?? 0;
        return new Size(count * CellWidth, 22);
    }

    private Brush _hoverBrush    = Brushes.Transparent;
    private Brush _selectedBrush = Brushes.Transparent;
    private Pen   _selectedPen   = new Pen(Brushes.Transparent, 1.0);
    private Pen   _weekPen       = new Pen(Brushes.Transparent, 1.0);
    private Pen   _rowPen        = new Pen(Brushes.Transparent, 1.0);
    private Brush _todayOverlay  = Brushes.Transparent;
    private static readonly Brush s_calloutMarker = new SolidColorBrush(Color.FromRgb(0xFF, 0x00, 0x00));

    public GanttRowElement()
    {
        UpdateBrushesFromResources();
        todochart.Services.ThemeService.ThemeChanged += () => { UpdateBrushesFromResources(); InvalidateVisual(); };
    }

    private void UpdateBrushesFromResources()
    {
        try
        {
            var res = Application.Current?.Resources;
            if (res is null) return;

            Color accent = res["AccentColor"] is Color ac ? ac : Color.FromRgb(0x00, 0x78, 0xD7);
            Color sub = res["SubText"] is Color sc ? sc : Color.FromRgb(0x3A, 0x3A, 0x3A);

            // ブラシは可能ならテーマで定義されたブラシを利用する
            if (res.Contains("AccentBrush") && res["AccentBrush"] is Brush ab)
            {
                _hoverBrush = new SolidColorBrush(((SolidColorBrush)ab).Color) { Opacity = 0.1 };
                _selectedBrush = new SolidColorBrush(((SolidColorBrush)ab).Color) { Opacity = 0.48 };
                _selectedPen = new Pen(ab, 1.0);
            }
            else
            {
                _hoverBrush    = new SolidColorBrush(Color.FromArgb(0x1A, accent.R, accent.G, accent.B));
                _selectedBrush = new SolidColorBrush(Color.FromArgb(0x7A, (byte)Math.Min(accent.R + 0x4D, 255), (byte)Math.Min(accent.G + 0x5E, 255), (byte)Math.Min(accent.B + 0xFF, 255)));
                _selectedPen   = new Pen(new SolidColorBrush(Color.FromArgb(0xFF, accent.R, accent.G, accent.B)), 1.0);
            }

            // 週境界線の色: テーマの WeekLine / WeekLineBrush を優先して使用する
            if (res.Contains("WeekLine") && res["WeekLine"] is Color wl)
            {
                _weekPen = new Pen(new SolidColorBrush(wl), 1.0);
            }
            else if (res.Contains("WeekLineBrush") && res["WeekLineBrush"] is Brush wlbr)
            {
                _weekPen = new Pen(wlbr, 1.0);
            }

            // 行下線・副線の設定: GridLineBrush があれば優先し、なければ SubTextBrush を利用
            if (res.Contains("GridLineBrush") && res["GridLineBrush"] is Brush glb)
            {
                _rowPen = new Pen(glb, 1.0);
            }
            else if (res.Contains("SubTextBrush") && res["SubTextBrush"] is Brush sb)
            {
                // SubTextBrush を行下線用に使う
                _rowPen = new Pen(sb, 1.0);
                // 未指定の場合のみ weekPen を SubTextBrush にフォールバック
                if (_weekPen == null || (_weekPen.Brush is SolidColorBrush scb && scb.Color == default))
                    _weekPen = new Pen(sb, 1.0);
            }
            else
            {
                // フォールバックカラー
                _weekPen = _weekPen ?? new Pen(new SolidColorBrush(Color.FromArgb(0x33, sub.R, sub.G, sub.B)), 1.0);
                _rowPen  = new Pen(new SolidColorBrush(Color.FromArgb(0x15, sub.R, sub.G, sub.B)), 1.0);
            }

            if (res.Contains("TodayOverlayBrush") && res["TodayOverlayBrush"] is Brush to)
                _todayOverlay = to;
            else
                _todayOverlay  = new SolidColorBrush(Color.FromArgb(0x33, 0xFF, 0x88, 0x88));
        }
        catch { }
    }

    protected override void OnRender(DrawingContext dc)
    {
        if (Cells is null) return;

        var calloutTexts = CalloutTexts;
        double x = 0;
        double cw = CellWidth;
        double h = ActualHeight > 0 ? ActualHeight : 22;
        double dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;
        int hoverCol = HoveredColumnIndex;
        bool isSelected = IsSelected;

        double snapOffset = 0.5 / dpi;

        double todayX = -1;

        for (int i = 0; i < Cells.Count; i++)
        {
            var c = Cells[i];
            var rect = new Rect(x, 0, cw, h);

            // セル背景（横縞・土日）
            dc.DrawRectangle(c.Background, null, rect);


            // タスクバー：セルより上下3px・左右2px小さい正方形
            if (c.BarBrush is not null)
            {
                const double marginX = 0.6;
                const double marginY = 2.0;
                var barRect = new Rect(x + marginX, marginY, cw - marginX * 2, h - marginY * 2);
                dc.DrawRectangle(c.BarBrush, null, barRect);
            }

            // 日曜と月曜の間に縦線（月曜セルの左端）
            // 週境界線は選択色より下に描画するため、この位置で描画する。
            if (c.Date.DayOfWeek == DayOfWeek.Monday)
            {
                double lx = x + snapOffset;
                dc.DrawLine(_weekPen, new Point(lx, 0), new Point(lx, h));
            }

            // 選択色・ホバー色は週線より上に重ねる（選択色レイヤを最前面にする）
            if (isSelected)
                dc.DrawRectangle(_selectedBrush, null, rect);
            if (i == hoverCol)
                dc.DrawRectangle(_hoverBrush, null, rect);

            if (!string.IsNullOrEmpty(c.Symbol))
            {
                var ft = new FormattedText(c.Symbol,
                    System.Globalization.CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
                    new Typeface("Segoe UI"), 9, c.Foreground, dpi);
                dc.DrawText(ft, new Point(x + (cw - ft.Width) / 2, (h - ft.Height) / 2));
            }

            if (c.IsToday) todayX = x;

            // 吸き出しマーカー：セル右上に赤小三角（Excel コメントインジケーター类似）
            if (calloutTexts is not null && calloutTexts.ContainsKey(i))
            {
                const double ts = 5.0;
                var geo = new StreamGeometry();
                using (var ctx = geo.Open())
                {
                    ctx.BeginFigure(new Point(x + cw - ts, 0), isFilled: true, isClosed: true);
                    ctx.LineTo(new Point(x + cw, 0),  isStroked: false, isSmoothJoin: false);
                    ctx.LineTo(new Point(x + cw, ts), isStroked: false, isSmoothJoin: false);
                }
                geo.Freeze();
                dc.DrawGeometry(s_calloutMarker, null, geo);
            }

            x += cw;
        }

        // 行下端に横線（_rowPen: GridLineBrush またはフォールバック）
        double totalW = Cells.Count * cw;
        double ly = h - snapOffset;
        dc.DrawLine(_rowPen, new Point(0, ly), new Point(totalW, ly));

        // 今日列のオーバーレイを最後に重ねる（タスクバーの色を残しつつ薄い赤を載せる）
        if (todayX >= 0)
            dc.DrawRectangle(_todayOverlay, null, new Rect(todayX, 0, cw, h));

        // 選択時に外枠線を描画（上下1px、完全不透明の選択色）
        if (isSelected)
        {
            double selW = Cells.Count * cw;
            dc.DrawLine(_selectedPen, new Point(0, snapOffset),     new Point(selW, snapOffset));
            dc.DrawLine(_selectedPen, new Point(0, h - snapOffset), new Point(selW, h - snapOffset));
        }
    }

    // No hover selection: intentionally left empty to avoid selecting rows on mouse over.
    protected override void OnMouseEnter(System.Windows.Input.MouseEventArgs e)
    {
        base.OnMouseEnter(e);
        // Intentionally do nothing on hover per user request.
    }

    protected override void OnMouseMove(System.Windows.Input.MouseEventArgs e)
    {
        base.OnMouseMove(e);
        var texts = CalloutTexts;
        if (texts is null || texts.Count == 0) { ToolTip = null; return; }

        double cw  = CellWidth;
        int    col = (int)(e.GetPosition(this).X / cw);
        ToolTip = texts.TryGetValue(col, out var tip) ? tip : null;
    }

    protected override void OnMouseLeftButtonDown(System.Windows.Input.MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);

        if (DataContext is TaskRowViewModel row)
        {
            try
            {
                var wnd = Application.Current?.MainWindow;
                if (wnd?.DataContext is todochart.ViewModels.MainViewModel vm)
                {
                    // Select the row on click
                    vm.Selected = row;

                    // Double-click opens properties
                    if (e.ClickCount == 2 && vm.EditCommand?.CanExecute(null) == true)
                    {
                        vm.EditCommand.Execute(null);
                    }
                }
                else
                {
                    // Fallback selection
                    row.IsSelected = true;
                    if (e.ClickCount == 2)
                    {
                        var editCmd = row?.GetType().GetProperty("EditCommand")?.GetValue(row) as System.Windows.Input.ICommand;
                        if (editCmd?.CanExecute(null) == true) editCmd.Execute(null);
                    }
                }
            }
            catch { }
        }

        // Do not mark event handled so parent ScrollViewer can still start panning when dragging
    }
}
