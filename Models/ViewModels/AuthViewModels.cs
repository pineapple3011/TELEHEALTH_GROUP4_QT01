using System.ComponentModel.DataAnnotations;

namespace TELEHEALTH_GROUP4_QT01.Models.ViewModels
{
    // Lưu ý: các ViewModel này CỐ TÌNH không có thuộc tính Role / IsActive / PatientId...
    // => kẻ tấn công không thể "over-post" để tự gán quyền (mass assignment).

    public class LoginVm
    {
        [Required(ErrorMessage = "Vui lòng nhập email")]
        [EmailAddress(ErrorMessage = "Email không hợp lệ")]
        [StringLength(100, ErrorMessage = "Email tối đa 100 ký tự")]
        public string Email { get; set; } = "";

        [Required(ErrorMessage = "Vui lòng nhập mật khẩu")]
        [StringLength(128, ErrorMessage = "Mật khẩu quá dài")]
        [DataType(DataType.Password)]
        public string Password { get; set; } = "";

        // Chỉ dùng sau khi qua Url.IsLocalUrl() => chống open redirect
        public string? ReturnUrl { get; set; }
    }

    public class RegisterVm
    {
        [Required(ErrorMessage = "Vui lòng nhập họ tên")]
        [StringLength(100, MinimumLength = 2, ErrorMessage = "Họ tên từ 2 đến 100 ký tự")]
        [RegularExpression(@"^[\p{L}][\p{L}\p{M}\s.'\-]*$", ErrorMessage = "Họ tên chỉ gồm chữ cái, khoảng trắng và . ' -")]
        public string FullName { get; set; } = "";

        [Required(ErrorMessage = "Vui lòng nhập email")]
        [EmailAddress(ErrorMessage = "Email không hợp lệ")]
        [StringLength(100, ErrorMessage = "Email tối đa 100 ký tự")]
        public string Email { get; set; } = "";

        [Required(ErrorMessage = "Vui lòng nhập số điện thoại")]
        [RegularExpression(@"^(0|\+84)[0-9]{9,10}$", ErrorMessage = "Số điện thoại không hợp lệ (vd: 0912345678)")]
        public string Phone { get; set; } = "";

        [StringLength(255, ErrorMessage = "Địa chỉ tối đa 255 ký tự")]
        public string? Address { get; set; }

        [RegularExpression(@"^(0|\+84)[0-9]{9,10}$", ErrorMessage = "Số điện thoại khẩn cấp không hợp lệ")]
        public string? EmergencyPhone { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập mật khẩu")]
        [StringLength(128, ErrorMessage = "Mật khẩu quá dài")]
        [DataType(DataType.Password)]
        public string Password { get; set; } = "";

        [Required(ErrorMessage = "Vui lòng nhập lại mật khẩu")]
        [Compare(nameof(Password), ErrorMessage = "Mật khẩu nhập lại không khớp")]
        [DataType(DataType.Password)]
        public string ConfirmPassword { get; set; } = "";
    }
}
