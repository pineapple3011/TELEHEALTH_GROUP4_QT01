using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using TELEHEALTH_GROUP4_QT01.Helpers;
using TELEHEALTH_GROUP4_QT01.Models;
using TELEHEALTH_GROUP4_QT01.Models.ViewModels;
using TELEHEALTH_GROUP4_QT01.Services;

namespace TELEHEALTH_GROUP4_QT01.Controllers
{
    public class AppointmentsController : PatientBaseController
    {
        // Giới hạn kích thước request (5 file x 5 MB + dữ liệu form). Giới hạn từng file được kiểm tra trong IFileStorageService.
        private const long MaxCreateRequestBytes = 26 * 1024 * 1024;
        private const long MaxReceiptRequestBytes = 6 * 1024 * 1024;

        private readonly TelehealthContext _context;
        private readonly IFileStorageService _files;
        private readonly BankOptions _bank;
        private readonly ILogger<AppointmentsController> _logger;     // ← thêm dòng này

        public AppointmentsController(TelehealthContext context, IFileStorageService files,
                                      IOptions<BankOptions> bank, ILogger<AppointmentsController> logger)   // ← thêm tham số
        {
            _context = context;
            _files = files;
            _bank = bank.Value;
            _logger = logger;                                         // ← thêm dòng này
        }

        // =====================================================================
        // 1. LỊCH SỬ KHÁM  GET: /Appointments
        // =====================================================================
        public async Task<IActionResult> Index()
        {
            var list = await _context.Appointments.Where(c => c.PatientId == CurrentPatientId).ToListAsync();
            var doctors = (await _context.Doctors.ToListAsync()).ToDictionary(d => d.DoctorId, d => d.FullName ?? "");

            var rows = list
                .OrderByDescending(c => Cv.D(c.AppointmentDate)).ThenByDescending(c => Cv.T(c.AppointmentTime))
                .Select(c => new AppointmentRow
                {
                    AppointId = c.AppointId,
                    DateText = Cv.F(c.AppointmentDate, "dd/MM/yyyy"),
                    TimeText = Cv.F(c.AppointmentTime, "HH:mm"),
                    DoctorName = doctors.GetValueOrDefault(Cv.I(c.DoctorId), ""),
                    HinhThuc = c.BookingType ?? "",
                    TrangThai = c.Status ?? ""
                })
                .ToList();

            return View(rows);
        }

        // =====================================================================
        // 2. CHI TIẾT CA KHÁM + BỆNH ÁN + ĐƠN THUỐC  GET: /Appointments/Details/5
        // =====================================================================
        public async Task<IActionResult> Details(int id)
        {
            var ca = await GetOwnedAsync(id);
            if (ca == null) return NotFound();

            var doctor = await _context.Doctors.FindAsync(Cv.I(ca.DoctorId));
            var spec = doctor == null ? null : await _context.Specialties.FindAsync(Cv.I(doctor.SpecId));
            var files = await _context.AppointmentFiles.Where(t => t.AppointId == id).ToListAsync();
            var pay = await _context.Payments.Where(g => g.AppointId == id)
                                    .OrderByDescending(g => g.PaymentId).FirstOrDefaultAsync();
            var record = await _context.MedicalRecords.FirstOrDefaultAsync(h => h.AppointId == id);

            var lines = new List<PrescriptionLine>();
            if (record != null)
            {
                var rx = await _context.Prescriptions.Where(d => d.RecordId == record.RecordId).ToListAsync();
                var meds = (await _context.Medicines.ToListAsync()).ToDictionary(m => m.MedicineId);
                foreach (var r in rx)
                {
                    meds.TryGetValue(r.MedicineId, out var m);
                    var usage = Convert.ToString(r.Hds);
                    if (string.IsNullOrWhiteSpace(usage)) usage = Convert.ToString(m?.Hds);

                    lines.Add(new PrescriptionLine
                    {
                        TenThuoc = m?.MedicineName ?? "(không rõ)",
                        SoLuong = Convert.ToString(r.Quantity) ?? "",
                        LieuDung = Convert.ToString(r.Dosage) ?? "",
                        CachDung = usage ?? ""
                    });
                }
            }

            var vm = new AppointmentDetailsVm
            {
                AppointId = ca.AppointId,
                DoctorName = doctor?.FullName ?? "",
                SpecName = spec?.SpecialtyName ?? "",
                DateText = Cv.F(ca.AppointmentDate, "dd/MM/yyyy"),
                TimeText = Cv.F(ca.AppointmentTime, "HH:mm"),
                HinhThuc = ca.BookingType ?? "",
                TrangThai = ca.Status ?? "",
                MucDoKhanCap = Convert.ToString(ca.TriageLevel) ?? "",
                ViTriKhanCap = Convert.ToString(ca.EmergencyLocation) ?? "",
                Files = files.Select(f => new FileLine
                {
                    Id = f.FileId,                       // link tải file đi qua FilesController (có kiểm tra quyền)
                    Loai = f.FileType ?? "",
                    Ngay = Cv.F(f.UploadDate, "dd/MM/yyyy HH:mm")
                }).ToList(),
                HasPayment = pay != null,
                LoaiGiaoDich = ca.BookingType == HinhThucKham.Offline ? "Đặt cọc" : "Phí khám",
                Amount = Cv.M(pay?.Amount),
                TrangThaiGiaoDich = pay?.Status ?? "",
                CanPay = pay != null && pay.Status == TtGiaoDich.ChoThanhToan,
                HasRecord = record != null,
                // Cột chẩn đoán trong ERD ghi "Diagnonis"; thử cả hai cách viết để tránh lỗi biên dịch
                ChanDoan = Cv.Prop(record, "Diagnosis", "Diagnonis"),
                NgayTaiKham = Cv.F(record?.FollowUpDate, "dd/MM/yyyy"),
                Prescriptions = lines
            };
            return View(vm);
        }

