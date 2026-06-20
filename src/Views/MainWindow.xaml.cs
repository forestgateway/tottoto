using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using todochart.ViewModels;

namespace todochart.Views;

public partial class MainWindow : Window, INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    private MainViewModel Vm => (MainViewModel)DataContext;

    // ── 列幅（ヘッダーと行で共有） ────────────────────────
    private GridLength[] _columnWidths = [new GridLength(1, GridUnitType.Star), new GridLength(80), new GridLength(36)];

    public GridLength[] ColumnWidths
    {
        get => _columnWidths;
        private set { _columnWidths = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ColumnWidths))); }
    }

    public MainWindow()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();

        InitializeComponent();

        var vm = new MainViewModel();
        DataContext = vm;
        vm.RequestBeginEdit += OnRequestBeginEdit;
        // 選択がプログラムで変更されたときに ListBoxItem にキーボードフォーカスを移す
        vm.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName != nameof(vm.Selected)) return;
            // Dispatcher で遅延実行し、ItemContainerGenerator がコンテナを生成するまで待つ
            Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() =>
            {
                var selected = vm.Selected;
                if (selected is null) return;
                // アイテムが可視でなければスクロールして生成を促す
                if (TaskListBox.ItemContainerGenerator.ContainerFromItem(selected) is not ListBoxItem container)
                {
                    TaskListBox.ScrollIntoView(selected);
                    container = TaskListBox.ItemContainerGenerator.ContainerFromItem(selected) as ListBoxItem;
                }

                if (container is not null)
                {
                    // フォーカスを与えてキーボード入力がその行に届くようにする
                    container.Focus();
                    Keyboard.Focus(container);
                }
                else
                {
                    // 最低でも ListBox 自体にフォーカス
                    TaskListBox.Focus();
                }
            }));
        };

        TodayLabel.Text = $"今日: {DateTime.Today.ToString("yyyy'年'M'月'd'日' (ddd)", System.Globalization.CultureInfo.CurrentCulture)}";

        Loaded += OnLoaded;

        sw.Stop();
        System.Diagnostics.Debug.WriteLine($"[STARTUP] MainWindow 初期化完了: {sw.ElapsedMilliseconds}ms");
    }
}

