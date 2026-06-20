using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Threading;
using System.Windows.Controls;
using System.Windows.Input;
using todochart.ViewModels;

namespace todochart.Views;

public partial class TodayScheduleWindow : Window
{
    private TodayScheduleViewModel Vm => (TodayScheduleViewModel)DataContext;

    public TodayScheduleWindow(TodayScheduleViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;
        // XAML Title = "今日の予定"; append today's date here
        Title = Title + " - " + DateTime.Today.ToString("yyyy/MM/dd");
        // 選択変更時に ListBoxItem にフォーカスを移す
        vm.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName != nameof(vm.Selected)) return;
            Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() =>
            {
                var selected = vm.Selected;
                if (selected is null) return;
                if (TaskListBox.ItemContainerGenerator.ContainerFromItem(selected) is not ListBoxItem container)
                {
                    TaskListBox.ScrollIntoView(selected);
                    container = TaskListBox.ItemContainerGenerator.ContainerFromItem(selected) as ListBoxItem;
                }

                if (container is not null)
                {
                    container.Focus();
                    Keyboard.Focus(container);
                }
                else
                {
                    TaskListBox.Focus();
                }
            }));
        };
    }

    private void OnListBoxMouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (Vm.Selected is not null)
            Vm.EditCommand.Execute(null);
    }

    private void OnNameEditorIsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (sender is TextBox tb && tb.IsVisible)
            tb.Dispatcher.BeginInvoke(() => { tb.Focus(); tb.SelectAll(); });
    }

    private void OnNameEditorKeyDown(object sender, KeyEventArgs e)
    {
        if (sender is not TextBox tb) return;
        if (tb.DataContext is not TaskRowViewModel row) return;
        if (e.Key == Key.Return)
        {
            row.CommitEdit(); e.Handled = true; TaskListBox.Focus();
        }
        else if (e.Key == Key.Escape)
        {
            row.CancelEdit(); e.Handled = true; TaskListBox.Focus();
        }
    }

    private void OnNameEditorLostFocus(object sender, RoutedEventArgs e)
    {
        if (sender is TextBox tb && tb.DataContext is TaskRowViewModel row)
            row.CommitEdit();
    }

    // ListBox の PreviewKeyDown で Ctrl+C/X/V/D/N/P をコマンド経由に統一する。
    // TextBox 編集中は TextBox のデフォルト動作を優先するため、インライン編集中はスキップ。
    private void OnTaskListPreviewKeyDown(object sender, KeyEventArgs e)
    {
        // Debug: ログで押下 Key / SystemKey を確認する（Ctrl 押下時のみ）
        //if ((Keyboard.Modifiers & ModifierKeys.Control) != 0)
        //{
        //    Debug.WriteLine($"Today PreviewKeyDown: Key={e.Key}, SystemKey={e.SystemKey}");
        //}

        // インライン編集中（TextBox にフォーカス）は横取りしない
        if (Keyboard.FocusedElement is TextBox) return;

        if (e.Key is not (Key.C or Key.X or Key.V or Key.D or Key.N or Key.P)) return;
        if ((Keyboard.Modifiers & ModifierKeys.Control) == 0) return;

        switch (e.Key)
        {
            case Key.C: Vm.CopyItemCommand.Execute(null);  e.Handled = true; break;
            case Key.X: Vm.CutItemCommand.Execute(null);   e.Handled = true; break;
            case Key.V: Vm.PasteItemCommand.Execute(null); e.Handled = true; break;
            case Key.D: Vm.DeleteCommand.Execute(null);   e.Handled = true; break;
            case Key.N: Vm.SelectNextCommand.Execute(null); e.Handled = true; break;
            case Key.P: Vm.SelectPreviousCommand.Execute(null); e.Handled = true; break;
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        Vm.Detach();
        base.OnClosed(e);
    }
}