        // =====================================================================
        // 3. ĐẶT LỊCH  GET: /Appointments/Create?doctorId=1&scheduleId=2&time=08:30
        // =====================================================================
        [HttpGet]
        public async Task<IActionResult> Create(int doctorId, int scheduleId, string time)
        {
            var ctx = await LoadAsync(doctorId, scheduleId);
            if (ctx == null) return NotFound();
            var (doctor, lich, spec) = ctx.Value;

            var vm = new AppointmentCreateVm { DoctorId = doctorId, ScheduleId = scheduleId, Time = time ?? "" };
            await FillDisplayAsync(vm, doctor, lich, spec);
            return View(vm);
        }

        [HttpPost, ValidateAntiForgeryToken]
        [RequestSizeLimit(MaxCreateRequestBytes)]
        [RequestFormLimits(MultipartBodyLengthLimit = MaxCreateRequestBytes)]
        public async Task<IActionResult> Create(AppointmentCreateVm vm)
        {
            var ctx = await LoadAsync(vm.DoctorId, vm.ScheduleId);
            if (ctx == null) return NotFound();
            var (doctor, lich, spec) = ctx.Value;
            var basePrice = Cv.M(spec?.BasePrice);

            // --- kiểm tra khung giờ còn trống (đề phòng 2 người đặt cùng lúc) ---
            TimeOnly gio;
            if (!TimeOnly.TryParseExact(vm.Time, "HH:mm", out gio))
            {
                ModelState.AddModelError("", "Khung giờ không hợp lệ.");
            }
            else
            {
                // Không cho đặt vào thời điểm đã qua
                var today = DateOnly.FromDateTime(DateTime.Today);
                var workDate = Cv.D(lich.WorkDate);
                if (workDate < today || (workDate == today && gio <= TimeOnly.FromDateTime(DateTime.Now)))
                    ModelState.AddModelError("", "Không thể đặt lịch cho thời điểm đã qua.");

                var booked = await _context.Appointments.Where(c => c.DoctorId == doctor.DoctorId).ToListAsync();
                var inSchedule = booked.Where(c => Cv.I(c.ScheduleId) == lich.ScheduleId);
                if (!SlotHelper.FreeSlots(lich, inSchedule).Contains(gio))
                    ModelState.AddModelError("", "Khung giờ này đã có người đặt. Vui lòng chọn giờ khác.");
            }

            // --- kiểm tra theo hình thức khám ---
            bool capCuu = vm.HinhThuc == HinhThucKham.CapCuu;
            bool offline = vm.HinhThuc == HinhThucKham.Offline;

            if (vm.HinhThuc is not (HinhThucKham.Online or HinhThucKham.Offline or HinhThucKham.CapCuu))
                ModelState.AddModelError("HinhThuc", "Hình thức khám không hợp lệ.");
            if (offline)
            {
                if (vm.ClinicId == null)
                    ModelState.AddModelError("ClinicId", "Vui lòng chọn phòng khám.");
                else if (!await _context.Clinics.AnyAsync(c => c.ClinicId == vm.ClinicId && c.SpecId == doctor.SpecId))
                    ModelState.AddModelError("ClinicId", "Phòng khám không hợp lệ.");   // không tin ClinicId do client gửi
            }
            if (capCuu)
            {
                if (vm.MucDoKhanCap is null or < 1 or > 5)
                    ModelState.AddModelError("MucDoKhanCap", "Vui lòng chọn mức độ cấp cứu từ 1 đến 5.");
                if (string.IsNullOrWhiteSpace(vm.ViTriKhanCap))
                    ModelState.AddModelError("ViTriKhanCap", "Vui lòng nhập vị trí hiện tại của bạn.");
            }

            // --- kiểm tra file đính kèm: số lượng, kích thước, đuôi file, nội dung thật (magic bytes) ---
            var uploads = vm.Files?.Where(x => x.Length > 0).ToList() ?? new List<IFormFile>();
            if (uploads.Count > _files.MaxFilesPerRequest)
                ModelState.AddModelError("Files", $"Chỉ được đính kèm tối đa {_files.MaxFilesPerRequest} file.");
            else
            {
                foreach (var f in uploads)
                {
                    var err = await _files.ValidateAsync(f);
                    if (err != null)
                    {
                        ModelState.AddModelError("Files", err);
                        _logger.LogWarning("Symptom file rejected for patient {PatientId}: {Error}", CurrentPatientId, err);
                    }
                }
            }

            if (!ModelState.IsValid)
            {
                await FillDisplayAsync(vm, doctor, lich, spec);
                return View(vm);
            }

            // --- lưu ca khám ---
            var ca = new Appointment
            {
                PatientId = CurrentPatientId,
                DoctorId = doctor.DoctorId,
                ScheduleId = lich.ScheduleId,
                ClinicId = offline ? vm.ClinicId : null,
                AppointmentDate = Cv.D(lich.WorkDate),
                AppointmentTime = gio,
                BookingType = vm.HinhThuc,
                TriageLevel = capCuu ? vm.MucDoKhanCap?.ToString() : null,
                EmergencyLocation = capCuu ? vm.ViTriKhanCap : null,
                Status = TrangThaiCa.ChoXacNhan
                // Reason = vm.LyDo,   // <-- bỏ comment sau khi thêm cột Reason vào bảng Appointments và class Appointment
            };
            _context.Appointments.Add(ca);
            await _context.SaveChangesAsync();          // để có ca.AppointId

            // --- lưu file triệu chứng (ngoài wwwroot, tên ngẫu nhiên) ---
            foreach (var f in uploads)
            {
                var path = await _files.SaveAsync(f, "symptoms");
                _context.AppointmentFiles.Add(new AppointmentFile
                {
                    AppointId = ca.AppointId,
                    FilePath = path,
                    FileType = "Triệu chứng",
                    UploadDate = DateTime.Now
                });
            }

            // --- tạo giao dịch thanh toán (nếu có phí) ---
            var fee = CalcFee(vm.HinhThuc, basePrice);
            if (fee > 0)
            {
                _context.Payments.Add(new Payment
                {
                    AppointId = ca.AppointId,
                    Amount = fee,
                    PaymentMethod = "Chuyển khoản",
                    Status = TtGiaoDich.ChoThanhToan,
                    PaymentTime = DateTime.Now
                });
            }
            await _context.SaveChangesAsync();

            _logger.LogInformation("Patient {PatientId} created appointment {AppointId} ({BookingType})",
                       CurrentPatientId, ca.AppointId, ca.BookingType);

            if (fee > 0) return RedirectToAction(nameof(Pay), new { id = ca.AppointId });

            TempData["Msg"] = "Đã gửi yêu cầu khám. Vui lòng chờ bác sĩ xác nhận.";
            return RedirectToAction(nameof(Details), new { id = ca.AppointId });
        }

