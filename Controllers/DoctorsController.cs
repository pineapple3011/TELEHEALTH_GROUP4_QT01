using System.Globalization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using TELEHEALTH_GROUP4_QT01.Helpers;
using TELEHEALTH_GROUP4_QT01.Models;
using TELEHEALTH_GROUP4_QT01.Models.ViewModels;

namespace TELEHEALTH_GROUP4_QT01.Controllers
{
    public class DoctorsController : PatientBaseController
    {
        private readonly TelehealthContext _context;   // đổi tên nếu DbContext của bạn khác

        public DoctorsController(TelehealthContext context)
        {
            _context = context;
        }

        // GET: /Doctors?q=...&specId=...
        public async Task<IActionResult> Index(string? q, int? specId)
        {
            var specs = await _context.Specialties.ToListAsync();
            var doctors = await _context.Doctors.ToListAsync();

            var items = doctors
                .Where(d => specId == null || Cv.I(d.SpecId) == specId)
                .Where(d => string.IsNullOrWhiteSpace(q) ||
                            (d.FullName ?? "").Contains(q.Trim(), StringComparison.OrdinalIgnoreCase))
                .Select(d => new DoctorListItem
                {
                    DoctorId = d.DoctorId,
                    HoTen = d.FullName ?? "",
                    TenKhoa = specs.FirstOrDefault(s => s.SpecId == Cv.I(d.SpecId))?.SpecialtyName ?? "",
                    KinhNghiem = Convert.ToString(d.Exp) ?? "",
                    TrangThai = Convert.ToString(d.Current) ?? "",
                    AvatarUrl = d.AvatarUrl,
                    Fee = (long)Cv.M(specs.FirstOrDefault(s => s.SpecId == Cv.I(d.SpecId))?.BasePrice)
                })
                .OrderByDescending(x => Cv.IsAvailable(x.TrangThai))
                .ToList();

            var vm = new DoctorListVm
            {
                Q = q,
                SpecId = specId,
                Doctors = items,
                Specs = specs.Select(s => new SelectListItem
                {
                    Value = s.SpecId.ToString(),
                    Text = s.SpecialtyName ?? "",
                    Selected = s.SpecId == specId
                }).ToList()
            };
            return View(vm);
        }

        // GET: /Doctors/Details/5
        public async Task<IActionResult> Details(int id)
        {
            var doctor = await _context.Doctors.FindAsync(id);
            if (doctor == null) return NotFound();

            var spec = await _context.Specialties.FindAsync(Cv.I(doctor.SpecId));

            var today = DateOnly.FromDateTime(DateTime.Today);
            var nowTime = TimeOnly.FromDateTime(DateTime.Now);
            var vi = CultureInfo.GetCultureInfo("vi-VN");

            // Các ca làm việc từ hôm nay đến 90 ngày tới (lịch chọn ngày sẽ hiển thị theo tháng)
            var horizon = today.AddDays(90);
            var schedules = (await _context.Schedules.Where(l => l.DoctorId == id).ToListAsync())
                .Where(l => Cv.D(l.WorkDate) >= today && Cv.D(l.WorkDate) <= horizon)
                .OrderBy(l => Cv.D(l.WorkDate)).ThenBy(l => Cv.T(l.StartTime))
                .ToList();

            var booked = await _context.Appointments.Where(c => c.DoctorId == id).ToListAsync();

            var days = new List<DayVm>();
            foreach (var l in schedules)
            {
                var free = SlotHelper.FreeSlots(l, booked.Where(c => Cv.I(c.ScheduleId) == l.ScheduleId));
                var date = Cv.D(l.WorkDate);
                if (date == today) free = free.Where(t => t > nowTime).ToList();   // bỏ giờ đã qua

                days.Add(new DayVm
                {
                    ScheduleId = l.ScheduleId,
                    DateIso = date.ToString("yyyy-MM-dd"),
                    DateText = date.ToString("dddd, dd/MM/yyyy", vi),
                    RangeText = $"{Cv.F(l.StartTime, "HH:mm")} - {Cv.F(l.EndTime, "HH:mm")}",
                    FreeSlots = free.Select(t => t.ToString("HH:mm")).ToList()
                });
            }

            var vm = new DoctorDetailsVm
            {
                DoctorId = doctor.DoctorId,
                HoTen = doctor.FullName ?? "",
                TenKhoa = spec?.SpecialtyName ?? "",
                MoTa = Convert.ToString(spec?.Description) ?? "",
                KinhNghiem = Convert.ToString(doctor.Exp) ?? "",
                TrangThai = Convert.ToString(doctor.Current) ?? "",
                AvatarUrl = doctor.AvatarUrl,
                Fee = (long)Cv.M(spec?.BasePrice),
                Days = days
            };
            return View(vm);
        }
    }
}
