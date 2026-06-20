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

        // 起動 10 秒後に非同期で更新確認を開始
        _ = CheckForUpdateDelayedAsync();

        sw.Stop();
        System.Diagnostics.Debug.WriteLine($"[STARTUP] MainWindow 初期化完了: {sw.ElapsedMilliseconds}ms");
    }

    private async Task CheckForUpdateDelayedAsync()
    {
        try
        {
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

