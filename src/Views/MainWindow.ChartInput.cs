using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using todochart.ViewModels;

namespace todochart.Views;

/// <summary>ガントチャート領域のマウス入力（ドラッグスクロール・タスク期間シフト・吹き出し）。</summary>
public partial class MainWindow
{
    // ── チャートドラッグスクロール ────────────────────────
    private bool   _chartDragging;
    private bool   _chartPotentialDrag;
    private Point  _chartDragStart;
    private double _chartScrollStart;

    // Shift + ドラッグでタスク期間を平行移動
    private bool              _chartTaskShiftDragging;
    private Point             _chartTaskShiftDragStart;
    private int               _chartTaskShiftAppliedDays;
    private TaskRowViewModel? _chartTaskShiftRow;
    private todochart.Models.ScheduleItemBase? _chartTaskShiftItem;

    private void OnChartMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        var pos = e.GetPosition(ChartRowsControl);
        var row = GetChartRowViewModelAt(pos);

        if ((Keyboard.Modifiers & ModifierKeys.Shift) != 0 &&
            row?.Item is todochart.Models.ScheduleToDo &&
            IsOnTaskPeriodCell(row, pos))
        {
            Vm.Selected = row;
            _chartTaskShiftDragging    = true;
            _chartTaskShiftDragStart   = e.GetPosition(ChartBodyScroll);
            _chartTaskShiftAppliedDays = 0;
            _chartTaskShiftRow         = row;
            _chartTaskShiftItem        = row.Item;
            ChartBodyScroll.CaptureMouse();
            ChartBodyScroll.Cursor = Cursors.ScrollWE;
            e.Handled = true;
            return;
        }

