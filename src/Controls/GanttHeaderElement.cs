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

    private Pen   _gridPen  = new(new SolidColorBrush(Color.FromArgb(0x55, 0x00, 0xC8, 0xFF)), 0.5);
    private Pen   _weekPen  = new(new SolidColorBrush(Color.FromArgb(0x55, 0x00, 0xC8, 0xFF)), 1.0);
    private static readonly Brush s_todayBg   = new SolidColorBrush(Color.FromRgb(0xFF, 0xCC, 0x00));
    private Brush _holiday2  = new SolidColorBrush(Color.FromRgb(0x20, 0x30, 0x60));
    private Brush _holiday1  = new SolidColorBrush(Color.FromRgb(0x18, 0x28, 0x50));
    private Brush _normalBg  = new SolidColorBrush(Color.FromRgb(0x1A, 0x2A, 0x3E));
    private Brush _monthBg   = new SolidColorBrush(Color.FromRgb(0x0F, 0x1E, 0x30));

    static GanttHeaderElement()
    {
        ((SolidColorBrush)s_todayBg).Freeze();
    }

    public GanttHeaderElement()
    {
        // テーマ変更時にブラシを更新して再描画
        todochart.Services.ThemeService.ThemeChanged += OnThemeChanged;
    }

    private void OnThemeChanged()
    {
        UpdateBrushesFromTheme();
        InvalidateVisual();
    }

    private void UpdateBrushesFromTheme()
    {
        var res = Application.Current?.Resources;
        if (res is null) return;

        var accent  = res["AccentBrush"]  is SolidColorBrush ab ? ab.Color : Color.FromArgb(0xFF, 0x00, 0xC8, 0xFF);
        var panelBg = res["PanelBgBrush"] is SolidColorBrush pb ? pb.Color : Color.FromRgb(0x1A, 0x2A, 0x3E);
        var chartBg = res["ChartBgBrush"] is SolidColorBrush cb ? cb.Color : Color.FromRgb(0x0F, 0x1E, 0x30);

        _gridPen  = new Pen(new SolidColorBrush(Color.FromArgb(0x55, accent.R, accent.G, accent.B)), 0.5);
        _weekPen  = new Pen(new SolidColorBrush(Color.FromArgb(0x55, accent.R, accent.G, accent.B)), 1.0);
        _normalBg = new SolidColorBrush(panelBg);
        _monthBg  = new SolidColorBrush(chartBg);
        // holiday2 / holiday1 はパネル背景より少し暗い色で生成
        _holiday2 = new SolidColorBrush(Color.FromRgb(
            (byte)Math.Min(panelBg.R + 10, 255), (byte)Math.Min(panelBg.G + 10, 255), (byte)Math.Min(panelBg.B + 30, 255)));
        _holiday1 = new SolidColorBrush(Color.FromRgb(
            (byte)Math.Max(panelBg.R - 8, 0),  (byte)Math.Max(panelBg.G - 8, 0),  (byte)Math.Min(panelBg.B + 20, 255)));
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
            dc.DrawRectangle(_monthBg, null, monthRect);
            dc.DrawRectangle(null, _gridPen, monthRect);

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
                     : d.HolidayLv >= 2 ? _holiday2
                     : d.HolidayLv == 1 ? _holiday1
                     : _normalBg;

            dc.DrawRectangle(bg, null, rect);
            dc.DrawRectangle(null, _gridPen, rect);

            // 日曜と月曜の間に週境界線（ヘッダー全高）
            if (d.Date.DayOfWeek == DayOfWeek.Monday)
            {
                double lx = x + snapOffset;
                dc.DrawLine(_weekPen, new Point(lx, 0), new Point(lx, h));
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
