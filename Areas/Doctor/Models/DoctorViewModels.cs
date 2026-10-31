using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using System.ComponentModel.DataAnnotations;

namespace TELEHEALTH_GROUP4_QT01.Areas.Doctor.Models;

// ===== Hồ sơ bác sĩ =====
public class DoctorProfile
{
    public int DoctorId { get; set; }
    public int SpecId { get; set; }
    public string FullName { get; set; } = "";
    public string Email { get; set; } = "";
    public string Phone { get; set; } = "";
    public int Exp { get; set; }
    public string Current { get; set; } = "Available";   // Available | Busy
    public string? AvatarUrl { get; set; }
    public string SpecialtyName { get; set; } = "";
    public string? SpecialtyDescription { get; set; }
    public decimal BasePrice { get; set; }

    public bool IsAvailable => string.Equals(Current, "Available", StringComparison.OrdinalIgnoreCase);

    public string Initials
    {
        get
        {
            var parts = FullName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            return parts.Length == 0 ? "BS" : parts[^1].Substring(0, 1).ToUpper();
        }
    }
}

public class ProfileStats
{
    public int TotalAppointments { get; set; }
    public int Completed { get; set; }
    public int Patients { get; set; }
    public int UpcomingShifts { get; set; }
}

public class ProfilePageVm
{
    public DoctorProfile Doctor { get; set; } = new();
    public ProfileStats Stats { get; set; } = new();
}

public class ProfileEditInput
{
    [Required(ErrorMessage = "Vui lòng nhập số điện thoại")]
    [RegularExpression(@"^0\d{9,10}$", ErrorMessage = "Số điện thoại không hợp lệ (10-11 số, bắt đầu bằng 0)")]
    public string Phone { get; set; } = "";
    public IFormFile? Avatar { get; set; }
}

public class ChangePasswordInput
{
    [Required(ErrorMessage = "Nhập mật khẩu hiện tại")] public string CurrentPassword { get; set; } = "";
    [Required(ErrorMessage = "Nhập mật khẩu mới")]
    [MinLength(6, ErrorMessage = "Mật khẩu mới tối thiểu 6 ký tự")] public string NewPassword { get; set; } = "";
    [Required(ErrorMessage = "Nhập lại mật khẩu mới")]
    [Compare(nameof(NewPassword), ErrorMessage = "Mật khẩu nhập lại không khớp")] public string ConfirmPassword { get; set; } = "";
}

public class ProfileEditVm
{
    public DoctorProfile Doctor { get; set; } = new();
    public ProfileEditInput Input { get; set; } = new();
}

// ===== Lịch trực =====
public class ScheduleRow
{
    public int ScheduleId { get; set; }
    public DateTime WorkDate { get; set; }
    public TimeSpan StartTime { get; set; }
    public TimeSpan EndTime { get; set; }
    public int ActiveAppointments { get; set; }   // ca chưa hủy gắn với khung giờ này
}

public class ScheduleInput
{
    [Required] public DateTime WorkDate { get; set; } = DateTime.Today;
    [Required] public TimeSpan StartTime { get; set; } = new(8, 0, 0);
    [Required] public TimeSpan EndTime { get; set; } = new(12, 0, 0);
}

// Thêm đoạn này vào Areas/Doctor/Models/DoctorViewModels.cs, ngay dưới class ScheduleInput

public class ScheduleBulkInput
{
    [Required] public DateTime FromDate { get; set; } = DateTime.Today;
    [Required] public DateTime ToDate { get; set; } = DateTime.Today.AddDays(6);
    public List<int> Days { get; set; } = new();       // 0 = Chủ nhật ... 6 = Thứ bảy
    public List<string> Slots { get; set; } = new();   // "08:00-12:00"
    public string? CustomStart { get; set; }
    public string? CustomEnd { get; set; }
}

