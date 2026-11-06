using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace TELEHEALTH_GROUP4_QT01.Areas.Doctor.Controllers;

/// <summary>
/// Trước đây là đăng nhập Session riêng cho bác sĩ (so sánh SHA-256, không giới hạn số lần thử).
/// Nay cả 3 vai trò dùng CHUNG trang đăng nhập /Account/Login; controller này chỉ còn chuyển hướng
/// để các đường dẫn cũ (/Doctor/Auth/Login) vẫn hoạt động.
/// </summary>
[Area("Doctor")]
[AllowAnonymous]
public class AuthController : Controller
{
    [HttpGet]
    public IActionResult Login(string? returnUrl) =>
        RedirectToAction("Login", "Account", new { area = "", returnUrl });

    // Đăng xuất phải là POST (có token CSRF) – dùng nút "Đăng xuất" (form POST tới /Account/Logout).
    [HttpGet]
    public IActionResult Logout() => RedirectToAction("Index", "Home", new { area = "" });
}

// Giữ lại class này vì view cũ Areas/Doctor/Views/Auth/Login.cshtml còn khai báo @model DoctorLoginVm.
// Có thể xoá cả view đó lẫn class này khi nhóm đã dọn dẹp.
public class DoctorLoginVm
{
    [Required(ErrorMessage = "Nhập email")][EmailAddress(ErrorMessage = "Email không hợp lệ")] public string Email { get; set; } = "";
    [Required(ErrorMessage = "Nhập mật khẩu")] public string Password { get; set; } = "";
    public string? ReturnUrl { get; set; }
}