        // =====================================================================
        // 4. THANH TOÁN  GET/POST: /Appointments/Pay/5
        //    (đặt tên Pay thay vì Payment để không trùng tên với class Payment)
        // =====================================================================
        [HttpGet]
        public async Task<IActionResult> Pay(int id)
        {
            var ca = await GetOwnedAsync(id);
            if (ca == null) return NotFound();

            var pay = await _context.Payments.Where(g => g.AppointId == id)
                                    .OrderByDescending(g => g.PaymentId).FirstOrDefaultAsync();
            if (pay == null) return RedirectToAction(nameof(Details), new { id });

            if (!_bank.IsConfigured)
                return StatusCode(StatusCodes.Status503ServiceUnavailable,
                    "Hệ thống chưa cấu hình tài khoản ngân hàng nhận thanh toán. Vui lòng liên hệ quản trị viên.");

            var doctor = await _context.Doctors.FindAsync(Cv.I(ca.DoctorId));
            var amount = Cv.M(pay.Amount);
            var note = $"TELEHEALTH {id}";

            var vm = new PaymentVm
            {
                AppointId = id,
                DoctorName = doctor?.FullName ?? "",
                DateText = Cv.F(ca.AppointmentDate, "dd/MM/yyyy"),
                TimeText = Cv.F(ca.AppointmentTime, "HH:mm"),
                HinhThuc = ca.BookingType ?? "",
                LoaiGiaoDich = ca.BookingType == HinhThucKham.Offline ? "Đặt cọc" : "Phí khám",
                Amount = amount,
                TrangThaiGiaoDich = pay.Status ?? "",
                CanUpload = pay.Status == TtGiaoDich.ChoThanhToan,
                BankId = _bank.BankId,
                AccountNo = _bank.AccountNo,
                AccountName = _bank.AccountName,
                TransferNote = note,
                QrUrl = $"https://img.vietqr.io/image/{Uri.EscapeDataString(_bank.BankId)}-{Uri.EscapeDataString(_bank.AccountNo)}-compact2.png" +
                        $"?amount={(long)amount}&addInfo={Uri.EscapeDataString(note)}" +
                        $"&accountName={Uri.EscapeDataString(_bank.AccountName)}"
            };
            return View(vm);
        }

