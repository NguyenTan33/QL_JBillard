namespace QL_JBillard.Domain.Enums;

public enum StockTransactionType
{
    Import = 1,       // Nhập hàng từ nhà cung cấp
    ExportSale = 2,   // Xuất bán cho khách tại bàn
    Spoilage = 3,     // Xuất hủy do hỏng/hết hạn/vỡ
    Adjustment = 4    // Điều chỉnh sau kiểm kê kho
}
