using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace todochart.Services;

/// <summary>
/// アプリ設定の保存・読み込み（JSON）。
/// 設定ファイルは実行ファイルと同じフォルダに「tottoto.json」として保存。
/// </summary>
public class AppSettings
{
    // ── ウィンドウ位置 ────────────────────────────────────
    public double WindowLeft   { get; set; } = 100;
    public double WindowTop    { get; set; } = 100;
    public double WindowWidth  { get; set; } = 1200;
    public double WindowHeight { get; set; } = 700;
    public bool   Maximized    { get; set; } = false;

    // ── ペイン幅 ─────────────────────────────────────────
    public double TaskPaneWidth { get; set; } = 440;

    // ── 列幅 ─────────────────────────────────────────────
    public double ColNameWidth  { get; set; } = 200;
    public double ColBeginWidth { get; set; } = 90;
    public double ColEndWidth   { get; set; } = 90;
    public double ColDaysWidth  { get; set; } = 55;

    // ── タスクリスト列幅 ──────────────────────────────────
    // TaskColNameWidth: 0 = Star（デフォルト）、> 0 = Pixel 固定
    public double TaskColNameWidth   { get; set; } = 0;
    public double TaskColStatusWidth { get; set; } = 80;
    public double TaskColMLWidth     { get; set; } = 36;

    // ── チャート ─────────────────────────────────────────
    public double CellWidth           { get; set; } = 20;
    public int    ChartOffsetFromToday { get; set; } = -7;   // 表示開始(今日から+n日)

    // ── 休日設定 ─────────────────────────────────────────
    // インデックス: 0=日 1=月 2=火 3=水 4=木 5=金 6=土
    public int[] WeekdayLevels { get; set; } = { 2, 0, 0, 0, 0, 0, 1 };
    public int   DateCountLevel { get; set; } = 0;
    public int   AlertCount     { get; set; } = 3;

    // ── 最後に開いたファイル ──────────────────────────────
    public string LastFile { get; set; } = string.Empty;

    // ── 複数ファイルの表示順序 ────────────────────────────
    public List<string> OpenFiles { get; set; } = new();

    // ── 自動保存 ─────────────────────────────────────────
    public bool AutoSave { get; set; } = false;

    // ── 完了タスク非表示 ─────────────────────────────────
    public bool HideCompleted { get; set; } = false;

    // ─────────────────────────────────────────────────────
    private static string SettingsPath =>
        Path.Combine(AppContext.BaseDirectory, "tottoto.json");

    private static readonly JsonSerializerOptions s_opts =
        new() { WriteIndented = true, DefaultIgnoreCondition = JsonIgnoreCondition.Never };

    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(SettingsPath))
            {
                var json = File.ReadAllText(SettingsPath);
                return JsonSerializer.Deserialize<AppSettings>(json, s_opts) ?? new AppSettings();
            }
        }
        catch { /* 読み込み失敗時はデフォルト値で起動 */ }
        return new AppSettings();
    }

    public void Save()
    {
        try
        {
            File.WriteAllText(SettingsPath, JsonSerializer.Serialize(this, s_opts));
        }
        catch { /* 保存失敗は無視 */ }
    }
}
