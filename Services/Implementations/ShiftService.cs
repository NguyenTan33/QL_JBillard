using Microsoft.EntityFrameworkCore;
using QL_JBillard.Data;
using QL_JBillard.Domain.Entities;
using QL_JBillard.Domain.Enums;
using QL_JBillard.Services.Interfaces;

namespace QL_JBillard.Services.Implementations;

public class ShiftService : IShiftService
{
    private readonly IDbContextFactory<AppDbContext> _contextFactory;
    private readonly IAuditService _auditService;

    private Shift? _currentActiveShift;
    public Shift? CurrentActiveShift
    {
        get
        {
            LoadActiveShift();
            return _currentActiveShift;
        }
        private set => _currentActiveShift = value;
    }

    public ShiftService(IDbContextFactory<AppDbContext> contextFactory, IAuditService auditService)
    {
        _contextFactory = contextFactory;
        _auditService = auditService;
        LoadActiveShift();
    }

    private void LoadActiveShift()
    {
        using var context = _contextFactory.CreateDbContext();
        _currentActiveShift = context.Shifts
                                    .Include(s => s.User)
                                    .Include(s => s.BankDepositSlips)
                                    .OrderByDescending(s => s.ShiftId)
                                    .FirstOrDefault(s => s.Status == ShiftStatus.Open);
    }

    public Shift? GetLastClosedShift()
    {
        using var context = _contextFactory.CreateDbContext();
        return context.Shifts
                      .Include(s => s.User)
                      .Where(s => s.Status == ShiftStatus.Closed)
                      .OrderByDescending(s => s.EndTime ?? s.StartTime)
                      .FirstOrDefault();
    }

    public decimal GetDrawerBalanceFromLastShift()
    {
        var lastShift = GetLastClosedShift();
        if (lastShift == null)
        {
            using var context = _contextFactory.CreateDbContext();
            var setting = context.SystemSettings.Find("InitialDrawerCash");
            if (setting != null && decimal.TryParse(setting.SettingValue, out decimal initCash))
            {
                return initCash;
            }
            return 1_000_000m; // Mặc định 1.000.000 đ theo quy định của quán
        }

        decimal balance = lastShift.ActualCashRecorded ?? lastShift.OpeningCash;
        
        // Trừ các khoản nộp ngân hàng sau khi ca đó đã đóng (nếu có phiếu nộp tiền ngoài ca)
        if (lastShift.EndTime.HasValue)
        {
            using var context = _contextFactory.CreateDbContext();
            decimal depositsAfterClose = context.BankDepositSlips
                .Where(b => b.CreatedAt >= lastShift.EndTime.Value && b.Status != "Rejected")
                .ToList()
                .Sum(b => b.Amount);
            balance -= depositsAfterClose;
        }

        return Math.Max(0, balance);
    }

    public Shift StartShift(int userId, decimal openingCash, string? notes = null, bool isDiscrepant = false, string? discrepancyReason = null)
    {
        if (openingCash < 0) throw new ArgumentException("Tiền két đầu ca không được là số âm!");
        using var context = _contextFactory.CreateDbContext();
        
        // Kiểm tra xem đã có ca nào đang mở chưa (bất kể user nào)
        var active = context.Shifts.Include(s => s.User).FirstOrDefault(s => s.Status == ShiftStatus.Open);
        if (active != null)
        {
            if (active.UserId == userId)
            {
                CurrentActiveShift = active;
                return active;
            }

            string activeUserName = active.User?.FullName ?? $"User #{active.UserId}";
            throw new InvalidOperationException($"Ca trực đang hoạt động thuộc về nhân viên [{activeUserName}]. Vui lòng để nhân viên này chốt ca hoặc liên hệ Quản trị viên (Admin)!");
        }

        decimal previousClosingCash = GetDrawerBalanceFromLastShift();

        var shift = new Shift
        {
            UserId = userId,
            StartTime = DateTime.Now,
            OpeningCash = openingCash,
            PreviousShiftClosingCash = previousClosingCash,
            IsOpeningDiscrepant = isDiscrepant,
            OpeningDiscrepancyNotes = discrepancyReason,
            Status = ShiftStatus.Open,
            Notes = notes
        };

        context.Shifts.Add(shift);
        context.SaveChanges();

        // Tự động gán các hóa đơn chưa được gắn ca vào ca mới này để không bao giờ bị mất doanh thu
        var unassignedInvoices = context.Invoices.Where(i => i.ShiftId == null).ToList();
        foreach (var unassigned in unassignedInvoices)
        {
            unassigned.ShiftId = shift.ShiftId;
        }
        if (unassignedInvoices.Any())
        {
            context.SaveChanges();
        }

        _auditService.Log(userId, "SHIFT_START", "Shift", shift.ShiftId.ToString(), null, new
        {
            OpeningCash = openingCash,
            PreviousClosingCash = previousClosingCash,
            IsDiscrepant = isDiscrepant,
            DiscrepancyReason = discrepancyReason,
            StartTime = shift.StartTime
        });

        CurrentActiveShift = context.Shifts.Include(s => s.User).First(s => s.ShiftId == shift.ShiftId);
        return CurrentActiveShift;
    }

