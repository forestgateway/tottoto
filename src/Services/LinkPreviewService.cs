using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.RegularExpressions;
using todochart.Models;

namespace todochart.Services;

/// <summary>
/// URL から HTML ページタイトルを取得するサービス。
/// 既知の Issue Tracking インスタンス（GitLab / Jira）は API 経由でイシュータイトルを取得する。
/// </summary>
public static class LinkPreviewService
{
    private static readonly HttpClient s_client = new()
    {
        Timeout = TimeSpan.FromSeconds(5),
        DefaultRequestHeaders =
        {
            { "User-Agent", "Mozilla/5.0 (compatible; todochart/1.0)" },
        },
    };

    // <title>...</title> を抽出する正規表現
    private static readonly Regex s_titleRegex = new(
        @"<title[^>]*>\s*(?<t>[^<]{1,300})\s*</title>",
        RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);

    // GitLab issue URL: /-/issues/{iid} または /issues/{iid}
    private static readonly Regex s_gitlabIssueRegex = new(
        @"(?:/-/issues|/issues)/(\d+)",
        RegexOptions.Compiled);

    // Jira issue URL: /browse/{KEY-123}
    private static readonly Regex s_jiraIssueRegex = new(
        @"/browse/([A-Za-z][A-Za-z0-9_]*-\d+)",
        RegexOptions.Compiled);

    /// <summary>
    /// 指定 URL のタイトルを取得する。
    /// <paramref name="knownInstances"/> に一致する Issue Tracking インスタンスがあれば
    /// API 経由でイシュータイトルを取得し、なければ HTML の &lt;title&gt; を使う。
    /// </summary>
    public static async Task<string?> FetchTitleAsync(
        string url,
        IEnumerable<IssueTrackingSettings>? knownInstances = null,
        TimeSpan? timeout = null)
    {
        using var cts = new CancellationTokenSource(timeout ?? TimeSpan.FromSeconds(4));

        // 1. 既知インスタンスで API 取得を試みる
        if (knownInstances is not null)
        {
            var apiTitle = await TryFetchFromKnownInstanceAsync(url, knownInstances, cts.Token);
            if (!string.IsNullOrWhiteSpace(apiTitle))
                return apiTitle;
        }

        // 2. HTML <title> スクレイピングにフォールバック
        return await FetchHtmlTitleAsync(url, cts.Token);
    }

    // ── 既知インスタンスへの API アクセス ────────────────────────────────

    private static async Task<string?> TryFetchFromKnownInstanceAsync(
        string url, IEnumerable<IssueTrackingSettings> instances, CancellationToken ct)
    {
        foreach (var s in instances)
        {
            if (string.IsNullOrEmpty(s.BaseUrl)) continue;
            var baseUrl = s.BaseUrl.TrimEnd('/');
            if (!url.StartsWith(baseUrl, StringComparison.OrdinalIgnoreCase)) continue;

            var pathPart = url.Substring(baseUrl.Length);

            try
            {
                if (s.Provider.Equals("GitLab", StringComparison.OrdinalIgnoreCase))
                {
                    var title = await FetchGitLabIssueTitleAsync(baseUrl, pathPart, s, ct);
                    if (title is not null) return title;
                }
                else if (s.Provider.Equals("Jira", StringComparison.OrdinalIgnoreCase) ||
                         s.Provider.Equals("JiraOnPrem", StringComparison.OrdinalIgnoreCase))
                {
                    var title = await FetchJiraIssueTitleAsync(baseUrl, pathPart, s, ct);
                    if (title is not null) return title;
                }
            }
            catch { /* 失敗時は次のインスタンスへ */ }
        }
        return null;
    }

