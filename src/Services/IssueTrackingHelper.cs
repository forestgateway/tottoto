using todochart.Models;

namespace todochart.Services;

/// <summary>
/// Issue Tracking 関連のユーティリティ。
/// - Issue の状態文字列を ItemStatus へマッピング
/// - IssueCacheItem から ScheduleToDo を生成
/// </summary>
public static class IssueTrackingHelper
{
    /// <summary>
    /// プロバイダー固有の状態文字列を ItemStatus にマッピングする。
    /// </summary>
    public static ItemStatus MapState(string state, DateTime? dueDate)
    {
        var normalized = state.Trim().ToLowerInvariant();

        // 完了
        if (normalized is "closed" or "done" or "resolved")
            return ItemStatus.Complete;

        // 進行中
        if (normalized is "in progress" or "doing" or "in_progress")
            return ItemStatus.Progress;

        // レビュー・テスト・ブロック中
        if (normalized is "review" or "test" or "blocked" or "in review")
            return ItemStatus.Warning;

        // 開始前・バックログ
        if (normalized is "open" or "opened" or "to do" or "todo" or "backlog" or "new")
        {
            // 期限超過チェック
            if (dueDate.HasValue && dueDate.Value.Date < DateTime.Today)
                return ItemStatus.Error;
            return ItemStatus.Wait;
        }

        // 不明な状態はデフォルト Wait
        if (dueDate.HasValue && dueDate.Value.Date < DateTime.Today)
            return ItemStatus.Error;
        return ItemStatus.Wait;
    }

    /// <summary>
    /// IssueCacheItem から ScheduleToDo を生成する。
    /// </summary>
    public static ScheduleToDo ToScheduleToDo(IssueCacheItem issue)
    {
        var dueDate = ParseDate(issue.DueDate);
        var status  = MapState(issue.State, dueDate);

        // Memo: 担当者・ラベル・元ステータスをまとめて格納
        var memoParts = new List<string>();
        if (!string.IsNullOrEmpty(issue.Assignee))
            memoParts.Add($"担当: {issue.Assignee}");
        if (!string.IsNullOrEmpty(issue.Labels))
            memoParts.Add($"ラベル: {issue.Labels}");
        if (!string.IsNullOrEmpty(issue.State))
            memoParts.Add($"状態: {issue.State}");
        var memo = string.Join("\n", memoParts);

        return new ScheduleToDo
        {
            Name      = issue.Title,
            Link      = issue.WebUrl,
            Memo      = memo,
            BeginDate = ParseDate(issue.CreatedAt),
            EndDate   = dueDate,
            Completed = status == ItemStatus.Complete,
        };
    }

    private static DateTime? ParseDate(string? s)
    {
        if (string.IsNullOrEmpty(s)) return null;
        if (DateTime.TryParseExact(s, "yyyy-MM-dd",
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None, out var dt))
            return dt;
        if (DateTime.TryParse(s, out var dt2))
            return dt2.Date;
        return null;
    }

    /// <summary>
    /// プロバイダー名に対応する IIssueProvider を返す。
    /// </summary>
    public static IIssueProvider CreateProvider(string provider) =>
        provider.Trim().ToLowerInvariant() switch
        {
            "gitlab"     => new GitLabIssueProvider(),
            "jira"       => new JiraIssueProvider(),
            "jiraonprem" => new JiraOnPremIssueProvider(),
            _            => throw new NotSupportedException($"未対応のプロバイダー: {provider}"),
        };
}