public class SchedulePageVm
{
    public DoctorProfile Doctor { get; set; } = new();
    public List<ScheduleRow> Upcoming { get; set; } = new();
    public ScheduleInput Input { get; set; } = new();
}

// ===== Ca khám =====
public class AppointmentRowVm
{
    public int AppointId { get; set; }
    public int PatientId { get; set; }
    public string PatientName { get; set; } = "";
    public string PatientPhone { get; set; } = "";
    public DateTime Date { get; set; }
    public TimeSpan Time { get; set; }
    public string BookingType { get; set; } = "";     // Online | Offline | Cấp cứu
    public string? EmergencyLocation { get; set; }
    public string? TriageLevel { get; set; }
    public string Status { get; set; } = "";          // Chờ xác nhận | Đã xác nhận | Hoàn thành | Đã hủy
    public string? ClinicName { get; set; }
    public int FileCount { get; set; }
    public string? PaymentStatus { get; set; }
    public bool HasRecord { get; set; }

    public string Code => "TK-" + AppointId.ToString("D5");
    public string TimeText => DateTime.Today.Add(Time).ToString("HH:mm");
    public bool IsPending => Status == "Chờ xác nhận";
    public bool IsConfirmed => Status == "Đã xác nhận";
    public bool IsCompleted => Status == "Hoàn thành";
    public bool IsCancelled => Status == "Đã hủy";
}

public class DayPageVm
{
    public DateTime Date { get; set; } = DateTime.Today;
    public List<AppointmentRowVm> Rows { get; set; } = new();
    public DateTime? NextDateWithAppointments { get; set; }
    public int PendingElsewhere { get; set; }   // ca chờ xác nhận ở các ngày khác
}

public class PatientInfo
{
    public int PatientId { get; set; }
    public string FullName { get; set; } = "";
    public string Email { get; set; } = "";
    public string Phone { get; set; } = "";
    public string? Address { get; set; }
    public string? EmergencyPhone { get; set; }
}

public class AppointmentFileVm
{
    public int FileId { get; set; }
    public string FilePath { get; set; } = "";
    public string FileType { get; set; } = "";
    public DateTime UploadDate { get; set; }
    public bool IsImage
    {
        get
        {
            var p = FilePath.ToLowerInvariant();
            return p.EndsWith(".jpg") || p.EndsWith(".jpeg") || p.EndsWith(".png") || p.EndsWith(".gif") || p.EndsWith(".webp");
        }
    }
}

public class PaymentVm
{
    public decimal Amount { get; set; }
    public string Method { get; set; } = "";
    public string Status { get; set; } = "";
    public DateTime PaymentTime { get; set; }
}

public class AppointmentDetailVm
{
    public AppointmentRowVm Appointment { get; set; } = new();
    public PatientInfo Patient { get; set; } = new();
    public List<AppointmentFileVm> Files { get; set; } = new();
    public PaymentVm? Payment { get; set; }
}

// ===== Khám bệnh & kê đơn =====
public class MedicineOption
{
    public int MedicineId { get; set; }
    public string Name { get; set; } = "";
    public string? Hds { get; set; }
    public decimal Price { get; set; }
}

public class PrescriptionItemInput
{
    public int MedicineId { get; set; }
    public int Quantity { get; set; } = 1;
    public string Dosage { get; set; } = "";
    public string? Hds { get; set; }
}

public class ExamInput
{
    public int AppointId { get; set; }
    public string Diagnosis { get; set; } = "";
    public DateTime? FollowUpDate { get; set; }
    public List<PrescriptionItemInput> Items { get; set; } = new();
}

public class ExamVm
{
    public AppointmentRowVm Appointment { get; set; } = new();
    public PatientInfo Patient { get; set; } = new();
    public List<AppointmentFileVm> Files { get; set; } = new();
    public List<MedicineOption> Medicines { get; set; } = new();
    public ExamInput Input { get; set; } = new();
    public bool HasRecord { get; set; }
}
