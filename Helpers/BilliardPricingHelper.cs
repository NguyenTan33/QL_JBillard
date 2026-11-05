using System;
using System.Collections.Generic;
using System.Linq;
using QL_JBillard.Domain.Entities;

namespace QL_JBillard.Helpers;

public static class BilliardPricingHelper
{
    /// <summary>
    /// Làm tròn tiền chơi bida theo quy tắc thực tế:
    /// Dưới 600đ làm tròn xuống hàng nghìn, từ 600đ trở lên làm tròn lên hàng nghìn.
    /// Giờ chơi tăng tuần tự tự nhiên theo thời gian, bắt đầu từ 0đ.
    /// Ví dụ:
    /// - 0đ -> 0đ
    /// - 590đ -> 0đ (dưới 600đ làm tròn xuống)
    /// - 600đ -> 1.000đ (từ 600đ làm tròn lên)
    /// - 15.500đ -> 15.000đ
    /// - 15.600đ -> 16.000đ
    /// </summary>
    public static decimal RoundBilliardAmount(decimal rawCost)
    {
        if (rawCost <= 0) return 0m;
        decimal baseThousand = Math.Floor(rawCost / 1000m) * 1000m;
        decimal remainder = rawCost - baseThousand;
        return remainder >= 600m ? baseThousand + 1000m : baseThousand;
    }

    /// <summary>
    /// Tính tiền giờ theo Khung Giờ Giá (Time-Slot Pricing):
    /// Khi bàn chơi qua nhiều khung giờ (ví dụ mở lúc 8h sáng - 12h trưa giá 50k, sau 12h tính giá khác),
    /// thuật toán sẽ phân rã thời gian chính xác từng phút theo từng khung giờ áp dụng.
    /// </summary>
    public static decimal CalculateTimeSlotCost(
        DateTime startTime, 
        DateTime endTime, 
        decimal defaultHourlyRate, 
        IEnumerable<TimeSlotPricing>? timeSlots)
    {
        if (endTime <= startTime) return 0m;

        var activeSlots = timeSlots?.Where(s => s.IsActive).ToList();
        if (activeSlots == null || !activeSlots.Any())
        {
            // Nếu chưa cài đặt khung giờ giá, tính theo đơn giá giờ chuẩn của bàn
            double totalHours = (endTime - startTime).TotalHours;
            decimal rawCost = (decimal)totalHours * defaultHourlyRate;
            return RoundBilliardAmount(rawCost);
        }

        int totalMinutes = (int)Math.Max(1, (endTime - startTime).TotalMinutes);
        decimal totalRawCost = 0m;

        // ƯU TIÊN KHUNG GIỜ GIÁ (Time-Slot Priority):
        // Khung giờ cụ thể (phạm vi ngắn hơn, ví dụ Cao điểm tối 18h-22h: 4 tiếng) 
        // phải luôn được ưu tiên áp dụng trước khung giờ rộng bao quát (ví dụ Cả ngày 0h-24h: 24 tiếng).
        var prioritizedSlots = activeSlots
            .OrderBy(s =>
            {
                int start = s.StartHour * 60 + s.StartMinute;
                int end = (s.EndHour >= 24 ? 24 : s.EndHour) * 60 + s.EndMinute;
                return end > start ? (end - start) : (1440 - start + end);
            })
            .ThenByDescending(s => s.HourlyRate)
            .ToList();

        for (int i = 0; i < totalMinutes; i++)
        {
            DateTime currentMinute = startTime.AddMinutes(i);
            int currentDayMinute = currentMinute.Hour * 60 + currentMinute.Minute;

            decimal minuteRate = defaultHourlyRate / 60m;

            foreach (var slot in prioritizedSlots)
            {
                int slotStart = slot.StartHour * 60 + slot.StartMinute;
                int slotEnd = (slot.EndHour >= 24 ? 24 : slot.EndHour) * 60 + slot.EndMinute;

                bool isMatch = false;
                if (slotEnd > slotStart)
                {
                    // Khung giờ thông thường trong ngày (ví dụ 08:00 - 12:00 hoặc 12:00 - 18:00)
                    isMatch = currentDayMinute >= slotStart && currentDayMinute < slotEnd;
                }
                else
                {
                    // Khung giờ qua đêm (ví dụ 22:00 - 06:00 hoặc 18:00 - 02:00)
                    isMatch = currentDayMinute >= slotStart || currentDayMinute < slotEnd;
                }

                if (isMatch)
                {
                    minuteRate = slot.HourlyRate / 60m;
                    break;
                }
            }

            totalRawCost += minuteRate;
        }

        return RoundBilliardAmount(totalRawCost);
    }

    /// <summary>
    /// Tính toàn diện tiền giờ cho phiên chơi hỗ trợ cả Chơi Theo Giờ và Gói Combo.
    /// Đối với Gói Combo:
    /// - Trong thời lượng combo (ví dụ 180 phút = 3 giờ): Tính đúng giá trọn gói combo (ví dụ 150k).
    /// - Nếu chơi vượt quá giờ quy định của combo: Phần thời gian vượt tính theo đơn giá giờ/khung giờ phụ trội.
    /// </summary>
    public static decimal CalculateSessionCost(
        DateTime startTime,
        DateTime endTime,
        decimal defaultHourlyRate,
        string playMode,
        decimal? comboPrice,
        int? comboDurationMinutes,
        IEnumerable<TimeSlotPricing>? timeSlots)
    {
        if (endTime <= startTime) return 0m;

        if (string.Equals(playMode, "COMBO", StringComparison.OrdinalIgnoreCase) && comboPrice.HasValue && comboPrice.Value > 0)
        {
            int durationMinutes = (int)(endTime - startTime).TotalMinutes;
            int packageMinutes = comboDurationMinutes ?? 180;

            if (durationMinutes <= packageMinutes)
            {
                return comboPrice.Value;
            }

            // Chơi vượt quá giờ của Combo: Tính giá gói + tiền giờ phụ trội từ phút kết thúc gói
            DateTime overtimeStart = startTime.AddMinutes(packageMinutes);
            decimal overtimeCost = CalculateTimeSlotCost(overtimeStart, endTime, defaultHourlyRate, timeSlots);
            return comboPrice.Value + overtimeCost;
        }

        return CalculateTimeSlotCost(startTime, endTime, defaultHourlyRate, timeSlots);
    }
}
