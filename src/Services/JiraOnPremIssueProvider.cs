using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using todochart.Models;

namespace todochart.Services;

/// <summary>
/// JIRA On-Premises（Server / Data Center）用 Issue 取得プロバイダー。
/// REST API v2 <c>GET /rest/api/2/search</c> を使用し、
/// Basic 認証（username:password または username:apiToken）で接続する。
/// </summary>
public class JiraOnPremIssueProvider : IIssueProvider
{
    /// <inheritdoc/>
    public async Task<List<IssueCacheItem>> FetchIssuesAsync(
        IssueTrackingSettings settings,
        CancellationToken cancellationToken = default)
    {
        var baseUrl    = settings.BaseUrl.TrimEnd('/');
        var jql        = BuildJql(settings.ProjectId, settings.Query);
        var maxResults = settings.MaxResults > 0 ? settings.MaxResults : 50;
        var result     = new List<IssueCacheItem>();

        using var client = BuildClient(settings);

        int startAt = 0;
        while (true)
        {
            var url = $"{baseUrl}/rest/api/2/search"
                    + $"?jql={Uri.EscapeDataString(jql)}";

            if (!string.IsNullOrWhiteSpace(settings.ExtraParams))
                url += "&" + settings.ExtraParams.TrimStart('&');

            url += $"&maxResults={maxResults}"
                 + $"&startAt={startAt}"
                 + "&fields=key,summary,status,assignee,labels,startdate,duedate";

            var response = await client.GetAsync(url, cancellationToken);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            var dto  = JsonSerializer.Deserialize<V2SearchResultDto>(json, s_jsonOptions);
            if (dto?.Issues is null || dto.Issues.Count == 0) break;

            foreach (var issue in dto.Issues)
                result.Add(MapToCache(issue, settings.BaseUrl));

            startAt += dto.Issues.Count;
            if (dto.Total.HasValue && startAt >= dto.Total.Value) break;
        }

        return result;
    }

    /// <inheritdoc/>
    public async Task<(string RequestUrl, string ResponseBody)> TestConnectionAsync(
        IssueTrackingSettings settings,
        CancellationToken cancellationToken = default)
    {
        var baseUrl = settings.BaseUrl.TrimEnd('/');
        var sb      = new StringBuilder();

        using var client = BuildClient(settings);

        // 仕様書 §6 の動作確認コマンドに従い /rest/api/2/search 1本でテスト
        // maxResults=3 で疎通・認証・JQL 構文を一括確認する
        var jql       = BuildJql(settings.ProjectId, settings.Query);
        var searchUrl = $"{baseUrl}/rest/api/2/search"
                      + $"?jql={Uri.EscapeDataString(jql)}";

        if (!string.IsNullOrWhiteSpace(settings.ExtraParams))
            searchUrl += "&" + settings.ExtraParams.TrimStart('&');

        searchUrl += "&maxResults=3"
                   + "&fields=key,summary,status,assignee,startdate,duedate";


        sb.AppendLine("=== リクエスト URL ===");
        sb.AppendLine(searchUrl);
        sb.AppendLine();

        var searchResp = await client.GetAsync(searchUrl, cancellationToken);
        var searchBody = await searchResp.Content.ReadAsStringAsync(cancellationToken);

        if (!searchResp.IsSuccessStatusCode)
        {
            sb.AppendLine(PrettyJson(searchBody));
            throw new HttpRequestException(
                $"HTTP {(int)searchResp.StatusCode} {searchResp.ReasonPhrase}"
                + $"\nエンドポイント: {searchUrl}"
                + $"\nJQL: {jql}\n\n{searchBody}",
                null,
                searchResp.StatusCode);
        }

        sb.AppendLine("=== レスポンス電文 ===");
        sb.AppendLine(PrettyJson(searchBody));
        return (searchUrl, sb.ToString());
    }

    // ── 内部ヘルパー ────────────────────────────────────────────────────────

    /// <summary>
    /// JQL を組み立てる（仕様書 5.3 JQL生成ルール準拠）。
    /// <para>customQuery が空: <c>project="{key}"</c></para>
    /// <para>customQuery あり: <c>project="{key}" AND ({customQuery})</c></para>
    /// </summary>
    private static string BuildJql(string projectKey, string customQuery)
    {
        var key     = projectKey.Trim();
        var baseJql = long.TryParse(key, out _)
            ? $"project = {key}"
            : $"project = \"{key}\"";

        return string.IsNullOrWhiteSpace(customQuery)
            ? baseJql
            : $"{baseJql} AND ({customQuery.Trim()})";
    }

    private static HttpClient BuildClient(IssueTrackingSettings settings)
    {
        var client   = new HttpClient();
        var username = settings.Username.Trim();
        var password = settings.ApiToken.Trim();  // Password / API Token は ApiToken フィールドで管理

        if (!string.IsNullOrEmpty(username) && !string.IsNullOrEmpty(password))
        {
            // Basic 認証: Base64("username:password")
            // 仕様書 9: パスワードは平文ログ出力しない（ここでは Headers にのみセット）
            var encoded = Convert.ToBase64String(
                Encoding.UTF8.GetBytes($"{username}:{password}"));
            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Basic", encoded);
        }

        client.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/json"));
        return client;
    }

    private static IssueCacheItem MapToCache(V2IssueDto dto, string baseUrl) => new()
    {
        Id        = dto.Key ?? string.Empty,
        Title     = dto.Fields?.Summary ?? string.Empty,
        State     = dto.Fields?.Status?.Name ?? string.Empty,
        Assignee  = dto.Fields?.Assignee?.DisplayName ?? string.Empty,
        Labels    = dto.Fields?.Labels is { Count: > 0 }
                    ? string.Join(", ", dto.Fields.Labels) : string.Empty,
        WebUrl    = string.IsNullOrEmpty(dto.Key) ? string.Empty
                    : $"{baseUrl.TrimEnd('/')}/browse/{dto.Key}",
        CreatedAt = ParseJiraDate(dto.Fields?.Startdate),
        DueDate   = ParseJiraDate(dto.Fields?.Duedate),
    };

    private static string? ParseJiraDate(string? value)
    {
        if (string.IsNullOrEmpty(value)) return null;
        if (DateTimeOffset.TryParse(value,
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None,
                out var dto))
            return dto.ToString("yyyy-MM-dd");
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

    // ── DTO（REST API v2 レスポンス）────────────────────────────────────────

    private class V2SearchResultDto
    {
        public int? Total { get; set; }
        public List<V2IssueDto>? Issues { get; set; }
    }

    private class V2IssueDto
    {
        public string?     Key    { get; set; }
        public V2FieldsDto? Fields { get; set; }
    }

    private class V2FieldsDto
    {
        public string?       Summary   { get; set; }
        public V2StatusDto?  Status    { get; set; }
        public V2UserDto?    Assignee  { get; set; }
        public List<string>? Labels    { get; set; }
        public string?       Startdate { get; set; }
        public string?       Duedate   { get; set; }
    }

    private class V2StatusDto { public string? Name        { get; set; } }
    private class V2UserDto   { public string? DisplayName { get; set; } }
}
