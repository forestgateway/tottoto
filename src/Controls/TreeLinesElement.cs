using System.Windows;
using System.Windows.Media;
using todochart.ViewModels;

namespace todochart.Controls;

/// <summary>
/// タスクツリーの罫線（縦線・└・├）を DrawingContext で描画するカスタム要素。
/// 1 行分の TreeLineSegments を受け取り、深さ × CellWidth のキャンバスに描く。
/// </summary>
public class TreeLinesElement : FrameworkElement
{
    private Pen _linePen = new Pen(Brushes.Gray, 1.0);

    public TreeLinesElement()
    {
        UpdatePenFromResources();
        todochart.Services.ThemeService.ThemeChanged += () => { UpdatePenFromResources(); InvalidateVisual(); };
    }

    private void UpdatePenFromResources()
    {
        try
        {
            var res = Application.Current?.Resources;
            if (res != null)
            {
                // 優先: GridLineBrush -> SubTextBrush -> 固定フォールバック
                if (res.Contains("GridLineBrush") && res["GridLineBrush"] is Brush gb)
                    _linePen = new Pen(gb, 1.0);
                else if (res.Contains("SubTextBrush") && res["SubTextBrush"] is Brush sb)
                    _linePen = new Pen(sb, 1.0);
                else if (res.Contains("GridLine") && res["GridLine"] is Color gc)
                    _linePen = new Pen(new SolidColorBrush(gc), 1.0);
                else
                    _linePen = new Pen(new SolidColorBrush(Color.FromRgb(0xB0, 0xB0, 0xB0)), 1.0);
                _linePen.Freeze();
            }
        }
        catch { }
    }

    // ── 依存関係プロパティ ──────────────────────────────────
    public static readonly DependencyProperty SegmentsProperty =
        DependencyProperty.Register(nameof(Segments),
            typeof(IReadOnlyList<TreeLineSegment>), typeof(TreeLinesElement),
            new FrameworkPropertyMetadata(null,
                FrameworkPropertyMetadataOptions.AffectsRender |
                FrameworkPropertyMetadataOptions.AffectsMeasure));

    public static readonly DependencyProperty CellWidthProperty =
        DependencyProperty.Register(nameof(CellWidth), typeof(double), typeof(TreeLinesElement),
            new FrameworkPropertyMetadata(16.0,
                FrameworkPropertyMetadataOptions.AffectsRender |
                FrameworkPropertyMetadataOptions.AffectsMeasure));

    public IReadOnlyList<TreeLineSegment>? Segments
    {
        get => (IReadOnlyList<TreeLineSegment>?)GetValue(SegmentsProperty);
        set => SetValue(SegmentsProperty, value);
    }

    public double CellWidth
    {
        get => (double)GetValue(CellWidthProperty);
        set => SetValue(CellWidthProperty, value);
    }

    // ── サイズ計算 ──────────────────────────────────────────
    protected override Size MeasureOverride(Size availableSize)
    {
        int depth = Segments?.Count ?? 0;
        return new Size(depth * CellWidth, 0);
    }

    // ── 描画 ────────────────────────────────────────────────
    protected override void OnRender(DrawingContext dc)
    {
        var segs = Segments;
        if (segs is null || segs.Count == 0) return;

        double w   = CellWidth;
        double h   = ActualHeight;
        double mid = h / 2.0;

        foreach (var seg in segs)
        {
            double cx = seg.Level * w + w / 2.0;

            switch (seg.Kind)
            {
                case TreeLineKind.Vertical:
                    dc.DrawLine(_linePen, new Point(cx, 0), new Point(cx, h));
                    break;

                case TreeLineKind.Corner:
                    dc.DrawLine(_linePen, new Point(cx, 0),   new Point(cx, mid));
                    dc.DrawLine(_linePen, new Point(cx, mid), new Point(seg.Level * w + w, mid));
                    break;

                case TreeLineKind.Tee:
                    dc.DrawLine(_linePen, new Point(cx, 0),   new Point(cx, h));
                    dc.DrawLine(_linePen, new Point(cx, mid), new Point(seg.Level * w + w, mid));
                    break;

                case TreeLineKind.None:
                default:
                    break;
            }
        }
    }
}
