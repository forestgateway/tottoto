using System.Windows;
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

    protected override void OnClosed(EventArgs e)
    {
        Vm.Detach();
        base.OnClosed(e);
    }
}
