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
    private static readonly Pen   s_linePen  = new(new SolidColorBrush(Color.FromRgb(0x00, 0x78, 0xD7)), 2.0);
    private static readonly Brush s_fillBrush = new SolidColorBrush(Color.FromArgb(40, 0, 120, 215));

    static DropIndicatorAdorner()
    {
        s_linePen.Freeze();
        s_fillBrush.Freeze();
    }

    /// <summary>-1=上に線, 0=枠表示（子）, 1=下に線</summary>
    public int Position { get; set; } = -1;

    public DropIndicatorAdorner(UIElement adornedElement) : base(adornedElement) 
    {
        IsHitTestVisible = false;
    }

    protected override void OnRender(DrawingContext dc)
    {
        var bounds = new Rect(AdornedElement.RenderSize);

        if (Position == 0)
        {
            // 子として追加：ハイライト枠
            dc.DrawRectangle(s_fillBrush, s_linePen, bounds);
        }
        else
        {
            // 前(上)または後(下)に挿入：横線
            double y = Position < 0 ? 1 : bounds.Height - 1;
            dc.DrawLine(s_linePen,
                new Point(bounds.Left, y),
                new Point(bounds.Right, y));
        }
    }
}
