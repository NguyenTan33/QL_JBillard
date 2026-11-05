using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Windows;
using Microsoft.Win32;
using MiniExcelLibs;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using QL_JBillard.Domain.Entities;
using QL_JBillard.Services.Interfaces;

namespace QL_JBillard.ViewModels;

public class AuditLogItemViewModel
{
    public int AuditLogId { get; set; }
    public DateTime CreatedAt { get; set; }
    public string CreatedAtFormatted => CreatedAt.ToString("dd/MM/yyyy HH:mm:ss");
    public string OperatorName { get; set; } = string.Empty;
    public string ActionCode { get; set; } = string.Empty;
    public string ActionDisplay { get; set; } = string.Empty;
    public string ActionBadgeBackground { get; set; } = "#F1F5F9";
    public string ActionBadgeForeground { get; set; } = "#334155";
    public string EntityNameDisplay { get; set; } = string.Empty;
    public string DetailsDisplay { get; set; } = string.Empty;
}

public partial class AuditLogViewModel : ObservableObject
{
    private readonly IAuditService _auditService;
    private readonly IBackupAndSecurityService _securityService;

    [ObservableProperty]
    private ObservableCollection<AuditLogItemViewModel> _logs = new();

    [ObservableProperty]
    private string _integrityStatusMessage = "Chưa kiểm tra";

    [ObservableProperty]
    private bool _isTampered = false;

    [ObservableProperty]
    private string _selectedDateFilter = "TODAY"; // TODAY, YESTERDAY, 7DAYS, CUSTOM, ALL

    [ObservableProperty]
    private DateTime _fromDate = DateTime.Today;

    [ObservableProperty]
    private DateTime _toDate = DateTime.Today;

    [ObservableProperty]
    private string _logCountText = string.Empty;

    public AuditLogViewModel(IAuditService auditService, IBackupAndSecurityService securityService)
    {
        _auditService = auditService;
        _securityService = securityService;
        LoadLogs();
        VerifyDatabase();
    }

    partial void OnFromDateChanged(DateTime value)
    {
        if (SelectedDateFilter == "CUSTOM") LoadLogs();
    }

    partial void OnToDateChanged(DateTime value)
    {
        if (SelectedDateFilter == "CUSTOM") LoadLogs();
    }

    [RelayCommand]
    public void SetDateFilter(string filter)
    {
        SelectedDateFilter = filter;
        LoadLogs();
    }

    [RelayCommand]
    public void LoadLogs()
    {
        DateTime today = DateTime.Today;
        List<AuditLog> rawList;

        switch (SelectedDateFilter)
        {
            case "TODAY":
                rawList = _auditService.GetLogs(today, today);
                break;
            case "YESTERDAY":
                DateTime yesterday = today.AddDays(-1);
                rawList = _auditService.GetLogs(yesterday, yesterday);
                break;
            case "7DAYS":
                DateTime pastWeek = today.AddDays(-7);
                rawList = _auditService.GetLogs(pastWeek, today);
                break;
            case "CUSTOM":
                rawList = _auditService.GetLogs(FromDate, ToDate);
                break;
            case "ALL":
            default:
                rawList = _auditService.GetLogs(null, null, null);
                break;
        }

        LogCountText = $"Hiển thị: {rawList.Count} bản ghi";

        var viewModels = rawList.Select(log =>
        {
            var (actionText, bg, fg) = FormatAction(log.Action);
            return new AuditLogItemViewModel
            {
                AuditLogId = log.AuditLogId,
                CreatedAt = log.CreatedAt,
                OperatorName = log.User?.FullName ?? "Hệ Thống",
                ActionCode = log.Action,
                ActionDisplay = actionText,
                ActionBadgeBackground = bg,
                ActionBadgeForeground = fg,
                EntityNameDisplay = FormatEntity(log.EntityName),
                DetailsDisplay = FormatDetails(log.NewValuesJson)
            };
        }).ToList();

        Logs = new ObservableCollection<AuditLogItemViewModel>(viewModels);
    }

