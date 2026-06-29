using System.Windows;
using todochart.Services;

namespace todochart;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        // 起動時に保存済みテーマを適用（AppSettings はファイル読み込み）
        try
        {
            var settings = AppSettings.Load();
            if (ThemeService.IsValidTheme(settings.ThemeName))
                ThemeService.ApplyTheme(settings.ThemeName);
            else
                ThemeService.ApplyTheme("Light");
        }
        catch { /* テーマ適用失敗は無視 */ }
        // コマンドライン引数は MainViewModel が処理する
    }
}
