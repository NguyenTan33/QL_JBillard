using System.Drawing.Printing;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using Microsoft.EntityFrameworkCore;
using QL_JBillard.Data;
using QL_JBillard.Domain.Entities;
using QL_JBillard.Services.Interfaces;

namespace QL_JBillard.Services.Implementations;

public class PrintService : IPrintService
{
    private readonly IDbContextFactory<AppDbContext> _contextFactory;

    public PrintService(IDbContextFactory<AppDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    private static string ToPrinterText(string text)
    {
        if (string.IsNullOrEmpty(text)) return string.Empty;
        var normalizedString = text.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder();
        foreach (var c in normalizedString)
        {
            var category = System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c);
            if (category != System.Globalization.UnicodeCategory.NonSpacingMark)
            {
                sb.Append(c);
            }
        }
        return sb.ToString().Normalize(NormalizationForm.FormC).Replace("đ", "d").Replace("Đ", "D");
    }

    private static byte[] ToPrinterBytes(string text)
    {
        return Encoding.ASCII.GetBytes(ToPrinterText(text));
    }

    public byte[] GenerateEscPosBytes(Invoice invoice, bool kickCashDrawer = true, bool cutPaper = true)
    {
        using var ms = new MemoryStream();
        using var writer = new BinaryWriter(ms, Encoding.UTF8);

        // 1. Khởi tạo máy in (ESC @)
        writer.Write(new byte[] { 0x1B, 0x40 });

        // 2. Kích mở ngăn kéo đựng tiền (ESC p 0 25 250)
        if (kickCashDrawer)
        {
            writer.Write(new byte[] { 0x1B, 0x70, 0x00, 0x19, 0xFA });
        }

        // Lấy thông tin quán từ cài đặt
        using var context = _contextFactory.CreateDbContext();
        string clubName = context.SystemSettings.Find("ClubName")?.SettingValue ?? "J-BILLIARDS CLUB";
        string clubAddress = context.SystemSettings.Find("ClubAddress")?.SettingValue ?? "";
        string clubPhone = context.SystemSettings.Find("ClubPhone")?.SettingValue ?? "";

        // 3. Header: Căn giữa, in đậm tên quán
        writer.Write(new byte[] { 0x1B, 0x61, 0x01 }); // Căn giữa
        writer.Write(new byte[] { 0x1B, 0x45, 0x01 }); // Bật In đậm
        writer.Write(new byte[] { 0x1D, 0x21, 0x11 }); // Double width & height
        writer.Write(ToPrinterBytes($"{clubName}\n"));

        writer.Write(new byte[] { 0x1D, 0x21, 0x00 }); // Normal size
        writer.Write(new byte[] { 0x1B, 0x45, 0x00 }); // Tắt in đậm
        if (!string.IsNullOrEmpty(clubAddress)) writer.Write(ToPrinterBytes($"DC: {clubAddress}\n"));
        if (!string.IsNullOrEmpty(clubPhone)) writer.Write(ToPrinterBytes($"Hotline: {clubPhone}\n"));

        writer.Write(ToPrinterBytes("------------------------------------------\n"));
        writer.Write(new byte[] { 0x1B, 0x45, 0x01 });
        writer.Write(ToPrinterBytes("HOA DON THANH TOAN\n"));
        writer.Write(new byte[] { 0x1B, 0x45, 0x00 });
        writer.Write(ToPrinterBytes($"So HD: {invoice.InvoiceCode}\n"));
        writer.Write(ToPrinterBytes($"Ngay: {invoice.CreatedAt:dd/MM/yyyy HH:mm:ss}\n"));
        writer.Write(ToPrinterBytes($"Ban: {invoice.Session?.Table?.TableNumber ?? ""} | Thu ngan: {invoice.CashierUser?.FullName ?? ""}\n"));

        // Thông tin giờ chơi
        if (invoice.Session != null)
        {
            if (!string.IsNullOrEmpty(invoice.AppliedComboName ?? invoice.Session.ComboName))
            {
                writer.Write(ToPrinterBytes($"Goi Combo: {invoice.AppliedComboName ?? invoice.Session.ComboName}\n"));
            }

            var segments = invoice.Session.GetSegments();
            if (segments.Count > 1)
            {
                writer.Write(ToPrinterBytes("Chi tiet tien gio theo ban:\n"));
                foreach (var seg in segments)
                {
                    string segLine = $"- {ToPrinterText(seg.TableNumber)} ({seg.HourlyRate / 1000:N0}k/h): {seg.PlayMinutes}p = {seg.Cost:N0}d\n";
                    writer.Write(ToPrinterBytes(segLine));
                }
            }
            else
            {
                writer.Write(ToPrinterBytes($"Gio vao: {invoice.Session.StartTime:HH:mm} - Gio ra: {invoice.Session.EndTime:HH:mm}\n"));
            }
            writer.Write(ToPrinterBytes($"Thoi gian choi: {invoice.TotalPlayMinutes} phut | Tien gio: {invoice.TimeAmount:N0}d\n"));
        }

        writer.Write(ToPrinterBytes("------------------------------------------\n"));

        // 4. Danh sách món F&B
        writer.Write(new byte[] { 0x1B, 0x61, 0x00 }); // Căn trái
        writer.Write(ToPrinterBytes("Ten Mon            SL   Don Gia    T.Tien\n"));

        if (invoice.Session?.OrderItems != null)
        {
            foreach (var item in invoice.Session.OrderItems.Where(x => !x.IsCancelled))
            {
                string name = ToPrinterText(item.Product.ProductName);
                if (name.Length > 17) name = name.Substring(0, 17);
                name = name.PadRight(18);
                string qty = item.Quantity.ToString().PadLeft(3);
                string price = $"{item.UnitPrice / 1000:N0}k".PadLeft(9);
                string total = $"{item.TotalAmount:N0}d".PadLeft(11);

                writer.Write(Encoding.ASCII.GetBytes($"{name}{qty}{price}{total}\n"));
            }
        }

        writer.Write(ToPrinterBytes("------------------------------------------\n"));

        // 5. Tổng kết thanh toán
        writer.Write(new byte[] { 0x1B, 0x61, 0x02 }); // Căn phải
        writer.Write(ToPrinterBytes($"Tien gio: {invoice.TimeAmount:N0} d\n"));
        writer.Write(ToPrinterBytes($"Tien dich vu: {invoice.ServiceAmount:N0} d\n"));

        if (invoice.DiscountAmount > 0)
        {
            writer.Write(ToPrinterBytes($"Giam gia ({invoice.DiscountPercent}%): -{invoice.DiscountAmount:N0} d\n"));
        }

        if (invoice.PointsDiscountAmount > 0)
        {
            writer.Write(ToPrinterBytes($"Tru diem ({invoice.PointsUsed} diem): -{invoice.PointsDiscountAmount:N0} d\n"));
        }

        writer.Write(new byte[] { 0x1B, 0x45, 0x01 });
        writer.Write(new byte[] { 0x1D, 0x21, 0x01 }); // Double height
        writer.Write(ToPrinterBytes($"TONG CONG: {invoice.FinalAmount:N0} d\n"));
        writer.Write(new byte[] { 0x1D, 0x21, 0x00 });
        writer.Write(new byte[] { 0x1B, 0x45, 0x00 });

        if (invoice.PointsEarned > 0)
        {
            writer.Write(ToPrinterBytes($"Diem tich luy duoc: +{invoice.PointsEarned} diem\n"));
        }

        // 6. Footer
        writer.Write(new byte[] { 0x1B, 0x61, 0x01 }); // Căn giữa
        writer.Write(ToPrinterBytes("\nCam on quy khach - Hen gap lai!\n\n\n\n"));

        // 7. Cắt giấy (GS V 66 0)
        if (cutPaper)
        {
            writer.Write(new byte[] { 0x1D, 0x56, 0x42, 0x00 });
        }

        return ms.ToArray();
    }

