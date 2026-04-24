using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using todochart.Controls;
using todochart.ViewModels;

namespace todochart.Views;

public partial class MainWindow : Window, INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    private MainViewModel Vm => (MainViewModel)DataContext;

    // チャートの縦スクロールをタスクリストに同期する際の再帰防止フラグ
    private bool _syncingScroll;

    // 列幅変更の自動保存用タイマー（デバウンス）
    private DispatcherTimer? _colWidthSaveTimer;

    // 左ペイン幅変更の自動保存用タイマー（デバウンス）
    private DispatcherTimer? _paneWidthSaveTimer;

    // ── 列幅 ──────────────────────────────────────────────────────────────
    private GridLength[] _columnWidths = [new GridLength(1, GridUnitType.Star), new GridLength(80), new GridLength(36)];

    /// <summary>タスクリストの列幅（ヘッダー GridSplitter と行の両方で共有）</summary>
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

    // ──── 初期化 ────────────────────────────────────────────────────────────
    private ScrollViewer? _taskScrollViewer;

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        // ListBox 内の ScrollViewer を取得してスクロール同期に使う
        _taskScrollViewer = GetScrollViewer(TaskListBox);
        if (_taskScrollViewer is not null)
            _taskScrollViewer.ScrollChanged += OnTaskScrollChanged;

        // ヘッダー列幅の変化を監視してリスト行に同期する
        HookHeaderColumnWidths();

        // 保存済みの列幅を復元する
        RestoreColumnWidths();

        // 左ペイン（タスクリスト）の幅を復元する
        RestoreTaskPaneWidth();

        // 左ペイン幅の変化を監視して保存する
        HookTaskPaneWidth();

        // ── 段階的初期化
        // Step1: この時点で Schedules[0] のタスクリスト（FlatItems）は
        //        コンストラクタで構築済み → UI はすでに表示されている

        // Step2: Schedules[0] のガントチャートセルをバックグラウンドで構築
        Dispatcher.BeginInvoke(DispatcherPriority.Background, () =>
        {
            Vm.RefreshAllChartCells();
            Vm.IsChartReady = true;
            // 初期 Canvas サイズをコンテンツサイズに合わせる
            ChartCalloutOverlay.UpdateCanvasSize(
                ChartRowsControl.ActualWidth,
                ChartRowsControl.ActualHeight);

            // Step3: 残タブ（_pendingFiles）をガントチャート構築後にロード
            Dispatcher.BeginInvoke(DispatcherPriority.Background, () =>
            {
                Vm.LoadPendingFiles();
            });
        });
    }

    /// <summary>ヘッダーの各 ColumnDefinition の Width 変化を監視する</summary>
    private void HookHeaderColumnWidths()
    {
        var descriptor = DependencyPropertyDescriptor.FromProperty(
            ColumnDefinition.WidthProperty, typeof(ColumnDefinition));

        descriptor.AddValueChanged(ColName,   OnHeaderColumnWidthChanged);
        descriptor.AddValueChanged(ColStatus, OnHeaderColumnWidthChanged);
        descriptor.AddValueChanged(ColML,     OnHeaderColumnWidthChanged);
    }

    private void OnHeaderColumnWidthChanged(object? sender, EventArgs e)
    {
        // ヘッダー列の現在の ActualWidth から GridLength を再構築してバインドを更新する
        // GridSplitter 操作後は Width が Star/Pixel 混在になるため ActualWidth で Pixel 固定する
        // ただし初期の Star 列 (ColName) はリサイズ後 Pixel になるため問題ない
        _columnWidths[0] = ColName.Width;
        _columnWidths[1] = ColStatus.Width;
        _columnWidths[2] = ColML.Width;
        // 配列の中身を差し替えたので PropertyChanged で通知
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ColumnWidths)));

        // デバウンス: 500ms 後に設定を保存する
        if (_colWidthSaveTimer is null)
        {
            _colWidthSaveTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
            _colWidthSaveTimer.Tick += (_, _) =>
            {
                _colWidthSaveTimer.Stop();
                var nameW   = ColName.ActualWidth;
                var statusW = ColStatus.ActualWidth;
                var mlW     = ColML.ActualWidth;
                Vm.SaveTaskColumnWidths(nameW, statusW, mlW);
            };
        }
        _colWidthSaveTimer.Stop();
        _colWidthSaveTimer.Start();
    }

    /// <summary>保存済みの列幅を復元する。</summary>
    private void RestoreColumnWidths()
    {
        var (nameW, statusW, mlW) = Vm.GetTaskColumnWidths();

        if (nameW > 0)
            ColName.Width = new GridLength(nameW, GridUnitType.Pixel);
        if (statusW > 0)
            ColStatus.Width = new GridLength(statusW, GridUnitType.Pixel);
        if (mlW > 0)
            ColML.Width = new GridLength(mlW, GridUnitType.Pixel);
    }

    /// <summary>保存済みのタスクペイン幅を LeftColDef に適用する。</summary>
    private void RestoreTaskPaneWidth()
    {
        var w = Vm.GetTaskPaneWidth();
        if (w > 0)
            LeftColDef.Width = new GridLength(w, GridUnitType.Pixel);
    }

    /// <summary>LeftColDef の幅変化を監視してデバウンス保存する。</summary>
    private void HookTaskPaneWidth()
    {
        var descriptor = DependencyPropertyDescriptor.FromProperty(
            ColumnDefinition.WidthProperty, typeof(ColumnDefinition));
        descriptor.AddValueChanged(LeftColDef, OnTaskPaneWidthChanged);
    }

    private void OnTaskPaneWidthChanged(object? sender, EventArgs e)
    {
        if (_paneWidthSaveTimer is null)
        {
            _paneWidthSaveTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
            _paneWidthSaveTimer.Tick += (_, _) =>
            {
                _paneWidthSaveTimer.Stop();
                Vm.SaveTaskPaneWidth(LeftColDef.ActualWidth);
            };
        }
        _paneWidthSaveTimer.Stop();
        _paneWidthSaveTimer.Start();
    }

    private static ScrollViewer? GetScrollViewer(DependencyObject? root)
    {
        if (root is null) return null;
        if (root is ScrollViewer sv) return sv;
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var result = GetScrollViewer(VisualTreeHelper.GetChild(root, i));
            if (result is not null) return result;
        }
        return null;
    }

    // ── スクロール同期 ────────────────────────────────────
    private void OnTaskScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        if (_syncingScroll) return;
        _syncingScroll = true;
        ChartBodyScroll.ScrollToVerticalOffset(e.VerticalOffset);
        _syncingScroll = false;
    }

    private void OnChartBodyScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        // 水平スクロールをヘッダーに同期
        ChartHeaderScroll.ScrollToHorizontalOffset(e.HorizontalOffset);

        // 吹き出しオーバーレイのスクロールを同期
        ChartCalloutOverlay.SetScrollOffset(e.HorizontalOffset, e.VerticalOffset);

        // オーバーレイ Canvas のサイズをスクロールコンテンツ全体に合わせる
        // （Canvas は子要素サイズを測定に使わないため手動で設定する必要がある）
        ChartCalloutOverlay.UpdateCanvasSize(
            ChartRowsControl.ActualWidth,
            ChartRowsControl.ActualHeight);

        // 垂直スクロールをタスクリストに同期
        if (_syncingScroll || _taskScrollViewer is null) return;
        _syncingScroll = true;
        _taskScrollViewer.ScrollToVerticalOffset(e.VerticalOffset);
        _syncingScroll = false;
    }

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
        // TaskListBox が仮想化で ItemContainer を生成するまで Background 優先度で遅延
        Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Background, () =>
        {
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

    // ── タスクリスト ドラッグ＆ドロップ ──────────────────
    private Point?              _dragStartPoint;
    private TaskRowViewModel?   _dragSource;
    private DropIndicatorAdorner? _dropAdorner;
    private UIElement?          _adornerTarget;

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

        // ドラッグ開始
        var data = new DataObject(typeof(TaskRowViewModel), _dragSource);
        DragDrop.DoDragDrop(TaskListBox, data, DragDropEffects.Move);
        ResetDrag();
    }

    private void OnTaskListDragOver(object sender, DragEventArgs e)
    {
        // ファイルドロップ
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            e.Effects = DragDropEffects.Copy;
            e.Handled = true;
            return;
        }

        // URLドロップ (ブラウザ等からのリンクドラッグ)
        if (e.Data.GetDataPresent(DataFormats.UnicodeText) ||
            e.Data.GetDataPresent("UniformResourceLocator") ||
            e.Data.GetDataPresent("UniformResourceLocatorW"))
        {
            e.Effects = DragDropEffects.Copy;
            e.Handled = true;
            return;
        }

        // 行の並び替え
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

        // ドロップ位置のAdornerを更新
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
        // ListBox 外に出たときのみクリア（子要素間移動では発生させない）
        var pos = e.GetPosition(TaskListBox);
        var bounds = new Rect(TaskListBox.RenderSize);
        if (!bounds.Contains(pos))
            ClearAdorner();
    }

    private void OnTaskListDrop(object sender, DragEventArgs e)
    {
        ClearAdorner();

        // ファイルドロップ：ファイル名（拡張子なし）をタスク名としてリンクに登録
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            var files = (string[])e.Data.GetData(DataFormats.FileDrop);
            var nearItem = GetRowViewModelAt(e.GetPosition(TaskListBox))?.Item;
            foreach (var file in files)
                Vm.AddToDoFromFile(file, nearItem);
            e.Handled = true;
            return;
        }

        // URLドロップ：URLをリンクに登録してタスク新規作成
        string? url = GetUrlFromDragData(e.Data);
        if (url is not null)
        {
            var nearItem = GetRowViewModelAt(e.GetPosition(TaskListBox))?.Item;
            _ = Vm.AddToDoFromUrl(url, nearItem);
            e.Handled = true;
            return;
        }

        // 行の並び替え
        if (!e.Data.GetDataPresent(typeof(TaskRowViewModel))) return;

        var dragRow = (TaskRowViewModel)e.Data.GetData(typeof(TaskRowViewModel));
        var (targetRow, _, insertPos) = GetDropTarget(e);

        if (targetRow is null || dragRow.Item == targetRow.Item) return;

        Vm.MoveItemByDrag(dragRow.Item, targetRow.Item, insertPos);
        e.Handled = true;
    }

    /// <summary>マウス位置から対象行ViewModel・UIElement・挿入位置(-1/0/1)を返す</summary>
    private (TaskRowViewModel? row, UIElement? element, int position) GetDropTarget(DragEventArgs e)
    {
        var hit = TaskListBox.InputHitTest(e.GetPosition(TaskListBox)) as DependencyObject;
        while (hit is not null && hit is not ListBoxItem)
            hit = VisualTreeHelper.GetParent(hit);

        if (hit is not ListBoxItem lbi) return (null, null, -1);
        if (lbi.DataContext is not TaskRowViewModel row) return (null, null, -1);

        // アイテム内の縦位置で挿入位置を決定
        var localPos = e.GetPosition(lbi);
        double h = lbi.ActualHeight;
        int pos;
        if (row.IsFolder && localPos.Y > h * 0.25 && localPos.Y < h * 0.75)
            pos = 0;   // フォルダの中央ゾーン → 子として追加
        else if (localPos.Y <= h * 0.5)
            pos = -1;  // 上半分 → 前に挿入
        else
            pos = 1;   // 下半分 → 後に挿入

        return (row, lbi, pos);
    }

    private TaskRowViewModel? GetRowViewModelAt(Point listBoxPoint)
    {
        var hit = TaskListBox.InputHitTest(listBoxPoint) as DependencyObject;
        while (hit is not null && hit is not ListBoxItem)
            hit = VisualTreeHelper.GetParent(hit);
        return (hit as ListBoxItem)?.DataContext as TaskRowViewModel;
    }

    /// <summary>
    /// ドラッグデータからURLを取り出す。
    /// ブラウザは "UniformResourceLocator" / "UniformResourceLocatorW" / UnicodeText の
    /// いずれかでURLを渡してくる。
    /// </summary>
    private static string? GetUrlFromDragData(IDataObject data)
    {
        // "UniformResourceLocatorW" (Unicode版) を優先
        foreach (var fmt in new[] { "UniformResourceLocatorW", "UniformResourceLocator" })
        {
            if (!data.GetDataPresent(fmt)) continue;
            var raw = data.GetData(fmt);
            string? url = raw switch
            {
                string s => s.Trim(),
                System.IO.Stream st => new System.IO.StreamReader(st).ReadToEnd().Trim('\0').Trim(),
                _ => null,
            };
            if (!string.IsNullOrEmpty(url) && IsUrl(url)) return url;
        }

        // UnicodeText フォールバック（テキストエリアからのドラッグ等）
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

    // ──── メニュー ────────────────────────────────────────────────────────
    private void OnExitClick(object sender, RoutedEventArgs e) => Close();

    private void OnAboutClick(object sender, RoutedEventArgs e)
    {
        var asm     = System.Reflection.Assembly.GetExecutingAssembly();
        var version = asm.GetName().Version?.ToString(3) ?? "1.0.0";
        var copy    = System.Diagnostics.FileVersionInfo.GetVersionInfo(asm.Location).LegalCopyright
                      ?? "Copyright (c) 2026 Toyoshige Kido";

        MessageBox.Show(
            $"Tottoto  ver {version}\n\n{copy}\n\nThis software uses .NET 8 / WPF\nCopyright (c) Microsoft Corporation\nLicensed under the MIT License.",
            "バージョン情報",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }

    // ──── スケジュールタブ ────────────────────────────────────────────────
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

    // ──── スケジュールタブ ドラッグ&ドロップ ─────────────────────────────
    private const string TabDragFormat = "todochart.ScheduleEntry";
    private Point?         _tabDragStartPoint;
    private ScheduleEntry? _tabDragSource;
    private Border?        _tabDropTarget;
    private bool           _tabDropAfter;

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

        bool after = e.GetPosition(fe).X > (fe as Border)?.ActualWidth / 2;
        int targetIndex = Vm.Schedules.IndexOf(target);
        if (after) targetIndex++;

        // ドラッグ元が挿入先より前にある場合、移動後のインデックスを補正
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

    // ──── ウィンドウ終了 ──────────────────────────────────────────────────
    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        foreach (var entry in Vm.Schedules.ToList())
        {
            if (!Vm.ConfirmDiscard(entry))
            {
                e.Cancel = true;
                return;
            }
        }

        Vm.SaveSettings();
        base.OnClosing(e);
    }

    // ── チャートドラッグスクロール ────────────────────────
    private bool   _chartDragging;
    private bool   _chartPotentialDrag;
    private Point  _chartDragStart;
    private double _chartScrollStart;

    private void OnChartMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        // Begin potential drag but do not capture immediately so click handlers (elements) run first.
        _chartPotentialDrag = true;
        _chartDragStart = e.GetPosition(ChartBodyScroll);
        _chartScrollStart = ChartBodyScroll.HorizontalOffset;
    }

    private TaskRowViewModel? _hoveredChartRow;

    private void OnChartMouseMove(object sender, MouseEventArgs e)
    {
        // If potential drag and user moved enough with left button pressed, start actual dragging/capture
        if (_chartPotentialDrag && e.LeftButton == MouseButtonState.Pressed && !_chartDragging)
        {
            var pos = e.GetPosition(ChartBodyScroll);
            var dx = Math.Abs(pos.X - _chartDragStart.X);
            if (dx >= SystemParameters.MinimumHorizontalDragDistance)
            {
                _chartDragging = true;
                ChartBodyScroll.CaptureMouse();
                ChartBodyScroll.Cursor = Cursors.SizeWE;
            }
        }

        if (_chartDragging)
        {
            double delta = e.GetPosition(ChartBodyScroll).X - _chartDragStart.X;
            ChartBodyScroll.ScrollToHorizontalOffset(_chartScrollStart - delta);
            e.Handled = true;
        }

        // マウス位置に応じたコラムの Hover 表示（既存）
        double cellWidth = (double)FindResource("CellWidth");
        double posX = e.GetPosition(ChartRowsControl).X;
        int col = (int)(posX / cellWidth);
        int count = Vm.FlatItems.FirstOrDefault()?.ChartCells.Count ?? 0;
        Vm.HoveredColumnIndex = (col >= 0 && col < count) ? col : -1;

        // HoverOnly 吹き出し：マウス位置の行を特定してホバー状態を更新
        var rowHeight = (double)FindResource("RowHeight");
        double posY = e.GetPosition(ChartRowsControl).Y;
        int rowIndex = (int)(posY / rowHeight);
        var newHoveredRow = (rowIndex >= 0 && rowIndex < Vm.FlatItems.Count)
            ? Vm.FlatItems[rowIndex] : null;
        if (_hoveredChartRow != newHoveredRow)
        {
            if (_hoveredChartRow is not null) _hoveredChartRow.IsChartRowHovered = false;
            _hoveredChartRow = newHoveredRow;
            if (_hoveredChartRow is not null) _hoveredChartRow.IsChartRowHovered = true;
        }
    }

    private TaskRowViewModel? GetChartRowViewModelAt(Point chartPoint)
    {
        var hit = ChartRowsControl.InputHitTest(chartPoint) as DependencyObject;
        while (hit is not null && hit is not FrameworkElement)
            hit = VisualTreeHelper.GetParent(hit);
        // Walk up to find element with DataContext TaskRowViewModel
        while (hit is not null)
        {
            if (hit is FrameworkElement fe && fe.DataContext is TaskRowViewModel row)
                return row;
            hit = VisualTreeHelper.GetParent(hit);
        }
        return null;
    }

    private void OnChartMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_chartDragging)
        {
            _chartDragging = false;
            ChartBodyScroll.ReleaseMouseCapture();
            ChartBodyScroll.Cursor = Cursors.Arrow;
            e.Handled = true;
        }
        else if (_chartPotentialDrag)
        {
            // This was a click (no actual drag). Select the row under mouse if any.
            var pos = e.GetPosition(ChartRowsControl);
            var row = GetChartRowViewModelAt(pos);
            if (row is not null && Vm != null)
            {
                Vm.Selected = row;
                e.Handled = true;
            }
        }

        // clear potential drag in any case
        _chartPotentialDrag = false;
    }

    private void OnChartMouseLeave(object sender, MouseEventArgs e)
    {
        if (_chartDragging)
        {
            _chartDragging = false;
            ChartBodyScroll.ReleaseMouseCapture();
            ChartBodyScroll.Cursor = Cursors.Arrow;
        }
        _chartPotentialDrag = false;
        Vm.HoveredColumnIndex = -1;

        if (_hoveredChartRow is not null)
        {
            _hoveredChartRow.IsChartRowHovered = false;
            _hoveredChartRow = null;
        }
    }


    // ── 吹き出し ──────────────────────────────────────────────────────────

    private void OnChartRowsControlSizeChanged(object sender, SizeChangedEventArgs e)
    {
        ChartCalloutOverlay.UpdateCanvasSize(
            ChartRowsControl.ActualWidth,
            ChartRowsControl.ActualHeight);
    }

    private void OnChartMouseRightButtonUp(object sender, MouseButtonEventArgs e)
    {
        var pos = e.GetPosition(ChartRowsControl);
        var row = GetChartRowViewModelAt(pos);
        if (row?.Item is not todochart.Models.ScheduleToDo) return;

        double cellWidth = (double)FindResource("CellWidth");
        int col = (int)(pos.X / cellWidth);
        int count = row.ChartCells.Count;
        if (col < 0 || col >= count) return;

        var menu = new ContextMenu();
        var addItem = new MenuItem { Header = "吹き出しを追加(_B)" };
        addItem.Click += (_, _) =>
        {
            var calloutVm = Vm.AddCallout(row.Item, col);
            if (calloutVm is not null)
                Dispatcher.BeginInvoke(DispatcherPriority.Background, () => calloutVm.BeginEdit());
        };
        menu.Items.Add(addItem);
        menu.PlacementTarget = ChartBodyScroll;
        menu.IsOpen = true;
        e.Handled = true;
    }
}
