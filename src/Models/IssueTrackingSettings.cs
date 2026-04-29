using System.Text.Json.Serialization;

namespace todochart.Models;

/// <summary>
/// Issue Tracking 連携の接続設定。
/// JSON ファイルの "issueTrackingSettings" フィールドに保存される。
/// </summary>
public class IssueTrackingSettings
{
    /// <summary>プロバイダー種別: "GitLab" / "Jira" / "JiraOnPrem" / "Redmine"</summary>
    [JsonPropertyName("provider")]
    public string Provider { get; set; } = string.Empty;

    /// <summary>タブ名・ルートフォルダ名に使う表示名</summary>
    [JsonPropertyName("displayName")]
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>サービスのベース URL (例: https://gitlab.example.com)</summary>
    [JsonPropertyName("baseUrl")]
    public string BaseUrl { get; set; } = string.Empty;

    /// <summary>GitLab: プロジェクト ID / Jira Cloud: プロジェクトキー / JiraOnPrem: プロジェクトキー / Redmine: プロジェクト識別子</summary>
    [JsonPropertyName("projectId")]
    public string ProjectId { get; set; } = string.Empty;

    /// <summary>
    /// API トークン / パスワード (平文保存。将来的には暗号化を検討)
    /// GitLab: Personal Access Token / Jira Cloud: API トークン / JiraOnPrem: パスワードまたは API トークン / Redmine: API Access Key
    /// </summary>
    [JsonPropertyName("apiToken")]
    public string ApiToken { get; set; } = string.Empty;

    /// <summary>Jira Cloud Basic 認証用メールアドレス</summary>
    [JsonPropertyName("email")]
    public string Email { get; set; } = string.Empty;

    /// <summary>
    /// JiraOnPrem Basic 認証用ユーザー名。
    /// Jira Cloud では Email フィールドを使用するため本フィールドは使用しない。
    /// </summary>
    [JsonPropertyName("username")]
    public string Username { get; set; } = string.Empty;

    /// <summary>1回の取得件数上限 (Jira Cloud / JiraOnPrem 用、0 以下は既定値 100 を使用)</summary>
    [JsonPropertyName("maxResults")]
    public int MaxResults { get; set; } = 0;

    /// <summary>追加クエリ条件 (GitLab: "assignee=me&amp;state=opened" / JiraOnPrem: JQL (任意) / Redmine: URLクエリ追加条件 例: status_id=open&amp;assigned_to_id=me)</summary>
    [JsonPropertyName("query")]
    public string Query { get; set; } = string.Empty;

    /// <summary>
    /// JiraOnPrem: URL クエリ文字列形式の追加パラメータ。
    /// "&amp;" 区切りで URL 末尾に連結される。例: fields=key,summary&amp;expand=changelog
    /// </summary>
    [JsonPropertyName("extraParams")]
    public string ExtraParams { get; set; } = string.Empty;

    /// <summary>起動時に自動更新するか</summary>
    [JsonPropertyName("autoRefreshOnOpen")]
    public bool AutoRefreshOnOpen { get; set; } = false;
}
