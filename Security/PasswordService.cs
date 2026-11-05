using System.Security.Cryptography;

namespace TELEHEALTH_GROUP4_QT01.Security
{
    public enum PasswordVerifyResult { Failed, Success, SuccessRehashNeeded }

    public interface IPasswordService
    {
        string Hash(string password);
        PasswordVerifyResult Verify(string storedHash, string password);

        /// <summary>
        /// Tốn thời gian tương đương một lần Verify – dùng khi email không tồn tại
        /// (chống dò tài khoản qua thời gian phản hồi).
        /// </summary>
        void BurnTime(string password);
    }

    /// <summary>
    /// Băm mật khẩu bằng PBKDF2-HMAC-SHA256 (có sẵn trong .NET, không cần thêm NuGet).
    /// Định dạng lưu: PBKDF2-SHA256$[số vòng lặp]$[salt base64]$[hash base64]
    /// Lưu cả số vòng lặp trong chuỗi => sau này tăng độ khó mà vẫn đăng nhập được tài khoản cũ
    /// (Verify trả về SuccessRehashNeeded để tự nâng cấp hash).
    /// </summary>
    public sealed class Pbkdf2PasswordService : IPasswordService
    {
        private const string Prefix = "PBKDF2-SHA256";
        private const int Iterations = 600_000;   // khuyến nghị OWASP cho PBKDF2-HMAC-SHA256
        private const int SaltSize = 16;          // 128 bit
        private const int KeySize = 32;           // 256 bit

        private static readonly string DummyHash = HashCore("dummy-password-for-timing-only");

        public string Hash(string password) => HashCore(password);

        public PasswordVerifyResult Verify(string storedHash, string password)
        {
            if (string.IsNullOrEmpty(storedHash) || password is null) return PasswordVerifyResult.Failed;

            var parts = storedHash.Split('$');
            if (parts.Length != 4 || parts[0] != Prefix) return PasswordVerifyResult.Failed;
            if (!int.TryParse(parts[1], out var iterations) || iterations < 1 || iterations > 10_000_000)
                return PasswordVerifyResult.Failed;

            byte[] salt, expected;
            try
            {
                salt = Convert.FromBase64String(parts[2]);
                expected = Convert.FromBase64String(parts[3]);
            }
            catch (FormatException)
            {
                return PasswordVerifyResult.Failed;
            }

            var actual = Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, HashAlgorithmName.SHA256, expected.Length);

            // So sánh thời gian cố định, không để lộ vị trí byte đầu tiên khác nhau
            if (!CryptographicOperations.FixedTimeEquals(actual, expected))
                return PasswordVerifyResult.Failed;

            return iterations < Iterations
                ? PasswordVerifyResult.SuccessRehashNeeded
                : PasswordVerifyResult.Success;
        }

        public void BurnTime(string password) => Verify(DummyHash, password ?? "");

        private static string HashCore(string password)
        {
            var salt = RandomNumberGenerator.GetBytes(SaltSize);   // salt ngẫu nhiên riêng cho từng người dùng
            var key = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, HashAlgorithmName.SHA256, KeySize);
            return $"{Prefix}${Iterations}${Convert.ToBase64String(salt)}${Convert.ToBase64String(key)}";
        }
    }
}
