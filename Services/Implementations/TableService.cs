using Microsoft.EntityFrameworkCore;
using QL_JBillard.Data;
using QL_JBillard.Domain.Entities;
using QL_JBillard.Domain.Enums;
using QL_JBillard.Domain.Models;
using QL_JBillard.Helpers;
using QL_JBillard.Services.Interfaces;

namespace QL_JBillard.Services.Implementations;

public class TableService : ITableService
{
    private readonly IDbContextFactory<AppDbContext> _contextFactory;
    private readonly IAuditService _auditService;

    public TableService(IDbContextFactory<AppDbContext> contextFactory, IAuditService auditService)
    {
        _contextFactory = contextFactory;
        _auditService = auditService;
    }

    public List<Floor> GetAllFloors()
    {
        using var context = _contextFactory.CreateDbContext();
        return context.Floors
                      .Include(f => f.Areas)
                      .OrderBy(f => f.OrderIndex)
                      .ToList();
    }

    public List<Area> GetAllAreas()
    {
        using var context = _contextFactory.CreateDbContext();
        return context.Areas
                      .Include(a => a.Floor)
                      .Include(a => a.Tables)
                      .OrderBy(a => a.AreaName)
                      .ToList();
    }

    public List<TableType> GetAllTableTypes()
    {
        using var context = _contextFactory.CreateDbContext();
        return context.TableTypes.OrderBy(t => t.TableTypeId).ToList();
    }

    public List<Table> GetAllTables()
    {
        using var context = _contextFactory.CreateDbContext();
        var tables = context.Tables
                            .Include(t => t.Area)
                            .ThenInclude(a => a.Floor)
                            .Include(t => t.TableType)
                            .Include(t => t.CurrentSession)
                            .ThenInclude(s => s!.Customer)
                            .Include(t => t.CurrentSession)
                            .ThenInclude(s => s!.OrderItems.Where(o => !o.IsCancelled))
                            .ThenInclude(o => o.Product)
                            .Where(t => t.IsActive)
                            .OrderBy(t => t.TableNumber)
                            .ToList();

        // Đảm bảo phiên chơi đang hoạt động luôn được nạp đầy đủ
        foreach (var t in tables)
        {
            if (t.Status == TableStatus.Occupied && (t.CurrentSession == null || t.CurrentSessionId == null))
            {
                var activeSession = context.TableSessions
                                           .Include(s => s.Customer)
                                           .Include(s => s.OrderItems.Where(o => !o.IsCancelled))
                                           .ThenInclude(o => o.Product)
                                           .FirstOrDefault(s => s.TableId == t.TableId && s.Status == SessionStatus.Active);
                if (activeSession != null)
                {
                    t.CurrentSessionId = activeSession.SessionId;
                    t.CurrentSession = activeSession;
                }
            }
        }
        return tables;
    }

    public Table? GetTableById(int tableId)
    {
        using var context = _contextFactory.CreateDbContext();
        var t = context.Tables
                       .Include(t => t.Area)
                       .ThenInclude(a => a.Floor)
                       .Include(t => t.TableType)
                       .Include(t => t.CurrentSession)
                       .ThenInclude(s => s!.Customer)
                       .Include(t => t.CurrentSession)
                       .ThenInclude(s => s!.OrderItems.Where(o => !o.IsCancelled))
                       .ThenInclude(o => o.Product)
                       .FirstOrDefault(t => t.TableId == tableId);

        if (t != null && t.Status == TableStatus.Occupied && (t.CurrentSession == null || t.CurrentSessionId == null))
        {
            var activeSession = context.TableSessions
                                       .Include(s => s.Customer)
                                       .Include(s => s.OrderItems.Where(o => !o.IsCancelled))
                                       .ThenInclude(o => o.Product)
                                       .FirstOrDefault(s => s.TableId == t.TableId && s.Status == SessionStatus.Active);
            if (activeSession != null)
            {
                t.CurrentSessionId = activeSession.SessionId;
                t.CurrentSession = activeSession;
            }
        }
        return t;
    }

