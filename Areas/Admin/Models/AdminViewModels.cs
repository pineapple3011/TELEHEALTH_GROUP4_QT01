using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace TELEHEALTH_GROUP4_QT01.Areas.Admin.Models
{
    // =============== TÀI KHOẢN: BỆNH NHÂN ===============
    public class PatientRowVm
    {
        public int PatientId { get; set; }
        public string FullName { get; set; } = "";
        public string Email { get; set; } = "";
        public string Phone { get; set; } = "";
        public string? Address { get; set; }
        public int AppointmentCount { get; set; }
        public bool HasAccount { get; set; }
        public bool IsActive { get; set; }
    }

    public class PatientListVm
    {
        public string? Q { get; set; }
        public List<PatientRowVm> Items { get; set; } = new();
    }

    public class PatientFormVm
    {
        public int PatientId { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập họ tên"), StringLength(100)]
        [Display(Name = "Họ và tên")]
        public string FullName { get; set; } = "";

        [Required(ErrorMessage = "Vui lòng nhập email"), EmailAddress(ErrorMessage = "Email không hợp lệ"), StringLength(100)]
        public string Email { get; set; } = "";

        [Required(ErrorMessage = "Vui lòng nhập số điện thoại")]
        [RegularExpression(@"^[0-9+\s]{9,15}$", ErrorMessage = "Số điện thoại không hợp lệ")]
        [Display(Name = "Số điện thoại")]
        public string Phone { get; set; } = "";

        [StringLength(255)]
        [Display(Name = "Địa chỉ")]
        public string? Address { get; set; }

        [RegularExpression(@"^[0-9+\s]{9,15}$", ErrorMessage = "Số điện thoại không hợp lệ")]
        [Display(Name = "SĐT khẩn cấp")]
        public string? EmergencyPhone { get; set; }

        // Bắt buộc khi tạo mới; khi sửa: để trống = giữ nguyên, nhập = đặt lại mật khẩu
        [StringLength(100, MinimumLength = 6, ErrorMessage = "Mật khẩu tối thiểu 6 ký tự")]
        [Display(Name = "Mật khẩu")]
        public string? Password { get; set; }

        [Display(Name = "Tài khoản đang hoạt động")]
        public bool IsActive { get; set; } = true;

        public bool IsNew => PatientId == 0;
    }

    // =============== TÀI KHOẢN: BÁC SĨ ===============
    public class DoctorRowVm
    {
        public int DoctorId { get; set; }
        public string FullName { get; set; } = "";
        public string Email { get; set; } = "";
        public string Phone { get; set; } = "";
        public string Specialty { get; set; } = "";
        public int Exp { get; set; }
        public string Status { get; set; } = "";
        public string? AvatarUrl { get; set; }
        public int AppointmentCount { get; set; }
        public bool HasAccount { get; set; }
        public bool IsActive { get; set; }
    }

    public class DoctorListVm
    {
        public string? Q { get; set; }
        public int? SpecId { get; set; }
        public List<SelectListItem> Specs { get; set; } = new();
        public List<DoctorRowVm> Items { get; set; } = new();
    }

    public class DoctorFormVm
    {
        public int DoctorId { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập họ tên"), StringLength(100)]
        [Display(Name = "Họ và tên")]
        public string FullName { get; set; } = "";

        [Required(ErrorMessage = "Vui lòng nhập email"), EmailAddress(ErrorMessage = "Email không hợp lệ"), StringLength(100)]
        public string Email { get; set; } = "";

        [Required(ErrorMessage = "Vui lòng nhập số điện thoại")]
        [RegularExpression(@"^[0-9+\s]{9,15}$", ErrorMessage = "Số điện thoại không hợp lệ")]
        [Display(Name = "Số điện thoại")]
        public string Phone { get; set; } = "";

        [Range(1, int.MaxValue, ErrorMessage = "Vui lòng chọn chuyên khoa")]
        [Display(Name = "Chuyên khoa")]
        public int SpecId { get; set; }

        [Range(0, 60, ErrorMessage = "Kinh nghiệm từ 0 đến 60 năm")]
        [Display(Name = "Kinh nghiệm (năm)")]
        public int Exp { get; set; }

        [StringLength(50)]
        [Display(Name = "Trạng thái làm việc")]
        public string Current { get; set; } = "Available";

        public string? AvatarUrl { get; set; }

        [Display(Name = "Ảnh đại diện")]
        public IFormFile? AvatarFile { get; set; }

        [StringLength(100, MinimumLength = 6, ErrorMessage = "Mật khẩu tối thiểu 6 ký tự")]
        [Display(Name = "Mật khẩu")]
        public string? Password { get; set; }

        [Display(Name = "Tài khoản đang hoạt động")]
        public bool IsActive { get; set; } = true;

        public List<SelectListItem> Specs { get; set; } = new();
        public bool IsNew => DoctorId == 0;
    }

    // =============== THEO DÕI ===============
    public class DoctorWorkloadRow
    {
        public int DoctorId { get; set; }
        public string DoctorName { get; set; } = "";
        public string Specialty { get; set; } = "";
        public int Appointments { get; set; }
        public int Completed { get; set; }
        public int Records { get; set; }
    }

    public class DoctorStatusRow
    {
        public int DoctorId { get; set; }
        public string DoctorName { get; set; } = "";
        public string Specialty { get; set; } = "";
        public string Status { get; set; } = "";
        public int TodayCases { get; set; }
        public int TodayRecords { get; set; }
        public int OverdueNoRecord { get; set; }
        public DateTime? LastRecordAt { get; set; }
        public List<string> Reasons { get; set; } = new();
        public bool Suspicious => Reasons.Count > 0;
    }

    public class MonitoringVm
    {
        public DateOnly From { get; set; }
        public DateOnly To { get; set; }
        public int Hours { get; set; } = 3;

        // Bệnh nhân
        public int PatientTotal { get; set; }
        public int PatientWithAppt { get; set; }
        public int PatientActive { get; set; }
        public int PatientLocked { get; set; }
        public int PatientNewInRange { get; set; }
        public int PatientServedInRange { get; set; }
        public List<string> MonthLabels { get; set; } = new();
        public List<int> MonthNew { get; set; } = new();

        // Bác sĩ
        public List<DoctorWorkloadRow> Workload { get; set; } = new();
        public List<DoctorStatusRow> StatusRows { get; set; } = new();
        public List<(string Label, int Count)> StatusSummary { get; set; } = new();
    }

    // =============== DOANH THU ===============
    public class PaidRow
    {
        public int PaymentId { get; set; }
        public int AppointId { get; set; }
        public DateTime PaymentTime { get; set; }
        public decimal Amount { get; set; }
        public string Method { get; set; } = "";
        public string Patient { get; set; } = "";
        public string Doctor { get; set; } = "";
        public string Specialty { get; set; } = "";
        public string BookingType { get; set; } = "";
    }

    public class PendingPaymentRow
    {
        public int PaymentId { get; set; }
        public int AppointId { get; set; }
        public DateTime Time { get; set; }
        public decimal Amount { get; set; }
        public string Patient { get; set; } = "";
        public string Doctor { get; set; } = "";
        public string Method { get; set; } = "";
        public string? ReceiptUrl { get; set; }
    }

    public class RevenueItem
    {
        public string Label { get; set; } = "";
        public decimal Total { get; set; }
        public int Count { get; set; }
    }

    public class RevenueVm
    {
        public DateOnly From { get; set; }
        public DateOnly To { get; set; }
        public decimal Total { get; set; }
        public int Count { get; set; }
        public decimal Average => Count == 0 ? 0 : Total / Count;
        public decimal PendingAmount { get; set; }
        public List<RevenueItem> ByMonth { get; set; } = new();
        public List<RevenueItem> BySpecialty { get; set; } = new();
        public List<RevenueItem> ByDoctor { get; set; } = new();
        public List<RevenueItem> ByType { get; set; } = new();
        public List<PendingPaymentRow> Pending { get; set; } = new();
    }

    // =============== DANH MỤC ===============
    public class SpecRowVm
    {
        public int SpecId { get; set; }
        public string Name { get; set; } = "";
        public string? Description { get; set; }
        public decimal Price { get; set; }
        public int DoctorCount { get; set; }
        public int ClinicCount { get; set; }
    }

    public class SpecFormVm
    {
        public int SpecId { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập tên chuyên khoa"), StringLength(100)]
        [Display(Name = "Tên chuyên khoa")]
        public string SpecialtyName { get; set; } = "";

        [StringLength(500)]
        [Display(Name = "Mô tả")]
        public string? Description { get; set; }

        [Range(0, 1000000000, ErrorMessage = "Giá không hợp lệ")]
        [Display(Name = "Giá khám cơ bản (đ)")]
        public decimal BasePrice { get; set; }

        public bool IsNew => SpecId == 0;
    }

    public class ClinicRowVm
    {
        public int ClinicId { get; set; }
        public string Name { get; set; } = "";
        public string Location { get; set; } = "";
        public string Specialty { get; set; } = "";
        public int AppointmentCount { get; set; }
    }

    public class ClinicListVm
    {
        public int? SpecId { get; set; }
        public List<SelectListItem> Specs { get; set; } = new();
        public List<ClinicRowVm> Items { get; set; } = new();
    }

    public class ClinicFormVm
    {
        public int ClinicId { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập tên phòng khám"), StringLength(100)]
        [Display(Name = "Tên phòng khám")]
        public string ClinicName { get; set; } = "";

        [Required(ErrorMessage = "Vui lòng nhập vị trí"), StringLength(255)]
        [Display(Name = "Vị trí / địa chỉ")]
        public string Location { get; set; } = "";

        [Range(1, int.MaxValue, ErrorMessage = "Vui lòng chọn chuyên khoa")]
        [Display(Name = "Chuyên khoa")]
        public int SpecId { get; set; }

        public List<SelectListItem> Specs { get; set; } = new();
        public bool IsNew => ClinicId == 0;
    }

    // =============== TỔNG QUAN ===============
    public class DashboardVm
    {
        public int PatientTotal { get; set; }
        public int DoctorTotal { get; set; }
        public int TodayCases { get; set; }
        public decimal RevenueThisMonth { get; set; }
        public int PendingPayments { get; set; }
        public int PendingAppointments { get; set; }
        public List<string> MonthLabels { get; set; } = new();
        public List<decimal> MonthRevenue { get; set; } = new();
        public List<DoctorStatusRow> Suspicious { get; set; } = new();
    }
}
