using System.Globalization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TELEHEALTH_GROUP4_QT01.Areas.Admin.Helpers;
using TELEHEALTH_GROUP4_QT01.Areas.Admin.Models;
using TELEHEALTH_GROUP4_QT01.Helpers;
using TELEHEALTH_GROUP4_QT01.Models;

namespace TELEHEALTH_GROUP4_QT01.Areas.Admin.Controllers
{
    public class DashboardController : AdminBaseController
    {
        private readonly TelehealthContext _context;
        public DashboardController(TelehealthContext context) => _context = context;

        public async Task<IActionResult> Index()
        {
            var today = DateOnly.FromDateTime(DateTime.Today);
            var firstMonth = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1).AddMonths(-5);

            var vm = new DashboardVm
            {
                PatientTotal = await _context.Patients.CountAsync(),
                DoctorTotal = await _context.Doctors.CountAsync(),
                TodayCases = await _context.Appointments.CountAsync(a => a.AppointmentDate == today && a.Status != TrangThaiCa.DaHuy),
                PendingPayments = await _context.Payments.CountAsync(p => p.Status == TtGiaoDich.ChoXacNhan),
                PendingAppointments = await _context.Appointments.CountAsync(a => a.Status == TrangThaiCa.ChoXacNhan)
            };

            var paid = await _context.Payments
                .Where(p => p.Status == TtGiaoDich.DaThanhToan && p.PaymentTime >= firstMonth)
                .Select(p => new { p.PaymentTime, p.Amount }).ToListAsync();
            for (var i = 0; i < 6; i++)
            {
                var m = firstMonth.AddMonths(i);
                vm.MonthLabels.Add(m.ToString("MM/yyyy", CultureInfo.InvariantCulture));
                vm.MonthRevenue.Add(paid.Where(x => x.PaymentTime.Year == m.Year && x.PaymentTime.Month == m.Month).Sum(x => x.Amount));
            }
            vm.RevenueThisMonth = vm.MonthRevenue.Last();

            vm.Suspicious = (await DoctorStatusAnalyzer.AnalyzeAsync(_context)).Where(r => r.Suspicious).Take(5).ToList();
            return View(vm);
        }
    }
}
