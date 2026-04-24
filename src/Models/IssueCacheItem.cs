using System.Text.Json.Serialization;

namespace todochart.Models;

/// <summary>
/// API から取得した Issue 1 件のキャッシュデータ。
/// JSON ファイルの "issueCache" 配列に保存される。
/// </summary>
public class IssueCacheItem
{
    /// <summary>Issue ID または番号 (文字列)</summary>
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    /// <summary>Issue タイトル</summary>
    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    /// <summary>プロバイダー固有の状態文字列 (例: "opened", "In Progress")</summary>
    [JsonPropertyName("state")]
    public string State { get; set; } = string.Empty;

    /// <summary>担当者名</summary>
    [JsonPropertyName("assignee")]
    public string Assignee { get; set; } = string.Empty;

    /// <summary>ラベル (カンマ区切り)</summary>
    [JsonPropertyName("labels")]
    public string Labels { get; set; } = string.Empty;

    /// <summary>Issue の Web URL</summary>
    [JsonPropertyName("webUrl")]
    public string WebUrl { get; set; } = string.Empty;

    /// <summary>開始日 (yyyy-MM-dd 形式。なければ null)</summary>
    [JsonPropertyName("createdAt")]
    public string? CreatedAt { get; set; }

    /// <summary>期限日 (yyyy-MM-dd 形式。なければ null)</summary>
    [JsonPropertyName("dueDate")]
    public string? DueDate { get; set; }
}
