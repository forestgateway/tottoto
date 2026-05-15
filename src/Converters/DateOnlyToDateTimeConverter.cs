using System.Globalization;
using System.Windows.Data;

namespace todochart.Converters;

/// <summary>
/// DateOnly ⇔ DateTime? の相互変換を行うコンバーター。
/// DatePicker のバインディングに使用。
/// </summary>
public class DateOnlyToDateTimeConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is DateOnly dateOnly)
        {
            return dateOnly.ToDateTime(TimeOnly.MinValue);
        }
        return null;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is DateTime dateTime)
        {
            return DateOnly.FromDateTime(dateTime);
        }
        return DateOnly.FromDateTime(DateTime.Today);
    }
}
