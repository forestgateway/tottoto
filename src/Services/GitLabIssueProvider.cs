using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using todochart.Models;

namespace todochart.Services;

/// <summary>
/// GitLab REST API v4 を使用して Issue を取得するプロバイダー。
/// </summary>
public class GitLabIssueProvider : IIssueProvider
{
    public async Task<List<IssueCacheItem>> FetchIssuesAsync(IssueTrackingSettings settings,
                                                             CancellationToken cancellationToken = default)
    {
        var baseUrl = settings.BaseUrl.TrimEnd('/');
        var projectId = Uri.EscapeDataString(settings.ProjectId);

        // クエリパラメーターを構築
        var queryParts = new List<string> { "per_page=100" };
        if (!string.IsNullOrWhiteSpace(settings.Query))
            queryParts.Add(settings.Query.TrimStart('?', '&'));

        var url = $"{baseUrl}/api/v4/projects/{projectId}/issues?{string.Join("&", queryParts)}";

        using var client = BuildClient(settings);
        var result = new List<IssueCacheItem>();
        int page = 1;

        while (true)
        {
            var pagedUrl = $"{url}&page={page}";
            var response = await client.GetAsync(pagedUrl, cancellationToken);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            var issues = JsonSerializer.Deserialize<List<GitLabIssueDto>>(json, s_jsonOptions)
                         ?? [];

            if (issues.Count == 0) break;

            foreach (var issue in issues)
                result.Add(MapToCache(issue));

            // GitLab の X-Next-Page ヘッダーで次ページを判断
            if (response.Headers.TryGetValues("X-Next-Page", out var nextVals)
                && int.TryParse(nextVals.FirstOrDefault(), out int nextPage)
                && nextPage > 0)
            {
                page = nextPage;
            }
            else
            {
                break;
            }
        }

        return result;
    }

    /// <inheritdoc/>
    public async Task<(string RequestUrl, string ResponseBody)> TestConnectionAsync(
        IssueTrackingSettings settings,
        CancellationToken cancellationToken = default)
    {
        var baseUrl   = settings.BaseUrl.TrimEnd('/');
        var projectId = Uri.EscapeDataString(settings.ProjectId);

        // プロジェクト情報を1件取得してトークンや URL の疎通を確認
        var url = $"{baseUrl}/api/v4/projects/{projectId}";

        using var client = BuildClient(settings);
        var response = await client.GetAsync(url, cancellationToken);

        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        // ステータスコードが 2xx 以外は例外を生成して呼び出し元でエラー処理にする
        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException(
                $"HTTP {(int)response.StatusCode} {response.ReasonPhrase}\n{body}",
                null,
                response.StatusCode);
        }

        // レスポンスを読みやすいインデント形式にして返す
        var pretty = PrettyJson(body);
        return (url, pretty);
    }

    private static HttpClient BuildClient(IssueTrackingSettings settings)
    {
        var client = new HttpClient();
        if (!string.IsNullOrEmpty(settings.ApiToken))
            client.DefaultRequestHeaders.Add("PRIVATE-TOKEN", settings.ApiToken);
        client.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/json"));
        return client;
    }

    private static IssueCacheItem MapToCache(GitLabIssueDto dto) => new()
    {
        Id        = dto.Iid?.ToString() ?? dto.Id?.ToString() ?? string.Empty,
        Title     = dto.Title    ?? string.Empty,
        State     = dto.State    ?? string.Empty,
        Assignee  = dto.Assignee?.Username ?? string.Empty,
        Labels    = dto.Labels is { Count: > 0 } ? string.Join(", ", dto.Labels) : string.Empty,
        WebUrl    = dto.WebUrl   ?? string.Empty,
        CreatedAt = dto.CreatedAt?.ToString("yyyy-MM-dd"),
        DueDate   = dto.DueDate,
    };

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

    // ── GitLab API レスポンス DTO ─────────────────────────────────────────────
    private class GitLabIssueDto
    {
        public int? Id       { get; set; }
        public int? Iid      { get; set; }
        public string? Title { get; set; }
        public string? State { get; set; }
        public List<string>? Labels { get; set; }
        [System.Text.Json.Serialization.JsonPropertyName("web_url")]
        public string? WebUrl { get; set; }
        [System.Text.Json.Serialization.JsonPropertyName("created_at")]
        public DateTime? CreatedAt { get; set; }
        [System.Text.Json.Serialization.JsonPropertyName("due_date")]
        public string? DueDate { get; set; }
        public GitLabUserDto? Assignee { get; set; }
    }

    private class GitLabUserDto
    {
        public string? Username { get; set; }
        public string? Name     { get; set; }
    }
}