    public Shift CloseShift(int shiftId, decimal actualCash, string? notes = null, int? closingUserId = null)
    {
        if (actualCash < 0) throw new ArgumentException("Số tiền thực đếm chốt ca không được là số âm!");
        using var context = _contextFactory.CreateDbContext();
        var shift = context.Shifts
                           .Include(s => s.User)
                           .Include(s => s.BankDepositSlips)
                           .FirstOrDefault(s => s.ShiftId == shiftId);
        if (shift == null) throw new InvalidOperationException("Không tìm thấy ca làm việc!");

        // Phân quyền chốt ca: Chỉ chính nhân viên đó hoặc Admin mới được chốt ca
        if (closingUserId.HasValue)
        {
            var closingUser = context.Users.Find(closingUserId.Value);
            if (closingUser != null)
            {
                if (closingUser.Role != UserRole.Admin && closingUser.UserId != shift.UserId)
                {
                    string ownerName = shift.User?.FullName ?? $"User #{shift.UserId}";
                    throw new InvalidOperationException($"Bạn không có quyền chốt ca của nhân viên [{ownerName}]. Chỉ nhân viên này hoặc Quản trị viên (Admin) mới có quyền chốt ca!");
                }

                // Nếu là Admin chốt ca thay thế nhân viên khác
                if (closingUser.Role == UserRole.Admin && closingUser.UserId != shift.UserId)
                {
                    string ownerName = shift.User?.FullName ?? $"User #{shift.UserId}";
                    notes = $"[Admin ({closingUser.FullName}) can thiệp chốt ca thay thế nhân viên {ownerName}] " + (notes ?? "");
                }
            }
        }

        var now = DateTime.Now;
        shift.EndTime = now;

        // Tính doanh thu trong ca: dựa trên ShiftId hoặc thời gian tạo
        var shiftInvoices = context.Invoices
                                   .Include(i => i.Payments)
                                   .Where(i => i.ShiftId == shiftId || (i.ShiftId == null && i.CreatedAt >= shift.StartTime && i.CreatedAt <= now))
                                   .ToList();

        var payments = shiftInvoices.SelectMany(i => i.Payments).ToList();

        decimal cashSum = payments.Where(p => p.Method == PaymentMethod.Cash).Sum(p => p.Amount);
        decimal transferSum = payments.Where(p => p.Method == PaymentMethod.VietQr || p.Method == PaymentMethod.BankTransfer).Sum(p => p.Amount);

        // Tính tổng tiền đã nộp ngân hàng trong ca (Chỉ tính các phiếu đã được Admin duyệt Approved)
        decimal totalDepositsInShift = context.BankDepositSlips
                                              .Where(b => b.ShiftId == shiftId && b.Status == "Approved")
                                              .ToList()
                                              .Sum(b => b.Amount);

        shift.TotalBankDeposited = totalDepositsInShift;
        shift.SystemCalculatedCash = cashSum;
        shift.SystemCalculatedTransfer = transferSum;
        shift.ActualCashRecorded = actualCash;

        // Tiền mặt dự kiến có trong két = Tiền đầu ca + Doanh thu tiền mặt trong ca - Tiền đã nộp ngân hàng
        decimal expectedTotalCashInDrawer = shift.OpeningCash + cashSum - totalDepositsInShift;
        shift.Discrepancy = actualCash - expectedTotalCashInDrawer;
        shift.Status = ShiftStatus.Closed;
        shift.Notes = notes;

        context.SaveChanges();

        _auditService.Log(shift.UserId, "SHIFT_CLOSE", "Shift", shift.ShiftId.ToString(), null, new
        {
            OpeningCash = shift.OpeningCash,
            CalculatedCash = cashSum,
            CalculatedTransfer = transferSum,
            TotalDeposits = totalDepositsInShift,
            ExpectedInDrawer = expectedTotalCashInDrawer,
            ActualCash = actualCash,
            Discrepancy = shift.Discrepancy
        });

        CurrentActiveShift = null;
        return shift;
    }

