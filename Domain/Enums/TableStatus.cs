namespace QL_JBillard.Domain.Enums;

public enum TableStatus
{
    Available = 0,    // Bàn trống, sẵn sàng đón khách
    Occupied = 1,     // Đang có khách chơi
    Reserved = 2,     // Đã đặt trước
    Maintenance = 3   // Đang bảo trì / lau bi / thay vải
}
