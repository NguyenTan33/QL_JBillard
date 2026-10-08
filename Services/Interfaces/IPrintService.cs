using System.Windows.Documents;
using QL_JBillard.Domain.Entities;

namespace QL_JBillard.Services.Interfaces;

public interface IPrintService
{
    /// <summary>
    /// Sinh mảng byte lệnh ESC/POS chuẩn cho máy in hóa đơn nhiệt K80/K58
    /// </summary>
    byte[] GenerateEscPosBytes(Invoice invoice, bool kickCashDrawer = true, bool cutPaper = true);

    /// <summary>
    /// Tạo tài liệu FlowDocument để xem trước (Print Preview) hoặc in máy in văn phòng
    /// </summary>
    FlowDocument GenerateReceiptFlowDocument(Invoice invoice);

    /// <summary>
    /// In trực tiếp ra máy in qua tên máy in Windows
    /// </summary>
    bool PrintReceipt(Invoice invoice, string? printerName = null);
}
