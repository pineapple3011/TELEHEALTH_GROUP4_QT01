using System.ComponentModel.DataAnnotations;

namespace TELEHEALTH_GROUP4_QT01.Models;

// File riêng (partial) bổ sung 3 cột phục vụ bảo mật cho bảng Accounts có sẵn.
// Khi scaffold lại DB, file Account.cs bị ghi đè nhưng file này vẫn giữ nguyên.
// Cần chạy Database/01_Accounts_Auth.sql để thêm 3 cột tương ứng vào bảng.
public partial class Account
{
    /// <summary>Số lần đăng nhập sai liên tiếp.</summary>
    public int FailedLoginCount { get; set; }

    /// <summary>Tài khoản bị khoá tạm đến thời điểm này (UTC). Null = không bị khoá.</summary>
    public DateTime? LockoutEndUtc { get; set; }

    /// <summary>Đổi giá trị này => mọi cookie đăng nhập cũ của tài khoản bị vô hiệu.</summary>
    [MaxLength(64)]
    public string SecurityStamp { get; set; } = Guid.NewGuid().ToString("N");
}
