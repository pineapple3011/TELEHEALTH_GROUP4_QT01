namespace TELEHEALTH_GROUP4_QT01.Services
{
    /// <summary>
    /// Thông tin tài khoản nhận tiền – đọc từ cấu hình (section "Bank"), KHÔNG gán cứng trong code.
    /// Đặt giá trị thật bằng user-secrets (dev) hoặc biến môi trường Bank__AccountNo (production).
    /// </summary>
    public sealed class BankOptions
    {
        public const string SectionName = "Bank";

        public string BankId { get; set; } = "";
        public string AccountNo { get; set; } = "";
        public string AccountName { get; set; } = "";

        public bool IsConfigured =>
            !string.IsNullOrWhiteSpace(BankId) &&
            !string.IsNullOrWhiteSpace(AccountNo) &&
            !string.IsNullOrWhiteSpace(AccountName);
    }
}
