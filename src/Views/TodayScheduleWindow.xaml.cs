using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Threading;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using todochart.ViewModels;
using todochart.Services;

namespace todochart.Views;

public partial class TodayScheduleWindow : Window
{
    private TodayScheduleViewModel Vm => (TodayScheduleViewModel)DataContext;

    public TodayScheduleWindow(TodayScheduleViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;
        // テーマに応じた Window スタイルを適用
        ApplyThemeWindowStyle();
        ThemeService.ThemeChanged += ApplyThemeWindowStyle;
        // ウィンドウ状態変更で最大化ボタン表示を更新
        this.StateChanged += (s, e) => UpdateMaximizeIcon();
        UpdateMaximizeIcon();
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
                var containerObj = TaskListBox.ItemContainerGenerator.ContainerFromItem(selected);
                if (containerObj is not ListBoxItem)
                {
                    TaskListBox.ScrollIntoView(selected);
                    containerObj = TaskListBox.ItemContainerGenerator.ContainerFromItem(selected);
                }

                var container = containerObj as ListBoxItem;
                if (container != null)
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

    private void ApplyThemeWindowStyle()
    {
        // UI スレッドで実行
        Dispatcher.BeginInvoke(new Action(() =>
        {
            try
            {
                var styleObj = TryFindResource("TransparentWindowStyle");
                if (styleObj is Style s)
                {
                    Style = s;
                }
                else
                {
                    // テーマが透明ウィンドウを提供していない場合は既定に戻す
                    ClearValue(StyleProperty);
                    // AllowsTransparency はスタイルに依存するため、強制的に設定しない
                }
            }
            catch { }
        }));
    }

    private void OnListBoxMouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (Vm.Selected is not null)
            Vm.EditCommand.Execute(null);
    }

    private void OnMinimizeClick(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void OnMaximizeRestoreClick(object sender, RoutedEventArgs e)
    {
        WindowState = (WindowState == WindowState.Maximized) ? WindowState.Normal : WindowState.Maximized;
    }

    private void OnCloseClick(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void OnTitleBarMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        // ダブルクリックで最大化/復元
        if (e.ChangedButton == MouseButton.Left && e.ClickCount == 2)
        {
            WindowState = (WindowState == WindowState.Maximized) ? WindowState.Normal : WindowState.Maximized;
            e.Handled = true;
            return;
        }

        // 単一クリックでドラッグ移動（ただしインタラクティブなコントロールは除外）
        if (e.ChangedButton == MouseButton.Left && e.ClickCount == 1)
        {
            if (!(e.OriginalSource is DependencyObject dep && IsControlInteractive(dep)))
            {
                try { DragMove(); }
                catch { }
                e.Handled = true;
            }
        }
    }

    private void UpdateMaximizeIcon()
    {
        try
        {
            if (BtnMax != null)
            {
                BtnMax.Content = (WindowState == WindowState.Maximized) ? "❐" : "▢";
                BtnMax.ToolTip = (WindowState == WindowState.Maximized) ? "復元" : "最大化";
            }
        }
        catch { }
    }

    private static bool IsControlInteractive(DependencyObject? dep)
    {
        while (dep != null)
        {
            if (dep is System.Windows.Controls.Primitives.ButtonBase
                || dep is System.Windows.Controls.Primitives.TextBoxBase
                || dep is System.Windows.Controls.Menu
                || dep is System.Windows.Controls.MenuItem
                || dep is System.Windows.Controls.Primitives.Thumb
                || dep is System.Windows.Controls.Primitives.ScrollBar)
            {
                return true;
            }
            dep = VisualTreeHelper.GetParent(dep);
        }
        return false;
    }

    private void Window_MouseLeftButtonDown(object? sender, MouseButtonEventArgs e)
    {
        // クリック元が操作可能なコントロール（ボタン等）の場合はドラッグを開始しない
        if (e.OriginalSource is DependencyObject dep && IsControlInteractive(dep))
            return;

        // 背景やヘッダー領域をドラッグしてウィンドウを移動できるようにする
        if (e.ButtonState == MouseButtonState.Pressed)
        {
            try
            {
                DragMove();
            }
            catch
            {
                // DragMove が失敗してもアプリを落とさない
            }
        }
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
        ThemeService.ThemeChanged -= ApplyThemeWindowStyle;
        base.OnClosed(e);
    }
}
