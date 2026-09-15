namespace QL_JBillard.Domain.Enums;

public enum UserRole
{
    Admin = 1,    // Toàn quyền hệ thống
    Manager = 2,  // Quản lý kho, giá, khuyến mãi, hủy món
    Cashier = 3,  // Thu ngân mở bàn, chốt đơn, in bill, nhận ca
    Staff = 4     // Nhân viên phục vụ xem bàn, order món
}