        [HttpPost, ValidateAntiForgeryToken]
        [RequestSizeLimit(MaxReceiptRequestBytes)]
        [RequestFormLimits(MultipartBodyLengthLimit = MaxReceiptRequestBytes)]
        public async Task<IActionResult> Pay(int id, IFormFile? receipt)
        {
            var ca = await GetOwnedAsync(id);
            if (ca == null) return NotFound();

            var pay = await _context.Payments.Where(g => g.AppointId == id)
                                    .OrderByDescending(g => g.PaymentId).FirstOrDefaultAsync();
            if (pay == null) return NotFound();

            // Chỉ cho gửi biên lai khi giao dịch đang chờ thanh toán (không cho gửi lặp / ghi đè trạng thái)
            if (pay.Status != TtGiaoDich.ChoThanhToan)
            {
                TempData["Err"] = "Giao dịch này không còn ở trạng thái chờ thanh toán.";
                return RedirectToAction(nameof(Pay), new { id });
            }

            if (receipt == null || receipt.Length == 0)
            {
                TempData["Err"] = "Vui lòng chọn ảnh biên lai chuyển khoản.";
                return RedirectToAction(nameof(Pay), new { id });
            }
            var err = await _files.ValidateAsync(receipt);
            if (err != null)
            {
                _logger.LogWarning("Receipt rejected for appointment {AppointId}: {Error}", id, err);
                TempData["Err"] = err;
                return RedirectToAction(nameof(Pay), new { id });
            }

            var url = await _files.SaveAsync(receipt, "receipts");
            _context.AppointmentFiles.Add(new AppointmentFile
            {
                AppointId = id,
                FilePath = url,
                FileType = "Biên lai",
                UploadDate = DateTime.Now
            });
            pay.Status = TtGiaoDich.ChoXacNhan;
            await _context.SaveChangesAsync();

            _logger.LogInformation("Receipt uploaded for appointment {AppointId}, waiting for admin confirmation", id);

            TempData["Msg"] = "Đã gửi biên lai. Lịch hẹn sẽ được xác nhận sau khi kiểm tra thanh toán.";
            return RedirectToAction(nameof(Details), new { id });
        }

