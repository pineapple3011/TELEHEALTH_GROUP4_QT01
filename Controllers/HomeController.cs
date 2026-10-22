using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using TELEHEALTH_GROUP4_QT01.Helpers;
using TELEHEALTH_GROUP4_QT01.Models;
using TELEHEALTH_GROUP4_QT01.Models.ViewModels;

namespace TELEHEALTH_GROUP4_QT01.Controllers
{
    public class HomeController : Controller
    {
        private readonly TelehealthContext _context;
        public HomeController(TelehealthContext context)
        {
            _context = context;
        }
        private readonly ILogger<HomeController> _logger;
        //=======================================

        public async Task<IActionResult> Index()
        {
            var specs = await _context.Specialties.ToListAsync();
            var doctors = await _context.Doctors.ToListAsync();

            // Bác sĩ nổi bật: ưu tiên người đang nhận khám, nhiều kinh nghiệm nhất
            var featured = doctors
                .OrderByDescending(d => Cv.IsAvailable(d.Current))
                .ThenByDescending(d => d.Exp)
                .Take(4)
                .Select(d =>
                {
                    var sp = specs.FirstOrDefault(s => s.SpecId == Cv.I(d.SpecId));
                    return new DoctorListItem
                    {
                        DoctorId = d.DoctorId,
                        HoTen = d.FullName ?? "",
                        TenKhoa = sp?.SpecialtyName ?? "",
                        KinhNghiem = Convert.ToString(d.Exp) ?? "",
                        TrangThai = Convert.ToString(d.Current) ?? "",
                        AvatarUrl = d.AvatarUrl,
                        Fee = (long)Cv.M(sp?.BasePrice)
                    };
                })
                .ToList();

            var vm = new HomeVm
            {
                Doctors = featured,
                Specs = specs.Select(s => new SpecCard
                {
                    SpecId = s.SpecId,
                    Name = s.SpecialtyName ?? "",
                    DoctorCount = doctors.Count(d => Cv.I(d.SpecId) == s.SpecId)
                }).ToList()
            };
            return View(vm);
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
