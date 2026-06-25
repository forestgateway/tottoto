using System;
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

    private static bool BrushesEqual(Brush? a, Brush? b)
    {
        if (ReferenceEquals(a, b)) return true;
        if (a is null || b is null) return false;
        if (a is SolidColorBrush sa && b is SolidColorBrush sb)
            return sa.Color == sb.Color;
        // Fallback: compare string representations (Brush.ToString includes color for solid brushes)
        return string.Equals(a.ToString(), b.ToString(), StringComparison.Ordinal);
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
        double rowH = 22;
        try
        {
            var val = TryFindResource("RowHeight");
            if (val is double rh) rowH = rh;
        }
        catch { }
        return new Size(count * CellWidth, rowH);
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
        todochart.Services.ThemeService.ThemeChanged += () => { UpdateBrushesFromResources(); InvalidateMeasure(); InvalidateVisual(); };
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
        double defaultH = 22;
        try
        {
            var res = Application.Current?.Resources;
            if (res != null && res.Contains("RowHeight") && res["RowHeight"] is double rh2)
                defaultH = rh2;
        }
        catch { }
        double h = ActualHeight > 0 ? ActualHeight : defaultH;
        double dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;
        int hoverCol = HoveredColumnIndex;
        bool isSelected = IsSelected;

        double snapOffset = 0.5 / dpi;

        double todayX = -1;


        // ガントバーの描画: 隣接する同色のセルをまとめて 1 つの角丸矩形で描画します。
        // これによりセル間の微小な隙間を防ぎ、角丸・高さをテーマリソースで制御できます。
        double defaultBarHeight = Math.Max(0, h - 4.0);
        double cornerRadius = 3.0;
        double horizontalGap = 0.0; // セル間の横マージン (0 にすれば隙間なし)
        try
        {
            var val1 = TryFindResource("GanttProgressBarHeight");
            if (val1 is double bh) defaultBarHeight = bh;
            var val2 = TryFindResource("GanttProgressCornerRadius");
            if (val2 is double cr) cornerRadius = cr;
            var val3 = TryFindResource("GanttProgressHorizontalGap");
            if (val3 is double hg) horizontalGap = hg;
        }
        catch { }

        // Draw row base (striped background) first so overlays (weekend/holiday) can be drawn above it.
        double totalW = Cells.Count * cw;
        if (Cells.Count > 0)
        {
            var first = Cells[0];
            if (first.RowBase is not null)
                dc.DrawRectangle(first.RowBase, null, new Rect(0, 0, totalW, h));
        }

        int i = 0;
        while (i < Cells.Count)
        {
            var c = Cells[i];
            var rect = new Rect(x, 0, cw, h);

            // オーバーレイ（週末・祝日の色）は行ベースの縞の上に描画する
            if (c.OverlayBrush is not null)
            {
                dc.DrawRectangle(c.OverlayBrush, null, rect);
            }

            if (c.BarBrush is not null)
            {
                // 連続する同一色のセルをまとめる
                int j = i + 1;
                while (j < Cells.Count && BrushesEqual(Cells[j].BarBrush, c.BarBrush)) j++;

                double startXAbs = x; // セグメントの先頭セルの x
                double startX = startXAbs + horizontalGap / 2.0;
                double segmentWidth = (j - i) * cw - horizontalGap;
                double barHeight = Math.Min(defaultBarHeight, h);
                double marginY = Math.Max(0, (h - barHeight) / 2.0);

                // セグメントにまたがるセルのオーバーレイ（週末・祝日）を各セルごとに描画する
                for (int m = i; m < j; m++)
                {
                    var oc = Cells[m];
                    if (oc.OverlayBrush is not null)
                    {
                        double cellX = startXAbs + (m - i) * cw;
                        dc.DrawRectangle(oc.OverlayBrush, null, new Rect(cellX, 0, cw, h));
                    }
                }

                var barRect = new Rect(startX, marginY, segmentWidth, barHeight);
                // 角丸: 開始セルと終了セルのみ角丸にする。ChartCellInfo の IsTaskStart/IsTaskEnd を参照。
                // セグメント単位で開始・終了判定を行い、部分的に角丸を描画する。
                bool segStartRounded = Cells[i].IsTaskStart;
                bool segEndRounded = Cells[j - 1].IsTaskEnd;

                double capRadius = Math.Max(0.0, cornerRadius);
                double maxCap = barHeight / 2.0;
                if (capRadius > maxCap) capRadius = maxCap;
                if (segmentWidth < capRadius * 2.0) capRadius = segmentWidth / 2.0;

                // 中央矩形幅
                double coreWidth = Math.Max(0.0, segmentWidth - capRadius * 2.0);
                double leftCap = segStartRounded ? capRadius : 0.0;
                double rightCap = segEndRounded ? capRadius : 0.0;

                double coreX = startX + leftCap;
                double coreW = Math.Max(0.0, segmentWidth - leftCap - rightCap);
                if (coreW > 0)
                    dc.DrawRectangle(c.BarBrush, null, new Rect(coreX, marginY, coreW, barHeight));

                // 左キャップ（丸める場合）
                if (segStartRounded)
                {
                    var leftCenter = new Point(startX + capRadius, marginY + barHeight / 2.0);
                    dc.DrawEllipse(c.BarBrush, null, leftCenter, capRadius, barHeight / 2.0);
                }
                else if (leftCap == 0 && capRadius > 0 && coreW == segmentWidth)
                {
                    // 角丸無効時でも端が欠けないようコーナーを埋める（何もしない）
                }

                // 右キャップ（丸める場合）
                if (segEndRounded)
                {
                    var rightCenter = new Point(startX + segmentWidth - capRadius, marginY + barHeight / 2.0);
                    dc.DrawEllipse(c.BarBrush, null, rightCenter, capRadius, barHeight / 2.0);
                }

                // セグメント内の各セルに対して週境界線・選択・ホバー・シンボル・Today マーカー・吹き出しを描画
                for (int k = i; k < j; k++)
                {
                    var ck = Cells[k];
                    double cellX = startXAbs + (k - i) * cw;
                    var cellRect = new Rect(cellX, 0, cw, h);

                    if (ck.Date.DayOfWeek == DayOfWeek.Monday)
                    {
                        double lx = cellX + snapOffset;
                        dc.DrawLine(_weekPen, new Point(lx, 0), new Point(lx, h));
                    }

                    if (isSelected)
                        dc.DrawRectangle(_selectedBrush, null, cellRect);
                    if (k == hoverCol)
                        dc.DrawRectangle(_hoverBrush, null, cellRect);

                    if (!string.IsNullOrEmpty(ck.Symbol))
                    {
                        var ft = new FormattedText(ck.Symbol,
                            System.Globalization.CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
                            new Typeface("Segoe UI"), 9, ck.Foreground, dpi);
                        dc.DrawText(ft, new Point(cellX + (cw - ft.Width) / 2, (h - ft.Height) / 2));
                    }

                    if (ck.IsToday) todayX = cellX;

                    if (calloutTexts is not null && calloutTexts.ContainsKey(k))
                    {
                        const double ts = 5.0;
                        var geo = new StreamGeometry();
                        using (var ctx = geo.Open())
                        {
                            ctx.BeginFigure(new Point(cellX + cw - ts, 0), isFilled: true, isClosed: true);
                            ctx.LineTo(new Point(cellX + cw, 0), isStroked: false, isSmoothJoin: false);
                            ctx.LineTo(new Point(cellX + cw, ts), isStroked: false, isSmoothJoin: false);
                        }
                        geo.Freeze();
                        dc.DrawGeometry(s_calloutMarker, null, geo);
                    }
                }

                // advance i and x by the merged segment
                x += (j - i) * cw;
                i = j;
                continue; // skip the usual single-step x += cw below
            }

            // バーが無いセルはそのまま処理（週線・選択・ホバー・シンボル等）
            if (c.Date.DayOfWeek == DayOfWeek.Monday)
            {
                double lx = x + snapOffset;
                dc.DrawLine(_weekPen, new Point(lx, 0), new Point(lx, h));
            }

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
            i++;
        }

        // 行下端に横線（_rowPen: GridLineBrush またはフォールバック）
        // totalW はループ前に計算済み
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
