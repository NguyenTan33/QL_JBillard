using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace QL_JBillard.Views.Converters;

public class StringEqualsToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value == null || parameter == null) return Visibility.Collapsed;
        bool isEqual = string.Equals(value.ToString(), parameter.ToString(), StringComparison.OrdinalIgnoreCase);
        return isEqual ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
