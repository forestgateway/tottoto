using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace todochart.Converters;

/// <summary>
/// bool? 値をテスト結果色に変換するコンバーター。
/// true  → 青 (#FF0055CC)
/// false → 赤 (#FFCC0000)
/// null  → 透明
/// </summary>
public class NullableBoolToTestColorConverter : IValueConverter
{
    private static readonly Brush s_success = new SolidColorBrush(Color.FromRgb(0x00, 0x55, 0xCC));
    private static readonly Brush s_error   = new SolidColorBrush(Color.FromRgb(0xCC, 0x00, 0x00));
    private static readonly Brush s_none    = Brushes.Transparent;

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool b)
            return b ? s_success : s_error;
        return s_none;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
