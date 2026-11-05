namespace TELEHEALTH_GROUP4_QT01.Security
{
    /// <summary>Chính sách mật khẩu kiểm tra ở PHÍA MÁY CHỦ (không tin kiểm tra ở JS).</summary>
    public static class PasswordPolicy
    {
        public const int MinLength = 10;
        public const int MaxLength = 128;   // giới hạn trên để chống DoS bằng mật khẩu cực dài

        private static readonly HashSet<string> Common = new(StringComparer.OrdinalIgnoreCase)
        {
            "password123", "password1234", "1234567890", "12345678910", "123456789a",
            "qwertyuiop1", "qwerty12345", "matkhau123", "matkhau1234", "admin12345",
            "iloveyou123", "abc1234567", "a123456789", "1q2w3e4r5t"
        };

        /// <summary>Trả về thông báo lỗi, hoặc null nếu mật khẩu hợp lệ.</summary>
        public static string? Validate(string? password, string? email = null)
        {
            if (string.IsNullOrEmpty(password) || password.Length < MinLength)
                return $"Mật khẩu phải có ít nhất {MinLength} ký tự.";
            if (password.Length > MaxLength)
                return $"Mật khẩu tối đa {MaxLength} ký tự.";
            if (!password.Any(char.IsLetter) || !password.Any(char.IsDigit))
                return "Mật khẩu phải gồm cả chữ và số.";
            if (!string.IsNullOrEmpty(email) && password.Equals(email, StringComparison.OrdinalIgnoreCase))
                return "Mật khẩu không được trùng với email.";
            if (Common.Contains(password))
                return "Mật khẩu quá phổ biến, vui lòng chọn mật khẩu khác.";
            return null;
        }
    }
}
