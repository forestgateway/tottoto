using System.Collections.ObjectModel;
using System.Windows.Input;
using todochart.Services;

namespace todochart.ViewModels;

/// <summary>
/// バージョン更新確認ダイアログの ViewModel。
/// </summary>
public class UpdateCheckViewModel : ViewModelBase
{
    private readonly UpdateCheckService _service = new();

    /// <summary>現在のアプリバージョン文字列（例: "1.0.0"）。</summary>
    public string CurrentVersion { get; } = UpdateCheckService.CurrentVersion;

    private string _latestVersion = "確認中...";
    /// <summary>GitHub から取得した最新バージョン文字列。</summary>
    public string LatestVersion
    {
        get => _latestVersion;
        private set => SetField(ref _latestVersion, value);
    }

    private ObservableCollection<ReleaseEntry> _releaseEntries = [];
    /// <summary>現バージョンより新しいリリースのエントリ一覧（降順）。</summary>
    public ObservableCollection<ReleaseEntry> ReleaseEntries
    {
        get => _releaseEntries;
        private set => SetField(ref _releaseEntries, value);
    }

    private string _statusMessage = "GitHub からバージョン情報を取得しています...";
    /// <summary>ダイアログ下部に表示するステータス文字列。</summary>
    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetField(ref _statusMessage, value);
    }

    private bool _isChecking = true;
    /// <summary>チェック中フラグ（ProgressBar の IsIndeterminate に使用）。</summary>
    public bool IsChecking
    {
        get => _isChecking;
        private set => SetField(ref _isChecking, value);
    }

    private bool _isUpdating;
    /// <summary>ダウンロード・更新処理中フラグ（多重実行防止）。</summary>
    public bool IsUpdating
    {
        get => _isUpdating;
        private set
        {
            if (SetField(ref _isUpdating, value))
            {
                ((RelayCommand)UpdateCommand).RaiseCanExecuteChanged();
                ((RelayCommand)RestartNowCommand).RaiseCanExecuteChanged();
            }
        }
    }

    private bool _isUpdateComplete;
    /// <summary>bat 生成まで完了し、再起動をユーザーに選択させるフェーズかどうか。</summary>
    public bool IsUpdateComplete
    {
        get => _isUpdateComplete;
        private set
        {
            if (SetField(ref _isUpdateComplete, value))
                ((RelayCommand)RestartNowCommand).RaiseCanExecuteChanged();
        }
    }

    private bool _isUpdateAvailable;
    /// <summary>更新が利用可能かどうか。</summary>
    public bool IsUpdateAvailable
    {
        get => _isUpdateAvailable;
        private set => SetField(ref _isUpdateAvailable, value);
    }

    private double _progress;
    /// <summary>ダウンロード進捗 (0.0〜1.0)。</summary>
    public double Progress
    {
        get => _progress;
        private set => SetField(ref _progress, value);
    }

    private string? _downloadUrl;
    private string? _batPath;

    /// <summary>「更新する」ボタンのコマンド。</summary>
    public ICommand UpdateCommand { get; }

    /// <summary>「今すぐ再起動して適用する」ボタンのコマンド。</summary>
    public ICommand RestartNowCommand { get; }

    public UpdateCheckViewModel()
    {
        UpdateCommand = new RelayCommand(
            execute:    async () => await ExecuteUpdateAsync(),
            canExecute: ()    => IsUpdateAvailable && !IsUpdating);

        RestartNowCommand = new RelayCommand(
            execute:    () => ExecuteRestartNow(),
            canExecute: () => IsUpdateComplete && _batPath is not null);
    }

    /// <summary>
    /// ダイアログ表示後に呼び出す。GitHub API でバージョンを確認する。
    /// </summary>
    public async Task LoadAsync()
    {
        IsChecking    = true;
        StatusMessage = "GitHub からバージョン情報を取得しています...";
        try
        {
            var info          = await _service.CheckAsync();
            LatestVersion     = info.LatestVersion;
            ReleaseEntries    = new ObservableCollection<ReleaseEntry>(info.Entries);
            _downloadUrl      = info.DownloadUrl;
            IsUpdateAvailable = info.IsUpdateAvailable && info.DownloadUrl is not null;
            StatusMessage     = info.IsUpdateAvailable
                ? $"新しいバージョン {info.LatestVersion} が利用可能です。"
                : "最新バージョンを使用しています。";
        }
        catch (Exception ex)
        {
            LatestVersion     = "取得失敗";
            ReleaseEntries    = [];
            IsUpdateAvailable = false;
            StatusMessage     = $"バージョン確認に失敗しました: {ex.Message}";
        }
        finally
        {
            IsChecking = false;
        }
    }

    private async Task ExecuteUpdateAsync()
    {
        if (_downloadUrl is null) return;

        IsUpdating    = true;
        IsChecking    = false;
        StatusMessage = "ダウンロードを開始します...";
        Progress      = 0;

        var progressReporter = new Progress<double>(p =>
        {
            Progress      = p;
            StatusMessage = p < 0.7
                ? $"ダウンロード中... {p / 0.7 * 100:F0}%"
                : p < 0.85
                ? "ファイルを展開しています..."
                : "更新の準備をしています...";
        });

        try
        {
            _batPath         = await _service.UpdateAsync(_downloadUrl, progressReporter);
            IsUpdateComplete = true;
            IsUpdating       = false;
            StatusMessage    = "更新ファイルの準備が完了しました。再起動するか選択してください。";
        }
        catch (Exception ex)
        {
            StatusMessage = $"更新に失敗しました: {ex.Message}";
            IsUpdating    = false;
        }
    }

    private void ExecuteRestartNow()
    {
        if (_batPath is null) return;
        UpdateCheckService.LaunchAndRestart(_batPath);
    }
}