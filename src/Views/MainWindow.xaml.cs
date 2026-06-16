using System.ComponentModel;
using System.Windows;
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

        TodayLabel.Text = $"今日: {DateTime.Today.ToString("yyyy'年'M'月'd'日' (ddd)", System.Globalization.CultureInfo.CurrentCulture)}";

        Loaded += OnLoaded;

        sw.Stop();
        System.Diagnostics.Debug.WriteLine($"[STARTUP] MainWindow 初期化完了: {sw.ElapsedMilliseconds}ms");
    }
}

