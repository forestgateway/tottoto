using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using todochart.ViewModels;

namespace todochart.Views;

/// <summary>インライン名前編集・Ctrl+C/X/V キーハンドリング。</summary>
public partial class MainWindow
{
    // ── ダブルクリックでプロパティ編集 ───────────────────
    private void OnListBoxMouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (Vm.Selected is not null)
            Vm.EditCommand.Execute(null);
    }

    // ── インライン名前編集 ────────────────────────────────

    /// <summary>TextBox が表示されたら全選択してフォーカスを当てる</summary>
    private void OnNameEditorIsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (sender is TextBox tb && tb.IsVisible)
        {
            tb.Dispatcher.BeginInvoke(() =>
            {
                tb.Focus();
                tb.SelectAll();
            });
        }
    }

    /// <summary>新規タスク追加時に ListBox が行を生成するまで待ってから BeginEdit を呼ぶ</summary>
    private void OnRequestBeginEdit(TaskRowViewModel row)
    {
        Dispatcher.BeginInvoke(DispatcherPriority.Background, () =>
        {
            if (TaskListBox.ItemContainerGenerator.ContainerFromItem(row) is null)
                TaskListBox.ScrollIntoView(row);
            row.BeginEdit();
        });
    }

    /// <summary>Enter で確定、Escape でキャンセル</summary>
    private void OnNameEditorKeyDown(object sender, KeyEventArgs e)
    {
        if (sender is not TextBox tb) return;
        if (tb.DataContext is not TaskRowViewModel row) return;

        if (e.Key == Key.Return)
        {
            row.CommitEdit();
            e.Handled = true;
            TaskListBox.Focus();
        }
        else if (e.Key == Key.Escape)
        {
            row.CancelEdit();
            e.Handled = true;
            TaskListBox.Focus();
        }
    }

    /// <summary>フォーカスが外れたら確定</summary>
    private void OnNameEditorLostFocus(object sender, RoutedEventArgs e)
    {
        if (sender is TextBox tb && tb.DataContext is TaskRowViewModel row)
            row.CommitEdit();
    }

    // ListBox の PreviewKeyDown で Ctrl+C/X/V をコマンド経由に統一する。
    // TextBox 編集中は TextBox のデフォルト動作を優先するため、インライン編集中はスキップ。
    private void OnTaskListPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key is not (Key.C or Key.X or Key.V)) return;
        if ((Keyboard.Modifiers & ModifierKeys.Control) == 0) return;
        if (Keyboard.FocusedElement is TextBox) return;

        switch (e.Key)
        {
            case Key.C: Vm.CopyItemCommand.Execute(null);  e.Handled = true; break;
            case Key.X: Vm.CutItemCommand.Execute(null);   e.Handled = true; break;
            case Key.V: Vm.PasteItemCommand.Execute(null); e.Handled = true; break;
        }
    }
}
