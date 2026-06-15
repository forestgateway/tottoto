using System.Windows;
using todochart.Services;

namespace todochart;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        // 保存済みテーマを起動時に適用（ファイル I/O は AppSettings.Load() 1回のみ）
        var settings = AppSettings.Load();
        ThemeService.ApplyTheme(settings.ThemeName);
        // コマンドライン引数は MainViewModel が処理する
    }
}
