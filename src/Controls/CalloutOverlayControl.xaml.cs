using System.Globalization;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using todochart.ViewModels;

namespace todochart.Controls;

/// <summary>
/// double 値を Left だけ設定した Thickness に変換するコンバーター。
/// Polygon の Margin.Left を TailOffsetX にバインドするために使用。
/// </summary>
[ValueConversion(typeof(double), typeof(Thickness))]
public class LeftMarginConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is double d ? new Thickness(d, 0, 0, 0) : new Thickness(0);

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

public partial class CalloutOverlayControl : UserControl
{
    public CalloutOverlayControl()
    {
        InitializeComponent();
        Loaded += (_, _) => EnsureTransform();
    }

    // ── スクロールオフセット同期（チャート全体オーバーレイ用） ────────────

    private readonly TranslateTransform _translate = new();

    /// <summary>
    /// チャート本体のスクロールオフセットを設定する。
    /// </summary>
    public void SetScrollOffset(double x, double y)
    {
        _translate.X = -x;
        _translate.Y = -y;
    }

    /// <summary>
    /// オーバーレイ Canvas のサイズをスクロールコンテンツ全体に合わせる。
    /// Canvas は子要素サイズを測定に使わないため、行数変化時や初期化時に手動で設定する。
    /// </summary>
    public void UpdateCanvasSize(double width, double height)
    {
        OverlayCanvas.Width  = width;
        OverlayCanvas.Height = height;
    }

    private void EnsureTransform()
    {
        if (OverlayCanvas.RenderTransform != _translate)
            OverlayCanvas.RenderTransform = _translate;
    }

    // ── 吹き出し ダブルクリック編集 ────────────────────────────

    // ── ドラッグ状態 ────────────────────────────────────────────
    private CalloutViewModel? _dragVm;
    private FrameworkElement? _dragElement;
    private Point             _dragStartPos;
    private int               _dragAppliedDays;
    private bool              _isDragging;

    private void OnCalloutMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not FrameworkElement fe) return;
        if (fe.DataContext is not CalloutViewModel vm) return;
        if (e.ChangedButton != MouseButton.Left) return;

        if (e.ClickCount == 2 && !vm.IsEditing)
        {
            // ダブルクリック → 編集開始（ドラッグはキャンセル）
            CancelDrag(fe);
            vm.BeginEditCommand.Execute(null);
            e.Handled = true;
            return;
        }

        if (!vm.IsEditing)
        {
            _dragVm          = vm;
            _dragElement     = fe;
            _dragStartPos    = e.GetPosition(OverlayCanvas);
            _dragAppliedDays = 0;
            _isDragging      = false;
            fe.CaptureMouse();
        }
    }

    private void OnCalloutMouseMove(object sender, MouseEventArgs e)
    {
        if (_dragVm is null || _dragElement is null) return;
        if (e.LeftButton != MouseButtonState.Pressed) { CancelDrag(_dragElement); return; }

        var pos    = e.GetPosition(OverlayCanvas);
        var deltaX = pos.X - _dragStartPos.X;

        if (!_isDragging)
        {
            if (Math.Abs(deltaX) < SystemParameters.MinimumHorizontalDragDistance) return;
            _isDragging = true;
            _dragElement.Cursor = Cursors.SizeWE;
        }

        int targetDays     = (int)Math.Round(deltaX / CalloutViewModel.CellWidth);
        int additionalDays = targetDays - _dragAppliedDays;
        if (additionalDays != 0)
        {
            _dragVm.MoveByDays(additionalDays);
            _dragAppliedDays = targetDays;
        }
        e.Handled = true;
    }

    private void OnCalloutMouseUp(object sender, MouseButtonEventArgs e)
    {
        bool wasDragging = _isDragging;
        if (_dragElement is not null)
            CancelDrag(_dragElement);
        if (wasDragging)
            e.Handled = true;
    }

    private void CancelDrag(FrameworkElement? fe)
    {
        if (fe is not null)
        {
            if (fe.IsMouseCaptured) fe.ReleaseMouseCapture();
            fe.Cursor = null;
        }
        _dragVm      = null;
        _dragElement = null;
        _isDragging  = false;
    }

    // ── 吹き出しテキストボックス ──────────────────────────────────────────

    private void OnCalloutTextBoxVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (sender is TextBox tb && tb.IsVisible)
        {
            tb.Dispatcher.BeginInvoke(() =>
            {
                tb.Focus();
                tb.SelectAll();
            });
        }
    }

    private void OnCalloutTextBoxKeyDown(object sender, KeyEventArgs e)
    {
        if (sender is not TextBox tb) return;
        if (tb.DataContext is not CalloutViewModel vm) return;

        if (e.Key == Key.Return && (Keyboard.Modifiers & ModifierKeys.Shift) == 0)
        {
            vm.CommitEditCommand.Execute(null);
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            vm.CancelEditCommand.Execute(null);
            e.Handled = true;
        }
    }

    private void OnCalloutTextBoxLostFocus(object sender, RoutedEventArgs e)
    {
        if (sender is TextBox tb && tb.DataContext is CalloutViewModel vm)
            vm.CommitEditCommand.Execute(null);
    }
}
