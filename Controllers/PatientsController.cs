using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TELEHEALTH_GROUP4_QT01.Helpers;
using TELEHEALTH_GROUP4_QT01.Models;
using TELEHEALTH_GROUP4_QT01.Models.ViewModels;

namespace TELEHEALTH_GROUP4_QT01.Controllers
{
    // Phân hệ bệnh nhân: hồ sơ cá nhân + tổng quan lịch khám
    public class PatientsController : PatientBaseController
    {
        private readonly TelehealthContext _context;

        public PatientsController(TelehealthContext context)
        {
            _context = context;
        }

        // GET: /Patients
        public async Task<IActionResult> Index()
        {
            var p = await _context.Patients.FindAsync(CurrentPatientId);
            if (p == null) return NotFound();

            var today = DateOnly.FromDateTime(DateTime.Today);
            var list = await _context.Appointments.Where(c => c.PatientId == CurrentPatientId).ToListAsync();
            var doctors = (await _context.Doctors.ToListAsync()).ToDictionary(d => d.DoctorId, d => d.FullName ?? "");

            // Giao dịch đang chờ bệnh nhân thanh toán
            var myIds = list.Select(c => c.AppointId).ToHashSet();
            var pending = (await _context.Payments.ToListAsync())
                .Where(g => myIds.Contains(Cv.I(g.AppointId)) && g.Status == TtGiaoDich.ChoThanhToan)
                .OrderBy(g => g.PaymentId)
                .ToList();

            bool IsOpen(Appointment c) =>
                c.Status != TrangThaiCa.DaHuy && c.Status != TrangThaiCa.HoanThanh && Cv.D(c.AppointmentDate) >= today;

            AppointmentRow ToRow(Appointment c) => new AppointmentRow
            {
                AppointId = c.AppointId,
                DateText = Cv.F(c.AppointmentDate, "dd/MM/yyyy"),
                TimeText = Cv.F(c.AppointmentTime, "HH:mm"),
                DoctorName = doctors.GetValueOrDefault(Cv.I(c.DoctorId), ""),
                HinhThuc = c.BookingType ?? "",
                TrangThai = c.Status ?? ""
            };

            var upcoming = list.Where(IsOpen)
                .OrderBy(c => Cv.D(c.AppointmentDate)).ThenBy(c => Cv.T(c.AppointmentTime))
                .ToList();
            var recent = list.Where(c => !IsOpen(c))
                .OrderByDescending(c => Cv.D(c.AppointmentDate)).ThenByDescending(c => Cv.T(c.AppointmentTime))
                .Take(5)
                .ToList();

            var vm = new PatientProfileVm
            {
                PatientId = p.PatientId,
                FullName = p.FullName ?? "",
                Email = p.Email ?? "",
                Phone = p.Phone ?? "",
                Address = p.Address ?? "",
                EmergencyPhone = p.EmergencyPhone ?? "",
                Total = list.Count,
                Upcoming = upcoming.Count,
                Completed = list.Count(c => c.Status == TrangThaiCa.HoanThanh),
                PendingPayment = pending.Count,
                PendingPayAppointId = pending.Count > 0 ? Cv.I(pending[0].AppointId) : (int?)null,
                UpcomingList = upcoming.Take(5).Select(ToRow).ToList(),
                RecentList = recent.Select(ToRow).ToList()
            };
            return View(vm);
        }

        // GET: /Patients/Edit
        [HttpGet]
        public async Task<IActionResult> Edit()
        {
            var p = await _context.Patients.FindAsync(CurrentPatientId);
            if (p == null) return NotFound();

            return View(new PatientEditVm
            {
                FullName = p.FullName ?? "",
                Email = p.Email,
                Phone = p.Phone ?? "",
                Address = p.Address,
                EmergencyPhone = p.EmergencyPhone
            });
        }

        // POST: /Patients/Edit
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(PatientEditVm vm)
        {
            var p = await _context.Patients.FindAsync(CurrentPatientId);
            if (p == null) return NotFound();

            if (!ModelState.IsValid)
            {
                vm.Email = p.Email;          // Email không nhận từ form, luôn lấy lại từ DB để hiển thị
                return View(vm);
            }

            p.FullName = vm.FullName.Trim();
            p.Phone = vm.Phone.Trim();
            p.Address = string.IsNullOrWhiteSpace(vm.Address) ? null : vm.Address.Trim();
            p.EmergencyPhone = string.IsNullOrWhiteSpace(vm.EmergencyPhone) ? null : vm.EmergencyPhone.Trim();
            await _context.SaveChangesAsync();

            TempData["Msg"] = "Đã cập nhật thông tin cá nhân.";
            return RedirectToAction(nameof(Index));
        }
    }
}
