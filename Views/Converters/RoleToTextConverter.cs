using System.Globalization;
using System.Windows.Data;
using QL_JBillard.Domain.Enums;

namespace QL_JBillard.Views.Converters;

public class RoleToTextConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is UserRole role)
        {
            return role switch
            {
                UserRole.Admin => "Quản Trị Viên (Admin)",
                UserRole.Manager => "Quản Lý Quán",
                UserRole.Cashier => "Thu Ngân",
                UserRole.Staff => "Nhân Viên Phục Vụ",
                _ => role.ToString()
            };
        }
        return "Nhân Viên";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