    public TableSession OpenTable(int tableId, int userId, int? customerId = null, string? note = null, string playMode = "HOURLY", int? comboId = null)
    {
        using var context = _contextFactory.CreateDbContext();
        var table = context.Tables
                           .Include(t => t.TableType)
                           .FirstOrDefault(t => t.TableId == tableId);

        if (table == null)
            throw new InvalidOperationException($"Không tìm thấy bàn #{tableId}");

        // Bắt buộc phải có ca trực đang mở thì mới cho phép mở bàn
        bool hasActiveShift = context.Shifts.Any(s => s.Status == ShiftStatus.Open);
        if (!hasActiveShift)
        {
            throw new InvalidOperationException("Vui lòng mở ca trực trước khi mở bàn phục vụ khách!");
        }

        if (table.Status == TableStatus.Occupied)
            throw new InvalidOperationException($"Bàn {table.TableNumber} đang có khách chơi!");

        var now = DateTime.Now;
        var session = new TableSession
        {
            TableId = tableId,
            CustomerId = customerId,
            CreatedByUserId = userId,
            StartTime = now,
            AppliedHourlyRate = table.TableType.DefaultHourlyRate,
            Status = SessionStatus.Active,
            Note = note,
            PlayMode = playMode ?? "HOURLY"
        };

        if (string.Equals(playMode, "COMBO", StringComparison.OrdinalIgnoreCase) && comboId.HasValue)
        {
            var combo = context.PlayCombos.Find(comboId.Value);
            if (combo != null)
            {
                session.ComboId = combo.ComboId;
                session.ComboName = combo.ComboName;
                session.ComboPrice = combo.Price;
                session.ComboDurationMinutes = combo.DurationMinutes;
            }
        }

        context.TableSessions.Add(session);
        context.SaveChanges();

        // Nếu mở bàn theo gói Combo, tự động thêm các món ăn/uống của combo vào checklist order & bếp pha chế
        if (string.Equals(playMode, "COMBO", StringComparison.OrdinalIgnoreCase) && comboId.HasValue)
        {
            var combo = context.PlayCombos.Find(comboId.Value);
            if (combo != null)
            {
                AddComboOrderItems(context, session, combo, userId, now);
            }
        }

        // Gán phiên hiện tại vào bàn và đổi trạng thái
        table.CurrentSessionId = session.SessionId;
        table.Status = TableStatus.Occupied;
        context.SaveChanges();

        _auditService.Log(userId, "TABLE_OPEN", "Table", table.TableId.ToString(), null, new
        {
            TableNumber = table.TableNumber,
            SessionId = session.SessionId,
            StartTime = now,
            PlayMode = session.PlayMode,
            ComboName = session.ComboName,
            Rate = table.TableType.DefaultHourlyRate
        });

        return session;
    }

