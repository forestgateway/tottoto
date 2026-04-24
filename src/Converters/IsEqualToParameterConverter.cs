using System.Globalization;
using System.Globalization;
using System.Windows.Data;

namespace todochart.Converters;

/// <summary>value ‚Æ ConverterParameter ‚ª“™‚µ‚¯‚ê‚Î true ‚ğ•Ô‚· IValueConverterB</summary>
public class IsEqualToParameterConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => Equals(value?.ToString(), parameter?.ToString());

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