    public (decimal TotalCash, decimal TotalTransfer, int TotalInvoices, decimal TotalBankDeposits, decimal CurrentDrawerCash) GetCurrentShiftSummary()
    {
        if (CurrentActiveShift == null) return (0, 0, 0, 0, 0);

        using var context = _contextFactory.CreateDbContext();
        var shiftInvoices = context.Invoices
                                   .Include(i => i.Payments)
                                   .Where(i => i.ShiftId == CurrentActiveShift.ShiftId || (i.ShiftId == null && i.CreatedAt >= CurrentActiveShift.StartTime))
                                   .ToList();

        var payments = shiftInvoices.SelectMany(i => i.Payments).ToList();

        decimal cash = payments.Where(p => p.Method == PaymentMethod.Cash).Sum(p => p.Amount);
        decimal transfer = payments.Where(p => p.Method == PaymentMethod.VietQr || p.Method == PaymentMethod.BankTransfer).Sum(p => p.Amount);
        int invoiceCount = shiftInvoices.Count;

        decimal deposits = context.BankDepositSlips
                                  .Where(b => b.ShiftId == CurrentActiveShift.ShiftId && b.Status == "Approved")
                                  .ToList()
                                  .Sum(b => b.Amount);

        decimal currentDrawer = CurrentActiveShift.OpeningCash + cash - deposits;

        return (cash, transfer, invoiceCount, deposits, Math.Max(0, currentDrawer));
    }

    public BankDepositSlip CreateBankDepositSlip(int userId, decimal amount, string? imagePath, string? notes, int? shiftId = null)
    {
        // 1. Chặn số tiền âm hoặc bằng 0
        if (amount <= 0)
        {
            throw new ArgumentException("Số tiền nộp ngân hàng phải lớn hơn 0 đ!");
        }

        using var context = _contextFactory.CreateDbContext();

        int targetShiftId = shiftId ?? CurrentActiveShift?.ShiftId ?? 0;

        // 2. Chặn nộp vượt quá số tiền mặt hiện có trong két của ca
        if (targetShiftId > 0)
        {
            var targetShift = context.Shifts.Find(targetShiftId);
            if (targetShift != null)
            {
                var shiftInvoices = context.Invoices
                                           .Include(i => i.Payments)
                                           .Where(i => i.ShiftId == targetShiftId || (i.ShiftId == null && i.CreatedAt >= targetShift.StartTime))
                                           .ToList();
                decimal cashInShift = shiftInvoices.SelectMany(i => i.Payments).Where(p => p.Method == PaymentMethod.Cash).Sum(p => p.Amount);
                decimal approvedDeposits = context.BankDepositSlips
                                                  .Where(b => b.ShiftId == targetShiftId && b.Status == "Approved")
                                                  .Select(b => b.Amount)
                                                  .ToList()
                                                  .Sum();
                decimal currentCashInDrawer = targetShift.OpeningCash + cashInShift - approvedDeposits;

                if (amount > currentCashInDrawer)
                {
                    throw new InvalidOperationException($"Số tiền nộp ({amount:N0} đ) vượt quá số tiền mặt hiện có trong két ({currentCashInDrawer:N0} đ)!");
                }
            }
        }

        int countToday = context.BankDepositSlips.Count(b => b.CreatedAt.Date == DateTime.Today) + 1;
        string slipCode = $"NP-{DateTime.Now:yyyyMMdd}-{countToday:D3}";

        // Đọc thông tin tài khoản ngân hàng từ SystemSettings
        var bankNameSetting = context.SystemSettings.FirstOrDefault(s => s.SettingKey == "BankName")?.SettingValue ?? "MBBank (Napas)";
        var bankAccSetting = context.SystemSettings.FirstOrDefault(s => s.SettingKey == "BankAccountNo")?.SettingValue ?? "012345678999";
        var bankNameOwner = context.SystemSettings.FirstOrDefault(s => s.SettingKey == "BankAccountName")?.SettingValue ?? "CLB BIDA J BILLIARDS";

        var user = context.Users.Find(userId);
        bool isAdmin = user?.Role == UserRole.Admin;

        var slip = new BankDepositSlip
        {
            SlipCode = slipCode,
            ShiftId = targetShiftId > 0 ? targetShiftId : null,
            UserId = userId,
            Amount = amount,
            BankName = bankNameSetting,
            BankAccountNo = bankAccSetting,
            BankAccountName = bankNameOwner,
            ImagePath = imagePath,
            Notes = notes,
            CreatedAt = DateTime.Now,
            Status = "Approved", // Khi thu ngân bấm nộp tiền là tiền chuyển ngay vào tài khoản quán, két trừ ngay lập tức
            ApprovedByUserId = isAdmin ? userId : null,
            ApprovedAt = DateTime.Now
        };

        context.BankDepositSlips.Add(slip);

        if (targetShiftId > 0)
        {
            var shift = context.Shifts.Find(targetShiftId);
            if (shift != null)
            {
                shift.TotalBankDeposited += amount;
            }
        }

        context.SaveChanges();

        _auditService.Log(userId, "BANK_DEPOSIT", "BankDepositSlip", slip.DepositSlipId.ToString(), null, new
        {
            SlipCode = slipCode,
            Amount = amount,
            ImagePath = imagePath,
            Notes = notes
        });

        LoadActiveShift();
        return slip;
    }

