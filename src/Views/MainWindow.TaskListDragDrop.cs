using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using todochart.Controls;
using todochart.ViewModels;

namespace todochart.Views;

/// <summary>タスクリストのドラッグ＆ドロップ（並び替え・ファイル・URL）。</summary>
public partial class MainWindow
{
    private Point?               _dragStartPoint;
    private TaskRowViewModel?    _dragSource;
    private DropIndicatorAdorner? _dropAdorner;
    private UIElement?           _adornerTarget;

    private void OnTaskListPreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        _dragStartPoint = e.GetPosition(TaskListBox);
        _dragSource     = GetRowViewModelAt(e.GetPosition(TaskListBox));
    }

    private void OnTaskListPreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (_dragStartPoint is null || _dragSource is null) return;
        if (e.LeftButton != MouseButtonState.Pressed) { ResetDrag(); return; }

        var pos  = e.GetPosition(TaskListBox);
        var diff = pos - _dragStartPoint.Value;
        if (Math.Abs(diff.X) < SystemParameters.MinimumHorizontalDragDistance &&
            Math.Abs(diff.Y) < SystemParameters.MinimumVerticalDragDistance) return;

        var data = new DataObject(typeof(TaskRowViewModel), _dragSource);
        DragDrop.DoDragDrop(TaskListBox, data, DragDropEffects.Move);
        ResetDrag();
    }

    private void OnTaskListDragOver(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            e.Effects = DragDropEffects.Copy;
            e.Handled = true;
            return;
        }

        if (e.Data.GetDataPresent(DataFormats.UnicodeText) ||
            e.Data.GetDataPresent("UniformResourceLocator") ||
            e.Data.GetDataPresent("UniformResourceLocatorW"))
        {
            e.Effects = DragDropEffects.Copy;
            e.Handled = true;
            return;
        }

        if (!e.Data.GetDataPresent(typeof(TaskRowViewModel)))
        {
            e.Effects = DragDropEffects.None;
            e.Handled = true;
            return;
        }

        e.Effects = DragDropEffects.Move;
        e.Handled = true;

        var (targetRow, targetElement, pos) = GetDropTarget(e);
        if (targetElement is null || targetRow is null) { ClearAdorner(); return; }

        if (_adornerTarget != targetElement)
        {
            ClearAdorner();
            _adornerTarget = targetElement;
            var layer = AdornerLayer.GetAdornerLayer(targetElement);
            if (layer is not null)
            {
                _dropAdorner = new DropIndicatorAdorner(targetElement) { Position = pos };
                layer.Add(_dropAdorner);
            }
        }
        else if (_dropAdorner is not null && _dropAdorner.Position != pos)
        {
            _dropAdorner.Position = pos;
            _dropAdorner.InvalidateVisual();
        }
    }

    private void OnTaskListDragLeave(object sender, DragEventArgs e)
    {
        var pos    = e.GetPosition(TaskListBox);
        var bounds = new Rect(TaskListBox.RenderSize);
        if (!bounds.Contains(pos))
            ClearAdorner();
    }

    private void OnTaskListDrop(object sender, DragEventArgs e)
    {
        ClearAdorner();

        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            var files   = (string[])e.Data.GetData(DataFormats.FileDrop);
            var nearItem = GetRowViewModelAt(e.GetPosition(TaskListBox))?.Item;
            foreach (var file in files)
                Vm.AddToDoFromFile(file, nearItem);
            e.Handled = true;
            return;
        }

        string? url = GetUrlFromDragData(e.Data);
        if (url is not null)
        {
            var nearItem = GetRowViewModelAt(e.GetPosition(TaskListBox))?.Item;
            _ = Vm.AddToDoFromUrl(url, nearItem);
            e.Handled = true;
            return;
        }

        if (!e.Data.GetDataPresent(typeof(TaskRowViewModel))) return;

        var dragRow = (TaskRowViewModel)e.Data.GetData(typeof(TaskRowViewModel));
        var (targetRow, _, insertPos) = GetDropTarget(e);
        if (targetRow is null || dragRow.Item == targetRow.Item) return;

        Vm.MoveItemByDrag(dragRow.Item, targetRow.Item, insertPos);
        e.Handled = true;
    }

    private (TaskRowViewModel? row, UIElement? element, int position) GetDropTarget(DragEventArgs e)
    {
        var hit = TaskListBox.InputHitTest(e.GetPosition(TaskListBox)) as DependencyObject;
        while (hit is not null && hit is not ListBoxItem)
            hit = VisualTreeHelper.GetParent(hit);

        if (hit is not ListBoxItem lbi) return (null, null, -1);
        if (lbi.DataContext is not TaskRowViewModel row) return (null, null, -1);

        var localPos = e.GetPosition(lbi);
        double h     = lbi.ActualHeight;
        int pos;
        if (row.IsFolder && localPos.Y > h * 0.25 && localPos.Y < h * 0.75)
            pos = 0;
        else if (localPos.Y <= h * 0.5)
            pos = -1;
        else
            pos = 1;

        return (row, lbi, pos);
    }

    private TaskRowViewModel? GetRowViewModelAt(Point listBoxPoint)
    {
        var hit = TaskListBox.InputHitTest(listBoxPoint) as DependencyObject;
        while (hit is not null && hit is not ListBoxItem)
            hit = VisualTreeHelper.GetParent(hit);
        return (hit as ListBoxItem)?.DataContext as TaskRowViewModel;
    }

    private static string? GetUrlFromDragData(IDataObject data)
    {
        foreach (var fmt in new[] { "UniformResourceLocatorW", "UniformResourceLocator" })
        {
            if (!data.GetDataPresent(fmt)) continue;
            var raw = data.GetData(fmt);
            string? url = raw switch
            {
                string s            => s.Trim(),
                System.IO.Stream st => new System.IO.StreamReader(st).ReadToEnd().Trim('\0').Trim(),
                _ => null,
            };
            if (!string.IsNullOrEmpty(url) && IsUrl(url)) return url;
        }

        if (data.GetDataPresent(DataFormats.UnicodeText))
        {
            var text = data.GetData(DataFormats.UnicodeText) as string;
            if (!string.IsNullOrEmpty(text))
            {
                var first = text.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)[0].Trim();
                if (IsUrl(first)) return first;
            }
        }
        return null;
    }

    private static bool IsUrl(string s) =>
        s.StartsWith("http://",  StringComparison.OrdinalIgnoreCase) ||
        s.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
        s.StartsWith("ftp://",   StringComparison.OrdinalIgnoreCase);

    private void ClearAdorner()
    {
        if (_dropAdorner is not null && _adornerTarget is not null)
        {
            var layer = AdornerLayer.GetAdornerLayer(_adornerTarget);
            layer?.Remove(_dropAdorner);
            _dropAdorner = null;
        }
        _adornerTarget = null;
    }

    private void ResetDrag()
    {
        _dragStartPoint = null;
        _dragSource     = null;
    }
}
