namespace TELEHEALTH_GROUP4_QT01.Models;

/// <summary>
/// Tài khoản đăng nhập. Mỗi tài khoản gắn với ĐÚNG MỘT hồ sơ (Patient, Doctor hoặc Admin).
/// Vai trò (Role) chỉ được gán ở phía máy chủ – form đăng ký công khai luôn tạo Patient.
/// </summary>
public partial class UserAccount
{
    public int UserId { get; set; }

    /// <summary>Luôn lưu chữ thường, có unique index.</summary>
    public string Email { get; set; } = null!;

    /// <summary>Chuỗi PBKDF2 (xem Security/PasswordService.cs). Không bao giờ lưu mật khẩu gốc.</summary>
    public string PasswordHash { get; set; } = null!;

    public string Role { get; set; } = null!;

    public int? PatientId { get; set; }

    public int? DoctorId { get; set; }

    public int? AdminId { get; set; }

    public bool IsActive { get; set; } = true;

    public int FailedLoginCount { get; set; }

    public DateTime? LockoutEndUtc { get; set; }

    /// <summary>Đổi giá trị này => mọi cookie đăng nhập cũ của tài khoản bị vô hiệu.</summary>
    public string SecurityStamp { get; set; } = Guid.NewGuid().ToString("N");

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime? LastLoginUtc { get; set; }

    public virtual Patient? Patient { get; set; }

    public virtual Doctor? Doctor { get; set; }

    public virtual Admin? Admin { get; set; }
}
