using System.Globalization;
using System.Windows.Data;
using QL_JBillard.Domain.Enums;

namespace QL_JBillard.Views.Converters;

public class StatusToTextConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is TableStatus status)
        {
            return status switch
            {
                TableStatus.Available => "TRỐNG",
                TableStatus.Occupied => "ĐANG CHƠI",
                TableStatus.Reserved => "ĐÃ ĐẶT",
                TableStatus.Maintenance => "BẢO TRÌ",
                _ => "KHÔNG XÁC ĐỊNH"
            };
        }
        return "TRỐNG";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
