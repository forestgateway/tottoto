using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;

namespace todochart.Controls;

/// <summary>
/// ドラッグ&amp;ドロップ時のドロップ位置を示す Adorner。
/// 挿入線（上/下）またはハイライト枠（子として追加）を描画する。
/// </summary>
public class DropIndicatorAdorner : Adorner
{
    private Pen _linePen = new Pen(new SolidColorBrush(Color.FromRgb(0x00, 0x78, 0xD7)), 2.0);
    private Brush _fillBrush = new SolidColorBrush(Color.FromArgb(40, 0, 120, 215));

    public DropIndicatorAdorner(UIElement adornedElement) : base(adornedElement)
    {
        IsHitTestVisible = false;
        UpdateBrushesFromResources();
        todochart.Services.ThemeService.ThemeChanged += () => { UpdateBrushesFromResources(); InvalidateVisual(); };
    }

    private void UpdateBrushesFromResources()
    {
        try
        {
            var res = Application.Current?.Resources;
            if (res != null)
            {
                if (res.Contains("AccentBrush") && res["AccentBrush"] is Brush ab)
                {
                    _linePen = new Pen(ab, 2.0);
                    // fillBrush: same color but translucent
                    if (ab is SolidColorBrush sab)
                    {
                        var c = sab.Color;
                        _fillBrush = new SolidColorBrush(Color.FromArgb(40, c.R, c.G, c.B));
                    }
                    else
                    {
                        _fillBrush = ab;
                    }
                }
                else
                {
                    _linePen = new Pen(new SolidColorBrush(Color.FromRgb(0x00, 0x78, 0xD7)), 2.0);
                    _fillBrush = new SolidColorBrush(Color.FromArgb(40, 0, 120, 215));
                }
                _linePen.Freeze();
                if (_fillBrush is SolidColorBrush sb) sb.Freeze();
            }
        }
        catch { }
    }

    /// <summary>-1=上に線, 0=枠表示（子）, 1=下に線</summary>
    public int Position { get; set; } = -1;

    // (constructor above is used)

    protected override void OnRender(DrawingContext dc)
    {
        var bounds = new Rect(AdornedElement.RenderSize);

        if (Position == 0)
        {
            // 子として追加：ハイライト枠
            dc.DrawRectangle(_fillBrush, _linePen, bounds);
        }
        else
        {
            // 前(上)または後(下)に挿入：横線
            double y = Position < 0 ? 1 : bounds.Height - 1;
            dc.DrawLine(_linePen,
                new Point(bounds.Left, y),
                new Point(bounds.Right, y));
        }
    }
}