    private void AddComboOrderItems(AppDbContext context, TableSession session, PlayCombo combo, int userId, DateTime now)
    {
        // 1. Tải danh sách món đã gán vào gói combo theo chuẩn quan hệ 3NF (PlayComboItem)
        var comboWithItems = context.PlayCombos
            .Include(c => c.Items)
                .ThenInclude(i => i.Product)
            .FirstOrDefault(c => c.ComboId == combo.ComboId);

        var comboItems = comboWithItems?.Items?.ToList();

        if (comboItems != null && comboItems.Any())
        {
            foreach (var cItem in comboItems)
            {
                var product = cItem.Product ?? context.Products.Find(cItem.ProductId);
                if (product == null) continue;

                var orderItem = new OrderItem
                {
                    SessionId = session.SessionId,
                    ProductId = product.ProductId,
                    Quantity = cItem.Quantity,
                    UnitPrice = 0, // Đã bao gồm trong giá trọn gói Combo
                    TotalAmount = 0,
                    OrderedByUserId = userId,
                    OrderedAt = now,
                    IsCompleted = false,
                    IsComboItem = true,
                    ComboName = combo.ComboName
                };
                context.OrderItems.Add(orderItem);

                // Tự động trừ tồn kho nếu là sản phẩm quản lý tồn kho (IsInventoryItem = true)
                if (product.IsInventoryItem)
                {
                    var inv = context.InventoryItems.FirstOrDefault(i => i.ProductId == product.ProductId);
                    if (inv != null && inv.QuantityOnHand >= cItem.Quantity)
                    {
                        inv.QuantityOnHand -= cItem.Quantity;
                    }
                }
            }
            context.SaveChanges();
            return;
        }

        // 2. Fallback an toàn nếu gói combo cũ chưa có PlayComboItem
        var itemsToAdd = new List<(string ProductCode, string FallbackName, int Quantity)>();

        string comboName = (combo.ComboName ?? "").ToLower();
        if (comboName.Contains("cày đêm") || comboName.Contains("cay dem") || comboName.Contains("mì"))
        {
            itemsToAdd.Add(("STING", "Nước Tăng Lực Sting Dâu (Theo Combo)", 2));
            itemsToAdd.Add(("MI_BO_TRUNG", "Mì Xào Bò Trứng Ốp La (Theo Combo)", 1));
        }
        else if (comboName.Contains("sinh viên") || comboName.Contains("sinh vien") || comboName.Contains("trà đá"))
        {
            itemsToAdd.Add(("TRA_DAO", "Trà Đào Cam Sả Tươi (Theo Combo)", 2));
        }
        else // "Combo 3 Tiếng + 2 Nước" hoặc mặc định
        {
            itemsToAdd.Add(("STING", "Nước Tăng Lực Sting Dâu (Theo Combo)", 2));
        }

        var defaultUnit = context.Units.FirstOrDefault() ?? new Unit { UnitName = "Phần" };
        var defaultCat = context.ProductCategories.FirstOrDefault(c => c.CategoryName.Contains("Đồ ăn") || c.CategoryName.Contains("Thức uống")) 
                      ?? context.ProductCategories.FirstOrDefault() 
                      ?? new ProductCategory { CategoryName = "Món Combo" };

        foreach (var item in itemsToAdd)
        {
            var product = context.Products.FirstOrDefault(p => p.ProductCode == item.ProductCode);
            if (product == null)
            {
                product = context.Products.FirstOrDefault(p => p.ProductName.Contains(item.ProductCode));
            }
            if (product == null)
            {
                product = new Product
                {
                    ProductCode = item.ProductCode,
                    ProductName = item.FallbackName,
                    CategoryId = defaultCat.CategoryId,
                    UnitId = defaultUnit.UnitId,
                    CostPrice = 0,
                    SellingPrice = 0,
                    IsInventoryItem = false,
                    IsActive = true
                };
                context.Products.Add(product);
                context.SaveChanges();
            }

            var orderItem = new OrderItem
            {
                SessionId = session.SessionId,
                ProductId = product.ProductId,
                Quantity = item.Quantity,
                UnitPrice = 0,
                TotalAmount = 0,
                OrderedByUserId = userId,
                OrderedAt = now,
                IsCompleted = false,
                IsComboItem = true,
                ComboName = combo.ComboName
            };
            context.OrderItems.Add(orderItem);

            if (product.IsInventoryItem)
            {
                var inv = context.InventoryItems.FirstOrDefault(i => i.ProductId == product.ProductId);
                if (inv != null && inv.QuantityOnHand >= item.Quantity)
                {
                    inv.QuantityOnHand -= item.Quantity;
                }
            }
        }
        context.SaveChanges();
    }

