using System.IO;
using System.Text;
using System.Windows.Media.Imaging;
using QL_JBillard.Services.Interfaces;
using QRCoder;

namespace QL_JBillard.Services.Implementations;

public class VietQrService : IVietQrService
{
    public string GenerateEmvCoPayload(string bankBin, string accountNo, decimal amount, string transferContent)
    {
        // 1. Chuẩn hóa thông tin tài khoản ngân hàng (Tag 38)
        string sub00_Bin = FormatTlv("00", bankBin.Trim());
        string sub01_Acc = FormatTlv("01", accountNo.Trim());
        string sub01_Beneficiary = FormatTlv("01", sub00_Bin + sub01_Acc);
        string sub02_Service = FormatTlv("02", "QRIBFTTA"); // Napas 247 nhanh
        string sub00_Guid = FormatTlv("00", "A000000727");

        string tag38_Merchant = FormatTlv("38", sub00_Guid + sub01_Beneficiary + sub02_Service);

        // 2. Nội dung giao dịch (Tag 62 chuẩn Napas 247 EMVCo Tag 08: chữ, số và khoảng trắng - không dùng %20 URL encoding)
        string safeContent = RemoveDiacritics(transferContent).Trim();
        safeContent = System.Text.RegularExpressions.Regex.Replace(safeContent, @"[^a-zA-Z0-9 ]", " ").Trim();
        safeContent = System.Text.RegularExpressions.Regex.Replace(safeContent, @"\s+", " ");
        if (safeContent.Length > 25) safeContent = safeContent.Substring(0, 25).Trim();
        string tag62_Additional = FormatTlv("62", FormatTlv("08", safeContent));

        // 3. Ghép các thẻ dữ liệu EMVCo
        var sb = new StringBuilder();
        sb.Append(FormatTlv("00", "01"));                   // Format indicator
        sb.Append(FormatTlv("01", "12"));                   // 12 = Dynamic QR (có số tiền sẵn)
        sb.Append(tag38_Merchant);
        sb.Append(FormatTlv("53", "704"));                  // 704 = VND
        if (amount > 0)
        {
            sb.Append(FormatTlv("54", ((long)amount).ToString())); // Số tiền
        }
        sb.Append(FormatTlv("58", "VN"));                   // Country code
        sb.Append(tag62_Additional);
        sb.Append("6304");                                  // Tag CRC16

        // 4. Tính toán mã kiểm tra CRC-16 (CCITT-FALSE: Init 0xFFFF, Poly 0x1021)
        string rawWithoutCrc = sb.ToString();
        ushort crcValue = ComputeCrc16Ccitt(Encoding.ASCII.GetBytes(rawWithoutCrc));
        string crcHex = crcValue.ToString("X4");

        return rawWithoutCrc + crcHex;
    }

    public BitmapImage GenerateQrBitmap(string payload)
    {
        using var qrGenerator = new QRCodeGenerator();
        using var qrCodeData = qrGenerator.CreateQrCode(payload, QRCodeGenerator.ECCLevel.Q);
        using var qrCode = new PngByteQRCode(qrCodeData);
        byte[] qrCodeBytes = qrCode.GetGraphic(15);

        var bitmap = new BitmapImage();
        using var stream = new MemoryStream(qrCodeBytes);
        bitmap.BeginInit();
        bitmap.CacheOption = BitmapCacheOption.OnLoad;
        bitmap.StreamSource = stream;
        bitmap.EndInit();
        bitmap.Freeze();

        return bitmap;
    }

    private static string FormatTlv(string tag, string value)
    {
        return $"{tag}{value.Length:D2}{value}";
    }

    private static ushort ComputeCrc16Ccitt(byte[] bytes)
    {
        ushort crc = 0xFFFF;
        const ushort polynomial = 0x1021;

        foreach (byte b in bytes)
        {
            for (int i = 0; i < 8; i++)
            {
                bool bit = ((b >> (7 - i)) & 1) == 1;
                bool c15 = ((crc >> 15) & 1) == 1;
                crc <<= 1;
                if (c15 ^ bit) crc ^= polynomial;
            }
        }
        return crc;
    }

    private static string RemoveDiacritics(string text)
    {
        var normalizedString = text.Normalize(NormalizationForm.FormD);
        var stringBuilder = new StringBuilder();

        foreach (var c in normalizedString)
        {
            var unicodeCategory = System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c);
            if (unicodeCategory != System.Globalization.UnicodeCategory.NonSpacingMark)
            {
                stringBuilder.Append(c);
            }
        }

        return stringBuilder.ToString().Normalize(NormalizationForm.FormC);
    }
}
