using System.Globalization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using TELEHEALTH_GROUP4_QT01.Helpers;
using TELEHEALTH_GROUP4_QT01.Models;
using TELEHEALTH_GROUP4_QT01.Models.ViewModels;

namespace TELEHEALTH_GROUP4_QT01.Controllers
{
    // Danh sách / lịch bác sĩ là thông tin công khai => cho phép xem khi chưa đăng nhập.
    // Muốn ĐẶT LỊCH thì phải đăng nhập (AppointmentsController yêu cầu vai trò Patient).
    [AllowAnonymous]
    public class DoctorsController : Controller
    {
        private readonly TelehealthContext _context;   // đổi tên nếu DbContext của bạn khác

        public DoctorsController(TelehealthContext context)
        {
            _context = context;
        }

        // GET: /Doctors?q=...&specId=...&onlyAvailable=true&sort=exp
        public async Task<IActionResult> Index(string? q, int? specId, bool onlyAvailable = false,
                                       string? sort = null, int page = 1)
        {
            const int PageSize = 6;

            var query = _context.Doctors.AsNoTracking().AsQueryable();
            if (specId.HasValue) query = query.Where(d => d.SpecId == specId.Value);
            if (!string.IsNullOrWhiteSpace(q))
            {
                var kw = q.Trim();
                query = query.Where(d => d.FullName.Contains(kw));
            }
            if (onlyAvailable) query = query.Where(d => d.Current == "Available");

            query = sort switch
            {
                "exp" => query.OrderByDescending(d => d.Exp).ThenBy(d => d.FullName),
                "feeAsc" => query.OrderBy(d => d.Spec.BasePrice).ThenBy(d => d.FullName),
                "feeDesc" => query.OrderByDescending(d => d.Spec.BasePrice).ThenBy(d => d.FullName),
                "name" => query.OrderBy(d => d.FullName),
                _ => query.OrderBy(d => d.Current == "Available" ? 0 : 1).ThenBy(d => d.FullName)
            };

            var paged = await PagedList<DoctorListItem>.CreateAsync(
                query.Select(d => new DoctorListItem
                {
                    DoctorId = d.DoctorId,
                    HoTen = d.FullName,
                    TenKhoa = d.Spec.SpecialtyName,
                    KinhNghiem = d.Exp.ToString(),
                    TrangThai = d.Current,
                    AvatarUrl = d.AvatarUrl,
                    Fee = (long)d.Spec.BasePrice
                }), page, PageSize);

            var specs = await _context.Specialties.AsNoTracking()
                .OrderBy(s => s.SpecialtyName)
                .Select(s => new { s.SpecId, s.SpecialtyName, Count = s.Doctors.Count })
                .ToListAsync();

            var vm = new DoctorListVm
            {
                Q = q,
                SpecId = specId,
                OnlyAvailable = onlyAvailable,
                Sort = sort,
                TotalAll = await _context.Doctors.CountAsync(),
                Doctors = paged.Items.ToList(),
                Page = paged.Page,
                TotalPages = paged.TotalPages,
                TotalFiltered = paged.TotalItems,
                SpecCards = specs.Select(s => new SpecCard
                {
                    SpecId = s.SpecId,
                    Name = s.SpecialtyName,
                    DoctorCount = s.Count
                }).ToList(),
                Specs = specs.Select(s => new SelectListItem
                {
                    Value = s.SpecId.ToString(),
                    Text = s.SpecialtyName,
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