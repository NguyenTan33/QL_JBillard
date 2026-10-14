using System;
using System.Collections;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace QL_JBillard.Views.Converters;

/// <summary>
/// Converter thông minh: tự động chuyển đổi bool, string (rỗng/không rỗng), số (> 0), collection (> 0), object (null/khác null)
/// sang Visibility.Visible hoặc Visibility.Collapsed. Hỗ trợ tham số "Inverse" để đảo ngược.
/// </summary>
public class SmartVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        bool isVisible = false;

        if (value is bool b)
        {
            isVisible = b;
        }
        else if (value is string s)
        {
            isVisible = !string.IsNullOrWhiteSpace(s);
        }
        else if (value is int i)
        {
            isVisible = i > 0;
        }
        else if (value is long l)
        {
            isVisible = l > 0;
        }
        else if (value is double d)
        {
            isVisible = d > 0;
        }
        else if (value is decimal dec)
        {
            isVisible = dec > 0;
        }
        else if (value is ICollection coll)
        {
            isVisible = coll.Count > 0;
        }
        else if (value != null)
        {
            isVisible = true;
        }

        if (parameter is string paramStr && paramStr.Equals("Inverse", StringComparison.OrdinalIgnoreCase))
        {
            isVisible = !isVisible;
        }

        return isVisible ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
