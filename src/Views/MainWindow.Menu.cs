using System.Windows;

namespace todochart.Views;

/// <summary>メニュー操作・ウィンドウ終了処理。</summary>
public partial class MainWindow
{
    private void OnExitClick(object sender, RoutedEventArgs e) => Close();

    private void OnCheckUpdateClick(object sender, RoutedEventArgs e)
    {
        var vm  = new ViewModels.UpdateCheckViewModel();
        var win = new UpdateCheckWindow(vm) { Owner = this };
        win.ShowDialog();
    }

    private void OnAboutClick(object sender, RoutedEventArgs e)
    {
        var asm     = System.Reflection.Assembly.GetExecutingAssembly();
        var version = asm.GetName().Version?.ToString(3) ?? "1.0.0";
        var copy    = System.Diagnostics.FileVersionInfo.GetVersionInfo(asm.Location).LegalCopyright
                      ?? "Copyright (c) 2026 Toyoshige Kido";

        MessageBox.Show(
            $"Tottoto  ver {version}\n\n{copy}\n\nThis software uses .NET 8 / WPF\nCopyright (c) Microsoft Corporation\nLicensed under the MIT License.",
            "バージョン情報",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }

    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        foreach (var entry in Vm.Schedules.ToList())
        {
            if (!Vm.ConfirmDiscard(entry))
            {
                e.Cancel = true;
                return;
            }
        }

        Vm.SaveSettings();
        base.OnClosing(e);
    }
}
