using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace todochart.Views;

/// <summary>初期化・列幅・ペイン幅の復元と保存。</summary>
public partial class MainWindow
{
    private ScrollViewer?     _taskScrollViewer;
    private DispatcherTimer?  _colWidthSaveTimer;
    private DispatcherTimer?  _paneWidthSaveTimer;

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _taskScrollViewer = GetScrollViewer(TaskListBox);
        if (_taskScrollViewer is not null)
            _taskScrollViewer.ScrollChanged += OnTaskScrollChanged;

        HookHeaderColumnWidths();
        RestoreColumnWidths();
        RestoreTaskPaneWidth();
        HookTaskPaneWidth();

        // Step2: Schedules[0] のガントチャートセルをバックグラウンドで構築
        Dispatcher.BeginInvoke(DispatcherPriority.Background, () =>
        {
            Vm.RefreshAllChartCells();
            Vm.IsChartReady = true;
            ChartCalloutOverlay.UpdateCanvasSize(
                ChartRowsControl.ActualWidth,
                ChartRowsControl.ActualHeight);

            // Step3: 残タブ（_pendingFiles）をガントチャート構築後にロード
            Dispatcher.BeginInvoke(DispatcherPriority.Background, () =>
            {
                Vm.LoadPendingFiles();
            });
        });
    }

    private void HookHeaderColumnWidths()
    {
        var descriptor = DependencyPropertyDescriptor.FromProperty(
            ColumnDefinition.WidthProperty, typeof(ColumnDefinition));

        descriptor.AddValueChanged(ColName,   OnHeaderColumnWidthChanged);
        descriptor.AddValueChanged(ColStatus, OnHeaderColumnWidthChanged);
        descriptor.AddValueChanged(ColML,     OnHeaderColumnWidthChanged);
    }

    private void OnHeaderColumnWidthChanged(object? sender, EventArgs e)
    {
        _columnWidths[0] = ColName.Width;
        _columnWidths[1] = ColStatus.Width;
        _columnWidths[2] = ColML.Width;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ColumnWidths)));

        if (_colWidthSaveTimer is null)
        {
            _colWidthSaveTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
            _colWidthSaveTimer.Tick += (_, _) =>
            {
                _colWidthSaveTimer.Stop();
                Vm.SaveTaskColumnWidths(ColName.ActualWidth, ColStatus.ActualWidth, ColML.ActualWidth);
            };
        }
        _colWidthSaveTimer.Stop();
        _colWidthSaveTimer.Start();
    }

    private void RestoreColumnWidths()
    {
        var (nameW, statusW, mlW) = Vm.GetTaskColumnWidths();
        if (nameW   > 0) ColName.Width   = new GridLength(nameW,   GridUnitType.Pixel);
        if (statusW > 0) ColStatus.Width = new GridLength(statusW, GridUnitType.Pixel);
        if (mlW     > 0) ColML.Width     = new GridLength(mlW,     GridUnitType.Pixel);
    }

    private void RestoreTaskPaneWidth()
    {
        var w = Vm.GetTaskPaneWidth();
        if (w > 0) LeftColDef.Width = new GridLength(w, GridUnitType.Pixel);
    }

    private void HookTaskPaneWidth()
    {
        var descriptor = DependencyPropertyDescriptor.FromProperty(
            ColumnDefinition.WidthProperty, typeof(ColumnDefinition));
        descriptor.AddValueChanged(LeftColDef, OnTaskPaneWidthChanged);
    }

    private void OnTaskPaneWidthChanged(object? sender, EventArgs e)
    {
        if (_paneWidthSaveTimer is null)
        {
            _paneWidthSaveTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
            _paneWidthSaveTimer.Tick += (_, _) =>
            {
                _paneWidthSaveTimer.Stop();
                Vm.SaveTaskPaneWidth(LeftColDef.ActualWidth);
            };
        }
        _paneWidthSaveTimer.Stop();
        _paneWidthSaveTimer.Start();
    }

    private static ScrollViewer? GetScrollViewer(DependencyObject? root)
    {
        if (root is null) return null;
        if (root is ScrollViewer sv) return sv;
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var result = GetScrollViewer(VisualTreeHelper.GetChild(root, i));
            if (result is not null) return result;
        }
        return null;
    }
}