    public List<BankDepositSlip> GetAllBankDepositSlips(DateTime? fromDate = null, DateTime? toDate = null)
    {
        using var context = _contextFactory.CreateDbContext();
        var query = context.BankDepositSlips
                           .Include(b => b.User)
                           .Include(b => b.Shift)
                           .Include(b => b.ApprovedByUser)
                           .AsQueryable();

        if (fromDate.HasValue)
        {
            query = query.Where(b => b.CreatedAt >= fromDate.Value);
        }
        if (toDate.HasValue)
        {
            query = query.Where(b => b.CreatedAt <= toDate.Value);
        }

        return query.OrderByDescending(b => b.CreatedAt).ToList();
    }

    public bool ApproveBankDepositSlip(int depositSlipId, int adminUserId)
    {
        using var context = _contextFactory.CreateDbContext();
        var slip = context.BankDepositSlips.Find(depositSlipId);
        if (slip == null) return false;

        slip.Status = "Approved";
        slip.ApprovedByUserId = adminUserId;
        slip.ApprovedAt = DateTime.Now;

        if (slip.ShiftId.HasValue)
        {
            var shift = context.Shifts.Find(slip.ShiftId.Value);
            if (shift != null)
            {
                shift.TotalBankDeposited += slip.Amount;
            }
        }

        context.SaveChanges();
        _auditService.Log(adminUserId, "APPROVE_DEPOSIT", "BankDepositSlip", slip.DepositSlipId.ToString(), null, $"Admin duyệt phiếu nộp tiền {slip.SlipCode}");
        return true;
    }

    public bool RejectBankDepositSlip(int depositSlipId, int adminUserId, string? reason)
    {
        using var context = _contextFactory.CreateDbContext();
        var slip = context.BankDepositSlips.Find(depositSlipId);
        if (slip == null) return false;

        slip.Status = "Rejected";
        slip.ApprovedByUserId = adminUserId;
        slip.ApprovedAt = DateTime.Now;
        if (!string.IsNullOrWhiteSpace(reason))
        {
            slip.Notes = $"{slip.Notes} [Từ chối bởi Admin: {reason}]";
        }

        // Hoàn lại tiền vào két của ca nếu phiếu bị từ chối
        if (slip.ShiftId.HasValue)
        {
            var shift = context.Shifts.Find(slip.ShiftId.Value);
            if (shift != null)
            {
                shift.TotalBankDeposited = Math.Max(0, shift.TotalBankDeposited - slip.Amount);
            }
        }

        context.SaveChanges();
        _auditService.Log(adminUserId, "REJECT_DEPOSIT", "BankDepositSlip", slip.DepositSlipId.ToString(), null, $"Admin từ chối phiếu {slip.SlipCode}: {reason}");
        return true;
    }

    public bool UpdateBankDepositSlip(int depositSlipId, decimal amount, string? imagePath, string? notes, int editorUserId)
    {
        using var context = _contextFactory.CreateDbContext();
        var slip = context.BankDepositSlips.Find(depositSlipId);
        if (slip == null) return false;

        var user = context.Users.Find(editorUserId);
        bool isAdmin = user?.Role == UserRole.Admin;

        decimal oldAmount = slip.Amount;
        slip.Amount = amount;
        if (!string.IsNullOrWhiteSpace(imagePath)) slip.ImagePath = imagePath;
        slip.Notes = notes;
        slip.IsModified = true;

        if (!isAdmin)
        {
            // Nếu người sửa không phải admin thì chuyển về trạng thái Chờ duyệt
            slip.Status = "Pending";
            slip.ApprovedByUserId = null;
            slip.ApprovedAt = null;
        }

        // Điều chỉnh lại tổng tiền nộp trong ca
        if (slip.ShiftId.HasValue)
        {
            var shift = context.Shifts.Find(slip.ShiftId.Value);
            if (shift != null)
            {
                shift.TotalBankDeposited = Math.Max(0, shift.TotalBankDeposited - oldAmount + amount);
            }
        }

        context.SaveChanges();
        _auditService.Log(editorUserId, "UPDATE_DEPOSIT", "BankDepositSlip", slip.DepositSlipId.ToString(), null, $"Cập nhật phiếu nộp tiền {slip.SlipCode}, số tiền mới: {amount:N0} đ");
        return true;
    }
}
