using System.Windows;
using System.Text;
using System.Windows.Controls;
using todochart.Models;
using todochart.Services;
using todochart.ViewModels;

namespace todochart.Views;

public partial class IssueTrackingSettingsWindow : Window
{
    private readonly IssueTrackingSettingsViewModel _vm;

    public IssueTrackingSettingsWindow(IssueTrackingSettingsViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        DataContext = vm;

        // PasswordBox は DataBinding 非対応なので手動で設定
        ApiTokenBox.Password = vm.ApiToken;
    }

    private void OnOkClick(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_vm.DisplayName))
        {
            MessageBox.Show("表示名を入力してください。", "入力エラー",
                            MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        if (string.IsNullOrWhiteSpace(_vm.BaseUrl))
        {
            MessageBox.Show("ベース URL を入力してください。", "入力エラー",
                            MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        if (string.IsNullOrWhiteSpace(_vm.ProjectId))
        {
            MessageBox.Show("プロジェクト ID を入力してください。", "入力エラー",
                            MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        if (_vm.Provider == "Jira" && string.IsNullOrWhiteSpace(_vm.Email))
        {
            MessageBox.Show("メールアドレスを入力してください。", "入力エラー",
                            MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        if (_vm.Provider == "JiraOnPrem" && string.IsNullOrWhiteSpace(_vm.Username))
        {
            MessageBox.Show("Username を入力してください。", "入力エラー",
                            MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        // PasswordBox の値を VM に反映
        _vm.ApiToken = ApiTokenBox.Password;

        DialogResult = true;
    }

    private async void OnTestConnectionClick(object sender, RoutedEventArgs e)
    {
        // PasswordBox の現在値を VM に同期してからテスト設定を構築
        _vm.ApiToken = ApiTokenBox.Password;

        var settings = BuildCurrentSettings();

        if (string.IsNullOrWhiteSpace(settings.BaseUrl) ||
            string.IsNullOrWhiteSpace(settings.ProjectId))
        {
            _vm.TestLog     = "ベース URL とプロジェクト ID を入力してください。";
            _vm.TestSuccess = false;
            return;
        }

        _vm.IsTesting   = true;
        _vm.TestSuccess = null;
        _vm.TestLog     = string.Empty;

        try
        {
            var provider = IssueTrackingHelper.CreateProvider(settings.Provider);
            var (requestUrl, responseBody) = await provider.TestConnectionAsync(settings);

            // Jira 系 (Cloud / OnPrem) は responseBody に [1/2][2/2] の全ログが含まれるためそのまま表示
            // GitLab は requestUrl + responseBody を組み立てる
            string log;
            if (settings.Provider.Equals("Jira", StringComparison.OrdinalIgnoreCase)
             || settings.Provider.Equals("JiraOnPrem", StringComparison.OrdinalIgnoreCase))
            {
                log = responseBody;
            }
            else
            {
                var sb = new StringBuilder();
                sb.AppendLine("=== リクエスト URL ===");
                sb.AppendLine(requestUrl);
                sb.AppendLine();
                sb.AppendLine("=== レスポンス電文 ===");
                sb.Append(responseBody);
                log = sb.ToString();
            }

            _vm.TestLog     = log;
            _vm.TestSuccess = true;
        }
        catch (Exception ex)
        {
            var sb = new StringBuilder();
            sb.AppendLine("=== リクエスト URL ===");
            sb.AppendLine(BuildRequestUrlPreview(settings));
            sb.AppendLine();
            sb.AppendLine("=== エラー詳細 ===");
            sb.AppendLine(ex.Message);
            if (ex.InnerException is not null)
            {
                sb.AppendLine();
                sb.AppendLine("--- 内部例外 ---");
                sb.AppendLine(ex.InnerException.Message);
            }

            _vm.TestLog     = sb.ToString();
            _vm.TestSuccess = false;
        }
        finally
        {
            _vm.IsTesting = false;

            // ログ末尾までスクロール
            TestLogBox.ScrollToEnd();
        }
    }

    /// <summary>ダイアログの現在の入力値から IssueTrackingSettings を組み立てる。</summary>
    private IssueTrackingSettings BuildCurrentSettings() => new()
    {
        Provider   = _vm.Provider,
        BaseUrl    = _vm.BaseUrl.Trim(),
        ProjectId  = _vm.ProjectId.Trim(),
        ApiToken   = ApiTokenBox.Password,
        Email      = _vm.Email.Trim(),
        Username   = _vm.Username.Trim(),
        Query       = _vm.Query.Trim(),
        ExtraParams = _vm.ExtraParams.Trim(),
        MaxResults  = _vm.MaxResults,
    };

    /// <summary>エラー時のログ用にリクエスト URL のプレビューを返す。</summary>
    private static string BuildRequestUrlPreview(IssueTrackingSettings s)
    {
        var base_ = s.BaseUrl.TrimEnd('/');
        var id    = Uri.EscapeDataString(s.ProjectId);
        return s.Provider.ToLowerInvariant() switch
        {
            "jira"       => $"{base_}/rest/api/3/project/{id}",
            "jiraonprem" => $"{base_}/rest/api/2/search",
            "redmine"    => $"{base_}/projects/{id}.json",
            _            => $"{base_}/api/v4/projects/{id}",
        };
    }
}
