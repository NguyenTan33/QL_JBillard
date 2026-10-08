using System.Windows.Media.Imaging;

namespace QL_JBillard.Services.Interfaces;

public interface IVietQrService
{
    /// <summary>
    /// Tạo chuỗi Payload VietQR chuẩn EMVCo Napas 247
    /// </summary>
    string GenerateEmvCoPayload(string bankBin, string accountNo, decimal amount, string transferContent);

    /// <summary>
    /// Sinh hình ảnh mã QR (BitmapImage) hiển thị trực tiếp lên giao diện WPF
    /// </summary>
    BitmapImage GenerateQrBitmap(string payload);
}
