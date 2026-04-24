using System.Net.Http;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using todochart.Models;

namespace todochart.Services;

/// <summary>
/// Jira REST API v3 繝ｻ・ｽ繝ｻ・ｽ繝ｻ・ｽ繝ｻ・ｽ Issue 繝ｻ・ｽ繝ｻ・ｽ隰ｫ・ｾ繝ｻ・ｽ繝ｻ・ｽ繝ｻ・ｽ繝ｻ・ｽv繝ｻ・ｽ繝ｻ・ｽ繝ｻ・ｽo繝ｻ・ｽC繝ｻ・ｽ_繝ｻ・ｽ[繝ｻ・ｽB
/// JQL 繝ｻ・ｽ繝ｻ・ｽg繝ｻ・ｽ繝ｻ・ｽ繝ｻ・ｽ・・ｯ会ｽｿ・ｽ繝ｻ・ｽ繝ｻ・ｽ繝ｻ・ｽ繝ｻ・ｽ繝ｻ・ｽ繝ｻ・ｽB
/// </summary>
public class JiraIssueProvider : IIssueProvider
{
    public async Task<List<IssueCacheItem>> FetchIssuesAsync(IssueTrackingSettings settings,
                                                             CancellationToken cancellationToken = default)
    {
        var baseUrl    = settings.BaseUrl.TrimEnd('/');
        var jql        = string.IsNullOrWhiteSpace(settings.Query)
                         ? BuildDefaultJql(settings.ProjectId)
                         : settings.Query;
        var maxResults = settings.MaxResults > 0 ? settings.MaxResults : 100;
        var result     = new List<IssueCacheItem>();

        using var client = BuildClient(settings);

        // /rest/api/3/search/jql は nextPageToken ベースだが
        // startAt 互換モードもサポートされるため両方に対応する
        string? nextPageToken = null;
        int startAt = 0;

        while (true)
        {
            var urlBuilder = new System.Text.StringBuilder();
            urlBuilder.Append($"{baseUrl}/rest/api/3/search/jql");
            urlBuilder.Append($"?jql={Uri.EscapeDataString(jql)}");
            urlBuilder.Append($"&maxResults={maxResults}");
            urlBuilder.Append("&fields=summary,status,assignee,labels,created,duedate");

            if (nextPageToken is not null)
                urlBuilder.Append($"&nextPageToken={Uri.EscapeDataString(nextPageToken)}");
            else if (startAt > 0)
                urlBuilder.Append($"&startAt={startAt}");

            var response = await client.GetAsync(urlBuilder.ToString(), cancellationToken);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            var dto  = JsonSerializer.Deserialize<JiraSearchResultDto>(json, s_jsonOptions);
            if (dto?.Issues is null || dto.Issues.Count == 0) break;

            foreach (var issue in dto.Issues)
                result.Add(MapToCache(issue, settings.BaseUrl));

            // isLast=true 、または nextPageToken なしで total 以上取得済みなら終了
            if (dto.IsLast == true)
                break;

            if (!string.IsNullOrEmpty(dto.NextPageToken))
            {
                nextPageToken = dto.NextPageToken;
            }
            else
            {
                // nextPageToken なしかつ isLast もない場合は startAt で続行
                startAt += dto.Issues.Count;
                if (dto.Total.HasValue && startAt >= dto.Total.Value) break;
                // total 不明の場合は issues が 0 件の時に全体ブレーク（先頭のガードで層別済み）
            }
        }

        return result;
    }

    /// <inheritdoc/>
    public async Task<(string RequestUrl, string ResponseBody)> TestConnectionAsync(
        IssueTrackingSettings settings,
        CancellationToken cancellationToken = default)
    {
        var baseUrl = settings.BaseUrl.TrimEnd('/');
        var sb      = new System.Text.StringBuilder();

        using var client = BuildClient(settings);

        // ① プロジェクト情報取得
        var projectUrl = $"{baseUrl}/rest/api/3/project/{Uri.EscapeDataString(settings.ProjectId)}";
        sb.AppendLine("=== [1/2] プロジェクト情報取得 ===");
        sb.AppendLine(projectUrl);
        sb.AppendLine();

        var projResp = await client.GetAsync(projectUrl, cancellationToken);
        var projBody = await projResp.Content.ReadAsStringAsync(cancellationToken);

        if (!projResp.IsSuccessStatusCode)
        {
            throw new HttpRequestException(
                $"[1/2] HTTP {(int)projResp.StatusCode} {projResp.ReasonPhrase}\n{projBody}",
                null,
                projResp.StatusCode);
        }

        sb.AppendLine(PrettyJson(projBody));
        sb.AppendLine();

        // ② Issue 検索 (maxResults=3 で痎通確認)
        var jql        = string.IsNullOrWhiteSpace(settings.Query)
                         ? BuildDefaultJql(settings.ProjectId)
                         : settings.Query;
        var searchUrl  = $"{baseUrl}/rest/api/3/search/jql"
                       + $"?jql={Uri.EscapeDataString(jql)}"
                       + "&maxResults=3"
                       + "&fields=summary,status,assignee,created";

        sb.AppendLine("=== [2/2] Issue 検索テスト ===");
        sb.AppendLine(searchUrl);
        sb.AppendLine();

        var searchResp = await client.GetAsync(searchUrl, cancellationToken);
        var searchBody = await searchResp.Content.ReadAsStringAsync(cancellationToken);

        if (!searchResp.IsSuccessStatusCode)
        {
            // 検索失敗はエラーとして投げる（メッセージに検索レスポンスを含める）
            sb.AppendLine(PrettyJson(searchBody));
            throw new HttpRequestException(
                $"[2/2] HTTP {(int)searchResp.StatusCode} {searchResp.ReasonPhrase}\n\nプロジェクト情報取得は成功しましたが、Issue 検索に失敗しました。\nJQL: {jql}\n\n{searchBody}",
                null,
                searchResp.StatusCode);
        }

        sb.AppendLine(PrettyJson(searchBody));

        // 返却内容: リクエスト URL はプロジェクト URL、ボディは両方の合算
        return (searchUrl, sb.ToString());
    }

