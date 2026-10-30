using System.Globalization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TELEHEALTH_GROUP4_QT01.Areas.Admin.Helpers;
using TELEHEALTH_GROUP4_QT01.Areas.Admin.Models;
using TELEHEALTH_GROUP4_QT01.Helpers;
using TELEHEALTH_GROUP4_QT01.Models;

namespace TELEHEALTH_GROUP4_QT01.Areas.Admin.Controllers
{
    // Theo dõi: số lượng bệnh nhân, hồ sơ bệnh án theo bác sĩ, trạng thái làm việc của bác sĩ
    public class MonitoringController : AdminBaseController
    {
        private readonly TelehealthContext _context;
        public MonitoringController(TelehealthContext context) => _context = context;

        // GET: /Admin/Monitoring?from=2026-10-01&to=2026-10-30&hours=3
        public async Task<IActionResult> Index(DateOnly? from, DateOnly? to, int hours = 3)
        {
            var (f, t) = DefaultRange(from, to);
            hours = Math.Clamp(hours, 1, 12);
            var fromDt = f.ToDateTime(TimeOnly.MinValue);
            var toDt = t.AddDays(1).ToDateTime(TimeOnly.MinValue);

            var vm = new MonitoringVm { From = f, To = t, Hours = hours };

            // ----- Bệnh nhân -----
            vm.PatientTotal = await _context.Patients.CountAsync();
            vm.PatientWithAppt = await _context.Patients.CountAsync(p => p.Appointments.Any());
            vm.PatientActive = await _context.Accounts.CountAsync(a => a.PatientId != null && a.IsActive);
            vm.PatientLocked = await _context.Accounts.CountAsync(a => a.PatientId != null && !a.IsActive);
            vm.PatientNewInRange = await _context.Accounts
                .CountAsync(a => a.PatientId != null && a.CreatedAt >= fromDt && a.CreatedAt < toDt);
            vm.PatientServedInRange = await _context.Appointments
                .Where(a => a.AppointmentDate >= f && a.AppointmentDate <= t && a.Status != TrangThaiCa.DaHuy)
                .Select(a => a.PatientId).Distinct().CountAsync();

            // Bệnh nhân mới theo tháng (6 tháng gần nhất, tính theo ngày tạo tài khoản)
            var firstMonth = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1).AddMonths(-5);
            var created = await _context.Accounts
                .Where(a => a.PatientId != null && a.CreatedAt >= firstMonth)
                .Select(a => a.CreatedAt).ToListAsync();
            for (var i = 0; i < 6; i++)
            {
                var m = firstMonth.AddMonths(i);
                vm.MonthLabels.Add(m.ToString("MM/yyyy", CultureInfo.InvariantCulture));
                vm.MonthNew.Add(created.Count(c => c.Year == m.Year && c.Month == m.Month));
            }

            // ----- Hồ sơ bệnh án theo từng bác sĩ -----
            vm.Workload = await LoadWorkloadAsync(f, t);

            // ----- Trạng thái bác sĩ -----
            vm.StatusRows = await DoctorStatusAnalyzer.AnalyzeAsync(_context, hours);
            vm.StatusSummary = (await _context.Doctors.GroupBy(d => d.Current)
                    .Select(g => new { g.Key, N = g.Count() }).ToListAsync())
                .Select(x => (TrangThaiBacSi.Label(x.Key), x.N)).ToList();

            return View(vm);
        }

        // POST: đưa bác sĩ nghi ngờ về trạng thái "Đang nhận khám"
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> ResetStatus(int id)
        {
            var d = await _context.Doctors.FindAsync(id);
            if (d != null)
            {
                d.Current = TrangThaiBacSi.Available;
                await _context.SaveChangesAsync();
                TempData["Msg"] = $"Đã đặt \"{d.FullName}\" về trạng thái Đang nhận khám.";
            }
            return RedirectToAction(nameof(Index));
        }

        // GET: xuất báo cáo hồ sơ theo bác sĩ ra CSV
        public async Task<IActionResult> ExportWorkload(DateOnly? from, DateOnly? to)
        {
            var (f, t) = DefaultRange(from, to);
            var rows = await LoadWorkloadAsync(f, t);
            var bytes = CsvBuilder.Build(
                new[] { "Bác sĩ", "Chuyên khoa", "Số ca khám", "Số ca hoàn thành", "Số hồ sơ bệnh án" },
                rows.Select(r => new[] { r.DoctorName, r.Specialty, r.Appointments.ToString(), r.Completed.ToString(), r.Records.ToString() }));
            return File(bytes, "text/csv", $"ho-so-theo-bac-si_{f:yyyyMMdd}_{t:yyyyMMdd}.csv");
        }

        private Task<List<DoctorWorkloadRow>> LoadWorkloadAsync(DateOnly f, DateOnly t)
        {
            var fromDt = f.ToDateTime(TimeOnly.MinValue);
            var toDt = t.AddDays(1).ToDateTime(TimeOnly.MinValue);
            return _context.Doctors
                .Select(d => new DoctorWorkloadRow
                {
                    DoctorId = d.DoctorId,
                    DoctorName = d.FullName,
                    Specialty = d.Spec.SpecialtyName,
                    Appointments = d.Appointments.Count(a => a.AppointmentDate >= f && a.AppointmentDate <= t
                                                          && a.Status != TrangThaiCa.DaHuy),
                    Completed = d.Appointments.Count(a => a.AppointmentDate >= f && a.AppointmentDate <= t
                                                       && a.Status == TrangThaiCa.HoanThanh),
                    Records = _context.MedicalRecords.Count(r => r.Appoint.DoctorId == d.DoctorId
                                                              && r.CreateAt >= fromDt && r.CreateAt < toDt)
                })
                .OrderByDescending(x => x.Records).ThenBy(x => x.DoctorName)
                .ToListAsync();
        }
    }
}
