using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Input;
using Microsoft.Win32;
using todochart.Models;
using todochart.Services;

namespace todochart.ViewModels;

/// <summary>
/// メインウィンドウの ViewModel。
/// 複数のセーブファイル（ScheduleEntry）を一括管理し、
/// FlatItems にすべてのエントリを展開して表示する。
/// </summary>
public class MainViewModel : ViewModelBase
{
    /// <summary>インライン編集を開始すべき行を通知するイベント。</summary>
    public event Action<TaskRowViewModel>? RequestBeginEdit;

    // ──── サービス ─────────────────────────────────────────────────────────
    public  readonly HolidayService      Holidays    = new();
    private readonly ScheduleFileService _fileService = new();
    private readonly AppSettings         _settings;

    // ──── 複数エントリ ─────────────────────────────────────────────────────
    public ObservableCollection<ScheduleEntry> Schedules { get; } = new();

    private ScheduleEntry? _activeEntry;
    public ScheduleEntry? ActiveEntry
    {
        get => _activeEntry;
        set
        {
            var changed = _activeEntry != value;
            _activeEntry = value;
            OnPropertyChanged();
            if (changed)
            {
                OnPropertyChanged(nameof(WindowTitle));
                OnPropertyChanged(nameof(HasActiveEntry));
                CommandManager.InvalidateRequerySuggested();
                RefreshFlatList();
            }
        }
    }

    public bool HasActiveEntry => _activeEntry is not null;

    // ──── フラットリスト ───────────────────────────────────────────────────
    public ObservableCollection<TaskRowViewModel> FlatItems { get; } = new();

    // ──── 吹き出し（全行集約・チャート全体オーバーレイ用） ──────────────────
    public ObservableCollection<CalloutViewModel> AllCallouts { get; } = new();

    // ──── チャート ─────────────────────────────────────────────────────────
    private DateTime _chartStart;
    public DateTime ChartStart
    {
        get => _chartStart;
        set
        {
            if (SetField(ref _chartStart, value))
            {
                OnPropertyChanged(nameof(ChartDays));
                RefreshAllChartCells();
            }
        }
    }

    public const int CellCount = 60;
    public DateTime Today { get; } = DateTime.Today;

    public IReadOnlyList<ChartDayHeaderInfo> ChartDays { get; private set; } = Array.Empty<ChartDayHeaderInfo>();

    // ──── ウィンドウタイトル ───────────────────────────────────────────────
    public string WindowTitle
    {
        get
        {
            if (_activeEntry is null) return "Tottoto";
            return _activeEntry.TabTitle + " - Tottoto";
        }
    }

    // ──── 選択 ─────────────────────────────────────────────────────────────
    private TaskRowViewModel? _selected;
    public TaskRowViewModel? Selected
    {
        get => _selected;
        set
        {
            if (_selected == value) return;
            if (_selected != null) _selected.IsSelected = false;
            _selected = value;
            if (_selected != null) _selected.IsSelected = true;

            // 選択行のエントリをアクティブにする
            if (_selected is not null)
            {
                var entry = FindEntryForItem(_selected.Item);
                if (entry is not null)
                    ActiveEntry = entry;
            }

            OnPropertyChanged();
            OnPropertyChanged(nameof(IsContextCompleteEnabled));
            OnPropertyChanged(nameof(ContextCompleteHeader));
            OnPropertyChanged(nameof(IsContextWaitEnabled));
            OnPropertyChanged(nameof(ContextWaitHeader));
            _todayVm?.NotifySelectedChanged();
            CommandManager.InvalidateRequerySuggested();
        }
    }

    /// <summary>
    /// ActiveEntry（タブ）を切り替えずに選択行だけ変える。今日の予定ウィンドウ用。
    /// </summary>
    public void SelectWithoutTabSwitch(TaskRowViewModel? row)
    {
        if (_selected == row) return;
        if (_selected != null) _selected.IsSelected = false;
        _selected = row;
        if (_selected != null) _selected.IsSelected = true;

        OnPropertyChanged(nameof(Selected));
        OnPropertyChanged(nameof(IsContextCompleteEnabled));
        OnPropertyChanged(nameof(ContextCompleteHeader));
        OnPropertyChanged(nameof(IsContextWaitEnabled));
        OnPropertyChanged(nameof(ContextWaitHeader));
        _todayVm?.NotifySelectedChanged();
        CommandManager.InvalidateRequerySuggested();
    }

    public bool IsContextCompleteEnabled => Selected?.Item is ScheduleToDo;

    public string ContextCompleteHeader =>
        Selected?.Item is ScheduleToDo todo && todo.Completed ? "完了を外す(_C)" : "完了(_C)";

    public bool IsContextWaitEnabled => Selected?.Item is ScheduleToDo td && !td.Completed;

    public string ContextWaitHeader =>
        Selected?.Item is ScheduleToDo todo2 && todo2.IsWait ? "WAITを外す(_W)" : "WAIT(_W)";

    private string _statusText = "準備完了";
    public string StatusText
    {
        get => _statusText;
        set => SetField(ref _statusText, value);
    }

    private int _hoveredColumnIndex = -1;
    public int HoveredColumnIndex
    {
        get => _hoveredColumnIndex;
        set => SetField(ref _hoveredColumnIndex, value);
    }

    private bool _isChartReady;
    public bool IsChartReady
    {
        get => _isChartReady;
        set => SetField(ref _isChartReady, value);
    }

    // ──── 完了タスク非表示 ─────────────────────────────────────────────────
    private bool _hideCompleted;
    public bool HideCompleted
    {
        get => _hideCompleted;
        set
        {
            if (SetField(ref _hideCompleted, value))
            {
                _settings.HideCompleted = value;
                _settings.Save();
                RefreshFlatList();
            }
        }
    }

    // ──── コンストラクタ ───────────────────────────────────────────────────
    public MainViewModel()
    {
        _settings = AppSettings.Load();

        Holidays.SetWeekdayLevels(_settings.WeekdayLevels);
        Holidays.AlertCount     = _settings.AlertCount;
        Holidays.DateCountLevel = _settings.DateCountLevel;
        // 個別休日データのロード（AppSettings.SpecialHolidays があれば）
        if (_settings.SpecialHolidays is not null)
        {
            Holidays.ImportSpecialHolidays(_settings.SpecialHolidays);
        }

        _chartStart    = DateTime.Today.AddDays(_settings.ChartOffsetFromToday);
        _hideCompleted = _settings.HideCompleted;
        RefreshChartDays();

        InitCommands();

        // コマンドライン引数 → OpenFiles → LastFile の優先順で開く
        // 起動高速化: 最初の1件のみ同期ロード（タスクリスト即時表示）、残りは遅延ロード
        var args = Environment.GetCommandLineArgs();
        if (args.Length > 1 && File.Exists(args[1]))
        {
            LoadFile(args[1]);
        }
        else if (_settings.OpenFiles.Count > 0)
        {
            var validPaths = _settings.OpenFiles.Where(File.Exists).ToList();
            if (validPaths.Count > 0)
            {
                LoadFile(validPaths[0]);
                _pendingFiles.AddRange(validPaths.Skip(1));
            }
            if (Schedules.Count == 0) AddNewSchedule();
        }
        else if (!string.IsNullOrEmpty(_settings.LastFile) && File.Exists(_settings.LastFile))
        {
            LoadFile(_settings.LastFile);
        }
        else
        {
            AddNewSchedule();
        }
    }

    // 起動時の遅延ロード対象ファイルパス
    private readonly List<string> _pendingFiles = new();

    /// <summary>
    /// 起動時に遅延ロードが必要な残タブを順次ロードする。
    /// MainWindow.OnLoaded からバックグラウンドで呼び出す。
    /// ActiveEntry は Schedules[0]（一番左）のまま維持する。
    /// </summary>
    public void LoadPendingFiles()
    {
        var active = _activeEntry ?? Schedules.FirstOrDefault();
        foreach (var path in _pendingFiles)
            LoadFileBackground(path);
        _pendingFiles.Clear();
        // 一番左タブをアクティブに維持
        if (active is not null && Schedules.Contains(active))
            ActiveEntry = active;
    }

