using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using todochart.Models;

namespace todochart.Services;

public class ScheduleFileService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented          = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    // ── 保存（JSON形式） ──────────────────────────────────
    public void Save(string path, ScheduleItemBase root, bool autoSave,
                     DateTime? savedAt = null,
                     IssueTrackingSettings? issueSettings = null,
                     List<IssueCacheItem>? issueCache = null)
    {
        var dto = new FileDto
        {
            SavedAt    = (savedAt ?? DateTime.Now).ToString("yyyy/MM/dd HH:mm:ss"),
            AutoSave   = autoSave,
            SourceType = issueSettings is not null ? "IssueTracking" : null,
            IssueTrackingSettings = issueSettings,
            IssueCache = issueCache is { Count: > 0 } ? issueCache : null,
            Children   = SerializeChildren(root),
        };

        var json = JsonSerializer.Serialize(dto, JsonOptions);
        var enc  = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
        File.WriteAllText(path, json, enc);
    }

    private static List<ItemDto> SerializeChildren(ScheduleItemBase parent)
    {
        var list = new List<ItemDto>();
        foreach (var child in parent.Children)
            list.Add(SerializeNode(child));
        return list;
    }

    private static ItemDto SerializeNode(ScheduleItemBase item)
    {
        var dto = new ItemDto
        {
            Type           = item is ScheduleFolder ? "folder" : "todo",
            Name           = item.Name,
            Memo           = string.IsNullOrEmpty(item.Memo)           ? null : item.Memo,
            Link           = string.IsNullOrEmpty(item.Link)           ? null : item.Link,
            DateCountLevel = item.DateCountLevel != 0 ? item.DateCountLevel : null,
            BeginDate      = item.BeginDate?.ToString("yyyy-MM-dd"),
            EndDate        = item.EndDate?.ToString("yyyy-MM-dd"),
            Mark            = item.MarkLevel != 0 ? item.MarkLevel : (int?)null,
        };

        if (item is ScheduleFolder folder)
        {
            dto.IsExpanded = folder.IsExpanded ? null : false;  // true はデフォルトなので省略
        }
        else if (item is ScheduleToDo todo)
        {
            dto.Completed = todo.Completed ? true : null;       // false はデフォルトなので省略
            dto.IsWait    = todo.IsWait    ? true : null;       // false はデフォルトなので省略
            if (todo.Callouts.Count > 0)
                dto.Callouts = todo.Callouts.Select(c => new CalloutDto
                {
                    Id               = c.Id,
                    Text             = c.Text,
                    PositionMode     = c.PositionMode.ToString(),
                    AbsoluteDateTime = c.AbsoluteDateTime?.ToString("yyyy-MM-dd"),
                    OffsetDays       = c.OffsetDays != 0 ? c.OffsetDays : null,
                    Style            = c.Style.ToString(),
                    VisibilityMode   = c.VisibilityMode.ToString(),
                    CreatedAt        = c.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss"),
                    UpdatedAt        = c.UpdatedAt.ToString("yyyy-MM-dd HH:mm:ss"),
                }).ToList();

        }

        if (item.Children.Count > 0)
            dto.Children = SerializeChildren(item);

        return dto;
    }

    // ── 読み込み ─────────────────────────────────────────
    public (ScheduleItemBase root, bool autoSave) Load(string path)
    {
        var text = ReadAllTextAuto(path);
        return LoadJson(text, path);
    }

    // ── Issue Tracking 設定・キャッシュ付き読み込み ─────────────────────────
    public (ScheduleItemBase root, bool autoSave,
            IssueTrackingSettings? issueSettings,
            List<IssueCacheItem>? issueCache) LoadWithIssueTracking(string path)
    {
        var text = ReadAllTextAuto(path);
        var dto  = JsonSerializer.Deserialize<FileDto>(text, JsonOptions)
                   ?? throw new InvalidDataException("JSONのデシリアライズに失敗しました。");

        var root = new ScheduleFolder
        {
            Name = dto.IssueTrackingSettings?.DisplayName
                   ?? Path.GetFileNameWithoutExtension(path)
        };
        if (dto.Children is not null)
            DeserializeChildren(dto.Children, root);

        return (root, dto.AutoSave, dto.IssueTrackingSettings, dto.IssueCache);
    }

    // ── JSON 読み込み ────────────────────────────────────
    private static (ScheduleItemBase root, bool autoSave) LoadJson(string text, string path)
    {
        var dto  = JsonSerializer.Deserialize<FileDto>(text, JsonOptions)
                   ?? throw new InvalidDataException("JSONのデシリアライズに失敗しました。");

        var root = new ScheduleFolder
        {
            Name = dto.IssueTrackingSettings?.DisplayName
                   ?? Path.GetFileNameWithoutExtension(path)
        };
        if (dto.Children is not null)
            DeserializeChildren(dto.Children, root);

        return (root, dto.AutoSave);
    }

    private static void DeserializeChildren(List<ItemDto> dtos, ScheduleItemBase parent)
    {
        foreach (var dto in dtos)
        {
            ScheduleItemBase item;
            if (dto.Type == "folder")
            {
                item = new ScheduleFolder
                {
                    IsExpanded = dto.IsExpanded ?? true,
                };
            }
            else
            {
                item = new ScheduleToDo
                {
                    Completed = dto.Completed ?? false,
                    IsWait    = dto.IsWait    ?? false,
                };
            }

            item.Name           = dto.Name ?? string.Empty;
            item.Memo           = dto.Memo ?? string.Empty;
            item.Link           = dto.Link ?? string.Empty;
            item.DateCountLevel = dto.DateCountLevel ?? 0;
            item.MarkLevel      = dto.Mark ?? 0;
            item.BeginDate      = ParseJsonDate(dto.BeginDate);
            item.EndDate        = ParseJsonDate(dto.EndDate);
            item.Parent         = parent;
            parent.Children.Add(item);

            if (item is todochart.Models.ScheduleToDo todoItem && dto.Callouts is { Count: > 0 })
            {
                foreach (var cdto in dto.Callouts)
                {
                    var callout = new todochart.Models.Callout
                    {
                        Id             = cdto.Id ?? Guid.NewGuid().ToString(),
                        Text           = cdto.Text ?? string.Empty,
                        PositionMode   = Enum.TryParse<todochart.Models.CalloutPositionMode>(cdto.PositionMode, out var pm)
                                         ? pm : todochart.Models.CalloutPositionMode.EndDate,
                        AbsoluteDateTime = ParseJsonDate(cdto.AbsoluteDateTime),
                        OffsetDays     = cdto.OffsetDays ?? 0,
                        Style          = Enum.TryParse<todochart.Models.CalloutStyle>(cdto.Style, out var st)
                                         ? st : todochart.Models.CalloutStyle.BubbleLine,
                        VisibilityMode = Enum.TryParse<todochart.Models.CalloutVisibilityMode>(cdto.VisibilityMode, out var vm)
                                         ? vm : todochart.Models.CalloutVisibilityMode.AlwaysVisible,
                        CreatedAt      = DateTime.TryParse(cdto.CreatedAt, out var ca) ? ca : DateTime.Now,
                        UpdatedAt      = DateTime.TryParse(cdto.UpdatedAt, out var ua) ? ua : DateTime.Now,
                    };
                    todoItem.Callouts.Add(callout);
                }
            }

            if (dto.Children is { Count: > 0 })
                DeserializeChildren(dto.Children, item);
        }
    }

    private static DateTime? ParseJsonDate(string? s)
    {
        if (string.IsNullOrEmpty(s)) return null;
        return DateTime.TryParseExact(s, "yyyy-MM-dd",
                   System.Globalization.CultureInfo.InvariantCulture,
                   System.Globalization.DateTimeStyles.None, out var dt)
               ? dt : null;
    }

    // ── ユーティリティ ────────────────────────────────────
    private static string ReadAllTextAuto(string path)
        => File.ReadAllText(path, new UTF8Encoding(false));

    // ── JSON DTO ─────────────────────────────────────────
    private class FileDto
    {
        [JsonPropertyName("savedAt")]
        public string? SavedAt { get; set; }

        [JsonPropertyName("autoSave")]
        public bool AutoSave { get; set; }

        [JsonPropertyName("sourceType")]
        public string? SourceType { get; set; }

        [JsonPropertyName("issueTrackingSettings")]
        public IssueTrackingSettings? IssueTrackingSettings { get; set; }

        [JsonPropertyName("issueCache")]
        public List<IssueCacheItem>? IssueCache { get; set; }

        [JsonPropertyName("children")]
        public List<ItemDto>? Children { get; set; }
    }

    private class ItemDto
    {
        [JsonPropertyName("type")]
        public string? Type { get; set; }

        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("memo")]
        public string? Memo { get; set; }

        [JsonPropertyName("link")]
        public string? Link { get; set; }

        [JsonPropertyName("dateCountLevel")]
        public int? DateCountLevel { get; set; }

        [JsonPropertyName("beginDate")]
        public string? BeginDate { get; set; }

        [JsonPropertyName("endDate")]
        public string? EndDate { get; set; }

        [JsonPropertyName("mark")]
        public int? Mark { get; set; }

        [JsonPropertyName("isExpanded")]
        public bool? IsExpanded { get; set; }

        [JsonPropertyName("completed")]
        public bool? Completed { get; set; }

        [JsonPropertyName("isWait")]
        public bool? IsWait { get; set; }

        [JsonPropertyName("children")]
        public List<ItemDto>? Children { get; set; }

        [JsonPropertyName("callouts")]
        public List<CalloutDto>? Callouts { get; set; }
    }

    private class CalloutDto
    {
        [JsonPropertyName("id")]
        public string? Id { get; set; }

        [JsonPropertyName("text")]
        public string? Text { get; set; }

        [JsonPropertyName("positionMode")]
        public string? PositionMode { get; set; }

        [JsonPropertyName("absoluteDateTime")]
        public string? AbsoluteDateTime { get; set; }

        [JsonPropertyName("offsetDays")]
        public int? OffsetDays { get; set; }

        [JsonPropertyName("style")]
        public string? Style { get; set; }

        [JsonPropertyName("visibilityMode")]
        public string? VisibilityMode { get; set; }

        [JsonPropertyName("createdAt")]
        public string? CreatedAt { get; set; }

        [JsonPropertyName("updatedAt")]
        public string? UpdatedAt { get; set; }
    }
}
