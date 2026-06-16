using System.Windows.Controls;

namespace todochart.Views;

/// <summary>タスクリスト・チャートボディ・チャートヘッダー間のスクロール同期。</summary>
public partial class MainWindow
{
    private bool _syncingScroll;

    private void OnTaskScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        if (_syncingScroll) return;
        _syncingScroll = true;
        ChartBodyScroll.ScrollToVerticalOffset(e.VerticalOffset);
        _syncingScroll = false;
    }

    private void OnChartBodyScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        // 水平スクロールをヘッダーに同期
        ChartHeaderScroll.ScrollToHorizontalOffset(e.HorizontalOffset);

        // 吹き出しオーバーレイのスクロールを同期
        ChartCalloutOverlay.SetScrollOffset(e.HorizontalOffset, e.VerticalOffset);

        // オーバーレイ Canvas のサイズをスクロールコンテンツ全体に合わせる
        ChartCalloutOverlay.UpdateCanvasSize(
            ChartRowsControl.ActualWidth,
            ChartRowsControl.ActualHeight);

        // 垂直スクロールをタスクリストに同期
        if (_syncingScroll || _taskScrollViewer is null) return;
        _syncingScroll = true;
        _taskScrollViewer.ScrollToVerticalOffset(e.VerticalOffset);
        _syncingScroll = false;
    }
}