    // ──── コマンド（プロパティ） ───────────────────────────────────────────
    public ICommand NewCommand               { get; private set; } = null!;
    public ICommand OpenCommand              { get; private set; } = null!;
    public ICommand SaveCommand              { get; private set; } = null!;
    public ICommand SaveAsCommand            { get; private set; } = null!;
    public ICommand CloseCommand             { get; private set; } = null!;
    public ICommand CloseScheduleCommand     { get; private set; } = null!;
    public ICommand NewFolderCommand         { get; private set; } = null!;
    public ICommand NewToDoCommand           { get; private set; } = null!;
    public ICommand DeleteCommand            { get; private set; } = null!;
    public ICommand EditCommand              { get; private set; } = null!;
    public ICommand ToggleCompleteCommand    { get; private set; } = null!;
    public ICommand ToggleWaitCommand        { get; private set; } = null!;
    public ICommand MoveUpCommand            { get; private set; } = null!;
    public ICommand MoveDownCommand          { get; private set; } = null!;
    public ICommand ChartNextCommand         { get; private set; } = null!;
    public ICommand ChartPrevCommand         { get; private set; } = null!;
    public ICommand ChartNext7Command        { get; private set; } = null!;
    public ICommand ChartPrev7Command        { get; private set; } = null!;
    public ICommand ChartTodayCommand        { get; private set; } = null!;
    public ICommand ShiftBeginPlusCommand    { get; private set; } = null!;
    public ICommand ShiftBeginMinusCommand   { get; private set; } = null!;
    public ICommand ShiftEndPlusCommand      { get; private set; } = null!;
    public ICommand ShiftEndMinusCommand     { get; private set; } = null!;
    public ICommand ShiftBothPlusCommand     { get; private set; } = null!;
    public ICommand ShiftBothMinusCommand    { get; private set; } = null!;
    public ICommand BeginRenameCommand       { get; private set; } = null!;
    public ICommand ToggleHideCompletedCommand { get; private set; } = null!;
    public ICommand ShowTodayScheduleCommand   { get; private set; } = null!;
    public ICommand MoveScheduleLeftCommand  { get; private set; } = null!;
    public ICommand MoveScheduleRightCommand { get; private set; } = null!;
    public ICommand CloseEntryCommand        { get; private set; } = null!;
    public ICommand ArchiveCommand           { get; private set; } = null!;
    public ICommand ShowArchiveListCommand   { get; private set; } = null!;
    public ICommand NewIssueTrackingCommand      { get; private set; } = null!;
    public ICommand UpdateIssueTrackingCommand   { get; private set; } = null!;
    public ICommand EditIssueTrackingCommand     { get; private set; } = null!;
    public ICommand ChangeThemeCommand           { get; private set; } = null!;
    public ICommand CopyItemCommand              { get; private set; } = null!;
    public ICommand CutItemCommand               { get; private set; } = null!;
    public ICommand PasteItemCommand             { get; private set; } = null!;
    public ICommand ShowHolidaySettingsCommand   { get; private set; } = null!;

    // ── 現在のテーマ名 ────────────────────────────────────
    private string _currentTheme = "Light";
    public string CurrentTheme
    {
        get => _currentTheme;
        private set => SetField(ref _currentTheme, value);
    }

    // ── コピー/カット用の内部クリップボード ──────────────
    private ScheduleItemBase? _clipboardItem;
    private bool _clipboardIsCut;

    // 今日の予定ウィンドウ ViewModel（シングルトン管理）
    private TodayScheduleViewModel? _todayVm;
    public void RegisterTodayVm(TodayScheduleViewModel? vm) => _todayVm = vm;

    // ScheduleEntry から呼ばれる：IsModified 変化時にタイトル更新
    internal void OnEntryModifiedChanged()
    {
        OnPropertyChanged(nameof(WindowTitle));
        CommandManager.InvalidateRequerySuggested();
    }

    private void InitCommands()
    {
        NewCommand           = new RelayCommand(AskAndNew);
        OpenCommand          = new RelayCommand(AskAndOpen);
        SaveCommand          = new RelayCommand(Save,   () => _activeEntry?.IsModified == true);
        SaveAsCommand        = new RelayCommand(SaveAs, () => _activeEntry is not null && !string.IsNullOrEmpty(_activeEntry.ScheduleName));
        CloseCommand         = new RelayCommand(AskAndClose);
        CloseScheduleCommand = new RelayCommand(CloseActiveSchedule, () => _activeEntry is not null);

        NewFolderCommand = new RelayCommand(AddFolder,  () => _activeEntry is not null);
        NewToDoCommand   = new RelayCommand(AddToDo,    () => _activeEntry is not null);
        DeleteCommand    = new RelayCommand(DeleteSelected, () => Selected is not null);
        EditCommand      = new RelayCommand(() => EditItem(Selected!), () => Selected is not null);
        ToggleCompleteCommand = new RelayCommand(
            () => { Selected?.ToggleComplete(); OnPropertyChanged(nameof(ContextCompleteHeader)); OnPropertyChanged(nameof(IsContextWaitEnabled)); },
            () => Selected?.Item is ScheduleToDo);
        ToggleWaitCommand = new RelayCommand(
            () => { ToggleWait(); },
            () => Selected?.Item is ScheduleToDo td && !td.Completed);

        MoveUpCommand   = new RelayCommand(MoveUp,   () => CanMoveUp());
        MoveDownCommand = new RelayCommand(MoveDown, () => CanMoveDown());

        ChartNextCommand  = new RelayCommand(() => ChartStart = ChartStart.AddDays(1));
        ChartPrevCommand  = new RelayCommand(() => ChartStart = ChartStart.AddDays(-1));
        ChartNext7Command = new RelayCommand(() => ChartStart = ChartStart.AddDays(7));
        ChartPrev7Command = new RelayCommand(() => ChartStart = ChartStart.AddDays(-7));
        ChartTodayCommand = new RelayCommand(() => ChartStart = Today.AddDays(-7));

        ShiftBeginPlusCommand  = new RelayCommand(() => ShiftDate(+1, 0), () => Selected?.Item.BeginDate.HasValue == true);
        ShiftBeginMinusCommand = new RelayCommand(() => ShiftDate(-1, 0), () => Selected?.Item.BeginDate.HasValue == true);
        ShiftEndPlusCommand    = new RelayCommand(() => ShiftDate(0, +1), () => Selected?.Item.EndDate.HasValue == true);
        ShiftEndMinusCommand   = new RelayCommand(() => ShiftDate(0, -1), () => Selected?.Item.EndDate.HasValue == true);
        ShiftBothPlusCommand   = new RelayCommand(() => ShiftDate(+1, +1), () => Selected is not null);
        ShiftBothMinusCommand  = new RelayCommand(() => ShiftDate(-1, -1), () => Selected is not null);
        BeginRenameCommand     = new RelayCommand(() => Selected?.BeginEdit(), () => Selected is not null);
        ToggleHideCompletedCommand = new RelayCommand(() => HideCompleted = !HideCompleted);
        ShowTodayScheduleCommand   = new RelayCommand(OpenTodayScheduleWindow);

        MoveScheduleLeftCommand  = new RelayCommand(MoveActiveScheduleLeft,
            () => _activeEntry is not null && Schedules.IndexOf(_activeEntry) > 0);
        MoveScheduleRightCommand = new RelayCommand(MoveActiveScheduleRight,
            () => _activeEntry is not null && Schedules.IndexOf(_activeEntry) < Schedules.Count - 1);
        CloseEntryCommand = new RelayCommand(p => CloseEntry(p as ScheduleEntry));
        ArchiveCommand         = new RelayCommand(ArchiveSelected,
            () => Selected is not null && _activeEntry is not null && !string.IsNullOrEmpty(_activeEntry.FilePath));
        ShowArchiveListCommand = new RelayCommand(OpenArchiveListWindow,
            () => _activeEntry is not null && !string.IsNullOrEmpty(_activeEntry.FilePath));

        NewIssueTrackingCommand    = new RelayCommand(AddNewIssueTracking);
        UpdateIssueTrackingCommand = new RelayCommand(
            () => _ = UpdateIssueTrackingAsync(_activeEntry),
            () => _activeEntry?.IsIssueTracking == true);
        EditIssueTrackingCommand   = new RelayCommand(
            EditIssueTrackingSettings,
            () => _activeEntry?.IsIssueTracking == true);

        ChangeThemeCommand = new RelayCommand(p =>
        {
            if (p is not string name) return;
            CurrentTheme = name;
        });

        CopyItemCommand  = new RelayCommand(CopySelected,  () => Selected is not null);
        CutItemCommand   = new RelayCommand(CutSelected,   () => Selected is not null);
        PasteItemCommand = new RelayCommand(() => _ = PasteAsync());
        ShowHolidaySettingsCommand = new RelayCommand(ShowHolidaySettings);
    }