    public FlowDocument GenerateReceiptFlowDocument(Invoice invoice)
    {
        var doc = new FlowDocument
        {
            PageWidth = 320, // K80 chuẩn xấp xỉ 300-320px
            PagePadding = new Thickness(10),
            FontFamily = new FontFamily("Segoe UI"),
            FontSize = 12
        };

        using var context = _contextFactory.CreateDbContext();
        string clubName = context.SystemSettings.Find("ClubName")?.SettingValue ?? "J-BILLIARDS CLUB";
        string clubAddress = context.SystemSettings.Find("ClubAddress")?.SettingValue ?? "";
        string clubPhone = context.SystemSettings.Find("ClubPhone")?.SettingValue ?? "";

        // Header
        var pHeader = new Paragraph
        {
            TextAlignment = TextAlignment.Center,
            Margin = new Thickness(0, 0, 0, 5)
        };
        pHeader.Inlines.Add(new Bold(new Run(clubName)) { FontSize = 16 });
        pHeader.Inlines.Add(new LineBreak());
        pHeader.Inlines.Add(new Run(clubAddress));
        pHeader.Inlines.Add(new LineBreak());
        pHeader.Inlines.Add(new Run($"Hotline: {clubPhone}"));
        pHeader.Inlines.Add(new LineBreak());
        pHeader.Inlines.Add(new Bold(new Run("------------------------------------")));
        pHeader.Inlines.Add(new LineBreak());
        pHeader.Inlines.Add(new Bold(new Run("HÓA ĐƠN THANH TOÁN")) { FontSize = 14 });
        doc.Blocks.Add(pHeader);

        // Info
        var pInfo = new Paragraph { Margin = new Thickness(0, 0, 0, 5) };
        pInfo.Inlines.Add(new Run($"Số HĐ: {invoice.InvoiceCode}"));
        pInfo.Inlines.Add(new LineBreak());
        pInfo.Inlines.Add(new Run($"Ngày: {invoice.CreatedAt:dd/MM/yyyy HH:mm:ss}"));
        pInfo.Inlines.Add(new LineBreak());
        pInfo.Inlines.Add(new Run($"Bàn: {invoice.Session?.Table?.TableNumber} | Thu ngân: {invoice.CashierUser?.FullName}"));
        pInfo.Inlines.Add(new LineBreak());
        if (invoice.Customer != null)
        {
            pInfo.Inlines.Add(new Run($"Khách: {invoice.Customer.FullName} ({invoice.Customer.PhoneNumber})"));
            pInfo.Inlines.Add(new LineBreak());
        }
        if (invoice.Session != null)
        {
            if (!string.IsNullOrEmpty(invoice.AppliedComboName ?? invoice.Session.ComboName))
            {
                pInfo.Inlines.Add(new Bold(new Run($"Gói Combo: {invoice.AppliedComboName ?? invoice.Session.ComboName}")));
                pInfo.Inlines.Add(new LineBreak());
            }

            var segments = invoice.Session.GetSegments();
            if (segments.Count > 1)
            {
                pInfo.Inlines.Add(new Bold(new Run("Chi tiết tiền giờ theo bàn:")));
                pInfo.Inlines.Add(new LineBreak());
                foreach (var seg in segments)
                {
                    pInfo.Inlines.Add(new Run($"• {seg.TableNumber} ({seg.HourlyRate:N0} đ/h): {seg.DurationFormatted} = {seg.Cost:N0} đ"));
                    pInfo.Inlines.Add(new LineBreak());
                }
            }
            else
            {
                pInfo.Inlines.Add(new Run($"Giờ chơi: {invoice.Session.StartTime:HH:mm} - {invoice.Session.EndTime:HH:mm} ({invoice.TotalPlayMinutes} phút)"));
                pInfo.Inlines.Add(new LineBreak());
            }
            pInfo.Inlines.Add(new Bold(new Run($"Tổng tiền giờ: {invoice.TimeAmount:N0} đ")));
        }
        doc.Blocks.Add(pInfo);

        // Danh sách món
        if (invoice.Session?.OrderItems != null && invoice.Session.OrderItems.Any(x => !x.IsCancelled))
        {
            var pItems = new Paragraph { Margin = new Thickness(0, 5, 0, 5) };
            pItems.Inlines.Add(new Bold(new Run("Dịch vụ / Đồ uống:\n")));
            foreach (var item in invoice.Session.OrderItems.Where(x => !x.IsCancelled))
            {
                pItems.Inlines.Add(new Run($"- {item.Product.ProductName} x{item.Quantity} = {item.TotalAmount:N0} đ\n"));
            }
            doc.Blocks.Add(pItems);
        }

        // Tổng kết
        var pTotal = new Paragraph
        {
            TextAlignment = TextAlignment.Right,
            Margin = new Thickness(0, 10, 0, 10)
        };
        pTotal.Inlines.Add(new Run($"Tiền giờ: {invoice.TimeAmount:N0} đ\n"));
        pTotal.Inlines.Add(new Run($"Tiền dịch vụ: {invoice.ServiceAmount:N0} đ\n"));
        if (invoice.DiscountAmount > 0)
        {
            pTotal.Inlines.Add(new Run($"Giảm giá ({invoice.DiscountPercent}%): -{invoice.DiscountAmount:N0} đ\n"));
        }
        if (invoice.PointsDiscountAmount > 0)
        {
            pTotal.Inlines.Add(new Run($"Trừ điểm: -{invoice.PointsDiscountAmount:N0} đ\n"));
        }
        pTotal.Inlines.Add(new Bold(new Run($"TỔNG CỘNG: {invoice.FinalAmount:N0} đ\n")) { FontSize = 16 });

        if (invoice.PointsEarned > 0)
        {
            pTotal.Inlines.Add(new Run($"(Điểm tích lũy: +{invoice.PointsEarned} điểm)\n"));
        }

        doc.Blocks.Add(pTotal);

        // Footer
        var pFooter = new Paragraph
        {
            TextAlignment = TextAlignment.Center,
            Margin = new Thickness(0, 15, 0, 0)
        };
        pFooter.Inlines.Add(new Italic(new Run("Cảm ơn Quý Khách - Hẹn gặp lại!")));
        doc.Blocks.Add(pFooter);

        return doc;
    }

    public bool PrintReceipt(Invoice invoice, string? printerName = null)
    {
        try
        {
            var printDialog = new PrintDialog();
            if (!string.IsNullOrEmpty(printerName))
            {
                printDialog.PrintQueue = new System.Printing.PrintQueue(new System.Printing.LocalPrintServer(), printerName);
            }

            var doc = GenerateReceiptFlowDocument(invoice);
            var paginator = ((IDocumentPaginatorSource)doc).DocumentPaginator;
            printDialog.PrintDocument(paginator, $"HoaDon_{invoice.InvoiceCode}");
            return true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Lỗi in hóa đơn: {ex.Message}");
            return false;
        }
    }
}
