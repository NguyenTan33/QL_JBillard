using System.Globalization;
using System.Windows.Data;

namespace QL_JBillard.Views.Converters;

public class CurrencyConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is decimal d)
        {
            return $"{d:N0} đ";
        }
        if (value is double db)
        {
            return $"{db:N0} đ";
        }
        return "0 đ";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is string str)
        {
            if (string.IsNullOrWhiteSpace(str)) return 0m;
            // Loại bỏ tất cả ký tự không phải số (như 'đ', khoảng trắng, dấu chấm, phẩy)
            string clean = System.Text.RegularExpressions.Regex.Replace(str, @"[^\d]", "");
            if (decimal.TryParse(clean, out decimal result))
            {
                return result;
            }
        }
        if (value is decimal d) return d;
        return 0m;
    }
}
