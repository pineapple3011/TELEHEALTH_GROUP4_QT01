using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace TELEHEALTH_GROUP4_QT01.Models.ViewModels
{
    // ---------- TÌM BÁC SĨ ----------
    public class DoctorListItem
    {
        public int DoctorId { get; set; }
        public string HoTen { get; set; } = "";
        public string TenKhoa { get; set; } = "";
        public string KinhNghiem { get; set; } = "";
        public string TrangThai { get; set; } = "";
        public string? AvatarUrl { get; set; }
        public long Fee { get; set; }
    }

    public class DoctorListVm
    {
        public string? Q { get; set; }
        public int? SpecId { get; set; }
        public List<SelectListItem> Specs { get; set; } = new();
        public List<DoctorListItem> Doctors { get; set; } = new();

        // --- Thêm cho thanh lọc bên cạnh ---
        public bool OnlyAvailable { get; set; }                 // chỉ bác sĩ đang nhận khám
        public string? Sort { get; set; }                       // "", "exp", "feeAsc", "feeDesc", "name"
        public int TotalAll { get; set; }                       // tổng số bác sĩ (không lọc)
        public List<SpecCard> SpecCards { get; set; } = new();  // chuyên khoa + số bác sĩ
    }

    public class DayVm
    {
        public int ScheduleId { get; set; }
        public string DateIso { get; set; } = "";            // "2026-11-01" - dùng cho lịch chọn ngày
        public string DateText { get; set; } = "";
        public string RangeText { get; set; } = "";
        public List<string> FreeSlots { get; set; } = new();
    }

    public class DoctorDetailsVm
    {
        public int DoctorId { get; set; }
        public string HoTen { get; set; } = "";
        public string TenKhoa { get; set; } = "";
        public string MoTa { get; set; } = "";
        public string KinhNghiem { get; set; } = "";
        public string TrangThai { get; set; } = "";
        public string? AvatarUrl { get; set; }
        public long Fee { get; set; }
        public List<DayVm> Days { get; set; } = new();
    }

    // ---------- ĐẶT LỊCH ----------
    public class AppointmentCreateVm
    {
        public int DoctorId { get; set; }
        public int ScheduleId { get; set; }
        public string Time { get; set; } = "";               // dạng "08:30"

        [Required(ErrorMessage = "Vui lòng chọn hình thức khám")]
        public string HinhThuc { get; set; } = "Online";

        public int? ClinicId { get; set; }
        public int? MucDoKhanCap { get; set; }
        public string? ViTriKhanCap { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập lý do khám / triệu chứng")]
        public string LyDo { get; set; } = "";

        public List<IFormFile>? Files { get; set; }

        // --- chỉ để hiển thị (phải để nullable, nếu không MVC sẽ báo thiếu dữ liệu) ---
        public string? DoctorName { get; set; }
        public string? SpecName { get; set; }
        public string? DoctorAvatarUrl { get; set; }
        public string? DoctorExp { get; set; }
        public string? DateText { get; set; }
        public string? TimeText { get; set; }
        public long FeeOnline { get; set; }
        public long FeeOffline { get; set; }
        public List<SelectListItem>? Clinics { get; set; }
    }

    // ---------- LỊCH SỬ / CHI TIẾT ----------
    public class AppointmentRow
    {
        public int AppointId { get; set; }
        public string DateText { get; set; } = "";
        public string TimeText { get; set; } = "";
        public string DoctorName { get; set; } = "";
        public string HinhThuc { get; set; } = "";
        public string TrangThai { get; set; } = "";
    }

    public class FileLine
    {
        public string Url { get; set; } = "";
        public string Loai { get; set; } = "";
        public string Ngay { get; set; } = "";
    }

    public class PrescriptionLine
    {
        public string TenThuoc { get; set; } = "";
        public string SoLuong { get; set; } = "";
        public string LieuDung { get; set; } = "";
        public string CachDung { get; set; } = "";
    }

    public class AppointmentDetailsVm
    {
        public int AppointId { get; set; }
        public string DoctorName { get; set; } = "";
        public string SpecName { get; set; } = "";
        public string DateText { get; set; } = "";
        public string TimeText { get; set; } = "";
        public string HinhThuc { get; set; } = "";
        public string TrangThai { get; set; } = "";
        public string MucDoKhanCap { get; set; } = "";
        public string ViTriKhanCap { get; set; } = "";
        public List<FileLine> Files { get; set; } = new();

        public bool HasPayment { get; set; }
        public string LoaiGiaoDich { get; set; } = "";
        public decimal Amount { get; set; }
        public string TrangThaiGiaoDich { get; set; } = "";
        public bool CanPay { get; set; }

        public bool HasRecord { get; set; }
        public string ChanDoan { get; set; } = "";
        public string NgayTaiKham { get; set; } = "";
        public List<PrescriptionLine> Prescriptions { get; set; } = new();
    }

    // ---------- THANH TOÁN ----------
    public class PaymentVm
    {
        public int AppointId { get; set; }
        public string DoctorName { get; set; } = "";
        public string DateText { get; set; } = "";
        public string TimeText { get; set; } = "";
        public string HinhThuc { get; set; } = "";
        public string LoaiGiaoDich { get; set; } = "";
        public decimal Amount { get; set; }
        public string TrangThaiGiaoDich { get; set; } = "";
        public bool CanUpload { get; set; }
        public string QrUrl { get; set; } = "";
        public string BankId { get; set; } = "";
        public string AccountNo { get; set; } = "";
        public string AccountName { get; set; } = "";
        public string TransferNote { get; set; } = "";
    }

    // ---------- ẢNH BÁC SĨ (dùng cho partial _DoctorAvatar) ----------
    public class AvatarVm
    {
        public string Fit { get; set; } = "cover";   // "cover" = lấp đầy khung (có thể cắt), "contain" = hiện trọn ảnh
        public string? Name { get; set; }
        public string? Url { get; set; }
        public string Width { get; set; } = "96px";
        public string Height { get; set; } = "96px";
        public string Radius { get; set; } = "50%";
        public string FontSize { get; set; } = "2rem";
    }

    // ---------- TRANG CHỦ ----------
    public class SpecCard
    {
        public int SpecId { get; set; }
        public string Name { get; set; } = "";
        public int DoctorCount { get; set; }
    }

    public class HomeVm
    {
        public List<DoctorListItem> Doctors { get; set; } = new();
        public List<SpecCard> Specs { get; set; } = new();
    }

    // ---------- PHÂN HỆ BỆNH NHÂN ----------
    public class PatientProfileVm
    {
        public int PatientId { get; set; }
        public string FullName { get; set; } = "";
        public string Email { get; set; } = "";
        public string Phone { get; set; } = "";
        public string Address { get; set; } = "";
        public string EmergencyPhone { get; set; } = "";

        public int Total { get; set; }
        public int Upcoming { get; set; }
        public int Completed { get; set; }
        public int PendingPayment { get; set; }
        public int? PendingPayAppointId { get; set; }

        public List<AppointmentRow> UpcomingList { get; set; } = new();
        public List<AppointmentRow> RecentList { get; set; } = new();
    }

    public class PatientEditVm
    {
        [Required(ErrorMessage = "Vui lòng nhập họ tên")]
        [StringLength(100, ErrorMessage = "Họ tên tối đa 100 ký tự")]
        [Display(Name = "Họ và tên")]
        public string FullName { get; set; } = "";

        // Chỉ để hiển thị, không cho sửa (email là định danh duy nhất của tài khoản)
        public string? Email { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập số điện thoại")]
        [RegularExpression(@"^[0-9+\s]{9,15}$", ErrorMessage = "Số điện thoại không hợp lệ (9-15 chữ số)")]
        [Display(Name = "Số điện thoại")]
        public string Phone { get; set; } = "";

        [StringLength(255, ErrorMessage = "Địa chỉ tối đa 255 ký tự")]
        [Display(Name = "Địa chỉ")]
        public string? Address { get; set; }

        [RegularExpression(@"^[0-9+\s]{9,15}$", ErrorMessage = "Số điện thoại không hợp lệ (9-15 chữ số)")]
        [Display(Name = "SĐT người thân (khi khẩn cấp)")]
        public string? EmergencyPhone { get; set; }
    }
}