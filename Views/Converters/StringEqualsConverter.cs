using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace QL_JBillard.Views.Converters;

public class StringEqualsConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        bool isEqual = false;
        if (value != null && parameter != null)
        {
            isEqual = string.Equals(value.ToString(), parameter.ToString(), StringComparison.OrdinalIgnoreCase);
        }

        if (targetType == typeof(Visibility))
        {
            return isEqual ? Visibility.Visible : Visibility.Collapsed;
        }

        return isEqual;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool b && b && parameter != null)
        {
            return parameter.ToString()!;
        }
        return Binding.DoNothing;
    }
}
