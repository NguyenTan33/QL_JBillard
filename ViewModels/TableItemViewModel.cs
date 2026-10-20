using CommunityToolkit.Mvvm.ComponentModel;
using QL_JBillard.Domain.Entities;
using QL_JBillard.Domain.Enums;

using QL_JBillard.Helpers;

namespace QL_JBillard.ViewModels;

public partial class TableItemViewModel : ObservableObject
{
    public Table Model { get; }

    public int TableId => Model.TableId;
    public string TableNumber => Model.TableNumber;
    public string AreaName => Model.Area?.AreaName ?? "";
    public string FloorName => Model.Area?.Floor?.FloorName ?? "Tầng trệt";
    public string TableTypeName => Model.TableType?.TypeName ?? "";
    public int TableTypeId => Model.TableTypeId;
    public decimal DefaultHourlyRate => Model.TableType?.DefaultHourlyRate ?? 0;
    public string ColorCode => Model.TableType?.ColorCode ?? "#10B981";

    [ObservableProperty]
    private TableStatus _status;

    [ObservableProperty]
    private DateTime? _startTime;

    [ObservableProperty]
    private TimeSpan _elapsedTime = TimeSpan.Zero;

    [ObservableProperty]
    private decimal _estimatedTimeCost = 0;

    [ObservableProperty]
    private decimal _serviceCost = 0;

    [ObservableProperty]
    private decimal _estimatedTotalCost = 0;

    [ObservableProperty]
    private int _orderCount = 0;

    [ObservableProperty]
    private string? _customerName;

    [ObservableProperty]
    private string? _customerPhone;

    public bool IsAvailable => Status == TableStatus.Available;
    public bool IsOccupied => Status == TableStatus.Occupied;
    public bool IsReserved => Status == TableStatus.Reserved;
    public bool IsMaintenance => Status == TableStatus.Maintenance;

    public string PlayMode => Model.CurrentSession?.PlayMode ?? "HOURLY";
    public bool IsComboMode => string.Equals(PlayMode, "COMBO", StringComparison.OrdinalIgnoreCase);
    public string? ComboName => Model.CurrentSession?.ComboName;
    public decimal? ComboPrice => Model.CurrentSession?.ComboPrice;
    public int? ComboDurationMinutes => Model.CurrentSession?.ComboDurationMinutes;
    public string ComboBadgeText => !string.IsNullOrEmpty(ComboName) ? $"🎁 {ComboName}" : string.Empty;

    public TableItemViewModel(Table table)
    {
        Model = table;
        Status = table.Status;
        UpdateFromModel();
    }

    public void UpdateFromModel()
    {
        Status = Model.Status;
        if (Model.Status == TableStatus.Occupied)
        {
            StartTime = Model.CurrentSession?.StartTime ?? DateTime.Now;
            CustomerName = Model.CurrentSession?.Customer?.FullName;
            CustomerPhone = Model.CurrentSession?.Customer?.PhoneNumber;

            var completedOrders = Model.CurrentSession?.OrderItems?.Where(o => o.IsCompleted && !o.IsCancelled).ToList();
            OrderCount = completedOrders?.Sum(o => o.Quantity) ?? 0;
            ServiceCost = completedOrders?.Sum(o => o.TotalAmount) ?? 0;

            TickTimer();
        }
        else
        {
            StartTime = null;
            ElapsedTime = TimeSpan.Zero;
            EstimatedTimeCost = 0;
            ServiceCost = 0;
            EstimatedTotalCost = 0;
            OrderCount = 0;
            CustomerName = null;
            CustomerPhone = null;
        }

        OnPropertyChanged(nameof(IsAvailable));
        OnPropertyChanged(nameof(IsOccupied));
        OnPropertyChanged(nameof(IsReserved));
        OnPropertyChanged(nameof(IsMaintenance));
        OnPropertyChanged(nameof(PlayMode));
        OnPropertyChanged(nameof(IsComboMode));
        OnPropertyChanged(nameof(ComboName));
        OnPropertyChanged(nameof(ComboPrice));
        OnPropertyChanged(nameof(ComboBadgeText));
    }

    public void TickTimer()
    {
        if (Status == TableStatus.Occupied)
        {
            if (!StartTime.HasValue)
            {
                StartTime = Model.CurrentSession?.StartTime ?? DateTime.Now;
            }
            var now = DateTime.Now;
            ElapsedTime = now >= StartTime.Value ? now - StartTime.Value : TimeSpan.Zero;

            decimal hourlyRate = Model.CurrentSession?.AppliedHourlyRate ?? DefaultHourlyRate;

            // Tính tiền giờ hỗ trợ Gói Combo hoặc Chơi theo giờ
            EstimatedTimeCost = BilliardPricingHelper.CalculateSessionCost(
                StartTime.Value,
                now,
                hourlyRate,
                Model.CurrentSession?.PlayMode ?? "HOURLY",
                Model.CurrentSession?.ComboPrice,
                Model.CurrentSession?.ComboDurationMinutes,
                null
            );

            EstimatedTotalCost = EstimatedTimeCost + ServiceCost;
        }
    }
}
