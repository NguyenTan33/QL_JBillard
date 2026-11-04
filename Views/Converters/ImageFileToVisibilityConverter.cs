using System;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Data;

namespace QL_JBillard.Views.Converters;

/// <summary>
/// Converter kiểm tra xem file ảnh có thực sự tồn tại trên ổ cứng hay không.
/// Tránh trường hợp ImagePath lưu chuỗi nhưng file không tồn tại dẫn đến ô trắng trơn!
/// </summary>
public class ImageFileToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        bool fileExists = false;
        if (value is string path && !string.IsNullOrWhiteSpace(path))
        {
            try
            {
                fileExists = File.Exists(path);
            }
            catch
            {
                fileExists = false;
            }
        }

        if (parameter is string paramStr && paramStr.Equals("Inverse", StringComparison.OrdinalIgnoreCase))
        {
            return fileExists ? Visibility.Collapsed : Visibility.Visible;
        }

        return fileExists ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
