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
    }

    public class DoctorListVm
    {
        public string? Q { get; set; }
        public int? SpecId { get; set; }
        public List<SelectListItem> Specs { get; set; } = new();
        public List<DoctorListItem> Doctors { get; set; } = new();
    }

    public class DayVm
    {
        public int ScheduleId { get; set; }
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
}
