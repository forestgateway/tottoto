using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using todochart.Models;

namespace todochart.Services;

/// <summary>
/// Redmine REST API を使用して Issue を取得するプロバイダー。
/// </summary>
public class RedmineIssueProvider : IIssueProvider
{
    /// <inheritdoc/>
    public async Task<List<IssueCacheItem>> FetchIssuesAsync(
        IssueTrackingSettings settings,
        CancellationToken cancellationToken = default)
    {
        var baseUrl    = settings.BaseUrl.TrimEnd('/');
        var projectId  = settings.ProjectId.Trim();
        var result     = new List<IssueCacheItem>();
        var limit      = settings.MaxResults > 0 ? settings.MaxResults : 100;
        var offset     = 0;

        using var client = BuildClient(settings);

        while (true)
        {
            var url = $"{baseUrl}/issues.json"
                    + $"?project_id={Uri.EscapeDataString(projectId)}"
                    + $"&limit={limit}"
                    + $"&offset={offset}";

            if (!string.IsNullOrWhiteSpace(settings.Query))
                url += "&" + settings.Query.TrimStart('?', '&');

            var response = await client.GetAsync(url, cancellationToken);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            var dto  = JsonSerializer.Deserialize<RedmineIssuesResponseDto>(json, s_jsonOptions);
            if (dto?.Issues is null || dto.Issues.Count == 0)
                break;

            foreach (var issue in dto.Issues)
                result.Add(MapToCache(issue, baseUrl));

            offset += dto.Issues.Count;
            if (!dto.TotalCount.HasValue || offset >= dto.TotalCount.Value)
                break;
        }

        return result;
    }

    /// <inheritdoc/>
    public async Task<(string RequestUrl, string ResponseBody)> TestConnectionAsync(
        IssueTrackingSettings settings,
        CancellationToken cancellationToken = default)
    {
        var baseUrl   = settings.BaseUrl.TrimEnd('/');
        var projectId = Uri.EscapeDataString(settings.ProjectId.Trim());
        var url       = $"{baseUrl}/projects/{projectId}.json";

        using var client = BuildClient(settings);
        var response = await client.GetAsync(url, cancellationToken);
        var body     = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException(
                $"HTTP {(int)response.StatusCode} {response.ReasonPhrase}\n{body}",
                null,
                response.StatusCode);
        }

        return (url, PrettyJson(body));
    }

    private static HttpClient BuildClient(IssueTrackingSettings settings)
    {
        var client = new HttpClient();
        if (!string.IsNullOrWhiteSpace(settings.ApiToken))
            client.DefaultRequestHeaders.Add("X-Redmine-API-Key", settings.ApiToken.Trim());

        client.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/json"));
        return client;
    }

    private static IssueCacheItem MapToCache(RedmineIssueDto dto, string baseUrl) => new()
    {
        Id        = dto.Id?.ToString() ?? string.Empty,
        Title     = dto.Subject ?? string.Empty,
        State     = dto.Status?.Name ?? string.Empty,
        Assignee  = dto.AssignedTo?.Name ?? string.Empty,
        Labels    = dto.Tracker?.Name ?? string.Empty,
        WebUrl    = dto.Id.HasValue ? $"{baseUrl}/issues/{dto.Id.Value}" : string.Empty,
        CreatedAt = dto.StartDate,
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

    private class RedmineIssuesResponseDto
    {
        public List<RedmineIssueDto>? Issues { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("total_count")]
        public int? TotalCount { get; set; }
    }

    private class RedmineIssueDto
    {
        public int? Id { get; set; }
        public string? Subject { get; set; }
        public RedmineNamedDto? Status { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("assigned_to")]
        public RedmineNamedDto? AssignedTo { get; set; }

        public RedmineNamedDto? Tracker { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("start_date")]
        public string? StartDate { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("due_date")]
        public string? DueDate { get; set; }
    }

    private class RedmineNamedDto
    {
        public string? Name { get; set; }
    }
}
