using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace todochart.Converters;

/// <summary>bool=true のとき Collapsed を返すコンバーター</summary>
public class InverseBoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is true ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
