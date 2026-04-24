using System.Globalization;
using System.Windows;
using System.Windows.Media;
using todochart.ViewModels;

namespace todochart.Controls;

/// <summary>
/// ガントチャートのヘッダー行（日付ラベル）を描画するコントロール。
/// </summary>
public class GanttHeaderElement : FrameworkElement
{
    public static readonly DependencyProperty DaysProperty =
        DependencyProperty.Register(nameof(Days),
            typeof(IReadOnlyList<ChartDayHeaderInfo>), typeof(GanttHeaderElement),
            new FrameworkPropertyMetadata(null,
                FrameworkPropertyMetadataOptions.AffectsRender |
                FrameworkPropertyMetadataOptions.AffectsMeasure));

    public static readonly DependencyProperty CellWidthProperty =
        DependencyProperty.Register(nameof(CellWidth), typeof(double), typeof(GanttHeaderElement),
            new FrameworkPropertyMetadata(16.0,
                FrameworkPropertyMetadataOptions.AffectsRender |
                FrameworkPropertyMetadataOptions.AffectsMeasure));

    public IReadOnlyList<ChartDayHeaderInfo>? Days
    {
        get => (IReadOnlyList<ChartDayHeaderInfo>?)GetValue(DaysProperty);
        set => SetValue(DaysProperty, value);
    }

    public double CellWidth
    {
        get => (double)GetValue(CellWidthProperty);
        set => SetValue(CellWidthProperty, value);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        int count = Days?.Count ?? 0;
        return new Size(count * CellWidth, 32);
    }

    private static readonly Pen s_gridPen  = new(Brushes.Gray, 0.5);
    private static readonly Pen s_weekPen  = new(new SolidColorBrush(Color.FromArgb(0x33, 0x99, 0x99, 0x99)), 1.0);
    private static readonly Brush s_todayBg   = new SolidColorBrush(Color.FromRgb(0xFF, 0xCC, 0x00));
    private static readonly Brush s_holiday2  = new SolidColorBrush(Color.FromRgb(0x88, 0x88, 0xFF));
    private static readonly Brush s_holiday1  = new SolidColorBrush(Color.FromRgb(0xBB, 0xBB, 0xFF));
    private static readonly Brush s_normalBg  = new SolidColorBrush(Color.FromRgb(0x44, 0x44, 0x44));
    private static readonly Brush s_monthBg   = new SolidColorBrush(Color.FromRgb(0x22, 0x22, 0x22));

    static GanttHeaderElement()
    {
        s_gridPen.Freeze(); ((SolidColorBrush)s_weekPen.Brush).Freeze(); s_weekPen.Freeze();
        s_todayBg.Freeze(); s_holiday2.Freeze();
        s_holiday1.Freeze(); s_normalBg.Freeze(); s_monthBg.Freeze();
    }

    protected override void OnRender(DrawingContext dc)
    {
        if (Days is null) return;

        double x  = 0;
        double cw = CellWidth;
        double h  = ActualHeight > 0 ? ActualHeight : 32;
        double dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;

        // 月ラベル行（上半分）と日ラベル行（下半分）に分ける
        double topH = h * 0.45;
        double botH = h - topH;

        // 月ラベルを 1 日のセルにまとめて描画
        int i = 0;
        while (i < Days.Count)
        {
            var d = Days[i];
            // 同じ月のセル数を数える
            int monthCount = 0;
            while (i + monthCount < Days.Count
                   && Days[i + monthCount].Date.Month == d.Date.Month)
                monthCount++;

            double monthW = monthCount * cw;
            var monthRect = new Rect(x, 0, monthW, topH);
            dc.DrawRectangle(s_monthBg, null, monthRect);
            dc.DrawRectangle(null, s_gridPen, monthRect);

            var monthText = new FormattedText(
                d.Date.ToString("yyyy/M"),
                CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
                new Typeface("Segoe UI"), 8, Brushes.White, dpi);
            dc.DrawText(monthText, new Point(x + 2, (topH - monthText.Height) / 2));

            x     += monthW;
            i     += monthCount;
        }

        // 日ラベル行
        x = 0;
        double dpiVal = VisualTreeHelper.GetDpi(this).PixelsPerDip;
        double snapOffset = 0.5 / dpiVal;
        for (int j = 0; j < Days.Count; j++)
        {
            var d    = Days[j];
            var rect = new Rect(x, topH, cw, botH);

            Brush bg = d.IsToday ? s_todayBg
                     : d.HolidayLv >= 2 ? s_holiday2
                     : d.HolidayLv == 1 ? s_holiday1
                     : s_normalBg;

            dc.DrawRectangle(bg, null, rect);
            dc.DrawRectangle(null, s_gridPen, rect);

            // 日曜と月曜の間に週境界線（ヘッダー全高）
            if (d.Date.DayOfWeek == DayOfWeek.Monday)
            {
                double lx = x + snapOffset;
                dc.DrawLine(s_weekPen, new Point(lx, 0), new Point(lx, h));
            }

            Brush fg = d.IsToday ? Brushes.Black : Brushes.White;
            var ft = new FormattedText(d.DayText,
                CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
                new Typeface("Segoe UI"), cw >= 14 ? 9 : 7, fg, dpi);
            dc.DrawText(ft, new Point(x + (cw - ft.Width) / 2, topH + (botH - ft.Height) / 2));

            x += cw;
        }
    }
}
