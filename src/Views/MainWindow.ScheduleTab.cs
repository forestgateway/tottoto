using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using todochart.ViewModels;

namespace todochart.Views;

/// <summary>スケジュールタブの選択・閉じる・ドラッグ＆ドロップ。</summary>
public partial class MainWindow
{
    private const string   TabDragFormat    = "todochart.ScheduleEntry";
    private Point?         _tabDragStartPoint;
    private ScheduleEntry? _tabDragSource;
    private Border?        _tabDropTarget;
    private bool           _tabDropAfter;

    private void OnScheduleTabClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.DataContext is ScheduleEntry entry)
            Vm.ActiveEntry = entry;
    }

    private void OnScheduleTabClose(object sender, RoutedEventArgs e)
    {
        var menu = (sender as MenuItem)?.Parent as ContextMenu;
        if (menu?.PlacementTarget is FrameworkElement fe && fe.DataContext is ScheduleEntry entry)
            Vm.CloseEntryCommand.Execute(entry);
    }

    private void OnScheduleTabPreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed)
        {
            _tabDragStartPoint = null;
            _tabDragSource     = null;
            return;
        }
        if (sender is not FrameworkElement fe) return;
        if (fe.DataContext is not ScheduleEntry entry) return;

        if (_tabDragStartPoint is null)
        {
            _tabDragStartPoint = e.GetPosition(fe);
            _tabDragSource     = entry;
            return;
        }

        var diff = e.GetPosition(fe) - _tabDragStartPoint.Value;
        if (Math.Abs(diff.X) < SystemParameters.MinimumHorizontalDragDistance &&
            Math.Abs(diff.Y) < SystemParameters.MinimumVerticalDragDistance) return;

        if (_tabDragSource is null) return;
        _tabDragStartPoint = null;

        var data = new DataObject(TabDragFormat, _tabDragSource);
        DragDrop.DoDragDrop(fe, data, DragDropEffects.Move);
        _tabDragSource = null;
        ClearTabDropIndicator();
    }

    private void OnScheduleTabDragOver(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(TabDragFormat)) { e.Effects = DragDropEffects.None; e.Handled = true; return; }

        e.Effects = DragDropEffects.Move;
        e.Handled = true;

        if (sender is not Border border) return;
        bool after = e.GetPosition(border).X > border.ActualWidth / 2;

        if (_tabDropTarget != border || _tabDropAfter != after)
        {
            ClearTabDropIndicator();
            _tabDropTarget = border;
            _tabDropAfter  = after;
            UpdateTabDropIndicator(border, after);
        }
    }

    private void OnScheduleTabDragLeave(object sender, DragEventArgs e)
    {
        if (sender is Border border && border == _tabDropTarget)
            ClearTabDropIndicator();
    }

    private void OnScheduleTabDrop(object sender, DragEventArgs e)
    {
        ClearTabDropIndicator();
        if (!e.Data.GetDataPresent(TabDragFormat)) return;
        if (sender is not FrameworkElement fe) return;
        if (fe.DataContext is not ScheduleEntry target) return;

        var source = (ScheduleEntry)e.Data.GetData(TabDragFormat);
        if (source == target) return;

        bool after       = e.GetPosition(fe).X > (fe as Border)?.ActualWidth / 2;
        int targetIndex  = Vm.Schedules.IndexOf(target);
        if (after) targetIndex++;

        int sourceIndex = Vm.Schedules.IndexOf(source);
        if (sourceIndex < targetIndex) targetIndex--;

        Vm.MoveScheduleTo(source, targetIndex);
        e.Handled = true;
    }

    private void UpdateTabDropIndicator(Border border, bool after)
    {
        border.BorderThickness = after
            ? new Thickness(1, 1, 3, 0)
            : new Thickness(3, 1, 1, 0);
        border.BorderBrush = System.Windows.Media.Brushes.DodgerBlue;
    }

    private void ClearTabDropIndicator()
    {
        if (_tabDropTarget is not null)
        {
            _tabDropTarget.BorderBrush     = (System.Windows.Media.Brush)FindResource("BorderBrush");
            _tabDropTarget.BorderThickness = new Thickness(1, 1, 1, 0);
            _tabDropTarget = null;
        }
    }
}