    public bool SwitchTable(int fromTableId, int toTableId, int userId, string? reason = null)
    {
        if (fromTableId == toTableId) return false;

        using var context = _contextFactory.CreateDbContext();
        bool hasActiveShift = context.Shifts.Any(s => s.Status == ShiftStatus.Open);
        if (!hasActiveShift) return false;

        var fromTable = context.Tables
                               .Include(t => t.TableType)
                               .FirstOrDefault(t => t.TableId == fromTableId);

        var toTable = context.Tables
                             .Include(t => t.TableType)
                             .FirstOrDefault(t => t.TableId == toTableId);

        if (fromTable == null || toTable == null) return false;
        if (toTable.Status != TableStatus.Available) return false;

        TableSession? session = null;
        if (fromTable.CurrentSessionId.HasValue)
        {
            session = context.TableSessions.Find(fromTable.CurrentSessionId.Value);
        }
        if (session == null)
        {
            session = context.TableSessions.FirstOrDefault(s => s.TableId == fromTableId && s.Status == SessionStatus.Active);
        }
        if (session == null) return false;

        decimal fromRate = session.AppliedHourlyRate > 0 ? session.AppliedHourlyRate : (fromTable.TableType?.DefaultHourlyRate ?? 50000);
        decimal toRate = toTable.TableType?.DefaultHourlyRate ?? fromRate;
        var now = DateTime.Now;

        bool isCombo = string.Equals(session.PlayMode, "COMBO", StringComparison.OrdinalIgnoreCase);

        // 1. KIỂM TRA ĐỒNG GIÁ HAY LỆCH GIÁ BÀN
        if (isCombo || fromRate == toRate)
        {
            // TRƯỜNG HỢP 1: ĐỒNG GIÁ BÀN (VD: Bàn Lỗ 70k sang Bàn Lỗ khác 70k)
            // Giữ nguyên StartTime ban đầu, đếm tiếp tục từ thời gian bắt đầu, đơn giá bàn giữ nguyên
            session.TableId = toTableId;
            session.AppliedHourlyRate = toRate;
            // Không đổi session.StartTime! Thời gian cứ thế đếm tiếp tục từ lúc mở bàn ban đầu
            session.Note = string.IsNullOrEmpty(session.Note)
                ? $"Chuyển từ {fromTable.TableNumber} sang {toTable.TableNumber} (Đồng giá {toRate:N0} đ/h, cộng dồn tiếp tục)"
                : $"{session.Note} | Chuyển sang {toTable.TableNumber} (Đồng giá)";
        }
        else
        {
            // TRƯỜNG HỢP 2: LỆCH GIÁ BÀN (VD: từ 50k/h sang 80k/h)
            // Chốt tiền chặng tại bàn cũ, tính số phút tại bàn cũ và lưu thành segment
            int segmentMinutes = (int)Math.Max(1, Math.Round((now - session.StartTime).TotalMinutes));

            var fromSlots = context.TimeSlotPricings
                                   .Where(s => s.TableTypeId == fromTable.TableTypeId && s.IsActive)
                                   .ToList();

            decimal fromTableCost = BilliardPricingHelper.CalculateSessionCost(
                session.StartTime,
                now,
                fromRate,
                session.PlayMode,
                null,
                null,
                fromSlots
            );

            var segment = new TableSessionSegment
            {
                TableId = fromTable.TableId,
                TableNumber = fromTable.TableNumber,
                TableTypeName = fromTable.TableType?.TypeName ?? "Bàn cũ",
                HourlyRate = fromRate,
                StartTime = session.StartTime,
                EndTime = now,
                PlayMinutes = segmentMinutes,
                Cost = fromTableCost
            };

            var segments = session.GetSegments();
            segments.Add(segment);
            session.SetSegments(segments);

            // Tích lũy tiền giờ và phút của bàn cũ
            session.TimeCost += fromTableCost;
            session.AccumulatedPlayMinutes += segmentMinutes;

            // Bàn mới bắt đầu tính giờ từ đầu (từ thời điểm chuyển bàn) với đơn giá mới
            session.TableId = toTableId;
            session.AppliedHourlyRate = toRate;
            session.StartTime = now;
            session.Note = string.IsNullOrEmpty(session.Note)
                ? $"Chuyển từ {fromTable.TableNumber} ({fromRate:N0} đ/h) sang {toTable.TableNumber} ({toRate:N0} đ/h)"
                : $"{session.Note} | Chuyển từ {fromTable.TableNumber} sang {toTable.TableNumber}";
        }

        // 2. Cập nhật trạng thái bàn đích
        toTable.CurrentSessionId = session.SessionId;
        toTable.Status = TableStatus.Occupied;

        // 3. Giải phóng bàn cũ về trạng thái sẵn sàng
        fromTable.CurrentSessionId = null;
        fromTable.Status = TableStatus.Available;

        context.SaveChanges();

        _auditService.Log(userId, "TABLE_SWITCH", "Table", toTable.TableId.ToString(), new { From = fromTable.TableNumber }, new { To = toTable.TableNumber, Reason = reason });
        return true;
    }

