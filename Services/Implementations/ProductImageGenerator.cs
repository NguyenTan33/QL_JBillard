using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using QL_JBillard.Data;
using QL_JBillard.Domain.Entities;

namespace QL_JBillard.Services.Implementations;

public static class ProductImageGenerator
{
    private static readonly Dictionary<string, (string Icon, Color BgStart, Color BgEnd, string Subtitle)> ProductStyles = new()
    {
        // Đồ uống lon & chai
        ["STING"] = ("🥤", Color.FromRgb(225, 29, 72), Color.FromRgb(159, 18, 57), "STING DÂU ĐỎ"),
        ["REDBULL"] = ("⚡", Color.FromRgb(234, 179, 8), Color.FromRgb(202, 138, 4), "BÒ HÚC THÁI"),
        ["LAVIE"] = ("💧", Color.FromRgb(14, 165, 233), Color.FromRgb(2, 132, 199), "KHOÁNG THIÊN NHIÊN"),
        ["COCA"] = ("🥤", Color.FromRgb(220, 38, 38), Color.FromRgb(185, 28, 28), "COCA COLA"),

        // Cà phê & Pha chế
        ["CF_DEN"] = ("☕", Color.FromRgb(68, 64, 60), Color.FromRgb(41, 37, 36), "CÀ PHÊ ĐEN ĐÁ"),
        ["CF_SUA"] = ("🧋", Color.FromRgb(180, 83, 9), Color.FromRgb(120, 53, 15), "CÀ PHÊ SỮA ĐÁ"),
        ["CF_MUOI"] = ("☕", Color.FromRgb(146, 64, 14), Color.FromRgb(113, 63, 18), "CÀ PHÊ MUỐI"),
        ["TRA_DAO"] = ("🍹", Color.FromRgb(249, 115, 22), Color.FromRgb(194, 65, 12), "TRÀ ĐÀO CAM SẢ"),

        // Bia & Cồn
        ["HEINEKEN"] = ("🍺", Color.FromRgb(22, 101, 52), Color.FromRgb(20, 83, 45), "HEINEKEN SILVER"),
        ["TIGER_CRYSTAL"] = ("🍺", Color.FromRgb(30, 64, 175), Color.FromRgb(30, 58, 138), "TIGER CRYSTAL"),

        // Đồ ăn nhanh
        ["MI_BO_TRUNG"] = ("🍜", Color.FromRgb(217, 119, 6), Color.FromRgb(180, 83, 9), "MÌ BÒ TRỨNG ỐP"),
        ["KHO_BO"] = ("🥩", Color.FromRgb(190, 24, 93), Color.FromRgb(136, 19, 55), "KHÔ BÒ VẮT CHANH"),
        ["HUONG_DUONG"] = ("🌻", Color.FromRgb(202, 138, 4), Color.FromRgb(161, 98, 7), "HƯỚNG DƯƠNG VỊ"),
        ["KHAN_LANH"] = ("❄️", Color.FromRgb(13, 148, 136), Color.FromRgb(15, 118, 110), "KHĂN LẠNH CAO CẤP"),

        // Phụ kiện bida
        ["GANG_TAY"] = ("🧤", Color.FromRgb(71, 85, 105), Color.FromRgb(51, 65, 85), "GĂNG TAY CUETEC"),
        ["LO_XANH"] = ("🔷", Color.FromRgb(37, 99, 235), Color.FromRgb(29, 78, 216), "LƠ XANH MASTER"),
        ["DAU_CO"] = ("🎱", Color.FromRgb(30, 41, 59), Color.FromRgb(15, 23, 42), "ĐẦU CƠ KAMUI BLACK")
    };

    public static string EnsureDefaultProductImages(AppDbContext context)
    {
        string outputFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "AppData", "Images", "Products");
        if (!Directory.Exists(outputFolder))
        {
            Directory.CreateDirectory(outputFolder);
        }

        var products = context.Products.ToList();
        bool hasChanges = false;

