using System.Globalization;
using System.Windows.Data;
using QL_JBillard.Domain.Enums;

namespace QL_JBillard.Views.Converters;

public class PaymentMethodToStringConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is PaymentMethod method)
        {
            return method switch
            {
                PaymentMethod.Cash => "Tiền Mặt",
                PaymentMethod.VietQr => "Chuyển Khoản VietQR",
                PaymentMethod.BankTransfer => "Chuyển Khoản Ngân Hàng",
                PaymentMethod.Points => "Điểm Tích Lũy",
                PaymentMethod.Mixed => "Kết Hợp",
                _ => method.ToString()
            };
        }
        return "Tiền Mặt";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