    public bool UpdateTableStatus(int tableId, TableStatus newStatus, int userId)
    {
        using var context = _contextFactory.CreateDbContext();
        var table = context.Tables.Find(tableId);
        if (table == null) return false;

        // CHỐNG GIAN LẬN BÀN VÔ HÌNH: Không được chuyển bàn đang có phiên chơi Active về Available
        if (newStatus == TableStatus.Available)
        {
            bool hasActiveSession = context.TableSessions.Any(s => s.TableId == tableId && s.Status == SessionStatus.Active);
            if (hasActiveSession)
            {
                // Bàn đang có khách chơi hoạt động! Phải thanh toán hóa đơn hoặc hủy phiên có lý do
                return false;
            }
        }

        var oldStatus = table.Status;
        table.Status = newStatus;
        if (newStatus == TableStatus.Available)
        {
            table.CurrentSessionId = null;
        }

        context.SaveChanges();
        _auditService.Log(userId, "UPDATE_TABLE_STATUS", "Table", tableId.ToString(), new { OldStatus = oldStatus }, new { NewStatus = newStatus });
        return true;
    }

    public bool SaveFloor(Floor floor)
    {
        using var context = _contextFactory.CreateDbContext();
        if (floor.FloorId == 0)
        {
            context.Floors.Add(floor);
        }
        else
        {
            context.Floors.Update(floor);
        }
        return context.SaveChanges() > 0;
    }

    public bool DeleteFloor(int floorId)
    {
        using var context = _contextFactory.CreateDbContext();
        var floor = context.Floors.Include(f => f.Areas).FirstOrDefault(f => f.FloorId == floorId);
        if (floor == null) return false;
        if (floor.Areas.Any()) return false; // Không xóa nếu vẫn còn khu vực thuộc tầng này

        context.Floors.Remove(floor);
        return context.SaveChanges() > 0;
    }

    public bool SaveArea(Area area)
    {
        using var context = _contextFactory.CreateDbContext();
        if (area.AreaId == 0)
        {
            context.Areas.Add(area);
        }
        else
        {
            context.Areas.Update(area);
        }
        return context.SaveChanges() > 0;
    }

    public bool DeleteArea(int areaId)
    {
        using var context = _contextFactory.CreateDbContext();
        var area = context.Areas.Include(a => a.Tables).FirstOrDefault(a => a.AreaId == areaId);
        if (area == null) return false;
        if (area.Tables.Any()) return false; // Không xóa nếu vẫn còn bàn trong khu vực này

        context.Areas.Remove(area);
        return context.SaveChanges() > 0;
    }

    public bool SaveTable(Table table)
    {
        using var context = _contextFactory.CreateDbContext();
        if (table.TableId == 0)
        {
            context.Tables.Add(table);
        }
        else
        {
            context.Tables.Update(table);
        }
        return context.SaveChanges() > 0;
    }

    public bool DeleteTable(int tableId)
    {
        using var context = _contextFactory.CreateDbContext();
        var table = context.Tables.Include(t => t.Sessions).FirstOrDefault(t => t.TableId == tableId);
        if (table == null || table.Status == TableStatus.Occupied) return false;

        // Nếu bàn chưa từng có lịch sử phiên chơi nào -> Xóa hẳn khỏi DB
        if (!table.Sessions.Any())
        {
            context.Tables.Remove(table);
        }
        else
        {
            table.IsActive = false;
        }
        return context.SaveChanges() > 0;
    }

    public bool SaveTableType(TableType tableType)
    {
        using var context = _contextFactory.CreateDbContext();
        if (tableType.TableTypeId == 0)
        {
            context.TableTypes.Add(tableType);
        }
        else
        {
            context.TableTypes.Update(tableType);
        }
        return context.SaveChanges() > 0;
    }

    public bool DeleteTableType(int tableTypeId)
    {
        using var context = _contextFactory.CreateDbContext();
        var tt = context.TableTypes.Include(t => t.Tables).FirstOrDefault(t => t.TableTypeId == tableTypeId);
        if (tt == null) return false;
        if (tt.Tables.Any()) return false; // Không xóa nếu vẫn còn bàn thuộc loại này

        context.TableTypes.Remove(tt);
        return context.SaveChanges() > 0;
    }

