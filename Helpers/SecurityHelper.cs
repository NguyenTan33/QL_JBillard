using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace QL_JBillard.Helpers;

public static class SecurityHelper
{
    // Cố định một khóa Salt hệ thống kết hợp với Salt ngẫu nhiên từng User
    private const string SystemPepper = "JBillard@2026_SecureCore_SaltPepper#99";

    /// <summary>
    /// Tạo chuỗi Salt ngẫu nhiên 128-bit
    /// </summary>
    public static string GenerateSalt()
    {
        byte[] saltBytes = new byte[16];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(saltBytes);
        return Convert.ToBase64String(saltBytes);
    }

    /// <summary>
    /// Băm mật khẩu bằng thuật toán chuẩn PBKDF2 (100,000 vòng lặp)
    /// </summary>
    public static string HashPassword(string password, string salt)
    {
        byte[] saltBytes = Convert.FromBase64String(salt);
        byte[] pepperBytes = Encoding.UTF8.GetBytes(SystemPepper);

        // Kết hợp Salt + Pepper
        byte[] combinedSalt = new byte[saltBytes.Length + pepperBytes.Length];
        Buffer.BlockCopy(saltBytes, 0, combinedSalt, 0, saltBytes.Length);
        Buffer.BlockCopy(pepperBytes, 0, combinedSalt, saltBytes.Length, pepperBytes.Length);

        using var pbkdf2 = new Rfc2898DeriveBytes(password, combinedSalt, 100_000, HashAlgorithmName.SHA256);
        byte[] hash = pbkdf2.GetBytes(32);
        return Convert.ToBase64String(hash);
    }

    /// <summary>
    /// Kiểm tra mật khẩu nhập vào khớp với bản băm
    /// </summary>
    public static bool VerifyPassword(string inputPassword, string storedHash, string storedSalt)
    {
        string computedHash = HashPassword(inputPassword, storedSalt);
        return CryptographicOperations.FixedTimeEquals(
            Convert.FromBase64String(computedHash),
            Convert.FromBase64String(storedHash)
        );
    }

    /// <summary>
    /// Tính mã băm SHA-256 chuỗi văn bản
    /// </summary>
    public static string ComputeSha256(string rawData)
    {
        using var sha256 = SHA256.Create();
        byte[] bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(rawData + SystemPepper));
        return Convert.ToHexString(bytes);
    }

    /// <summary>
    /// Tính mã băm SHA-256 kiểm tra toàn vẹn file
    /// </summary>
    public static string ComputeFileSha256(string filePath)
    {
        if (!File.Exists(filePath)) return string.Empty;
        using var sha256 = SHA256.Create();
        using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        byte[] hash = sha256.ComputeHash(stream);
        return Convert.ToHexString(hash);
    }

    /// <summary>
    /// Mã hóa file bằng thuật toán AES-256 CBC
    /// </summary>
    public static void EncryptFile(string inputFilePath, string outputEncryptedPath, string encryptionKey)
    {
        byte[] keyBytes = SHA256.HashData(Encoding.UTF8.GetBytes(encryptionKey));
        byte[] iv = new byte[16];
        using (var rng = RandomNumberGenerator.Create())
        {
            rng.GetBytes(iv);
        }

        using var aes = Aes.Create();
        aes.Key = keyBytes;
        aes.IV = iv;

        using var outFs = new FileStream(outputEncryptedPath, FileMode.Create, FileAccess.Write);
        // Ghi IV vào đầu file mã hóa
        outFs.Write(iv, 0, iv.Length);

        using var cryptoStream = new CryptoStream(outFs, aes.CreateEncryptor(), CryptoStreamMode.Write);
        using var inFs = new FileStream(inputFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        inFs.CopyTo(cryptoStream);
    }

    /// <summary>
    /// Giải mã file AES-256 CBC
    /// </summary>
    public static void DecryptFile(string encryptedFilePath, string outputDecryptedPath, string encryptionKey)
    {
        byte[] keyBytes = SHA256.HashData(Encoding.UTF8.GetBytes(encryptionKey));

        using var inFs = new FileStream(encryptedFilePath, FileMode.Open, FileAccess.Read);
        byte[] iv = new byte[16];
        inFs.ReadExactly(iv, 0, iv.Length);

        using var aes = Aes.Create();
        aes.Key = keyBytes;
        aes.IV = iv;

        using var cryptoStream = new CryptoStream(inFs, aes.CreateDecryptor(), CryptoStreamMode.Read);
        using var outFs = new FileStream(outputDecryptedPath, FileMode.Create, FileAccess.Write);
        cryptoStream.CopyTo(outFs);
    }
}
