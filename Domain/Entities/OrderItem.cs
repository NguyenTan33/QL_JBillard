namespace QL_JBillard.Domain.Entities;

public class OrderItem
{
    public int OrderItemId { get; set; }
    public int SessionId { get; set; }
    public int ProductId { get; set; }
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }  // Giá bán tại thời điểm gọi món
    public decimal TotalAmount { get; set; }
    
    public int OrderedByUserId { get; set; }
    public DateTime OrderedAt { get; set; } = DateTime.Now;
    
    // Quản lý hoàn thành / phục vụ món
    public bool IsCompleted { get; set; } = false;
    public DateTime? CompletedAt { get; set; }
    public int? CompletedByUserId { get; set; }

    public bool IsCancelled { get; set; } = false;
    public string? CancellationReason { get; set; }
    public int? CancelledByUserId { get; set; }
    public DateTime? CancelledAt { get; set; }

    // Quản lý món thuộc gói Combo (Được tặng kèm / tính trọn gói)
    public bool IsComboItem { get; set; } = false;
    public string? ComboName { get; set; }

    // Navigation
    public virtual TableSession Session { get; set; } = null!;
    public virtual Product Product { get; set; } = null!;
    public virtual User OrderedByUser { get; set; } = null!;
    public virtual User? CompletedByUser { get; set; }
    public virtual User? CancelledByUser { get; set; }
}