        _chartPotentialDrag = true;
        _chartDragStart     = e.GetPosition(ChartBodyScroll);
        _chartScrollStart   = ChartBodyScroll.HorizontalOffset;
    }

    private TaskRowViewModel? _hoveredChartRow;

    private void OnChartMouseMove(object sender, MouseEventArgs e)
    {
        if (_chartTaskShiftDragging)
        {
            if (e.LeftButton != MouseButtonState.Pressed)
            {
                EndChartTaskShift();
            }
            else
            {
                double dragCellWidth = (double)FindResource("CellWidth");
                double deltaX        = e.GetPosition(ChartBodyScroll).X - _chartTaskShiftDragStart.X;
                int    shiftDays     = (int)(deltaX / dragCellWidth);
                int    diff          = shiftDays - _chartTaskShiftAppliedDays;
                if (diff != 0 && _chartTaskShiftItem is not null && Vm.Selected?.Item == _chartTaskShiftItem)
                {
                    Vm.ShiftSelectedKeepingDurationBy(diff);
                    _chartTaskShiftAppliedDays = shiftDays;
                }
                e.Handled = true;
            }
        }
        else
        {
            if (_chartPotentialDrag && e.LeftButton == MouseButtonState.Pressed && !_chartDragging)
            {
                var pos = e.GetPosition(ChartBodyScroll);
                if (Math.Abs(pos.X - _chartDragStart.X) >= SystemParameters.MinimumHorizontalDragDistance)
                {
                    _chartDragging = true;
                    ChartBodyScroll.CaptureMouse();
                    ChartBodyScroll.Cursor = Cursors.SizeWE;
                }
            }

            if (_chartDragging)
            {
                double delta = e.GetPosition(ChartBodyScroll).X - _chartDragStart.X;
                ChartBodyScroll.ScrollToHorizontalOffset(_chartScrollStart - delta);
                e.Handled = true;
            }
        }

        // 列 Hover 表示
        double cellWidth = (double)FindResource("CellWidth");
        double posX      = e.GetPosition(ChartRowsControl).X;
        int    col       = (int)(posX / cellWidth);
        int    count     = Vm.FlatItems.FirstOrDefault()?.ChartCells.Count ?? 0;
        Vm.HoveredColumnIndex = (col >= 0 && col < count) ? col : -1;

        // HoverOnly 吹き出し：行ホバー状態を更新
        var rowHeight    = (double)FindResource("RowHeight");
        double posY      = e.GetPosition(ChartRowsControl).Y;
        int    rowIndex  = (int)(posY / rowHeight);
        var    newHovRow = (rowIndex >= 0 && rowIndex < Vm.FlatItems.Count)
                           ? Vm.FlatItems[rowIndex] : null;
        if (_hoveredChartRow != newHovRow)
        {
            if (_hoveredChartRow is not null) _hoveredChartRow.IsChartRowHovered = false;
            _hoveredChartRow = newHovRow;
            if (_hoveredChartRow is not null) _hoveredChartRow.IsChartRowHovered = true;
        }
    }

    private void OnChartMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_chartTaskShiftDragging)
        {
            EndChartTaskShift();
            e.Handled = true;
        }
        else if (_chartDragging)
        {
            _chartDragging = false;
            ChartBodyScroll.ReleaseMouseCapture();
            ChartBodyScroll.Cursor = Cursors.Arrow;
            e.Handled = true;
        }
        else if (_chartPotentialDrag)
        {
            var pos = e.GetPosition(ChartRowsControl);
            var row = GetChartRowViewModelAt(pos);
            if (row is not null)
            {
                Vm.Selected = row;
                e.Handled   = true;
            }
        }

        _chartPotentialDrag = false;
    }

    private void OnChartMouseLeave(object sender, MouseEventArgs e)
    {
        if (_chartTaskShiftDragging) EndChartTaskShift();

        if (_chartDragging)
        {
            _chartDragging = false;
            ChartBodyScroll.ReleaseMouseCapture();
            ChartBodyScroll.Cursor = Cursors.Arrow;
        }
        _chartPotentialDrag = false;
        Vm.HoveredColumnIndex = -1;

        if (_hoveredChartRow is not null)
        {
            _hoveredChartRow.IsChartRowHovered = false;
            _hoveredChartRow = null;
        }
    }

    private void EndChartTaskShift()
    {
        _chartTaskShiftDragging    = false;
        _chartTaskShiftRow         = null;
        _chartTaskShiftItem        = null;
        _chartTaskShiftAppliedDays = 0;
        ChartBodyScroll.ReleaseMouseCapture();
        ChartBodyScroll.Cursor = Cursors.Arrow;
    }

    private TaskRowViewModel? GetChartRowViewModelAt(Point chartPoint)
    {
        var hit = ChartRowsControl.InputHitTest(chartPoint) as DependencyObject;
        while (hit is not null && hit is not FrameworkElement)
            hit = VisualTreeHelper.GetParent(hit);
        while (hit is not null)
        {
            if (hit is FrameworkElement fe && fe.DataContext is TaskRowViewModel row)
                return row;
            hit = VisualTreeHelper.GetParent(hit);
        }
        return null;
    }

    private bool IsOnTaskPeriodCell(TaskRowViewModel row, Point chartPoint)
    {
        double cellWidth = (double)FindResource("CellWidth");
        int    col       = (int)(chartPoint.X / cellWidth);
        if (col < 0 || col >= row.ChartCells.Count) return false;
        return row.ChartCells[col].BarBrush is not null;
    }

    // ── 吹き出し ──────────────────────────────────────────

    private void OnChartRowsControlSizeChanged(object sender, SizeChangedEventArgs e)
    {
        ChartCalloutOverlay.UpdateCanvasSize(
            ChartRowsControl.ActualWidth,
            ChartRowsControl.ActualHeight);
    }

    private void OnChartMouseRightButtonUp(object sender, MouseButtonEventArgs e)
    {
        var pos = e.GetPosition(ChartRowsControl);
        var row = GetChartRowViewModelAt(pos);
        if (row?.Item is not todochart.Models.ScheduleToDo) return;

        double cellWidth = (double)FindResource("CellWidth");
        int    col       = (int)(pos.X / cellWidth);
        int    count     = row.ChartCells.Count;
        if (col < 0 || col >= count) return;

        var menu    = new ContextMenu();
        var addItem = new MenuItem { Header = "吹き出しを追加(_B)" };
        addItem.Click += (_, _) =>
        {
            var calloutVm = Vm.AddCallout(row.Item, col);
            if (calloutVm is not null)
                Dispatcher.BeginInvoke(DispatcherPriority.Background, () => calloutVm.BeginEdit());
        };
        menu.Items.Add(addItem);
        menu.PlacementTarget = ChartBodyScroll;
        menu.IsOpen = true;
        e.Handled   = true;
    }
}
