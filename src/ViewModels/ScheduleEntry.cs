using System.IO;
using todochart.Models;

namespace todochart.ViewModels;

/// <summary>
/// 1 つのセーブファイルを表す ViewModel。
/// MainViewModel が複数保持し、それぞれ独立したルートフォルダを管理する。
/// </summary>
public class ScheduleEntry : ViewModelBase
{
    private readonly MainViewModel _main;

    public ScheduleFolder Root { get; set; }

    // ── Issue Tracking 拡張 ───────────────────────────────
    /// <summary>Issue Tracking 用エントリかどうか</summary>
    public bool IsIssueTracking => _issueSettings is not null;

    private IssueTrackingSettings? _issueSettings;
    /// <summary>Issue Tracking 接続設定。通常タスクリストでは null</summary>
    public IssueTrackingSettings? IssueSettings
    {
        get => _issueSettings;
        set
        {
            if (_issueSettings == value) return;
            _issueSettings = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsIssueTracking));
        }
    }

    /// <summary>最後に取得した Issue キャッシュ</summary>
    public List<IssueCacheItem> IssueCache { get; set; } = new();

    private string _filePath = string.Empty;
    public string FilePath
    {
        get => _filePath;
        set
        {
            if (SetField(ref _filePath, value))
            {
                OnPropertyChanged(nameof(DisplayName));
                OnPropertyChanged(nameof(TabTitle));
            }
        }
    }

    private string _scheduleName = string.Empty;
    public string ScheduleName
    {
        get => _scheduleName;
        set
        {
            if (SetField(ref _scheduleName, value))
            {
                OnPropertyChanged(nameof(DisplayName));
                OnPropertyChanged(nameof(TabTitle));
            }
        }
    }

    private bool _isModified;
    public bool IsModified
    {
        get => _isModified;
        set
        {
            if (SetField(ref _isModified, value))
            {
                OnPropertyChanged(nameof(TabTitle));
                _main.OnEntryModifiedChanged();
            }
        }
    }

    public string DisplayName =>
        string.IsNullOrEmpty(ScheduleName) ? "新規" : ScheduleName;

    public string TabTitle =>
        DisplayName + (IsModified ? " *" : "");

    public ScheduleEntry(MainViewModel main, ScheduleFolder root,
                         string filePath = "", string scheduleName = "")
    {
        _main        = main;
        Root         = root;
        _filePath    = filePath;
        _scheduleName = scheduleName;
    }
}