    // ──── 新規 / 開く / 保存 ───────────────────────────────────────────────

    private void AddNewSchedule()
    {
        var root  = new ScheduleFolder { Name = "新規スケジュール" };
        var entry = new ScheduleEntry(this, root);
        Schedules.Add(entry);
        ActiveEntry = entry;
        RefreshFlatList();
    }

    private void AskAndNew() => AddNewSchedule();

    // ──────── Issue Tracking 新規作成 ──────────────────────────────────
    private void AddNewIssueTracking()
    {
        var vm  = new IssueTrackingSettingsViewModel();
        var dlg = new Views.IssueTrackingSettingsWindow(vm)
        {
            Owner = Application.Current?.MainWindow,
        };
        if (dlg.ShowDialog() != true) return;

        var settings     = vm.ToSettings();
        var displayName  = string.IsNullOrWhiteSpace(settings.DisplayName) ? "新規" : settings.DisplayName;
        var root         = new ScheduleFolder { Name = displayName };
        var entry        = new ScheduleEntry(this, root, scheduleName: displayName)
        {
            IssueSettings = settings,
        };
        Schedules.Add(entry);
        ActiveEntry = entry;
        RefreshFlatList();

        // 初回更新を実行
        _ = UpdateIssueTrackingAsync(entry);
    }

    // ──────── Issue Tracking 設定編集 ────────────────────────────────
    private void EditIssueTrackingSettings()
    {
        if (_activeEntry?.IssueSettings is null) return;
        var vm = IssueTrackingSettingsViewModel.FromSettings(_activeEntry.IssueSettings);
        var dlg = new Views.IssueTrackingSettingsWindow(vm)
        {
            Owner = Application.Current?.MainWindow,
        };
        if (dlg.ShowDialog() != true) return;

        var settings     = vm.ToSettings();
        var displayName  = string.IsNullOrWhiteSpace(settings.DisplayName) ? "新規" : settings.DisplayName;
        _activeEntry.IssueSettings = settings;
        _activeEntry.Root.Name = displayName;
        _activeEntry.ScheduleName  = displayName;
        _activeEntry.IsModified = true;
        RefreshFlatList();
    }