    [RelayCommand]
    public void VerifyDatabase()
    {
        var (tampered, details) = _securityService.CheckTamperProofStatus();
        IsTampered = tampered;
        IntegrityStatusMessage = details;
    }

    [RelayCommand]
    public void ExportAuditLogs()
    {
        try
        {
            if (Logs == null || !Logs.Any())
            {
                MessageBox.Show("Không có dữ liệu nhật ký nào để xuất file.", 
                    "Thông Báo", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            string dateTag = SelectedDateFilter switch
            {
                "TODAY" => $"HomNay_{DateTime.Now:yyyyMMdd}",
                "YESTERDAY" => $"HomQua_{DateTime.Today.AddDays(-1):yyyyMMdd}",
                "7DAYS" => $"7NgayQua_{DateTime.Now:yyyyMMdd}",
                "CUSTOM" => $"{FromDate:yyyyMMdd}_den_{ToDate:yyyyMMdd}",
                _ => $"TatCa_{DateTime.Now:yyyyMMdd}"
            };

            var sfd = new SaveFileDialog
            {
                Title = "Xuất File Nhật Ký Hoạt Động Hệ Thống",
                Filter = "File Excel (*.xlsx)|*.xlsx|File CSV (*.csv)|*.csv",
                FileName = $"NhatKy_HeThong_JBillard_{dateTag}_{DateTime.Now:HHmmss}.xlsx"
            };

            if (sfd.ShowDialog() == true)
            {
                var exportList = Logs.Select(l => new Dictionary<string, object?>
                {
                    ["Mã Nhật Ký"] = l.AuditLogId,
                    ["Thời Gian"] = l.CreatedAtFormatted,
                    ["Người Thao Tác"] = l.OperatorName,
                    ["Mã Hành Động"] = l.ActionCode,
                    ["Tên Hành Động"] = l.ActionDisplay,
                    ["Đối Tượng"] = l.EntityNameDisplay,
                    ["Chi Tiết"] = l.DetailsDisplay
                }).ToList();

                if (sfd.FileName.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase))
                {
                    MiniExcel.SaveAs(sfd.FileName, exportList, overwriteFile: true);
                }
                else
                {
                    var sb = new StringBuilder();
                    sb.AppendLine("sep=,");
                    sb.AppendLine("Mã Nhật Ký,Thời Gian,Người Thao Tác,Mã Hành Động,Tên Hành Động,Đối Tượng,Chi Tiết");

                    foreach (var log in Logs)
                    {
                        sb.AppendLine(string.Join(",",
                            log.AuditLogId,
                            EscapeCsv(log.CreatedAtFormatted),
                            EscapeCsv(log.OperatorName),
                            EscapeCsv(log.ActionCode),
                            EscapeCsv(log.ActionDisplay),
                            EscapeCsv(log.EntityNameDisplay),
                            EscapeCsv(log.DetailsDisplay)
                        ));
                    }

                    File.WriteAllText(sfd.FileName, sb.ToString(), new UTF8Encoding(true));
                }

                var res = MessageBox.Show(
                    $"Xuất file nhật ký hệ thống thành công!\nTổng cộng: {Logs.Count} bản ghi.\nFile lưu tại: {sfd.FileName}\n\nBạn có muốn mở file ngay bây giờ không?",
                    "Xuất Nhật Ký Thành Công",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Information);

                if (res == MessageBoxResult.Yes)
                {
                    Process.Start(new ProcessStartInfo(sfd.FileName) { UseShellExecute = true });
                }
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Lỗi khi xuất file nhật ký: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private static string EscapeCsv(string? value)
    {
        if (string.IsNullOrEmpty(value)) return "\"\"";
        return $"\"{value.Replace("\"", "\"\"")}\"";
    }

    private static (string Display, string Bg, string Fg) FormatAction(string action)
    {
        return action switch
        {
            "LOGIN_SUCCESS" => ("● Đăng Nhập", "#ECFDF5", "#059669"),
            "LOGIN_FAILED" => ("✕ Đăng Nhập Lỗi", "#FEF2F2", "#DC2626"),
            "LOGIN_BLOCKED_SHIFT_ACTIVE" or "LOGIN_BLOCKED" => ("⛔ Chặn Đăng Nhập", "#FEF2F2", "#DC2626"),
            "LOGOUT" => ("○ Đăng Xuất", "#F1F5F9", "#475569"),
            "TABLE_OPEN" or "OPEN_TABLE" => ("▶ Mở Bàn", "#ECFDF5", "#059669"),
            "TABLE_SWITCH" or "SWITCH_TABLE" => ("⇄ Chuyển Bàn", "#EFF6FF", "#2563EB"),
            "UPDATE_TABLE_STATUS" => ("⚙ Trạng Thái Bàn", "#FFFBEB", "#D97706"),
            "INVOICE_CHECKOUT" => ("✓ Thanh Toán HĐ", "#EFF6FF", "#1D4ED8"),
            "ORDER_ITEM" => ("+ Gọi Món", "#F5F3FF", "#7C3AED"),
            "COMPLETE_ORDER_ITEM" or "COMPLETE_ORDER" => ("✓ Phục Vụ Món", "#ECFDF5", "#047857"),
            "CANCEL_ORDER_ITEM" or "CANCEL_ORDER" => ("✕ Hủy Món", "#FEF2F2", "#DC2626"),
            "SHIFT_START" or "START_SHIFT" => ("◆ Mở Ca Trực", "#ECFDF5", "#059669"),
            "SHIFT_CLOSE" or "CLOSE_SHIFT" => ("■ Chốt Ca Trực", "#EFF6FF", "#2563EB"),
            "BANK_DEPOSIT" => ("★ Nộp Tiền NH", "#EFF6FF", "#1E40AF"),
            "APPROVE_DEPOSIT" => ("✓ Duyệt Nộp Tiền", "#ECFDF5", "#047857"),
            "REJECT_DEPOSIT" => ("✕ Từ Chối Nộp", "#FEF2F2", "#DC2626"),
            "UPDATE_DEPOSIT" => ("✎ Sửa Phiếu Nộp", "#FFFBEB", "#D97706"),
            "IMPORT_STOCK" => ("+ Nhập Kho Hàng", "#F0FDF4", "#15803D"),
            "DATABASE_BACKUP" => ("⟳ Sao Lưu CSDL", "#F8FAFC", "#334155"),
            "DATABASE_RESTORE" => ("⟳ Phục Hồi CSDL", "#FEF2F2", "#DC2626"),
            "CREATE_USER" => ("+ Tạo Tài Khoản", "#ECFDF5", "#059669"),
            "UPDATE_USER" => ("✎ Sửa Tài Khoản", "#EFF6FF", "#2563EB"),
            "TOGGLE_USER_STATUS" => ("⚙ Đổi Trạng Thái TK", "#FFFBEB", "#D97706"),
            "LOCK_USER" or "DEACTIVATE_USER" => ("🔒 Khóa Tài Khoản", "#FEF2F2", "#DC2626"),
            "UNLOCK_USER" => ("🔓 Mở Khóa TK", "#ECFDF5", "#059669"),
            "DELETE_USER" => ("✕ Xóa Tài Khoản", "#FEF2F2", "#DC2626"),
            "CHANGE_PASSWORD" => ("🔒 Đổi Mật Khẩu", "#EFF6FF", "#2563EB"),
            _ => ($"● {action}", "#F1F5F9", "#334155")
        };
    }

    private static string FormatEntity(string entity)
    {
        return entity switch
        {
            "User" => "Tài Khoản",
            "Table" or "TableSession" => "Bàn Chơi",
            "Invoice" => "Hóa Đơn",
            "OrderItem" => "Món F&B",
            "Shift" => "Ca Trực",
            "BankDepositSlip" => "Phiếu Nộp Tiền",
            "Product" or "StockTransaction" => "Kho Hàng",
            "System" or "Database" => "Hệ Thống",
            _ => entity
        };
    }

    private static string FormatDetails(string? rawJson)
    {
        if (string.IsNullOrWhiteSpace(rawJson)) return string.Empty;

        string text = rawJson.Trim();

        // 1. Unescape Unicode ký tự tiếng Việt (vd: \u0110\u0103ng -> Đăng, \u00C0 -> À)
        try
        {
            if (text.Contains("\\u"))
            {
                text = Regex.Unescape(text);
            }
        }
        catch { }

        // 2. Nếu là chuỗi JSON có ngoặc kép bọc ngoài
        if (text.StartsWith("\"") && text.EndsWith("\"") && text.Length >= 2)
        {
            text = text.Substring(1, text.Length - 2).Trim();
            return text;
        }

        // 3. Nếu là JSON Object chuẩn
        if (text.StartsWith("{") && text.EndsWith("}"))
        {
            try
            {
                using var doc = JsonDocument.Parse(text);
                var root = doc.RootElement;

                // Mở bàn (TABLE_OPEN)
                if (root.TryGetProperty("TableNumber", out var tbNum))
                {
                    decimal rate = root.TryGetProperty("Rate", out var r) ? r.GetDecimal() : 0;
                    string mode = root.TryGetProperty("PlayMode", out var pm) ? pm.GetString() ?? "" : "";
                    string combo = root.TryGetProperty("ComboName", out var cn) ? cn.GetString() ?? "" : "";
                    int sId = root.TryGetProperty("SessionId", out var sid) ? sid.GetInt32() : 0;
                    string res = $"Mở {tbNum.GetString()}";
                    if (mode == "COMBO" || !string.IsNullOrWhiteSpace(combo))
                    {
                        res += $" | Gói Combo: {combo}";
                    }
                    else
                    {
                        res += $" | Giá: {rate:N0} đ/giờ";
                    }
                    if (sId > 0) res += $" | Phiên #{sId}";
                    return res;
                }

                // Nhập kho (IMPORT_STOCK)
                if (root.TryGetProperty("Added", out var addedQty) && (root.TryGetProperty("Product", out var prodStock) || root.TryGetProperty("ProductName", out prodStock)))
                {
                    string pName = prodStock.GetString() ?? "";
                    int added = addedQty.GetInt32();
                    int newStk = root.TryGetProperty("NewStock", out var ns) ? ns.GetInt32() : 0;
                    decimal cost = root.TryGetProperty("Cost", out var cVal) ? cVal.GetDecimal() : 0;
                    string res = $"Nhập hàng: {pName} (+{added}) | Tồn kho mới: {newStk}";
                    if (cost > 0) res += $" | Tổng tiền vốn: {cost:N0} đ";
                    return res;
                }

                // Chuyển bàn (TABLE_SWITCH)
                if (root.TryGetProperty("To", out var toTb))
                {
                    string to = toTb.GetString() ?? "";
                    string re = root.TryGetProperty("Reason", out var rVal) ? rVal.GetString() ?? "" : "";
                    return $"Chuyển sang bàn {to}" + (!string.IsNullOrWhiteSpace(re) ? $" (Lý do: {re})" : "");
                }

                // Thanh toán hóa đơn (INVOICE_CHECKOUT)
                if (root.TryGetProperty("InvoiceCode", out var invCode))
                {
                    string code = invCode.GetString() ?? "";
                    string table = root.TryGetProperty("Table", out var tb) ? tb.GetString() ?? "" : "";
                    decimal amount = root.TryGetProperty("FinalAmount", out var fa) ? fa.GetDecimal() : 0;
                    return $"Hóa đơn: {code} | {table} | Tổng tiền: {amount:N0} đ";
                }

                // Mở ca / Chốt ca (SHIFT_START / SHIFT_CLOSE)
                if (root.TryGetProperty("OpeningCash", out var oc))
                {
                    decimal openVal = oc.GetDecimal();
                    if (root.TryGetProperty("ActualCash", out var ac))
                    {
                        decimal actVal = ac.GetDecimal();
                        decimal disc = root.TryGetProperty("Discrepancy", out var d) ? d.GetDecimal() : 0;
                        decimal totalRev = (root.TryGetProperty("CalculatedCash", out var cc) ? cc.GetDecimal() : 0) + 
                                           (root.TryGetProperty("CalculatedTransfer", out var ct) ? ct.GetDecimal() : 0);
                        string res = $"Chốt ca | Tiền thực đếm nộp két: {actVal:N0} đ | Doanh thu: {totalRev:N0} đ";
                        if (disc != 0) res += $" | Lệch két: {disc:N0} đ";
                        else res += " (Khớp 100%)";
                        return res;
                    }
                    else
                    {
                        bool isDisc = root.TryGetProperty("IsDiscrepant", out var idisc) && idisc.GetBoolean();
                        string discNote = root.TryGetProperty("DiscrepancyReason", out var dnote) ? dnote.GetString() ?? "" : "";
                        string res = $"Mở ca trực | Tiền nhận bàn giao két: {openVal:N0} đ";
                        if (isDisc && !string.IsNullOrWhiteSpace(discNote)) res += $" | Lệch đầu ca! Lý do: {discNote}";
                        else res += " (Khớp 100%)";
                        return res;
                    }
                }

                // Phục vụ xong món (COMPLETE_ORDER_ITEM)
                if (root.TryGetProperty("Completed", out _) && (root.TryGetProperty("Product", out var prodComp) || root.TryGetProperty("ProductName", out prodComp)))
                {
                    return $"Đã hoàn thành / phục vụ món: {prodComp.GetString()}";
                }

                // Gọi món (ORDER_ITEM)
                if (root.TryGetProperty("Product", out var prodElem) || root.TryGetProperty("ProductName", out prodElem))
                {
                    string prod = prodElem.GetString() ?? "";
                    int qty = root.TryGetProperty("Quantity", out var q) ? q.GetInt32() : 1;
                    decimal amt = root.TryGetProperty("Amount", out var a) ? a.GetDecimal() : (root.TryGetProperty("TotalAmount", out var ta) ? ta.GetDecimal() : 0);
                    string tb = root.TryGetProperty("Table", out var tbp) ? $" cho {tbp.GetString()}" : "";
                    string res = $"Món: {prod} (x{qty}){tb}";
                    if (amt > 0) res += $" | Tổng: {amt:N0} đ";
                    return res;
                }

                // Phiếu nộp tiền ngân hàng (BANK_DEPOSIT)
                if (root.TryGetProperty("SlipCode", out var sc))
                {
                    string code = sc.GetString() ?? "";
                    decimal amt = root.TryGetProperty("Amount", out var a) ? a.GetDecimal() : 0;
                    return $"Phiếu nộp ngân hàng: {code} | Số tiền: {amt:N0} đ";
                }

                // Sao lưu CSDL (DATABASE_BACKUP)
                if (root.TryGetProperty("FileName", out var fn))
                {
                    string file = fn.GetString() ?? "";
                    long size = root.TryGetProperty("Size", out var sz) ? sz.GetInt64() : 0;
                    return $"Sao lưu an toàn CSDL ({size / 1024} KB) - File: {file}";
                }

                // Cập nhật trạng thái bàn
                if (root.TryGetProperty("OldStatus", out var oldSt) && root.TryGetProperty("NewStatus", out var newSt))
                {
                    return $"Đổi trạng thái bàn: {TranslateTableStatus(oldSt.ToString())} ➔ {TranslateTableStatus(newSt.ToString())}";
                }

                // Hủy món
                if (root.TryGetProperty("Cancelled", out _) && root.TryGetProperty("Reason", out var reason))
                {
                    return $"Hủy món | Lý do: {reason.GetString()}";
                }

                // Đổi trạng thái tài khoản (Kích hoạt / Khóa)
                if (root.TryGetProperty("Username", out var unToggle) && root.TryGetProperty("IsActive", out var isActiveElem) && !root.TryGetProperty("FullName", out _))
                {
                    bool isActive = isActiveElem.GetBoolean();
                    return $"Tài khoản @{unToggle.GetString()} ➔ {(isActive ? "Mở khóa kích hoạt" : "Khóa tài khoản")}";
                }

                // Tài khoản
                if (root.TryGetProperty("Username", out var un))
                {
                    string u = un.GetString() ?? "";
                    string fnUser = root.TryGetProperty("FullName", out var fnm) ? fnm.GetString() ?? "" : "";
                    string r = "";
                    if (root.TryGetProperty("Role", out var rl))
                    {
                        if (rl.ValueKind == JsonValueKind.Number)
                        {
                            int rInt = rl.GetInt32();
                            r = (rInt == 1) ? "Quản Trị Viên" : "Thu Ngân";
                        }
                        else
                        {
                            r = rl.GetString() ?? "";
                        }
                    }
                    return $"Tài khoản: {fnUser} (@{u})" + (!string.IsNullOrWhiteSpace(r) ? $" | Vai trò: {r}" : "");
                }

                var sb = new System.Text.StringBuilder();
                foreach (var prop in root.EnumerateObject())
                {
                    if (sb.Length > 0) sb.Append(" | ");
                    sb.Append($"{prop.Name}: {prop.Value}");
                }
                return sb.ToString();
            }
            catch { }
        }

        // 4. Nếu chứa thông tin tài khoản (kể cả dạng mảng ["Username":"270949",...])
        if (text.Contains("Username"))
        {
            var matchUser = Regex.Match(text, @"""Username""\s*:\s*""?([^"",\]\}]+)""?");
            var matchFull = Regex.Match(text, @"""FullName""\s*:\s*""?([^"",\]\}]+)""?");
            var matchRole = Regex.Match(text, @"""Role""\s*:\s*""?([^"",\]\}]+)""?");
            string u = matchUser.Success ? matchUser.Groups[1].Value.Trim() : "";
            string f = matchFull.Success ? matchFull.Groups[1].Value.Trim() : "";
            string r = matchRole.Success ? matchRole.Groups[1].Value.Trim() : "";
            string roleName = (r == "1" || r.Equals("Admin", StringComparison.OrdinalIgnoreCase)) ? "Quản Trị Viên" : "Thu Ngân";
            return $"Tài khoản: {f} (@{u}) | Vai trò: {roleName}";
        }

        // 4. Nếu là bản ghi cũ chứa chuỗi serialize dạng "TableNumber: BÀN 04 | SessionId: 13 | StartTime: ... | Rate: 50000"
        if (text.Contains("TableNumber:") && text.Contains("Rate:"))
        {
            var matchTb = Regex.Match(text, @"TableNumber:\s*([^\|]+)");
            var matchRate = Regex.Match(text, @"Rate:\s*([0-9\.]+)");
            string tbName = matchTb.Success ? matchTb.Groups[1].Value.Trim() : "Bàn";
            if (matchRate.Success && decimal.TryParse(matchRate.Groups[1].Value, out decimal r))
            {
                return $"Mở {tbName} | Giá giờ: {r:N0} đ/giờ";
            }
            return $"Mở {tbName}";
        }

        if (text.Contains("OpeningCash:") && text.Contains("PreviousClosingCash:"))
        {
            var matchOpen = Regex.Match(text, @"OpeningCash:\s*([0-9\.]+)");
            if (matchOpen.Success && decimal.TryParse(matchOpen.Groups[1].Value, out decimal oc))
            {
                return $"Mở ca trực | Tiền nhận bàn giao két: {oc:N0} đ";
            }
        }

        return text;
    }

    private static string TranslateTableStatus(string? status)
    {
        if (string.IsNullOrWhiteSpace(status)) return "";
        return status.Trim() switch
        {
            "0" or "Available" => "Bàn Trống",
            "1" or "Occupied" => "Đang Có Khách",
            "2" or "Reserved" => "Đã Đặt Trước",
            "3" or "Maintenance" => "Bảo Trì",
            _ => status
        };
    }
}
