using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using todochart.Models;

namespace todochart.Services;

/// <summary>
/// アーカイブファイル（*.archive）の読み書きを行うサービス。
/// アーカイブファイルはフラットな JSON 配列で保持する。
/// </summary>
public class ArchiveService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented          = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    /// <summary>管理ファイルのパスからアーカイブファイルのパスを導出する。</summary>
    public static string GetArchivePath(string scheduleFilePath)
        => scheduleFilePath + ".archive";

    /// <summary>アーカイブファイルを読み込む。ファイルが無ければ空リストを返す。</summary>
    public List<ArchivedItem> Load(string archivePath)
    {
        if (!File.Exists(archivePath))
            return new List<ArchivedItem>();

        var text = File.ReadAllText(archivePath, new UTF8Encoding(false));
        var dtos = JsonSerializer.Deserialize<List<ArchivedItemDto>>(text, JsonOptions);
        if (dtos is null) return new List<ArchivedItem>();

        return dtos.Select(DtoToModel).ToList();
    }

    /// <summary>アーカイブファイルへ保存する。</summary>
    public void Save(string archivePath, List<ArchivedItem> items)
    {
        var dtos = items.Select(ModelToDto).ToList();
        var json = JsonSerializer.Serialize(dtos, JsonOptions);
        var enc  = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
        File.WriteAllText(archivePath, json, enc);
    }

    /// <summary>
    /// タスクアイテムをアーカイブ用データに変換する。
    /// パスは親を辿って構築する。
    /// </summary>
    public static ArchivedItem ToArchived(ScheduleItemBase item)
    {
        return new ArchivedItem
        {
            Name           = item.Name,
            Path           = BuildTreePath(item),
            Memo           = item.Memo,
            Link           = item.Link,
            BeginDate      = item.BeginDate,
            EndDate        = item.EndDate,
            DateCountLevel = item.DateCountLevel,
            Completed      = item is ScheduleToDo todo ? todo.Completed : false,
            IsWait         = item.IsWait,
            MarkLevel      = item.MarkLevel,
            ArchivedAt     = DateTime.Now,
        };
    }

    /// <summary>
    /// アーカイブ済みアイテムを ScheduleToDo に復元する。
    /// </summary>
    public static ScheduleToDo ToScheduleItem(ArchivedItem archived)
    {
        return new ScheduleToDo
        {
            Name           = archived.Name,
            Memo           = archived.Memo,
            Link           = archived.Link,
            BeginDate      = archived.BeginDate,
            EndDate        = archived.EndDate,
            DateCountLevel = archived.DateCountLevel,
            Completed      = archived.Completed,
            IsWait         = archived.IsWait,
            MarkLevel      = archived.MarkLevel,
        };
    }

    /// <summary>親を辿ってツリーパスを構築する。</summary>
    private static string BuildTreePath(ScheduleItemBase item)
    {
        var parts = new List<string>();
        var current = item.Parent;
        while (current is not null)
        {
            parts.Add(current.Name);
            current = current.Parent;
        }
        parts.Reverse();
        return string.Join("/", parts);
    }

    /// <summary>
    /// パス文字列をもとに既存ツリー内のフォルダを検索し、
    /// 見つからなければフォルダを作成して返す。
    /// </summary>
    public static ScheduleFolder ResolveOrCreatePath(ScheduleFolder root, string path)
    {
        if (string.IsNullOrEmpty(path))
            return root;

        var segments = path.Split('/');
        ScheduleFolder current = root;

        // segments[0] がルート名と一致すればスキップ
        int start = (segments.Length > 0 && segments[0] == root.Name) ? 1 : 0;

        for (int i = start; i < segments.Length; i++)
        {
            var seg = segments[i];
            if (string.IsNullOrEmpty(seg)) continue;

            var child = current.Children
                .OfType<ScheduleFolder>()
                .FirstOrDefault(f => f.Name == seg);

            if (child is null)
            {
                child = new ScheduleFolder { Name = seg, IsExpanded = true, Parent = current };
                current.Children.Add(child);
            }

            current = child;
        }

        return current;
    }

    // ── DTO ──────────────────────────────────────────────
    private static ArchivedItem DtoToModel(ArchivedItemDto dto) => new()
    {
        Name           = dto.Name ?? string.Empty,
        Path           = dto.Path ?? string.Empty,
        Memo           = dto.Memo ?? string.Empty,
        Link           = dto.Link ?? string.Empty,
        BeginDate      = ParseDate(dto.BeginDate),
        EndDate        = ParseDate(dto.EndDate),
        DateCountLevel = dto.DateCountLevel ?? 0,
        Completed      = dto.Completed ?? false,
        IsWait         = dto.IsWait ?? false,
        MarkLevel      = dto.Mark ?? 0,
        ArchivedAt     = dto.ArchivedAt is not null
                            ? DateTime.TryParse(dto.ArchivedAt, out var dt) ? dt : DateTime.Now
                            : DateTime.Now,
    };

    private static ArchivedItemDto ModelToDto(ArchivedItem m) => new()
    {
        Name           = m.Name,
        Path           = m.Path,
        Memo           = string.IsNullOrEmpty(m.Memo)   ? null : m.Memo,
        Link           = string.IsNullOrEmpty(m.Link)   ? null : m.Link,
        BeginDate      = m.BeginDate?.ToString("yyyy-MM-dd"),
        EndDate        = m.EndDate?.ToString("yyyy-MM-dd"),
        DateCountLevel = m.DateCountLevel != 0 ? m.DateCountLevel : null,
        Completed      = m.Completed ? true : null,
        IsWait         = m.IsWait ? true : null,
        Mark           = m.MarkLevel != 0 ? m.MarkLevel : null,
        ArchivedAt     = m.ArchivedAt.ToString("yyyy-MM-ddTHH:mm:ss"),
    };

    private static DateTime? ParseDate(string? s)
    {
        if (string.IsNullOrEmpty(s)) return null;
        return DateTime.TryParseExact(s, "yyyy-MM-dd",
                   System.Globalization.CultureInfo.InvariantCulture,
                   System.Globalization.DateTimeStyles.None, out var dt)
               ? dt : null;
    }

    private class ArchivedItemDto
    {
        [JsonPropertyName("name")]       public string? Name           { get; set; }
        [JsonPropertyName("path")]       public string? Path           { get; set; }
        [JsonPropertyName("memo")]       public string? Memo           { get; set; }
        [JsonPropertyName("link")]       public string? Link           { get; set; }
        [JsonPropertyName("beginDate")]  public string? BeginDate      { get; set; }
        [JsonPropertyName("endDate")]    public string? EndDate        { get; set; }
        [JsonPropertyName("dateCountLevel")] public int? DateCountLevel { get; set; }
        [JsonPropertyName("completed")]  public bool?   Completed      { get; set; }
        [JsonPropertyName("isWait")]     public bool?   IsWait         { get; set; }
        [JsonPropertyName("mark")]       public int?    Mark           { get; set; }
        [JsonPropertyName("archivedAt")] public string? ArchivedAt     { get; set; }
    }
}
