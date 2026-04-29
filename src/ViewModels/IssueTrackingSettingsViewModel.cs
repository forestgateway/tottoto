using todochart.Models;

namespace todochart.ViewModels;

public class IssueTrackingSettingsViewModel : ViewModelBase
{
    private string _provider = "GitLab";
    public string Provider
    {
        get => _provider;
        set => SetField(ref _provider, value);
    }

    public IReadOnlyList<string> Providers { get; } = ["GitLab", "Jira", "JiraOnPrem", "Redmine"];

    private string _displayName = string.Empty;
    public string DisplayName
    {
        get => _displayName;
        set => SetField(ref _displayName, value);
    }

    private string _baseUrl = string.Empty;
    public string BaseUrl
    {
        get => _baseUrl;
        set => SetField(ref _baseUrl, value);
    }

    private string _projectId = string.Empty;
    public string ProjectId
    {
        get => _projectId;
        set => SetField(ref _projectId, value);
    }

    private string _apiToken = string.Empty;
    public string ApiToken
    {
        get => _apiToken;
        set => SetField(ref _apiToken, value);
    }

    private string _email = string.Empty;
    public string Email
    {
        get => _email;
        set => SetField(ref _email, value);
    }

    /// <summary>JiraOnPrem Basic 認証用ユーザー名。</summary>
    private string _username = string.Empty;
    public string Username
    {
        get => _username;
        set => SetField(ref _username, value);
    }

    private string _query = string.Empty;
    public string Query
    {
        get => _query;
        set => SetField(ref _query, value);
    }

    /// <summary>JiraOnPrem: URL クエリ文字列形式の追加パラメータ。</summary>
    private string _extraParams = string.Empty;
    public string ExtraParams
    {
        get => _extraParams;
        set => SetField(ref _extraParams, value);
    }

    private int _maxResults = 0;
    public int MaxResults
    {
        get => _maxResults;
        set => SetField(ref _maxResults, value);
    }

    private bool _autoRefreshOnOpen;
    public bool AutoRefreshOnOpen
    {
        get => _autoRefreshOnOpen;
        set => SetField(ref _autoRefreshOnOpen, value);
    }

    // ── 接続テスト結果 ────────────────────────────────────

    private bool _isTesting;
    public bool IsTesting
    {
        get => _isTesting;
        set
        {
            if (SetField(ref _isTesting, value))
                OnPropertyChanged(nameof(IsNotTesting));
        }
    }

    public bool IsNotTesting => !_isTesting;

    private string _testLog = string.Empty;
    public string TestLog
    {
        get => _testLog;
        set => SetField(ref _testLog, value);
    }

    private bool? _testSuccess;
    public bool? TestSuccess
    {
        get => _testSuccess;
        set
        {
            if (SetField(ref _testSuccess, value))
            {
                OnPropertyChanged(nameof(TestResultText));
                OnPropertyChanged(nameof(TestResultVisible));
            }
        }
    }

    public string TestResultText => _testSuccess switch
    {
        true  => "成功",
        false => "エラーあり",
        _     => string.Empty,
    };

    public bool TestResultVisible => _testSuccess.HasValue;

    public IssueTrackingSettings ToSettings() => new()
    {
        Provider          = Provider,
        DisplayName       = DisplayName.Trim(),
        BaseUrl           = BaseUrl.Trim(),
        ProjectId         = ProjectId.Trim(),
        ApiToken          = ApiToken,
        Email             = Email.Trim(),
        Username          = Username.Trim(),
        Query             = Query.Trim(),
        ExtraParams       = ExtraParams.Trim(),
        MaxResults        = MaxResults,
        AutoRefreshOnOpen = AutoRefreshOnOpen,
    };

    public static IssueTrackingSettingsViewModel FromSettings(IssueTrackingSettings s) => new()
    {
        Provider          = s.Provider,
        DisplayName       = s.DisplayName,
        BaseUrl           = s.BaseUrl,
        ProjectId         = s.ProjectId,
        ApiToken          = s.ApiToken,
        Email             = s.Email,
        Username          = s.Username,
        Query             = s.Query,
        ExtraParams       = s.ExtraParams,
        MaxResults        = s.MaxResults,
        AutoRefreshOnOpen = s.AutoRefreshOnOpen,
    };
}
