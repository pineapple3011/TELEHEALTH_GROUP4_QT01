using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TELEHEALTH_GROUP4_QT01.Models;
using TELEHEALTH_GROUP4_QT01.Models.ViewModels;

namespace TELEHEALTH_GROUP4_QT01.Controllers
{
    public class AboutController : Controller
    {
        private readonly TelehealthContext _db;

        public AboutController(TelehealthContext db)
        {
            _db = db;
        }

        public async Task<IActionResult> Index()
        {
            var vm = new AboutVm
            {
                TotalDoctors = await _db.Doctors.CountAsync(),
                TotalPatients = await _db.Patients.CountAsync(),
                TotalAppointments = await _db.Appointments.CountAsync()
            };

            // Số bác sĩ theo chuyên khoa
            var doctorCounts = await _db.Doctors
                .GroupBy(d => d.Spec.SpecialtyName)
                .Select(g => new { Name = g.Key, Count = g.Count() })
                .ToListAsync();

            // Số đơn khám theo chuyên khoa
            var apptCounts = await _db.Appointments
                .GroupBy(a => a.Doctor.Spec.SpecialtyName)
                .Select(g => new { Name = g.Key, Count = g.Count() })
                .ToListAsync();

            // Danh sách dịch vụ = các chuyên khoa
            vm.Services = doctorCounts
                .Select(d => new AboutServiceVm
                {
                    Name = d.Name ?? "Khác",
                    DoctorCount = d.Count,
                    AppointmentCount = apptCounts.FirstOrDefault(a => a.Name == d.Name)?.Count ?? 0
                })
                .OrderByDescending(s => s.AppointmentCount)
                .ThenBy(s => s.Name)
                .ToList();

            // Top 4 chuyên khoa nhiều đơn nhất (thanh tỷ lệ)
            vm.Bars = apptCounts
                .OrderByDescending(x => x.Count)
                .Take(4)
                .Select(r => new AboutBarVm
                {
                    Label = r.Name ?? "Khác",
                    Count = r.Count,
                    Percent = vm.TotalAppointments == 0
                        ? 0
                        : (int)Math.Round(100.0 * r.Count / vm.TotalAppointments)
                }).ToList();

            return View(vm);
        }
    }
}