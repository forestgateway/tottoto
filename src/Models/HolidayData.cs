namespace todochart.Models;

/// <summary>
/// 休日データを表すレコード。
/// 個別指定の休日（祝日・任意休日）に使用。
/// </summary>
/// <param name="Date">休日の日付</param>
/// <param name="Name">休日の名称</param>
/// <param name="Level">休日レベル（0=平日、1=半休日、2=全休日）</param>
public sealed record HolidayData(DateOnly Date, string Name, int Level)
{
    public string LevelLabel => Level switch
    {
        0 => "平日",
        1 => "半休日(土曜など)",
        2 => "全休日(日・祝など)",
        _ => Level.ToString(),
    };
}
