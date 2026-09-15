namespace QL_JBillard.Domain.Enums;

public enum PaymentMethod
{
    Cash = 1,          // Tiền mặt
    VietQr = 2,        // Quét mã VietQR chuyển khoản nhanh
    BankTransfer = 3,  // Chuyển khoản ngân hàng trực tiếp
    Points = 4,        // Khấu trừ điểm tích lũy
    Mixed = 5          // Kết hợp nhiều hình thức
}
