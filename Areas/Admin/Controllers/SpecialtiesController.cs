using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TELEHEALTH_GROUP4_QT01.Areas.Admin.Models;
using TELEHEALTH_GROUP4_QT01.Models;

namespace TELEHEALTH_GROUP4_QT01.Areas.Admin.Controllers
{
    // Danh mục chuyên khoa + giá dịch vụ (giá khám cơ bản = Specialty.BasePrice) cùng một trang
    public class SpecialtiesController : AdminBaseController
    {
        private readonly TelehealthContext _context;
        public SpecialtiesController(TelehealthContext context) => _context = context;

        public async Task<IActionResult> Index()
        {
            var items = await _context.Specialties.OrderBy(s => s.SpecialtyName)
                .Select(s => new SpecRowVm
                {
                    SpecId = s.SpecId,
                    Name = s.SpecialtyName,
                    Description = s.Description,
                    Price = s.BasePrice,
                    DoctorCount = s.Doctors.Count(),
                    ClinicCount = s.Clinics.Count()
                }).ToListAsync();
            return View(items);
        }

        [HttpGet]
        public IActionResult Create() => View("Form", new SpecFormVm());

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(SpecFormVm vm)
        {
            await CheckNameAsync(vm);
            if (!ModelState.IsValid) return View("Form", vm);

            _context.Specialties.Add(new Specialty
            {
                SpecialtyName = vm.SpecialtyName.Trim(),
                Description = string.IsNullOrWhiteSpace(vm.Description) ? null : vm.Description.Trim(),
                BasePrice = vm.BasePrice
            });
            await _context.SaveChangesAsync();
            TempData["Msg"] = "Đã thêm chuyên khoa.";
            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            var s = await _context.Specialties.FindAsync(id);
            if (s == null) return NotFound();
            return View("Form", new SpecFormVm
            {
                SpecId = s.SpecId, SpecialtyName = s.SpecialtyName, Description = s.Description, BasePrice = s.BasePrice
            });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(SpecFormVm vm)
        {
            var s = await _context.Specialties.FindAsync(vm.SpecId);
            if (s == null) return NotFound();

            await CheckNameAsync(vm);
            if (!ModelState.IsValid) return View("Form", vm);

            s.SpecialtyName = vm.SpecialtyName.Trim();
            s.Description = string.IsNullOrWhiteSpace(vm.Description) ? null : vm.Description.Trim();
            s.BasePrice = vm.BasePrice;
            await _context.SaveChangesAsync();
            TempData["Msg"] = "Đã cập nhật chuyên khoa.";
            return RedirectToAction(nameof(Index));
        }

        // Sửa nhanh giá ngay trên danh sách
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdatePrice(int id, decimal price)
        {
            var s = await _context.Specialties.FindAsync(id);
            if (s == null) return NotFound();
            if (price < 0 || price > 1_000_000_000m)
            {
                TempData["Err"] = "Giá không hợp lệ.";
                return RedirectToAction(nameof(Index));
            }
            s.BasePrice = price;
            await _context.SaveChangesAsync();
            TempData["Msg"] = $"Đã cập nhật giá khám của \"{s.SpecialtyName}\". Chỉ áp dụng cho lịch khám đặt mới.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var s = await _context.Specialties.FindAsync(id);
            if (s == null) return RedirectToAction(nameof(Index));

            var nDoctors = await _context.Doctors.CountAsync(d => d.SpecId == id);
            var nClinics = await _context.Clinics.CountAsync(c => c.SpecId == id);
            if (nDoctors > 0 || nClinics > 0)
            {
                TempData["Err"] = $"Không thể xóa \"{s.SpecialtyName}\": còn {nDoctors} bác sĩ và {nClinics} phòng khám thuộc chuyên khoa này.";
                return RedirectToAction(nameof(Index));
            }
            _context.Specialties.Remove(s);
            await _context.SaveChangesAsync();
            TempData["Msg"] = $"Đã xóa chuyên khoa \"{s.SpecialtyName}\".";
            return RedirectToAction(nameof(Index));
        }

        private async Task CheckNameAsync(SpecFormVm vm)
        {
            var name = vm.SpecialtyName?.Trim() ?? "";
            if (name.Length > 0 && await _context.Specialties.AnyAsync(s => s.SpecialtyName == name && s.SpecId != vm.SpecId))
                ModelState.AddModelError(nameof(vm.SpecialtyName), "Tên chuyên khoa đã tồn tại");
        }
    }
}
