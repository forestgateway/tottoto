using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Net.Http.Json;
using System.Reflection;
using System.Text.Json.Serialization;
using System.Windows;

namespace todochart.Services;

/// <summary>
/// GitHub Releases API を使用してバージョン確認・ダウンロード・更新を行うサービス。
/// </summary>
public class UpdateCheckService
{
    private const string ApiUrl =
        "https://api.github.com/repos/forestgateway/tottoto/releases";

    private static readonly HttpClient s_httpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(30),
        DefaultRequestHeaders = { { "User-Agent", "tottoto-updater/1.0" } },
    };

    /// <summary>現在の実行バージョンを返す（例: "1.0.0"）。</summary>
    public static string CurrentVersion
    {
        get
        {
            var v = Assembly.GetExecutingAssembly().GetName().Version;
            return v is null ? "0.0.0" : $"{v.Major}.{v.Minor}.{v.Build}";
        }
    }

    /// <summary>
    /// GitHub Releases の全リリースを取得し、現バージョンより新しいものを降順で返す。
    /// </summary>
    public async Task<ReleaseInfo> CheckAsync(CancellationToken ct = default)
    {
        var dtos = await s_httpClient.GetFromJsonAsync<List<ReleaseDto>>(ApiUrl, ct)
                   ?? throw new InvalidOperationException("GitHub API からのレスポンスが空です。");

        if (dtos.Count == 0)
            throw new InvalidOperationException("リリースが見つかりませんでした。");

        var curVer = Version.TryParse(CurrentVersion, out var cv) ? cv : new Version(0, 0, 0);

        // バージョン降順で整列（GitHub は通常降順だが念のため）
        var sorted = dtos
            .Select(d =>
            {
                var raw   = d.TagName ?? "0.0.0";
                var clean = raw.TrimStart('v', 'V');
                var ver   = Version.TryParse(clean, out var v) ? v : new Version(0, 0, 0);
                return (Dto: d, Raw: raw, Clean: clean, Ver: ver);
            })
            .OrderByDescending(x => x.Ver)
            .ToList();

        var latest    = sorted[0];
        var latestVer = latest.Ver;
        var dlUrl     = latest.Dto.Assets?.FirstOrDefault()?.BrowserDownloadUrl;

        // 現バージョンより新しいリリースのみ抽出（降順）
        var entries = sorted
            .Where(x => x.Ver > curVer)
            .Select(x => new ReleaseEntry(
                Version: x.Raw,
                Notes:   string.IsNullOrWhiteSpace(x.Dto.Body) ? "（リリースノートなし）" : x.Dto.Body!))
            .ToList();

        return new ReleaseInfo(
            TagName:           latest.Raw,
            LatestVersion:     latest.Clean,
            HtmlUrl:           latest.Dto.HtmlUrl ?? string.Empty,
            DownloadUrl:       dlUrl,
            IsUpdateAvailable: latestVer > curVer,
            Entries:           entries);
    }

    /// <summary>
    /// 指定 URL から zip をダウンロードし、update.bat を生成して batPath を返す。
    /// 再起動は呼び出し元が <see cref="LaunchAndRestart"/> で行う。
    /// </summary>
    public async Task<string> UpdateAsync(
        string downloadUrl,
        IProgress<double> progress,
        CancellationToken ct = default)
    {
        var tempDir  = Path.Combine(Path.GetTempPath(), "tottoto_update");
        var zipPath  = Path.Combine(tempDir, "update.zip");
        var unzipDir = Path.Combine(tempDir, "extracted");

        if (Directory.Exists(tempDir))
            Directory.Delete(tempDir, recursive: true);
        Directory.CreateDirectory(unzipDir);

        using var resp = await s_httpClient.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead, ct);
        resp.EnsureSuccessStatusCode();

        var total = resp.Content.Headers.ContentLength ?? -1L;
        // ダウンロード完了後に dest を閉じてから zip を展開するため、スコープを分ける
        await using (var src  = await resp.Content.ReadAsStreamAsync(ct))
        await using (var dest = File.Create(zipPath))
        {
            var buf       = new byte[81920];
            long received = 0;
            int  read;
            while ((read = await src.ReadAsync(buf, ct)) > 0)
            {
                await dest.WriteAsync(buf.AsMemory(0, read), ct);
                received += read;
                if (total > 0)
                    progress.Report((double)received / total * 0.7);
            }
        } // dest.Dispose() → ファイルハンドルを解放してから展開へ

        progress.Report(0.75);
        ZipFile.ExtractToDirectory(zipPath, unzipDir, overwriteFiles: true);
        progress.Report(0.85);

        var exePath = Environment.ProcessPath
                      ?? Path.Combine(AppContext.BaseDirectory, "Tottoto.exe");
        var exeDir  = Path.GetDirectoryName(exePath) ?? AppContext.BaseDirectory;
        var pid     = Environment.ProcessId;
        var batPath = Path.Combine(tempDir, "update.bat");

        var bat =
            "@echo off\r\n" +
            ":wait\r\n" +
            $"tasklist /FI \"PID eq {pid}\" 2>NUL | find \"{pid}\" >NUL\r\n" +
            "if not errorlevel 1 (\r\n" +
            "    timeout /t 1 /nobreak >NUL\r\n" +
            "    goto wait\r\n" +
            ")\r\n" +
            $"xcopy /E /Y /I \"{unzipDir}\\*\" \"{exeDir}\\\"\r\n" +
            $"start \"\" \"{exePath}\"\r\n" +
            "del \"%~f0\"\r\n";

        await File.WriteAllTextAsync(batPath, bat, ct);
        progress.Report(1.0);

        return batPath;
    }

    /// <summary>
    /// update.bat を起動してアプリを終了する（ユーザーが再起動を選択した場合に呼ぶ）。
    /// </summary>
    public static void LaunchAndRestart(string batPath)
    {
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
        {
            FileName        = batPath,
            UseShellExecute = true,
            WindowStyle     = System.Diagnostics.ProcessWindowStyle.Hidden,
        });
        Application.Current.Dispatcher.Invoke(Application.Current.Shutdown);
    }

    private sealed class ReleaseDto
    {
        [JsonPropertyName("tag_name")]  public string?       TagName { get; set; }
        [JsonPropertyName("body")]      public string?       Body    { get; set; }
        [JsonPropertyName("html_url")]  public string?       HtmlUrl { get; set; }
        [JsonPropertyName("assets")]    public List<AssetDto>? Assets { get; set; }
    }

    private sealed class AssetDto
    {
        [JsonPropertyName("browser_download_url")] public string? BrowserDownloadUrl { get; set; }
        [JsonPropertyName("name")]                 public string? Name               { get; set; }
    }
}

/// <summary>GitHub リリース情報（表示用）。</summary>
public sealed record ReleaseInfo(
    string  TagName,
    string  LatestVersion,
    string  HtmlUrl,
    string? DownloadUrl,
    bool    IsUpdateAvailable,
    IReadOnlyList<ReleaseEntry> Entries);

/// <summary>個別リリースのバージョン文字列とリリースノート。</summary>
public sealed record ReleaseEntry(string Version, string Notes);