        foreach (var p in products)
        {
            string expectedFile = Path.Combine(outputFolder, $"{p.ProductCode.ToUpperInvariant()}.png");

            // Nếu file ảnh chưa có trên đĩa -> render file ảnh
            if (!File.Exists(expectedFile))
            {
                RenderProductImage(p, expectedFile);
            }

            // Gán đường dẫn vào DB nếu chưa có hoặc khác file
            if (string.IsNullOrWhiteSpace(p.ImagePath) || !File.Exists(p.ImagePath))
            {
                p.ImagePath = expectedFile;
                hasChanges = true;
            }
        }

        if (hasChanges)
        {
            context.SaveChanges();
        }

        return outputFolder;
    }

    public static void RenderProductImage(Product product, string destinationPath)
    {
        int width = 280;
        int height = 280;

        string code = product.ProductCode?.ToUpperInvariant() ?? "PRODUCT";
        var (icon, startColor, endColor, subtitle) = ProductStyles.TryGetValue(code, out var style)
            ? style
            : ("🍽️", Color.FromRgb(59, 130, 246), Color.FromRgb(30, 64, 175), product.ProductName);

        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            // Nền Gradient thẻ card bo góc
            var bgBrush = new LinearGradientBrush(startColor, endColor, new Point(0, 0), new Point(1, 1));
            dc.DrawRoundedRectangle(bgBrush, null, new Rect(0, 0, width, height), 24, 24);

            // Vòng tròn ánh sáng mờ phía sau icon
            var glowBrush = new RadialGradientBrush(Color.FromArgb(90, 255, 255, 255), Color.FromArgb(0, 255, 255, 255));
            dc.DrawEllipse(glowBrush, null, new Point(width / 2.0, 105), 85, 85);

            // Icon minh họa lớn chính giữa
            var iconTypeface = new Typeface(new FontFamily("Segoe UI Emoji, Segoe UI Symbol, Arial"), FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);
            var iconText = new FormattedText(
                icon,
                CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight,
                iconTypeface,
                78,
                Brushes.White,
                VisualTreeHelper.GetDpi(visual).PixelsPerDip
            );
            dc.DrawText(iconText, new Point((width - iconText.Width) / 2.0, 60));

            // Thanh đen mờ phía chân card để tôn chữ
            var footerBrush = new SolidColorBrush(Color.FromArgb(170, 0, 0, 0));
            dc.DrawRoundedRectangle(footerBrush, null, new Rect(10, 195, width - 20, 75), 14, 14);

            // Tên viết tắt / Subtitle
            var titleTypeface = new Typeface(new FontFamily("Segoe UI, Tahoma"), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal);
            string displayTitle = !string.IsNullOrWhiteSpace(subtitle) ? subtitle : product.ProductName;
            if (displayTitle.Length > 22) displayTitle = displayTitle.Substring(0, 20) + "...";

            var titleText = new FormattedText(
                displayTitle,
                CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight,
                titleTypeface,
                14,
                Brushes.White,
                VisualTreeHelper.GetDpi(visual).PixelsPerDip
            );
            dc.DrawText(titleText, new Point((width - titleText.Width) / 2.0, 208));

            // Giá bán dạng Badge
            var priceTypeface = new Typeface(new FontFamily("Segoe UI, Tahoma"), FontStyles.Normal, FontWeights.ExtraBold, FontStretches.Normal);
            var priceText = new FormattedText(
                $"{product.SellingPrice:N0} đ",
                CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight,
                priceTypeface,
                16,
                new SolidColorBrush(Color.FromRgb(254, 240, 138)), // Vàng sáng
                VisualTreeHelper.GetDpi(visual).PixelsPerDip
            );
            dc.DrawText(priceText, new Point((width - priceText.Width) / 2.0, 234));
        }

        var rtb = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
        rtb.Render(visual);

        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(rtb));

        string? dir = Path.GetDirectoryName(destinationPath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }

        using var fs = new FileStream(destinationPath, FileMode.Create, FileAccess.Write);
        encoder.Save(fs);
    }
}
