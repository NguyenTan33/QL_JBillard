using QL_JBillard.Domain.Entities;
using QL_JBillard.Domain.Enums;

namespace QL_JBillard.Services.Interfaces;

public interface ITableService
{
    List<Floor> GetAllFloors();
    List<Area> GetAllAreas();
    List<TableType> GetAllTableTypes();
    List<Table> GetAllTables();
    Table? GetTableById(int tableId);

    TableSession OpenTable(int tableId, int userId, int? customerId = null, string? note = null, string playMode = "HOURLY", int? comboId = null);
    bool SwitchTable(int fromTableId, int toTableId, int userId, string? reason = null);
    bool UpdateTableStatus(int tableId, TableStatus newStatus, int userId);

    // Quản lý cấu hình động Tầng, Khu vực, Bàn
    bool SaveFloor(Floor floor);
    bool DeleteFloor(int floorId);
    bool SaveArea(Area area);
    bool DeleteArea(int areaId);
    bool SaveTable(Table table);
    bool DeleteTable(int tableId);
    bool SaveTableType(TableType tableType);
    bool DeleteTableType(int tableTypeId);

    // Quản lý Khung Giờ Giá & Gói Combo
    List<TimeSlotPricing> GetTimeSlots(int? tableTypeId = null);
    bool SaveTimeSlot(TimeSlotPricing slot);
    bool DeleteTimeSlot(int slotId);

    List<PlayCombo> GetCombos(int? tableTypeId = null, bool activeOnly = false);
    bool SaveCombo(PlayCombo combo);
    bool DeleteCombo(int comboId);
}
