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

    private static readonly Brush s_hoverBrush    = new SolidColorBrush(Color.FromArgb(0x22, 0x00, 0xC8, 0xFF));
    private static readonly Brush s_selectedBrush = new SolidColorBrush(Color.FromArgb(0x55, 0x00, 0xC8, 0xFF));
    private static readonly Pen   s_selectedPen   = new(new SolidColorBrush(Color.FromArgb(0xFF, 0x00, 0xC8, 0xFF)), 1.0);
    private static readonly Pen   s_weekPen       = new(new SolidColorBrush(Color.FromArgb(0x44, 0x00, 0xC8, 0xFF)), 1.0);
    private static readonly Pen   s_rowPen        = new(new SolidColorBrush(Color.FromArgb(0x30, 0xAA, 0xBB, 0xCC)), 1.0);
    private static readonly Brush s_todayOverlay  = new SolidColorBrush(Color.FromArgb(0x30, 0x00, 0xC8, 0xFF));
    private static readonly Brush s_calloutMarker = new SolidColorBrush(Color.FromRgb(0xFF, 0x44, 0x44));

    static GanttRowElement()
    {
        s_hoverBrush.Freeze();
        s_selectedBrush.Freeze();
        ((SolidColorBrush)s_selectedPen.Brush).Freeze();
        s_selectedPen.Freeze();
        ((SolidColorBrush)s_weekPen.Brush).Freeze();
        s_weekPen.Freeze();
        ((SolidColorBrush)s_rowPen.Brush).Freeze();
        s_rowPen.Freeze();
        ((SolidColorBrush)s_todayOverlay).Freeze();
        s_calloutMarker.Freeze();
    }

    protected override void OnRender(DrawingContext dc)
    {
        if (Cells is null) return;

        var calloutTexts = CalloutTexts;
        double cw = CellWidth;
        double h = ActualHeight > 0 ? ActualHeight : 22;
        double dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;
        int hoverCol = HoveredColumnIndex;
        bool isSelected = IsSelected;
        double snapOffset = 0.5 / dpi;
        double todayX = -1;

        // ── Pass 1: セル背景 ────────────────────────────────────────
        double x = 0;
        for (int i = 0; i < Cells.Count; i++, x += cw)
            dc.DrawRectangle(Cells[i].Background, null, new Rect(x, 0, cw, h));

        // ── タスクバー: 連続セルをセグメントとして連結・端を角丸で描画 ──
        {
            double barH   = (h - 4.0) * 0.8;
            double barTop = (h - barH) / 2.0;
            const double barRadius = 3.0;

            x = 0;
            Brush? segBrush  = null;
            double segStartX = 0;
            for (int i = 0; i < Cells.Count; i++, x += cw)
            {
                var bb = Cells[i].BarBrush;
                if (bb != segBrush)
                {
                    if (segBrush is not null)
                        dc.DrawRoundedRectangle(segBrush, null,
                            new Rect(segStartX, barTop, x - segStartX, barH),
                            barRadius, barRadius);
                    segBrush  = bb;
                    segStartX = x;
                }
            }
            if (segBrush is not null)
                dc.DrawRoundedRectangle(segBrush, null,
                    new Rect(segStartX, barTop, x - segStartX, barH),
                    barRadius, barRadius);
        }

        // ── Pass 2: 選択・ホバー・縦線・記号・コールアウト ──────────
        x = 0;
        for (int i = 0; i < Cells.Count; i++, x += cw)
        {
            var c    = Cells[i];
            var rect = new Rect(x, 0, cw, h);

            if (isSelected)   dc.DrawRectangle(s_selectedBrush, null, rect);
            if (i == hoverCol) dc.DrawRectangle(s_hoverBrush,   null, rect);

            // 日曜と月曜の間に縦線（月曜セルの左端）
            if (c.Date.DayOfWeek == DayOfWeek.Monday)
            {
                double lx = x + snapOffset;
                dc.DrawLine(s_weekPen, new Point(lx, 0), new Point(lx, h));
            }

            if (!string.IsNullOrEmpty(c.Symbol))
            {
                var ft = new FormattedText(c.Symbol,
                    System.Globalization.CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
                    new Typeface("Segoe UI"), 9, c.Foreground, dpi);
                dc.DrawText(ft, new Point(x + (cw - ft.Width) / 2, (h - ft.Height) / 2));
            }

            if (c.IsToday) todayX = x;

            // 吸き出しマーカー：セル右上に赤小三角
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
        }

        // 行下端に横線
        double totalW = Cells.Count * cw;
        double ly = h - snapOffset;
        dc.DrawLine(s_rowPen, new Point(0, ly), new Point(totalW, ly));

        // 今日列のオーバーレイを最後に重ねる
        if (todayX >= 0)
            dc.DrawRectangle(s_todayOverlay, null, new Rect(todayX, 0, cw, h));

        // 選択時に外枠線を描画
        if (isSelected)
        {
            double selW = Cells.Count * cw;
            dc.DrawLine(s_selectedPen, new Point(0, snapOffset),     new Point(selW, snapOffset));
            dc.DrawLine(s_selectedPen, new Point(0, h - snapOffset), new Point(selW, h - snapOffset));
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
