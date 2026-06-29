using System;
using System.Globalization;
using System.Windows.Data;
using todochart.Models;

namespace todochart.Converters
{
    /// <summary>
    /// Item が ScheduleToDo の場合に Completed を返すコンバータ。
    /// ScheduleFolder のように Completed プロパティを持たない場合は false を返す。
    /// </summary>
    public class ItemCompletedConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is ScheduleToDo td) return td.Completed;
            return false;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return Binding.DoNothing;
        }
    }
}
