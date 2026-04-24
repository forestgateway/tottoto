using System.Windows;
using todochart.Services;

namespace todochart;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        // コマンドライン引数は MainViewModel が処理する
    }
}