    private static async Task<string?> FetchGitLabIssueTitleAsync(
        string baseUrl, string pathPart, IssueTrackingSettings s, CancellationToken ct)
    {
        var m = s_gitlabIssueRegex.Match(pathPart);
        if (!m.Success) return null;

        var iid = m.Groups[1].Value;
        // プロジェクトパス = /-/issues/ または /issues/ の直前まで
        var projectPath = pathPart.Substring(1, m.Index - 1).Trim('/');
        if (string.IsNullOrEmpty(projectPath)) return null;

        var encodedPath = Uri.EscapeDataString(projectPath);
        var apiUrl = $"{baseUrl}/api/v4/projects/{encodedPath}/issues/{iid}";

        using var client = BuildGitLabClient(s);
        var res = await client.GetAsync(apiUrl, ct);
        if (!res.IsSuccessStatusCode) return null;

        var json = await res.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.TryGetProperty("title", out var t) ? t.GetString() : null;
    }

    private static async Task<string?> FetchJiraIssueTitleAsync(
        string baseUrl, string pathPart, IssueTrackingSettings s, CancellationToken ct)
    {
        var m = s_jiraIssueRegex.Match(pathPart);
        if (!m.Success) return null;

        var issueKey   = m.Groups[1].Value;
        var apiVersion = s.Provider.Equals("Jira", StringComparison.OrdinalIgnoreCase) ? "3" : "2";
        var apiUrl     = $"{baseUrl}/rest/api/{apiVersion}/issue/{issueKey}?fields=summary";

        using var client = BuildJiraClient(s);
        var res = await client.GetAsync(apiUrl, ct);
        if (!res.IsSuccessStatusCode) return null;

        var json = await res.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(json);
        if (doc.RootElement.TryGetProperty("fields", out var fields) &&
            fields.TryGetProperty("summary", out var summary))
            return summary.GetString();
        return null;
    }

    private static HttpClient BuildGitLabClient(IssueTrackingSettings s)
    {
        var client = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
        if (!string.IsNullOrEmpty(s.ApiToken))
            client.DefaultRequestHeaders.Add("PRIVATE-TOKEN", s.ApiToken);
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        return client;
    }

    private static HttpClient BuildJiraClient(IssueTrackingSettings s)
    {
        var client = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        // Jira Cloud: email + apiToken
        if (!string.IsNullOrEmpty(s.Email) && !string.IsNullOrEmpty(s.ApiToken))
        {
            var cred = Convert.ToBase64String(
                System.Text.Encoding.UTF8.GetBytes($"{s.Email}:{s.ApiToken}"));
            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Basic", cred);
        }
        // JiraOnPrem: username + apiToken/password
        else if (!string.IsNullOrEmpty(s.Username) && !string.IsNullOrEmpty(s.ApiToken))
        {
            var cred = Convert.ToBase64String(
                System.Text.Encoding.UTF8.GetBytes($"{s.Username}:{s.ApiToken}"));
            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Basic", cred);
        }
        return client;
    }

    // ── HTML <title> スクレイピング ───────────────────────────────────────

    private static async Task<string?> FetchHtmlTitleAsync(string url, CancellationToken ct)
    {
        try
        {
            // HEAD で Content-Type を確認（HTML でなければスキップ）
            try
            {
                using var headReq = new HttpRequestMessage(HttpMethod.Head, url);
                var headRes = await s_client.SendAsync(headReq, ct);
                var ct2 = headRes.Content.Headers.ContentType?.MediaType ?? string.Empty;
                if (!ct2.Contains("html", StringComparison.OrdinalIgnoreCase))
                    return null;
            }
            catch { /* HEAD 非対応サーバー → GET にフォールバック */ }

            // GET で先頭 8KB だけ取得
            using var getReq = new HttpRequestMessage(HttpMethod.Get, url);
            getReq.Headers.Range = new System.Net.Http.Headers.RangeHeaderValue(0, 8191);
            var res = await s_client.SendAsync(
                getReq, HttpCompletionOption.ResponseContentRead, ct);

            var html = await res.Content.ReadAsStringAsync(ct);
            var m    = s_titleRegex.Match(html);
            if (!m.Success) return null;

            var title = System.Net.WebUtility.HtmlDecode(m.Groups["t"].Value.Trim());
            return string.IsNullOrWhiteSpace(title) ? null : title;
        }
        catch
        {
            return null;
        }
    }
}