        // PHÒNG CHỜ 

        [HttpGet]
        public async Task<IActionResult> Room(int id)
        {
            var ca = await GetOwnedAsync(id);
            if (ca == null) return NotFound();

            if (ca.BookingType != HinhThucKham.Online || ca.Status == "Đã hủy")
                return RedirectToAction(nameof(Details), new { id });

            var doctor = await _context.Doctors.FindAsync(Cv.I(ca.DoctorId));
            var spec = doctor == null ? null : await _context.Specialties.FindAsync(Cv.I(doctor.SpecId));

            ViewBag.Doctor = doctor;
            ViewBag.Specialty = spec;     // đúng tên view đang đọc
            return View(ca);              // truyền model Appointment
        }

        // =====================================================================
        // 5. HỦY LỊCH  POST: /Appointments/Cancel
        // =====================================================================
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Cancel(int id, string? reason)
        {
            var ca = await GetOwnedAsync(id);
            if (ca == null) return NotFound();

            if (ca.Status != "Chờ xác nhận" && ca.Status != "Đã xác nhận")
            {
                TempData["Err"] = "Ca khám này không thể hủy ở trạng thái hiện tại.";
                return RedirectToAction(nameof(Index));
            }

            ca.Status = "Đã hủy";
            // ca.CancelReason = reason;   // bỏ comment nếu bảng Appointments có cột lý do hủy

            await _context.SaveChangesAsync();

            TempData["Msg"] = "Đã hủy lịch khám.";
            return RedirectToAction(nameof(Index));
        }

        // =====================================================================
        // HÀM PHỤ
        // =====================================================================
        private async Task<Appointment?> GetOwnedAsync(int id)
        {
            var ca = await _context.Appointments.FindAsync(id);
            if (ca == null || Cv.I(ca.PatientId) != CurrentPatientId) return null;   // không cho xem ca của người khác
            return ca;
        }

        private async Task<(Doctor Doctor, Schedule Lich, Specialty? Spec)?> LoadAsync(int doctorId, int scheduleId)
        {
            var doctor = await _context.Doctors.FindAsync(doctorId);
            var lich = await _context.Schedules.FindAsync(scheduleId);
            if (doctor == null || lich == null || Cv.I(lich.DoctorId) != doctorId) return null;
            var spec = await _context.Specialties.FindAsync(Cv.I(doctor.SpecId));
            return (doctor, lich, spec);
        }

        private async Task FillDisplayAsync(AppointmentCreateVm vm, Doctor doctor, Schedule lich, Specialty? spec)
        {
            var basePrice = Cv.M(spec?.BasePrice);
            vm.DoctorName = doctor.FullName;
            vm.SpecName = spec?.SpecialtyName;
            vm.DoctorAvatarUrl = doctor.AvatarUrl;
            vm.DoctorExp = Convert.ToString(doctor.Exp);
            vm.DateText = Cv.F(lich.WorkDate, "dd/MM/yyyy");
            vm.TimeText = vm.Time;
            vm.FeeOnline = (long)CalcFee(HinhThucKham.Online, basePrice);
            vm.FeeOffline = (long)CalcFee(HinhThucKham.Offline, basePrice);

            var clinics = await _context.Clinics.ToListAsync();
            vm.Clinics = clinics
                .Where(p => Cv.I(p.SpecId) == Cv.I(doctor.SpecId))
                .Select(p => new SelectListItem
                {
                    Value = p.ClinicId.ToString(),
                    Text = $"{p.ClinicName} - {p.Location}",
                    Selected = p.ClinicId == vm.ClinicId
                }).ToList();
        }

        private static decimal CalcFee(string hinhThuc, decimal basePrice) => hinhThuc switch
        {
            HinhThucKham.Online => basePrice,
            HinhThucKham.Offline => Math.Round(basePrice * PhiKham.TyLeDatCoc),
            _ => 0m      // Cấp cứu: không thu trước
        };
    }
}
