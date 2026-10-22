using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using TELEHEALTH_GROUP4_QT01.Helpers;
using TELEHEALTH_GROUP4_QT01.Models;
using TELEHEALTH_GROUP4_QT01.Models.ViewModels;

namespace TELEHEALTH_GROUP4_QT01.Controllers
{
    public class AppointmentsController : PatientBaseController
    {
        // ====== THÔNG TIN NGÂN HÀNG (đổi thành của nhóm; BankId theo danh sách của VietQR, vd: MB, VCB, ACB) ======
        private const string BankId = "MB";
        private const string AccountNo = "0123456789";
        private const string AccountName = "TELEHEALTH GROUP 4";

        private static readonly string[] AllowedExt = { ".jpg", ".jpeg", ".png", ".pdf" };
        private const long MaxFileSize = 5 * 1024 * 1024;   // 5 MB

        private readonly TelehealthContext _context;        // đổi tên nếu DbContext của bạn khác
        private readonly IWebHostEnvironment _env;

        public AppointmentsController(TelehealthContext context, IWebHostEnvironment env)
        {
            _context = context;
            _env = env;
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
                    Url = f.FilePath ?? "",
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
            if (offline && vm.ClinicId == null)
                ModelState.AddModelError("ClinicId", "Vui lòng chọn phòng khám.");
            if (capCuu)
            {
                if (vm.MucDoKhanCap is null or < 1 or > 5)
                    ModelState.AddModelError("MucDoKhanCap", "Vui lòng chọn mức độ cấp cứu từ 1 đến 5.");
                if (string.IsNullOrWhiteSpace(vm.ViTriKhanCap))
                    ModelState.AddModelError("ViTriKhanCap", "Vui lòng nhập vị trí hiện tại của bạn.");
            }

            // --- kiểm tra file đính kèm ---
            if (vm.Files != null)
            {
                foreach (var f in vm.Files.Where(x => x.Length > 0))
                {
                    var err = ValidateFile(f);
                    if (err != null) ModelState.AddModelError("Files", err);
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

            // --- lưu file triệu chứng ---
            if (vm.Files != null)
            {
                foreach (var f in vm.Files.Where(x => x.Length > 0))
                {
                    var url = await SaveFileAsync(f, "symptoms");
                    _context.AppointmentFiles.Add(new AppointmentFile
                    {
                        AppointId = ca.AppointId,
                        FilePath = url,
                        FileType = "Triệu chứng",
                        UploadDate = DateTime.Now
                    });
                }
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
                BankId = BankId,
                AccountNo = AccountNo,
                AccountName = AccountName,
                TransferNote = note,
                QrUrl = $"https://img.vietqr.io/image/{BankId}-{AccountNo}-compact2.png" +
                        $"?amount={(long)amount}&addInfo={Uri.EscapeDataString(note)}" +
                        $"&accountName={Uri.EscapeDataString(AccountName)}"
            };
            return View(vm);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Pay(int id, IFormFile? receipt)
        {
            var ca = await GetOwnedAsync(id);
            if (ca == null) return NotFound();

            var pay = await _context.Payments.Where(g => g.AppointId == id)
                                    .OrderByDescending(g => g.PaymentId).FirstOrDefaultAsync();
            if (pay == null) return NotFound();

            if (receipt == null || receipt.Length == 0)
            {
                TempData["Err"] = "Vui lòng chọn ảnh biên lai chuyển khoản.";
                return RedirectToAction(nameof(Pay), new { id });
            }
            var err = ValidateFile(receipt);
            if (err != null)
            {
                TempData["Err"] = err;
                return RedirectToAction(nameof(Pay), new { id });
            }

            var url = await SaveFileAsync(receipt, "receipts");
            _context.AppointmentFiles.Add(new AppointmentFile
            {
                AppointId = id,
                FilePath = url,
                FileType = "Biên lai",
                UploadDate = DateTime.Now
            });
            pay.Status = TtGiaoDich.ChoXacNhan;
            await _context.SaveChangesAsync();

            TempData["Msg"] = "Đã gửi biên lai. Lịch hẹn sẽ được xác nhận sau khi kiểm tra thanh toán.";
            return RedirectToAction(nameof(Details), new { id });
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

        private static string? ValidateFile(IFormFile f)
        {
            var ext = Path.GetExtension(f.FileName).ToLowerInvariant();
            if (!AllowedExt.Contains(ext)) return $"File \"{f.FileName}\" không hợp lệ. Chỉ nhận .jpg, .png, .pdf.";
            if (f.Length > MaxFileSize) return $"File \"{f.FileName}\" vượt quá 5 MB.";
            return null;
        }

        private async Task<string> SaveFileAsync(IFormFile file, string folder)
        {
            var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
            var dir = Path.Combine(_env.WebRootPath, "uploads", folder);
            Directory.CreateDirectory(dir);
            var name = $"{Guid.NewGuid():N}{ext}";
            using var stream = new FileStream(Path.Combine(dir, name), FileMode.Create);
            await file.CopyToAsync(stream);
            return $"/uploads/{folder}/{name}";
        }
    }
}
