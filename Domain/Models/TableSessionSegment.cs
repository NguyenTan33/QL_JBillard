using System;

namespace QL_JBillard.Domain.Models;

/// <summary>
/// Đại diện cho một chặng chơi tại một bàn cụ thể trong phiên chơi
/// Hỗ trợ trường hợp khách chuyển bàn lệch giá (VD: từ bàn thường 50k sang phòng VIP 80k)
/// </summary>
public class TableSessionSegment
{
    public int TableId { get; set; }
    public string TableNumber { get; set; } = string.Empty;
    public string TableTypeName { get; set; } = string.Empty;
    public decimal HourlyRate { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public int PlayMinutes { get; set; }
    public decimal Cost { get; set; }

    public string DurationFormatted
    {
        get
        {
            int h = PlayMinutes / 60;
            int m = PlayMinutes % 60;
            if (h > 0) return $"{h}h {m:D2}p ({StartTime:HH:mm} - {EndTime:HH:mm})";
            return $"{m} phút ({StartTime:HH:mm} - {EndTime:HH:mm})";
        }
    }
}