    // --- QUẢN LÝ KHUNG GIỜ GIÁ (TIME-SLOT PRICING) ---
    public List<TimeSlotPricing> GetTimeSlots(int? tableTypeId = null)
    {
        using var context = _contextFactory.CreateDbContext();
        var query = context.TimeSlotPricings.Include(s => s.TableType).AsQueryable();
        if (tableTypeId.HasValue && tableTypeId.Value > 0)
        {
            query = query.Where(s => s.TableTypeId == tableTypeId.Value);
        }
        return query.ToList().OrderBy(s => s.TableTypeId).ThenBy(s => s.StartHour).ThenBy(s => s.StartMinute).ToList();
    }

    public bool SaveTimeSlot(TimeSlotPricing slot)
    {
        using var context = _contextFactory.CreateDbContext();
        if (slot.TimeSlotId == 0)
        {
            context.TimeSlotPricings.Add(slot);
        }
        else
        {
            context.TimeSlotPricings.Update(slot);
        }
        return context.SaveChanges() > 0;
    }

    public bool DeleteTimeSlot(int slotId)
    {
        using var context = _contextFactory.CreateDbContext();
        var slot = context.TimeSlotPricings.Find(slotId);
        if (slot == null) return false;
        context.TimeSlotPricings.Remove(slot);
        return context.SaveChanges() > 0;
    }

    // --- QUẢN LÝ GÓI COMBO BIDA ---
    public List<PlayCombo> GetCombos(int? tableTypeId = null, bool activeOnly = false)
    {
        using var context = _contextFactory.CreateDbContext();
        var query = context.PlayCombos
            .Include(c => c.TableType)
            .Include(c => c.Items)
                .ThenInclude(i => i.Product)
                    .ThenInclude(p => p.Category)
            .AsQueryable();

        if (activeOnly)
        {
            query = query.Where(c => c.IsActive);
        }
        if (tableTypeId.HasValue && tableTypeId.Value > 0)
        {
            query = query.Where(c => c.TableTypeId == null || c.TableTypeId == tableTypeId.Value);
        }
        return query.ToList().OrderBy(c => c.Price).ToList();
    }

    public bool SaveCombo(PlayCombo combo, IEnumerable<(int ProductId, int Quantity)>? items = null)
    {
        using var context = _contextFactory.CreateDbContext();
        PlayCombo? dbCombo = null;

        if (combo.ComboId == 0)
        {
            dbCombo = new PlayCombo();
            context.PlayCombos.Add(dbCombo);
        }
        else
        {
            dbCombo = context.PlayCombos
                .Include(c => c.Items)
                .FirstOrDefault(c => c.ComboId == combo.ComboId);
            if (dbCombo == null) return false;
        }

        dbCombo.ComboName = combo.ComboName;
        dbCombo.TableTypeId = combo.TableTypeId;
        dbCombo.Price = combo.Price;
        dbCombo.DurationMinutes = combo.DurationMinutes;
        dbCombo.Description = combo.Description;
        dbCombo.IsActive = combo.IsActive;

        // Cập nhật danh sách món tặng kèm PlayComboItem theo chuẩn 3NF
        if (items != null)
        {
            dbCombo.Items.Clear();

            var summaryList = new List<string>();
            foreach (var (productId, qty) in items)
            {
                if (qty <= 0) continue;
                var prod = context.Products.Find(productId);
                if (prod == null) continue;

                dbCombo.Items.Add(new PlayComboItem
                {
                    ProductId = productId,
                    Quantity = qty
                });
                summaryList.Add($"{qty}x {prod.ProductName}");
            }

            dbCombo.IncludedItems = summaryList.Any() ? string.Join(", ", summaryList) : (combo.IncludedItems ?? "Không có");
        }
        else
        {
            dbCombo.IncludedItems = combo.IncludedItems;
        }

        return context.SaveChanges() > 0;
    }

    public bool DeleteCombo(int comboId)
    {
        using var context = _contextFactory.CreateDbContext();
        var combo = context.PlayCombos.Find(comboId);
        if (combo == null) return false;
        context.PlayCombos.Remove(combo);
        return context.SaveChanges() > 0;
    }
}
