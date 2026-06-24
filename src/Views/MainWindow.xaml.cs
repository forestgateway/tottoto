using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Input;
using System.Windows.Threading;
using todochart.Services;
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
                var containerObj = TaskListBox.ItemContainerGenerator.ContainerFromItem(selected);
                if (containerObj is not ListBoxItem)
                {
                    TaskListBox.ScrollIntoView(selected);
                    containerObj = TaskListBox.ItemContainerGenerator.ContainerFromItem(selected);
                }

                var container = containerObj as ListBoxItem;
                if (container != null)
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

        // ウィンドウ状態変更に応じて最大化ボタンの表示を更新
        this.StateChanged += (s, e) => UpdateMaximizeIcon();
        UpdateMaximizeIcon();

        // 透過ウィンドウでタイトルバーがないため、ウィンドウのドラッグ移動を許可する
        // Window_MouseLeftButtonDown イベントハンドラは XAML 側で指定しています

        // 起動 10 秒後に非同期で更新確認を開始（設定が有効な場合のみ）
        _ = CheckForUpdateDelayedAsync(AppSettings.Load());

        sw.Stop();
        System.Diagnostics.Debug.WriteLine($"[STARTUP] MainWindow 初期化完了: {sw.ElapsedMilliseconds}ms");
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

        // 単一クリックでドラッグ移動（ただしボタン等のインタラクティブ要素は除外）
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
        catch
        {
            // 無視
        }
    }

    private async Task CheckForUpdateDelayedAsync(AppSettings settings)
    {
        try
        {
            if (!settings.CheckForUpdatesOnStartup)
                return;

            // 起動後 10 秒待つ
            await Task.Delay(TimeSpan.FromSeconds(10));

            // 10 秒でタイムアウトする更新チェック
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            var svc = new todochart.Services.UpdateCheckService();
            var info = await svc.CheckAsync(cts.Token);

            if (info.IsUpdateAvailable)
            {
                Dispatcher.Invoke(() => { UpdateAvailableButton.Visibility = Visibility.Visible; });
            }
        }
        catch (OperationCanceledException)
        {
            // タイムアウト時は無視
        }
        catch
        {
            // 失敗時は無視
        }
    }
}

