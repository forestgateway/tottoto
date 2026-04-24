using todochart.Models;

namespace todochart.Services;

/// <summary>
/// Issue 取得プロバイダーの共通インターフェース。
/// </summary>
public interface IIssueProvider
{
    /// <summary>
    /// 設定に基づいて Issue 一覧を取得する。
    /// </summary>
    Task<List<IssueCacheItem>> FetchIssuesAsync(IssueTrackingSettings settings,
                                                CancellationToken cancellationToken = default);

    /// <summary>
    /// 接続テストを行い、送信した URL とレスポンス電文を返す。
    /// エラー時は例外をそのままスローする。
    /// </summary>
    /// <returns>(requestUrl, responseBody)</returns>
    Task<(string RequestUrl, string ResponseBody)> TestConnectionAsync(
        IssueTrackingSettings settings,
        CancellationToken cancellationToken = default);
}