    // ──────── Issue 更新 ────────────────────────────────────────────────
    public async Task UpdateIssueTrackingAsync(ScheduleEntry? entry)
    {
        if (entry?.IssueSettings is null) return;

        var settings = entry.IssueSettings;
        StatusText = $"Issue 取得中: {settings.DisplayName}...";

        try
        {
            var provider = IssueTrackingHelper.CreateProvider(settings.Provider);
            var issues   = await provider.FetchIssuesAsync(settings);

            // IssueCache を更新
            entry.IssueCache.Clear();
            entry.IssueCache.AddRange(issues);

            // Jira の場合 WebUrl を補完
            if (settings.Provider.Equals("Jira", StringComparison.OrdinalIgnoreCase))
            {
                foreach (var issue in entry.IssueCache)
                    if (string.IsNullOrEmpty(issue.WebUrl) && !string.IsNullOrEmpty(issue.Id))
                        issue.WebUrl = $"{settings.BaseUrl.TrimEnd('/')}/browse/{issue.Id}";
            }

            // Children を再構築
            RebuildIssueChildren(entry);

            // 保存
            if (!string.IsNullOrEmpty(entry.FilePath))
                DoSave(entry, entry.FilePath);
            else
                entry.IsModified = true;

            UpdateAllStatuses();
            RefreshFlatList();
            StatusText = $"Issue 更新完了: {issues.Count} 件 ({DateTime.Now:HH:mm:ss})";
        }
        catch (Exception ex)
        {
            StatusText = $"Issue 取得失敗: {ex.Message}";
            MessageBox.Show(
                $"イシューの取得に失敗しました。\n\n{ex.Message}\n\n履歴キャッシュはそのまま保持します。",
                "Issue 取得エラー", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private static void RebuildIssueChildren(ScheduleEntry entry)
    {
        entry.Root.Children.Clear();
        foreach (var issue in entry.IssueCache)
        {
            var todo = IssueTrackingHelper.ToScheduleToDo(issue);
            todo.Parent = entry.Root;
            entry.Root.Children.Add(todo);
        }
    }

    private void AskAndOpen()
    {
        var dlg = new OpenFileDialog
        {
            Filter      = "スケジュールファイル (*.oat)|*.oat|すべてのファイル (*.*)|*.*",
            Title       = "スケジュールを開く",
            Multiselect = true,
        };
        if (dlg.ShowDialog() != true) return;
        foreach (var fn in dlg.FileNames)
            LoadFile(fn);
    }

    public void LoadFile(string path)
    {
        // 既に開いている場合はそのエントリをアクティブにするだけ
        var existing = Schedules.FirstOrDefault(
            e => string.Equals(e.FilePath, path, StringComparison.OrdinalIgnoreCase));
        if (existing is not null)
        {
            ActiveEntry = existing;
            return;
        }

        try
        {
            var (root, _, issueSettings, issueCache) = _fileService.LoadWithIssueTracking(path);
            var entry = new ScheduleEntry(this, (ScheduleFolder)root,
                                          filePath:     path,
                                          scheduleName: Path.GetFileNameWithoutExtension(path))
            {
                IssueSettings = issueSettings,
                IssueCache    = issueCache ?? new List<IssueCacheItem>(),
            };
            Schedules.Add(entry);
            ActiveEntry = entry;
            UpdateAllStatuses();
            RefreshFlatList();
            StatusText = $"開きました: {path}";

            // AutoRefreshOnOpen が有効なら自動更新
            if (issueSettings?.AutoRefreshOnOpen == true)
                _ = UpdateIssueTrackingAsync(entry);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"ファイルの読み込みに失敗しました。\n{ex.Message}",
                            "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    /// <summary>
    /// 起動時の遅延ロード専用。ActiveEntry を変更せずにエントリを追加する。
    /// </summary>
    private void LoadFileBackground(string path)
    {
        if (Schedules.Any(e => string.Equals(e.FilePath, path, StringComparison.OrdinalIgnoreCase)))
            return;

        try
        {
            var (root, _, issueSettings, issueCache) = _fileService.LoadWithIssueTracking(path);
            var entry = new ScheduleEntry(this, (ScheduleFolder)root,
                                          filePath:     path,
                                          scheduleName: Path.GetFileNameWithoutExtension(path))
            {
                IssueSettings = issueSettings,
                IssueCache    = issueCache ?? new List<IssueCacheItem>(),
            };
            Schedules.Add(entry);
            UpdateAllStatuses();
            StatusText = $"読み込み完了: {path}";

            if (issueSettings?.AutoRefreshOnOpen == true)
                _ = UpdateIssueTrackingAsync(entry);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"ファイルの読み込みに失敗しました。\n{ex.Message}",
                            "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void Save()
    {
        if (_activeEntry is null) return;
        if (string.IsNullOrEmpty(_activeEntry.FilePath)) { SaveAs(); return; }
        DoSave(_activeEntry, _activeEntry.FilePath);
    }

    private void SaveAs()
    {
        if (_activeEntry is null) return;
        var dlg = new SaveFileDialog
        {
            Filter   = "Tottoto スケジュール (*.oat)|*.oat",
            FileName = _activeEntry.ScheduleName,
            Title    = "名前を付けて保存",
        };
        if (dlg.ShowDialog() != true) return;
        _activeEntry.FilePath     = dlg.FileName;
        _activeEntry.ScheduleName = Path.GetFileNameWithoutExtension(dlg.FileName);
        DoSave(_activeEntry, dlg.FileName);
    }

    private void DoSave(ScheduleEntry entry, string path)
    {
        try
        {
            if (File.Exists(path))
                File.Copy(path, path + "~", overwrite: true);

            _fileService.Save(path, entry.Root, _settings.AutoSave,
                              issueSettings: entry.IssueSettings,
                              issueCache:    entry.IssueCache.Count > 0 ? entry.IssueCache : null);
            entry.IsModified = false;
            StatusText = $"保存: {DateTime.Now:HH:mm:ss}";
        }
        catch (Exception ex)
        {
            MessageBox.Show($"保存に失敗しました。\n{ex.Message}",
                            "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void AskAndClose()
    {
        foreach (var entry in Schedules.ToList())
            if (!ConfirmDiscard(entry)) return;
        Application.Current.MainWindow?.Close();
    }

    private void CloseActiveSchedule()
    {
        if (_activeEntry is null) return;
        CloseEntry(_activeEntry);
    }

    private void CloseEntry(ScheduleEntry? entry)
    {
        if (entry is null) return;
        if (!ConfirmDiscard(entry)) return;

        var idx = Schedules.IndexOf(entry);
        var wasActive = entry == _activeEntry;
        Schedules.Remove(entry);

        if (Schedules.Count == 0)
        {
            AddNewSchedule();
        }
        else if (wasActive)
        {
            ActiveEntry = Schedules[Math.Min(idx, Schedules.Count - 1)];
        }
        else
        {
            RefreshFlatList();
        }
    }

    public bool ConfirmDiscard(ScheduleEntry entry)
    {
        if (!entry.IsModified) return true;
        var r = MessageBox.Show(
            $"「{entry.DisplayName}」は変更されています。保存しますか？",
            "確認", MessageBoxButton.YesNoCancel, MessageBoxImage.Question);
        if (r == MessageBoxResult.Cancel) return false;
        if (r == MessageBoxResult.Yes)
        {
            var prev = _activeEntry;
            _activeEntry = entry;
            Save();
            _activeEntry = prev;
        }
        return !entry.IsModified || r == MessageBoxResult.No;
    }

    // ──── スケジュール順序移動 ─────────────────────────────────────────────
    private void MoveActiveScheduleLeft()
    {
        if (_activeEntry is null) return;
        int idx = Schedules.IndexOf(_activeEntry);
        if (idx <= 0) return;
        Schedules.Move(idx, idx - 1);
        CommandManager.InvalidateRequerySuggested();
    }

    private void MoveActiveScheduleRight()
    {
        if (_activeEntry is null) return;
        int idx = Schedules.IndexOf(_activeEntry);
        if (idx < 0 || idx >= Schedules.Count - 1) return;
        Schedules.Move(idx, idx + 1);
        CommandManager.InvalidateRequerySuggested();
    }

    /// <summary>ドラッグ&ドロップでタブを任意の位置へ移動する。</summary>
    public void MoveScheduleTo(ScheduleEntry source, int targetIndex)
    {
        int fromIndex = Schedules.IndexOf(source);
        if (fromIndex < 0 || fromIndex == targetIndex) return;
        int clampedTarget = Math.Clamp(targetIndex, 0, Schedules.Count - 1);
        if (fromIndex == clampedTarget) return;
        Schedules.Move(fromIndex, clampedTarget);
        CommandManager.InvalidateRequerySuggested();
    }

    // ──── アイテム追加 ─────────────────────────────────────────────────────
    private ScheduleFolder ActiveRoot => _activeEntry?.Root
        ?? Schedules.FirstOrDefault()?.Root
        ?? new ScheduleFolder { Name = "新規スケジュール" };

    private void AddFolder()
    {
        if (_activeEntry is null) return;
        var folder = new ScheduleFolder { Name = "新しいフォルダ", IsExpanded = true };

        ScheduleItemBase parent;
        ScheduleItemBase? insertBefore;

        if (Selected?.Item is ScheduleToDo selectedTodo && FindEntryForItem(selectedTodo) == _activeEntry)
        {
            parent = selectedTodo.Parent ?? ActiveRoot;
            int idx = parent.Children.IndexOf(selectedTodo);
            insertBefore = idx >= 0 && idx + 1 < parent.Children.Count
                           ? parent.Children[idx + 1] : null;
        }
        else
        {
            parent = GetInsertParent(out insertBefore);
        }

        InsertItem(_activeEntry, parent, folder, insertBefore);
        var row = FlatItems.FirstOrDefault(r => r.Item == folder);
        if (row is not null) Selected = row;
        EditItem(row);
    }

    private void AddToDo()
    {
        if (_activeEntry is null) return;
        var todo = new ScheduleToDo
        {
            Name      = "新しいタスク",
            BeginDate = Today,
            EndDate   = Today.AddDays(7),
        };

        ScheduleItemBase parent;
        ScheduleItemBase? insertBefore;

        if (Selected?.Item is ScheduleToDo selectedTodo && FindEntryForItem(selectedTodo) == _activeEntry)
        {
            parent = selectedTodo.Parent ?? ActiveRoot;
            int idx = parent.Children.IndexOf(selectedTodo);
            insertBefore = idx >= 0 && idx + 1 < parent.Children.Count
                           ? parent.Children[idx + 1] : null;
        }
        else
        {
            parent = GetInsertParent(out insertBefore);
        }

        InsertItem(_activeEntry, parent, todo, insertBefore);
        var row = FlatItems.FirstOrDefault(r => r.Item == todo);
        if (row is not null) Selected = row;
        if (row is not null) RequestBeginEdit?.Invoke(row);
    }

    private ScheduleItemBase GetInsertParent(out ScheduleItemBase? insertBefore)
    {
        insertBefore = null;
        if (Selected is null) return ActiveRoot;
        if (FindEntryForItem(Selected.Item) != _activeEntry) return ActiveRoot;
        if (Selected.Item is ScheduleFolder) return Selected.Item;
        insertBefore = Selected.Item;
        return Selected.Item.Parent ?? ActiveRoot;
    }

    private void InsertItem(ScheduleEntry entry, ScheduleItemBase parent, ScheduleItemBase item,
                            ScheduleItemBase? insertBefore)
    {
        item.Parent = parent;
        if (insertBefore is not null)
        {
            int idx = parent.Children.IndexOf(insertBefore);
            if (idx >= 0)
                parent.Children.Insert(idx, item);
            else
                parent.Children.Add(item);
        }
        else
        {
            parent.Children.Add(item);
        }

        UpdateAllStatuses();
        RefreshFlatList();
        entry.IsModified = true;
    }

    public void AddToDoFromFile(string filePath, ScheduleItemBase? nearItem)
    {
        var entry = nearItem is not null ? FindEntryForItem(nearItem) ?? _activeEntry : _activeEntry;
        if (entry is null) return;

        var name = Path.GetFileNameWithoutExtension(filePath);
        var todo = new ScheduleToDo
        {
            Name      = string.IsNullOrEmpty(name) ? Path.GetFileName(filePath) : name,
            BeginDate = Today,
            EndDate   = Today.AddDays(7),
            Link      = filePath,
        };

        ResolveInsertPosition(entry.Root, nearItem, out var parent, out var insertBefore);
        InsertItem(entry, parent, todo, insertBefore);
        Selected = FlatItems.FirstOrDefault(r => r.Item == todo);
    }

    public async Task AddToDoFromUrl(string url, ScheduleItemBase? nearItem)
    {
        var entry = nearItem is not null ? FindEntryForItem(nearItem) ?? _activeEntry : _activeEntry;
        if (entry is null) return;

        // まずホスト名をプレースホルダーとしてタスクを作成
        string placeholderName;
        try { placeholderName = new Uri(url).Host; }
        catch { placeholderName = url; }

        var todo = new ScheduleToDo
        {
            Name      = string.IsNullOrEmpty(placeholderName) ? "新しいタスク" : placeholderName,
            BeginDate = Today,
            EndDate   = Today.AddDays(7),
            Link      = url,
        };

        ResolveInsertPosition(entry.Root, nearItem, out var parent, out var insertBefore);
        InsertItem(entry, parent, todo, insertBefore);
        var row = FlatItems.FirstOrDefault(r => r.Item == todo);
        if (row is not null) Selected = row;

        // ページタイトル／イシュータイトルをバックグラウンドで取得してからダイアログを開く
        var knownInstances = Schedules
            .Select(e => e.IssueSettings)
            .OfType<IssueTrackingSettings>();
        var title = await LinkPreviewService.FetchTitleAsync(url, knownInstances);
        if (!string.IsNullOrWhiteSpace(title))
        {
            todo.Name = title;
            row?.Refresh();
        }

        EditItem(row);
    }

    private void ResolveInsertPosition(ScheduleFolder root, ScheduleItemBase? nearItem,
                                       out ScheduleItemBase parent,
                                       out ScheduleItemBase? insertBefore)
    {
        insertBefore = null;
        if (nearItem is ScheduleToDo nearTodo)
        {
            parent = nearTodo.Parent ?? root;
            int idx = parent.Children.IndexOf(nearTodo);
            insertBefore = idx >= 0 && idx + 1 < parent.Children.Count
                           ? parent.Children[idx + 1] : null;
        }
        else if (nearItem is ScheduleFolder nearFolder)
        {
            parent = nearFolder;
        }
        else
        {
            parent = root;
        }
    }

    // ──── 削除 ─────────────────────────────────────────────────────────────
    private void DeleteSelected()
    {
        if (Selected is null) return;
        var r = MessageBox.Show(
            $"「{Selected.Name}」を削除しますか？",
            "確認", MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (r != MessageBoxResult.Yes) return;

        var entry  = FindEntryForItem(Selected.Item);
        var item   = Selected.Item;
        var parent = item.Parent ?? (ScheduleItemBase?)entry?.Root;
        parent?.Children.Remove(item);
        UpdateAllStatuses();
        RefreshFlatList();
        if (entry is not null) entry.IsModified = true;
    }

    // ──── プロパティ編集 ───────────────────────────────────────────────────
    public void EditItem(TaskRowViewModel? row)
    {
        if (row is null) return;
        var dlg = new Views.TaskPropertiesWindow(row.Item, this)
        {
            Owner = Application.Current?.MainWindow,
        };
        if (dlg.ShowDialog() == true)
        {
            UpdateAllStatuses();
            RefreshFlatList();
            MarkModifiedForItem(row.Item);
            row.Refresh();
        }
    }

    public void EditItemOnMemoTab(TaskRowViewModel? row)
    {
        if (row is null) return;
        var dlg = new Views.TaskPropertiesWindow(row.Item, this,
                                                 Views.PropertiesInitialTab.Memo)
        {
            Owner = Application.Current?.MainWindow,
        };
        if (dlg.ShowDialog() == true)
        {
            UpdateAllStatuses();
            RefreshFlatList();
            MarkModifiedForItem(row.Item);
            row.Refresh();
        }
    }

    // ──── 上下移動 ─────────────────────────────────────────────────────────
    private void MoveUp()
    {
        if (Selected is null) return;
        var entry  = FindEntryForItem(Selected.Item);
        var item   = Selected.Item;
        var parent = item.Parent ?? (ScheduleItemBase?)entry?.Root;
        if (parent is null) return;
        int idx = parent.Children.IndexOf(item);
        if (idx <= 0) return;
        parent.Children.RemoveAt(idx);
        parent.Children.Insert(idx - 1, item);
        RefreshFlatList();
        if (entry is not null) entry.IsModified = true;
    }

    private void MoveDown()
    {
        if (Selected is null) return;
        var entry  = FindEntryForItem(Selected.Item);
        var item   = Selected.Item;
        var parent = item.Parent ?? (ScheduleItemBase?)entry?.Root;
        if (parent is null) return;
        int idx = parent.Children.IndexOf(item);
        if (idx < 0 || idx >= parent.Children.Count - 1) return;
        parent.Children.RemoveAt(idx);
        parent.Children.Insert(idx + 1, item);
        RefreshFlatList();
        if (entry is not null) entry.IsModified = true;
    }

    private bool CanMoveUp()
    {
        if (Selected is null) return false;
        var entry  = FindEntryForItem(Selected.Item);
        var parent = Selected.Item.Parent ?? (ScheduleItemBase?)entry?.Root;
        return parent is not null && parent.Children.IndexOf(Selected.Item) > 0;
    }

    private bool CanMoveDown()
    {
        if (Selected is null) return false;
        var entry  = FindEntryForItem(Selected.Item);
        var parent = Selected.Item.Parent ?? (ScheduleItemBase?)entry?.Root;
        if (parent is null) return false;
        int idx = parent.Children.IndexOf(Selected.Item);
        return idx >= 0 && idx < parent.Children.Count - 1;
    }

    // ──── ドラッグ&ドロップ移動 ────────────────────────────────────────────
    /// <summary>
    /// dragItem を target の前・後・子として移動する。
    /// エントリをまたいだ移動も可能。
    /// insertPosition: -1=前に挿入, 0=子として追加, 1=後に挿入
    /// </summary>
    public void MoveItemByDrag(ScheduleItemBase dragItem,
                               ScheduleItemBase targetItem,
                               int insertPosition)
    {
        if (dragItem == targetItem) return;
        var anc = targetItem.Parent;
        while (anc is not null)
        {
            if (anc == dragItem) return;
            anc = anc.Parent;
        }

        var srcEntry = FindEntryForItem(dragItem);
        var dstEntry = FindEntryForItem(targetItem);

        var srcParent = dragItem.Parent ?? (ScheduleItemBase?)srcEntry?.Root;
        srcParent?.Children.Remove(dragItem);
        dragItem.Parent = null;

        if (insertPosition == 0)
        {
            var folder = targetItem as ScheduleFolder
                         ?? targetItem.Parent as ScheduleFolder
                         ?? dstEntry?.Root
                         ?? srcEntry?.Root;
            if (folder is null) return;
            dragItem.Parent = folder;
            folder.Children.Add(dragItem);
        }
        else
        {
            var dstParent = targetItem.Parent ?? (ScheduleItemBase?)dstEntry?.Root;
            if (dstParent is null) return;
            int dstIdx = dstParent.Children.IndexOf(targetItem);
            if (dstIdx < 0) dstIdx = dstParent.Children.Count;
            if (insertPosition == 1) dstIdx++;
            dstIdx = Math.Clamp(dstIdx, 0, dstParent.Children.Count);
            dragItem.Parent = dstParent;
            dstParent.Children.Insert(dstIdx, dragItem);
        }

        UpdateAllStatuses();
        RefreshFlatList();
        if (srcEntry is not null) srcEntry.IsModified = true;
        if (dstEntry is not null && dstEntry != srcEntry) dstEntry.IsModified = true;
    }

    // ──── 日付シフト ───────────────────────────────────────────────────────
    private void ShiftDate(int deltaBegin, int deltaEnd)
    {
        if (Selected is null) return;

        var item = Selected.Item;
        var beginDate = item.BeginDate;
        var endDate = item.EndDate;

        // 開始日の調整（終了日より後にならないように制限）
        if (deltaBegin != 0 && beginDate.HasValue)
        {
            var newBeginDate = beginDate.Value.AddDays(deltaBegin);
            if (endDate.HasValue && newBeginDate > endDate.Value)
                newBeginDate = endDate.Value;
            item.BeginDate = newBeginDate;
        }

        // 終了日の調整（開始日より前にならないように制限）
        if (deltaEnd != 0 && endDate.HasValue)
        {
            var newEndDate = endDate.Value.AddDays(deltaEnd);
            // 開始日の更新後の値を取得
            var currentBeginDate = item.BeginDate;
            if (currentBeginDate.HasValue && newEndDate < currentBeginDate.Value)
                newEndDate = currentBeginDate.Value;
            item.EndDate = newEndDate;
        }

        UpdateAllStatuses();
        RefreshFlatList();
        MarkModifiedForItem(Selected.Item);
        Selected.Refresh();
    }

    // ──── ステータス更新 ───────────────────────────────────────────────────
    public void UpdateAllStatuses()
    {
        foreach (var entry in Schedules)
            foreach (var child in entry.Root.Children)
                child.UpdateStatus(Today, Holidays.AlertCount, Holidays);
    }

    public void UpdateItemStatusAfterToggle(TaskRowViewModel row)
    {
        row.Item.UpdateStatus(Today, Holidays.AlertCount, Holidays);
        var parent = row.Item.Parent;
        while (parent is not null)
        {
            parent.UpdateStatus(Today, Holidays.AlertCount, Holidays);
            parent = parent.Parent;
        }
        _exceptFromHideCompleted = row.Item;
        try { RefreshFlatList(); }
        finally { _exceptFromHideCompleted = null; }
    }

    private void ToggleWait()
    {
        if (Selected is null) return;
        if (Selected.Item is not ScheduleToDo todo) return;
        todo.IsWait = !todo.IsWait;
        UpdateItemStatusAfterToggle(Selected);
        var entry = FindEntryForItem(todo);
        if (entry is not null) entry.IsModified = true;
        OnPropertyChanged(nameof(ContextWaitHeader));
    }

    public void UpdateItemStatus(TaskRowViewModel row)
    {
        row.Item.UpdateStatus(Today, Holidays.AlertCount, Holidays);
        var parent = row.Item.Parent;
        while (parent is not null)
        {
            parent.UpdateStatus(Today, Holidays.AlertCount, Holidays);
            parent = parent.Parent;
        }
        RefreshFlatList();
    }

    // ──── フラットリスト再構築 ─────────────────────────────────────────────
    private ScheduleItemBase? _exceptFromHideCompleted;

    public void RefreshFlatList()
    {
        var prev = Selected?.Item;

        var buffer = new List<TaskRowViewModel>();
        if (_activeEntry is not null)
            BuildFlatList(_activeEntry.Root, 0, buffer);

        // 行インデックスを付与して横縞色を確定
        for (int i = 0; i < buffer.Count; i++)
            buffer[i].RowIndex = i;

        FlatItems.Clear();
        foreach (var row in buffer)
            FlatItems.Add(row);

        if (_isChartReady)
            RefreshAllChartCells();
        else
            RefreshAllCalloutColumns();

        if (prev is not null)
            Selected = FlatItems.FirstOrDefault(r => r.Item == prev);
    }

    private void BuildFlatList(ScheduleItemBase node, int depth,
                               List<TaskRowViewModel> target,
                               IReadOnlyList<bool>? ancestorHasNext = null)
    {
        bool filterCompleted = _hideCompleted;
        var children = node.Children;
        for (int i = 0; i < children.Count; i++)
        {
            var child = children[i];

            if (filterCompleted
                && child is ScheduleToDo { Completed: true }
                && child != _exceptFromHideCompleted)
                continue;

            bool isLast = i == children.Count - 1;

            var row = new TaskRowViewModel(child, depth, this,
                                           isLastChild: isLast,
                                           ancestorHasNext: ancestorHasNext);
            target.Add(row);

            if (child is ScheduleFolder folder && folder.IsExpanded)
            {
                var next = new List<bool>(ancestorHasNext ?? []);
                while (next.Count < depth) next.Add(false);
                if (next.Count == depth) next.Add(!isLast);
                else next[depth] = !isLast;

                BuildFlatList(child, depth + 1, target, next);
            }
        }
    }

    // ──── チャートセル更新 ─────────────────────────────────────────────────
    public void RefreshAllChartCells()
    {
        RefreshChartDays();
        foreach (var row in FlatItems)
            row.RefreshChartCells(_chartStart, CellCount, Today, Holidays,
                                  _settings.DateCountLevel);
        RefreshAllCalloutColumns();
    }

    private void RefreshChartDays()
    {
        var days = new List<ChartDayHeaderInfo>(CellCount);
        for (int i = 0; i < CellCount; i++)
        {
            var d   = _chartStart.AddDays(i);
            int hlv = Holidays.GetLevel(d);
            days.Add(new ChartDayHeaderInfo
            {
                Date      = d,
                DayText   = d.Date == Today.Date ? "今" : d.Day.ToString(),
                IsToday   = d.Date == Today.Date,
                HolidayLv  = hlv,
            });
        }
        ChartDays = days;
        OnPropertyChanged(nameof(ChartDays));
    }

    // ──── 変更フラグ ───────────────────────────────────────────────────────
    public void SetModified()
    {
        if (_activeEntry is not null) _activeEntry.IsModified = true;
    }

    private void MarkModifiedForItem(ScheduleItemBase item)
    {
        var entry = FindEntryForItem(item);
        if (entry is not null) entry.IsModified = true;
    }

    // ──── 今日の予定ウィンドウ ─────────────────────────────────────────────
    private void OpenTodayScheduleWindow()
    {
        foreach (Window w in Application.Current.Windows)
        {
            if (w is Views.TodayScheduleWindow existing)
            {
                existing.Activate();
                return;
            }
        }
        var vm  = new TodayScheduleViewModel(this);
        RegisterTodayVm(vm);
        var win = new Views.TodayScheduleWindow(vm);
        win.Closed += (_, _) => RegisterTodayVm(null);
        win.Show();
    }

    // ──── コピー / カット / ペースト ──────────────────────────────────────
    private void CopySelected()
    {
        if (Selected is null) return;
        _clipboardItem  = Selected.Item.CloneShallow();
        _clipboardIsCut = false;
    }

    private void CutSelected()
    {
        if (Selected is null) return;
        _clipboardItem  = Selected.Item;
        _clipboardIsCut = true;
    }

    private async Task PasteAsync()
    {
        // ① 内部クリップボードにタスクアイテムがある場合
        if (_clipboardItem is ScheduleToDo clipTodo)
        {
            var clone = (ScheduleToDo)clipTodo.CloneShallow();
            clone.Name = _clipboardIsCut ? clipTodo.Name : clipTodo.Name + " のコピー";

            if (_clipboardIsCut)
            {
                // カットの場合は元の場所から削除（削除前にエントリを特定する）
                var srcEntry = FindEntryForItem(clipTodo);
                var srcParent = clipTodo.Parent ?? (ScheduleItemBase?)srcEntry?.Root;
                srcParent?.Children.Remove(clipTodo);
                if (srcEntry is not null) srcEntry.IsModified = true;
                _clipboardItem  = clone; // 次貼り付けに備えてクローンに差し替え
                _clipboardIsCut = false;
            }

            var nearItem = Selected?.Item;
            ResolveInsertPosition(
                (_activeEntry ?? Schedules.FirstOrDefault())?.Root ?? new Models.ScheduleFolder(),
                nearItem, out var parent, out var insertBefore);
            InsertItem(_activeEntry ?? Schedules.First(), parent, clone, insertBefore);
            Selected = FlatItems.FirstOrDefault(r => r.Item == clone);
            return;
        }

        // ② システムクリップボードを確認
        IDataObject? data = null;
        try { data = System.Windows.Clipboard.GetDataObject(); } catch { }
        if (data is null) return;

        var nearItem2 = Selected?.Item;

        // ファイル/フォルダパス
        if (data.GetDataPresent(DataFormats.FileDrop))
        {
            var files = data.GetData(DataFormats.FileDrop) as string[];
            if (files is not null)
            {
                foreach (var file in files)
                    AddToDoFromFile(file, nearItem2);
                return;
            }
        }

        // ブラウザでコピーした URL（UniformResourceLocator / UniformResourceLocatorW 形式）
        string? urlFromBrowser = null;
        try
        {
            if (data.GetDataPresent("UniformResourceLocatorW"))
            {
                using var stream = data.GetData("UniformResourceLocatorW") as System.IO.Stream;
                if (stream is not null)
                {
                    var bytes = new byte[stream.Length];
                    _ = stream.Read(bytes, 0, bytes.Length);
                    urlFromBrowser = System.Text.Encoding.Unicode.GetString(bytes).TrimEnd('\0');
                }
            }
            if (string.IsNullOrWhiteSpace(urlFromBrowser) && data.GetDataPresent("UniformResourceLocator"))
            {
                using var stream = data.GetData("UniformResourceLocator") as System.IO.Stream;
                if (stream is not null)
                {
                    var bytes = new byte[stream.Length];
                    _ = stream.Read(bytes, 0, bytes.Length);
                    urlFromBrowser = System.Text.Encoding.Default.GetString(bytes).TrimEnd('\0');
                }
            }
        }
        catch { }

        if (!string.IsNullOrWhiteSpace(urlFromBrowser))
        {
            await AddToDoFromUrl(urlFromBrowser.Trim(), nearItem2);
            return;
        }

        // テキスト
        string? text = null;
        try
        {
            if (data.GetDataPresent(DataFormats.UnicodeText))
                text = data.GetData(DataFormats.UnicodeText) as string;
            else if (data.GetDataPresent(DataFormats.Text))
                text = data.GetData(DataFormats.Text) as string;
        }
        catch { }

        if (string.IsNullOrWhiteSpace(text)) return;
        text = text.Trim();

        // URL かどうか判定
        if (Uri.TryCreate(text, UriKind.Absolute, out var uri)
            && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps
                || uri.Scheme == Uri.UriSchemeFtp  || uri.Scheme == "file"))
        {
            if (uri.Scheme == "file")
            {
                // file:// → ローカルパスとして処理
                AddToDoFromFile(uri.LocalPath, nearItem2);
            }
            else
            {
                await AddToDoFromUrl(text, nearItem2);
            }
            return;
        }

        // それ以外の文字列 → タスク名として登録
        var entry = nearItem2 is not null ? FindEntryForItem(nearItem2) ?? _activeEntry : _activeEntry;
        if (entry is null) return;
        var todo = new Models.ScheduleToDo
        {
            Name      = text,
            BeginDate = Today,
            EndDate   = Today.AddDays(7),
        };
        ResolveInsertPosition(entry.Root, nearItem2, out var p2, out var ib2);
        InsertItem(entry, p2, todo, ib2);
        Selected = FlatItems.FirstOrDefault(r => r.Item == todo);
    }

    // ──── 設定保存 ─────────────────────────────────────────────────────────
    public void SaveSettings()
    {
        _settings.OpenFiles            = Schedules.Select(e => e.FilePath)
                                                   .Where(p => !string.IsNullOrEmpty(p))
                                                   .ToList();
        _settings.LastFile             = _activeEntry?.FilePath ?? string.Empty;
        _settings.ChartOffsetFromToday = (int)(_chartStart - Today).TotalDays;
        _settings.WeekdayLevels        = Holidays.GetWeekdayLevels();
        _settings.AlertCount           = Holidays.AlertCount;
        _settings.DateCountLevel       = Holidays.DateCountLevel;
        _settings.Save();
    }

    /// <summary>タスクペイン幅を取得する。</summary>
    public double GetTaskPaneWidth() => _settings.TaskPaneWidth;

    /// <summary>タスクペイン幅を保存する。</summary>
    public void SaveTaskPaneWidth(double width)
    {
        _settings.TaskPaneWidth = width;
        _settings.Save();
    }

    /// <summary>タスクリスト列幅（タスク名・状態・ML）を取得する。</summary>
    public (double name, double status, double ml) GetTaskColumnWidths() =>
        (_settings.TaskColNameWidth, _settings.TaskColStatusWidth, _settings.TaskColMLWidth);

    /// <summary>タスクリスト列幅を保存する。</summary>
    public void SaveTaskColumnWidths(double nameWidth, double statusWidth, double mlWidth)
    {
        _settings.TaskColNameWidth   = nameWidth;
        _settings.TaskColStatusWidth = statusWidth;
        _settings.TaskColMLWidth     = mlWidth;
        _settings.Save();
    }

    // ──── ヘルパー ─────────────────────────────────────────────────────────
    /// <summary>アイテムがどのエントリのルートツリーに属するかを返す。</summary>
    public ScheduleEntry? FindEntryForItem(ScheduleItemBase item)
    {
        var current = item;
        while (current.Parent is not null)
            current = current.Parent;
        return Schedules.FirstOrDefault(e => e.Root == current);
    }

    /// <summary>
    /// 全 ScheduleEntry を走査してフラットな TaskRowViewModel リストを返す。
    /// HideCompleted フィルターは適用しない（今日の予定ウィンドウ用）。
    /// </summary>
    public List<TaskRowViewModel> BuildAllScheduleRows()
    {
        var buffer = new List<TaskRowViewModel>();
        foreach (var entry in Schedules)
            BuildFlatListNoFilter(entry.Root, 0, buffer, expandedStates: null);
        return buffer;
    }

    /// <summary>
    /// 展開状態を一切考慮せず全アイテムを走査して返す（needed 判定用）。
    /// </summary>
    public List<TaskRowViewModel> BuildAllScheduleRowsFlat()
    {
        var buffer = new List<TaskRowViewModel>();
        foreach (var entry in Schedules)
            BuildFlatListAlwaysExpanded(entry.Root, 0, buffer);
        return buffer;
    }

    private void BuildFlatListAlwaysExpanded(ScheduleItemBase node, int depth,
                                             List<TaskRowViewModel> target,
                                             IReadOnlyList<bool>? ancestorHasNext = null)
    {
        var children = node.Children;
        for (int i = 0; i < children.Count; i++)
        {
            var child  = children[i];
            bool isLast = i == children.Count - 1;
            var row = new TaskRowViewModel(child, depth, this,
                                           isLastChild: isLast,
                                           ancestorHasNext: ancestorHasNext);
            target.Add(row);
            if (child.IsFolder)
            {
                var next = new List<bool>(ancestorHasNext ?? []);
                while (next.Count < depth) next.Add(false);
                if (next.Count == depth) next.Add(!isLast);
                else next[depth] = !isLast;
                BuildFlatListAlwaysExpanded(child, depth + 1, target, next);
            }
        }
    }

    /// <summary>
    /// 展開状態を外部辞書で上書きして全行を構築する。今日の予定ウィンドウ用。
    /// expandedStates が null の場合はモデルの IsExpanded を使う。
    /// </summary>
    public List<TaskRowViewModel> BuildAllScheduleRows(Dictionary<ScheduleItemBase, bool> expandedStates)
    {
        var buffer = new List<TaskRowViewModel>();
        foreach (var entry in Schedules)
            BuildFlatListNoFilter(entry.Root, 0, buffer, expandedStates);
        return buffer;
    }

    private void BuildFlatListNoFilter(ScheduleItemBase node, int depth,
                                       List<TaskRowViewModel> target,
                                       Dictionary<ScheduleItemBase, bool>? expandedStates,
                                       IReadOnlyList<bool>? ancestorHasNext = null)
    {
        var children = node.Children;
        for (int i = 0; i < children.Count; i++)
        {
            var child  = children[i];
            bool isLast = i == children.Count - 1;

            var row = new TaskRowViewModel(child, depth, this,
                                           isLastChild: isLast,
                                           ancestorHasNext: ancestorHasNext);
            target.Add(row);

            bool isExpanded = expandedStates is not null
                ? expandedStates.TryGetValue(child, out var v) ? v : (child is ScheduleFolder sf2 && sf2.IsExpanded)
                : child is ScheduleFolder sf3 && sf3.IsExpanded;

            if (child is ScheduleFolder folder && isExpanded)
            {
                // 独立展開モードでは行の _isExpanded をセットしておく
                if (expandedStates is not null)
                    row.SetIsExpandedDirect(true);

                var next = new List<bool>(ancestorHasNext ?? []);
                while (next.Count < depth) next.Add(false);
                if (next.Count == depth) next.Add(!isLast);
                else next[depth] = !isLast;
                BuildFlatListNoFilter(child, depth + 1, target, expandedStates, next);
            }
            else if (child is ScheduleFolder && expandedStates is not null)
            {
                // 折りたたみ状態を行に反映
                row.SetIsExpandedDirect(false);
            }
        }
    }

    // ──── アーカイブ ─────────────────────────────────────────────────────────────
    private readonly ArchiveService _archiveService = new();

    private void ArchiveSelected()
    {
        if (Selected is null || _activeEntry is null) return;
        if (string.IsNullOrEmpty(_activeEntry.FilePath))
        {
            MessageBox.Show("アーカイブするには、先にファイルを保存してください。",
                            "アーカイブ", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var r = MessageBox.Show(
            $"「{Selected.Name}」をアーカイブしますか？\n元のリストからは削除されます。",
            "アーカイブ確認", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (r != MessageBoxResult.Yes) return;

        var archivePath = ArchiveService.GetArchivePath(_activeEntry.FilePath);
        var archived    = _archiveService.Load(archivePath);

        // 選択アイテムとその子孫を再帰的にアーカイブ
        CollectItemsForArchive(Selected.Item, archived);

        _archiveService.Save(archivePath, archived);

        // 元ツリーから削除
        var item   = Selected.Item;
        var parent = item.Parent ?? (ScheduleItemBase?)_activeEntry.Root;
        parent?.Children.Remove(item);
        UpdateAllStatuses();
        RefreshFlatList();
        _activeEntry.IsModified = true;
        StatusText = $"アーカイブしました: {item.Name}";
    }

    private static void CollectItemsForArchive(ScheduleItemBase item, List<Models.ArchivedItem> list)
    {
        if (!item.IsFolder)
        {
            list.Add(ArchiveService.ToArchived(item));
        }
        else
        {
            // フォルダの場合、子を再帰的にアーカイブ
            foreach (var child in item.Children)
                CollectItemsForArchive(child, list);
        }
    }

    private void OpenArchiveListWindow()
    {
        if (_activeEntry is null || string.IsNullOrEmpty(_activeEntry.FilePath)) return;

        var archivePath = ArchiveService.GetArchivePath(_activeEntry.FilePath);
        var archived    = _archiveService.Load(archivePath);

        if (archived.Count == 0)
        {
            MessageBox.Show("アーカイブは空です。",
                            "アーカイブ一覧", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var vm  = new ArchiveListViewModel(this, archivePath, archived);
        var win = new Views.ArchiveListWindow(vm) { Owner = Application.Current.MainWindow };
        win.ShowDialog();

        if (vm.Restored)
        {
            UpdateAllStatuses();
            RefreshFlatList();
        }
    }

    // ──────── 休日設定ウィンドウ ──────────────────────────────────
    private void ShowHolidaySettings()
    {
        var vm  = new HolidaySettingsViewModel(Holidays);
        var win = new Views.HolidaySettingsWindow(vm) { Owner = Application.Current.MainWindow };
        if (win.ShowDialog() == true)
        {
            // 設定変更後、AppSettings に保存
            _settings.WeekdayLevels = Holidays.GetWeekdayLevels();
            _settings.AlertCount    = Holidays.AlertCount;
            _settings.DateCountLevel = Holidays.DateCountLevel;
            _settings.SpecialHolidays = Holidays.ExportSpecialHolidays();
            _settings.Save();

            // ガントチャートを再描画
            UpdateAllStatuses();
            RefreshFlatList();
        }
    }

    /// <summary>アーカイブ一覧から呼ばれる復元実行。</summary>
    public void RestoreArchivedItems(List<Models.ArchivedItem> items, string archivePath)
    {
        if (_activeEntry is null) return;

        foreach (var a in items)
        {
            var parent = ArchiveService.ResolveOrCreatePath(_activeEntry.Root, a.Path);
            var todo   = ArchiveService.ToScheduleItem(a);
            todo.Parent = parent;
            parent.Children.Add(todo);
        }

        // アーカイブファイルから削除して保存
        var all = _archiveService.Load(archivePath);
        all.RemoveAll(a => items.Any(r => r.Name == a.Name && r.Path == a.Path && r.ArchivedAt == a.ArchivedAt));
        _archiveService.Save(archivePath, all);

        _activeEntry.IsModified = true;
        StatusText = $"{items.Count} 件復元しました";
    }

    /// <summary>アーカイブ一覧から呼ばれる削除実行。</summary>
    public void DeleteArchivedItems(List<Models.ArchivedItem> items, string archivePath)
    {
        var all = _archiveService.Load(archivePath);
        all.RemoveAll(a => items.Any(r => r.Name == a.Name && r.Path == a.Path && r.ArchivedAt == a.ArchivedAt));
        _archiveService.Save(archivePath, all);
        StatusText = $"{items.Count} 件をアーカイブから削除しました";
    }

    // ──── 吹き出し ─────────────────────────────────────────────────────────

    /// <summary>
    /// ガントチャートの列インデックスから日時を返す。
    /// </summary>
    public DateTime ColumnIndexToDate(int columnIndex) =>
        _chartStart.AddDays(columnIndex);

    /// <summary>
    /// 指定タスクに吹き出しを追加し、追加直後に編集モードへ遷移する。
    /// </summary>
    public CalloutViewModel? AddCallout(ScheduleItemBase item, int columnIndex)
    {
        if (item is not Models.ScheduleToDo todo) return null;
        var entry = FindEntryForItem(item);
        if (entry is null) return null;

        var anchorDate = ColumnIndexToDate(columnIndex);
        var callout = new Models.Callout
        {
            PositionMode     = Models.CalloutPositionMode.EndDate,
            AbsoluteDateTime = anchorDate,
            OffsetDays       = todo.EndDate.HasValue
                               ? (int)(anchorDate.Date - todo.EndDate.Value.Date).TotalDays
                               : 0,
            Text             = string.Empty,
        };

        todo.Callouts.Add(callout);
        entry.IsModified = true;
        RefreshCalloutsForTask(todo);

        // RefreshCallouts() が _callouts を Clear→再生成するため、再構築後のコレクションから取得する
        var row = FlatItems.FirstOrDefault(r => r.Item == todo);
        return row?.Callouts.FirstOrDefault(c => c.Model == callout);
    }

    /// <summary>指定タスクの吹き出し ViewModel を再構築して通知する。</summary>
    public void RefreshCalloutsForTask(Models.ScheduleToDo todo)
    {
        var row = FlatItems.FirstOrDefault(r => r.Item == todo);
        row?.RefreshCallouts();
        SyncAllCalloutsFromRow(todo);
    }

    /// <summary>
    /// 指定タスク行の CalloutViewModel を AllCallouts に反映する。
    /// AllCallouts は CalloutOverlayControl の ItemsSource として使用される。
    /// </summary>
    private void SyncAllCalloutsFromRow(Models.ScheduleToDo todo)
    {
        var row = FlatItems.FirstOrDefault(r => r.Item == todo);

        // このタスクに属する古い ViewModel を AllCallouts から除去する。
        // RefreshCallouts() で _callouts が Clear → 再生成されるため、
        // Task が一致するエントリのうち再構築後のコレクションにないものをすべて削除する。
        var current = row?.Callouts.ToHashSet() ?? new System.Collections.Generic.HashSet<CalloutViewModel>();
        for (int i = AllCallouts.Count - 1; i >= 0; i--)
        {
            var cvm = AllCallouts[i];
            if (cvm.Task == todo && !current.Contains(cvm))
                AllCallouts.RemoveAt(i);
        }

        if (row is null) return;

        // 再構築後の ViewModel で不足分を追加
        foreach (var cvm in row.Callouts)
        {
            if (!AllCallouts.Contains(cvm))
                AllCallouts.Add(cvm);
        }
    }

    /// <summary>チャート表示開始日変更時に全行の吹き出しアンカー列を更新する。</summary>
    public void RefreshAllCalloutColumns()
    {
        foreach (var row in FlatItems)
            row.RefreshCallouts();

        AllCallouts.Clear();
        foreach (var row in FlatItems)
            foreach (var c in row.Callouts)
                AllCallouts.Add(c);
    }
}

/// <summary>チャートヘッダーの 1 列分。</summary>
public class ChartDayHeaderInfo
{
    public DateTime Date       { get; init; }
    public string   DayText    { get; init; } = string.Empty;
    public bool     IsToday    { get; init; }
    public int      HolidayLv  { get; init; }
}
