namespace QL_JBillard.Domain.Enums;

public enum SessionStatus
{
    Active = 0,     // Phiên đang diễn ra
    Completed = 1,  // Đã thanh toán kết thúc
    Cancelled = 2   // Hủy phiên
}