    /// <summary>
    /// プロジェクトID（数値またはキー）からデフォルトJQLを生成する。
    /// Jira Cloudでは数値IDをクオート付きデ指定すると文字列比較になり0件になるが、
    /// 数値のまま指定するか id() 関数を使うと正しく動く。
    /// </summary>
    private static string BuildDefaultJql(string projectId)
    {
        // 数値IDの場合: id() 関数 → project = 10000 (Jira Cloud Next-gen 対応)
        if (long.TryParse(projectId.Trim(), out _))
            return $"project = {projectId.Trim()} ORDER BY created DESC";

        // プロジェクトキーの場合: クオートで囲む
        return $"project = \"{projectId.Trim()}\" ORDER BY created DESC";
    }

    private static HttpClient BuildClient(IssueTrackingSettings settings)
    {
        var client = new HttpClient();
        var email  = settings.Email.Trim();
        var token  = settings.ApiToken.Trim();

        if (!string.IsNullOrEmpty(token))
        {
            // Jira Cloud Basic 認証: Base64("email:apiToken")
            var credential = string.IsNullOrEmpty(email) ? $":{token}" : $"{email}:{token}";
            var encoded    = Convert.ToBase64String(Encoding.UTF8.GetBytes(credential));
            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Basic", encoded);
        }

        client.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/json"));
        return client;
    }

    private static IssueCacheItem MapToCache(JiraIssueDto dto, string baseUrl) => new()
    {
        Id        = dto.Key ?? string.Empty,
        Title     = dto.Fields?.Summary ?? string.Empty,
        State     = dto.Fields?.Status?.Name ?? string.Empty,
        Assignee  = dto.Fields?.Assignee?.DisplayName ?? string.Empty,
        Labels    = dto.Fields?.Labels is { Count: > 0 }
                    ? string.Join(", ", dto.Fields.Labels) : string.Empty,
        WebUrl    = string.IsNullOrEmpty(dto.Key) ? string.Empty
                    : $"{baseUrl.TrimEnd('/')}/browse/{dto.Key}",
        CreatedAt = ParseJiraDate(dto.Fields?.Created),
        DueDate   = ParseJiraDate(dto.Fields?.Duedate),
    };

    /// <summary>
    /// Jira が返す日時文字列を日付文字列に変換する。
    /// "+0900"（コロンなし）形式など System.Text.Json が拒否する形式も DateTimeOffset.TryParse で処理する。
    /// </summary>
    private static string? ParseJiraDate(string? value)
    {
        if (string.IsNullOrEmpty(value)) return null;
        if (DateTimeOffset.TryParse(value,
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None,
                out var dto))
            return dto.ToString("yyyy-MM-dd");
        // 純粋日付形式の場合は先頭 10 文字だけ抽出
        return value.Length >= 10 ? value[..10] : value;
    }

    private static string PrettyJson(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            return JsonSerializer.Serialize(doc, s_prettyOptions);
        }
        catch
        {
            return json;
        }
    }

    private static readonly JsonSerializerOptions s_jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private static readonly JsonSerializerOptions s_prettyOptions = new()
    {
        WriteIndented = true,
    };

    // ── DTO ──────────────────────────────────────────────────────────────────
    private class JiraSearchResultDto
    {
        public int? Total { get; set; }
        public List<JiraIssueDto>? Issues { get; set; }
        [System.Text.Json.Serialization.JsonPropertyName("nextPageToken")]
        public string? NextPageToken { get; set; }
        [System.Text.Json.Serialization.JsonPropertyName("isLast")]
        public bool? IsLast { get; set; }
    }

    private class JiraIssueDto
    {
        public string? Key    { get; set; }
        public JiraFieldsDto? Fields { get; set; }
    }

    private class JiraFieldsDto
    {
        public string? Summary   { get; set; }
        public JiraStatusDto?   Status   { get; set; }
        public JiraUserDto?     Assignee { get; set; }
        public List<string>?    Labels   { get; set; }
        public string?          Created  { get; set; }
        public string?          Duedate  { get; set; }
    }

    private class JiraStatusDto
    {
        public string? Name { get; set; }
    }

    private class JiraUserDto
    {
        public string? DisplayName { get; set; }
    }
}
