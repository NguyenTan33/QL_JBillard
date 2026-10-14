using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using QL_JBillard.Domain.Enums;

namespace QL_JBillard.Views.Converters;

public class StatusToColorConverter : IValueConverter
{
    private static readonly SolidColorBrush AvailableBrush = new((Color)ColorConverter.ConvertFromString("#10B981"));   // Xanh ngọc Emerald
    private static readonly SolidColorBrush OccupiedBrush = new((Color)ColorConverter.ConvertFromString("#EF4444"));    // Đỏ Crimson
    private static readonly SolidColorBrush ReservedBrush = new((Color)ColorConverter.ConvertFromString("#F59E0B"));    // Vàng cam Amber
    private static readonly SolidColorBrush MaintenanceBrush = new((Color)ColorConverter.ConvertFromString("#64748B")); // Xám Slate

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is TableStatus status)
        {
            return status switch
            {
                TableStatus.Available => AvailableBrush,
                TableStatus.Occupied => OccupiedBrush,
                TableStatus.Reserved => ReservedBrush,
                TableStatus.Maintenance => MaintenanceBrush,
                _ => MaintenanceBrush
            };
        }
        return AvailableBrush;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
