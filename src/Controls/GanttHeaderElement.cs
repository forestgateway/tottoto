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
        double headerH = 32;
        try
        {
            var val = TryFindResource("HeaderHeight");
            if (val is double hh) headerH = hh;
        }
        catch { }
        return new Size(count * CellWidth, headerH);
    }

    private Pen _gridPen  = new Pen(Brushes.Gray, 0.5);
    private Pen _weekPen  = new Pen(Brushes.Transparent, 1.0);
    private Brush _todayBg   = Brushes.Yellow;
    private Brush _holiday2  = Brushes.LightBlue;
    private Brush _holiday1  = Brushes.LightSteelBlue;
    private Brush _normalBg  = Brushes.Gray;
    private Brush _monthBg   = Brushes.DarkGray;
    private Brush _monthTextBrush = Brushes.White;
    private Brush _dayTextBrush = Brushes.White;
    private Brush _todayTextBrush = Brushes.Black;
    private Brush _weekendBg = Brushes.LightBlue;

    public GanttHeaderElement()
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
            // テーマ辞書は Color キーを持っているため、可能なら Color を使ってブラシを構築する。
            var accent = res["AccentColor"] is Color ac ? ac : Colors.Gray;
            var panel = res["PanelBg"] is Color pc ? pc : Colors.DarkGray;
            // 枠線・週境界線: テーマの GridLineBrush があれば優先
            if (res.Contains("GridLineBrush") && res["GridLineBrush"] is Brush glb)
                _gridPen = new Pen(glb, 0.5);
            else
                _gridPen = new Pen(new SolidColorBrush(Color.FromArgb(0x55, accent.R, accent.G, accent.B)), 0.5);
            // 週境界線の色: テーマの WeekLine を優先
            if (res.Contains("WeekLine") && res["WeekLine"] is Color wl)
                _weekPen = new Pen(new SolidColorBrush(wl), 1.0);
            else if (res.Contains("WeekLineBrush") && res["WeekLineBrush"] is Brush wlbr)
                _weekPen = new Pen(wlbr, 1.0);
            else
                _weekPen = new Pen(new SolidColorBrush(Color.FromArgb(0x33, accent.R, accent.G, accent.B)), 1.0);
            // 日付セルの背景: 優先 CalendarDayBg -> ChartBgBrush -> PanelBgBrush -> 自動生成
            if (res.Contains("CalendarDayBg") && res["CalendarDayBg"] is Color cdb)
                _normalBg = new SolidColorBrush(cdb);
            else if (res.Contains("ChartBgBrush") && res["ChartBgBrush"] is Brush chartBg)
                _normalBg = chartBg;
            else if (res.Contains("PanelBgBrush") && res["PanelBgBrush"] is Brush panelBrush)
                _normalBg = panelBrush;
            else
                _normalBg = new SolidColorBrush(panel);

            // カレンダー上部トップストリップ（優先: CalendarTopBg -> HeaderBgBrush -> 自動生成）
            if (res.Contains("CalendarTopBg") && res["CalendarTopBg"] is Color ctb)
                _monthBg = new SolidColorBrush(ctb);
            else if (res.Contains("HeaderBgBrush") && res["HeaderBgBrush"] is Brush headerBrush)
                _monthBg = headerBrush;
            else
                _monthBg = new SolidColorBrush(Color.FromRgb((byte)Math.Max(panel.R - 16,0), (byte)Math.Max(panel.G - 16,0), (byte)Math.Max(panel.B - 32,0)));

            // 休日背景: 優先 CalendarHoliday2/1 (color) -> Holiday2Brush/Holiday1Brush -> 自動生成
            if (res.Contains("CalendarHoliday2") && res["CalendarHoliday2"] is Color ch2)
                _holiday2 = new SolidColorBrush(ch2);
            else if (res.Contains("Holiday2Brush") && res["Holiday2Brush"] is Brush h2)
                _holiday2 = h2;
            else
                _holiday2 = new SolidColorBrush(Color.FromRgb((byte)Math.Min(panel.R + 10,255), (byte)Math.Min(panel.G + 10,255), (byte)Math.Min(panel.B + 30,255)));

            if (res.Contains("CalendarHoliday1") && res["CalendarHoliday1"] is Color ch1)
                _holiday1 = new SolidColorBrush(ch1);
            else if (res.Contains("Holiday1Brush") && res["Holiday1Brush"] is Brush h1)
                _holiday1 = h1;
            else
                _holiday1 = new SolidColorBrush(Color.FromRgb((byte)Math.Max(panel.R - 8,0), (byte)Math.Max(panel.G - 8,0), (byte)Math.Min(panel.B + 20,255)));

            // 週末専用ブラシ: テーマで WeekendBrush を指定可能。なければ Holiday2 / Holiday1 をフォールバックとして利用
            if (res.Contains("WeekendBrush") && res["WeekendBrush"] is Brush wkb)
                _weekendBg = wkb;
            else if (res.Contains("CalendarHoliday2") && res["CalendarHoliday2"] is Color cw2)
                _weekendBg = new SolidColorBrush(cw2);
            else
                _weekendBg = _holiday2 ?? _holiday1;

            // テキスト色の決定: 可能なら WindowFgBrush を優先し、なければ WindowBg の明度により白/黒を選択
            // テキスト色: 優先 CalendarDayFg (日付) / CalendarTopFg (月見出し) -> WindowFgBrush -> 明度判定
            if (res.Contains("CalendarTopFg") && res["CalendarTopFg"] is Color ctf)
                _monthTextBrush = new SolidColorBrush(ctf);

            if (res.Contains("CalendarDayFg") && res["CalendarDayFg"] is Color cdf)
                _dayTextBrush = new SolidColorBrush(cdf);

            if (_monthTextBrush == null || (_monthTextBrush is SolidColorBrush scm && scm.Color == default))
            {
                if (res.Contains("WindowFgBrush") && res["WindowFgBrush"] is Brush wf)
                    _monthTextBrush = wf;
                else if (res.Contains("WindowBg") && res["WindowBg"] is Color wb)
                {
                    var lum = (0.2126 * wb.R + 0.7152 * wb.G + 0.0722 * wb.B) / 255.0;
                    _monthTextBrush = lum < 0.5 ? Brushes.White : Brushes.Black;
                }
            }

            if (_dayTextBrush == null || (_dayTextBrush is SolidColorBrush scd && scd.Color == default))
            {
                if (res.Contains("WindowFgBrush") && res["WindowFgBrush"] is Brush wf2)
                    _dayTextBrush = wf2;
                else if (res.Contains("WindowBg") && res["WindowBg"] is Color wb2)
                {
                    var lum2 = (0.2126 * wb2.R + 0.7152 * wb2.G + 0.0722 * wb2.B) / 255.0;
                    _dayTextBrush = lum2 < 0.5 ? Brushes.White : Brushes.Black;
                }
            }

            // 今日の背景色がテーマで指定されていればそれを使い、テキストは背景との対比で決める
            if (res.Contains("TodayOverlayBrush") && res["TodayOverlayBrush"] is Brush to)
                _todayBg = to;
            // _todayBg が SolidColorBrush なら色を取り出し、対比色を選ぶ
            if (_todayBg is SolidColorBrush scb)
            {
                var c = scb.Color;
                var lum = (0.2126 * c.R + 0.7152 * c.G + 0.0722 * c.B) / 255.0;
                _todayTextBrush = lum < 0.5 ? Brushes.White : Brushes.Black;
            }
            else
            {
                // フォールバック
                _todayTextBrush = Brushes.Black;
            }
        }
        catch { }
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
                new Typeface("Segoe UI"), 8, (_monthTextBrush is SolidColorBrush mcb) ? mcb : Brushes.White, dpi);
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
            bool isWeekend = d.Date.DayOfWeek == DayOfWeek.Saturday || d.Date.DayOfWeek == DayOfWeek.Sunday;

            Brush bg = d.IsToday ? _todayBg
                     : d.HolidayLv >= 2 ? _holiday2
                     : d.HolidayLv == 1 ? _holiday1
                     : isWeekend ? _weekendBg
                     : _normalBg;

            dc.DrawRectangle(bg, null, rect);
            dc.DrawRectangle(null, _gridPen, rect);

            // 日曜と月曜の間に週境界線（ヘッダー全高）
            //if (d.Date.DayOfWeek == DayOfWeek.Monday)
            //{
            //    double lx = x + snapOffset;
            //    dc.DrawLine(_weekPen, new Point(lx, 0), new Point(lx, h));
            //}

            // 日付テキストの色は年表示（month）と同じ前景色を使用する
            // 本日は常に黒文字にする
            var dayFg = d.IsToday ? Brushes.Black : ((_monthTextBrush is SolidColorBrush mcb) ? mcb : Brushes.White);
            var ft = new FormattedText(d.DayText,
                CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
                new Typeface("Segoe UI"), cw >= 14 ? 9 : 7, dayFg, dpi);
            dc.DrawText(ft, new Point(x + (cw - ft.Width) / 2, topH + (botH - ft.Height) / 2));

            x += cw;
        }
    }
}
