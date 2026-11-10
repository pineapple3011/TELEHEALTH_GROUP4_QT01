using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TELEHEALTH_GROUP4_QT01.Areas.Admin.Models;
using TELEHEALTH_GROUP4_QT01.Models;

namespace TELEHEALTH_GROUP4_QT01.Areas.Admin.Controllers
{
    // Danh mục thuốc: CHỈ admin được thêm / sửa / xóa (AdminBaseController đã có [Authorize(Roles = Admin)]).
    // Bác sĩ chỉ chọn thuốc từ danh mục này khi kê đơn.
    public class MedicinesController : AdminBaseController
    {
        private readonly TelehealthContext _context;
        private readonly ILogger<MedicinesController> _logger;

        public MedicinesController(TelehealthContext context, ILogger<MedicinesController> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<IActionResult> Index(string? q)
        {
            var query = _context.Medicines.AsQueryable();
            if (!string.IsNullOrWhiteSpace(q))
            {
                var key = q.Trim();
                query = query.Where(m => m.MedicineName.Contains(key) || (m.Ingredient != null && m.Ingredient.Contains(key)));
            }

            var items = await query.OrderBy(m => m.MedicineName)
                .Select(m => new MedicineRowVm
                {
                    MedicineId = m.MedicineId,
                    Name = m.MedicineName,
                    Ingredient = m.Ingredient,
                    Hds = m.Hds,
                    Price = m.Price,
                    UsedCount = m.Prescriptions.Count()
                }).ToListAsync();

            return View(new MedicineListVm { Q = q, Items = items });
        }

        [HttpGet]
        public IActionResult Create() => View("Form", new MedicineFormVm());

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(MedicineFormVm vm)
        {
            await CheckNameAsync(vm);
            if (!ModelState.IsValid) return View("Form", vm);

            var m = new Medicine
            {
                MedicineName = vm.MedicineName.Trim(),
                Ingredient = Clean(vm.Ingredient),
                Hds = Clean(vm.Hds),
                Price = vm.Price
            };
            _context.Medicines.Add(m);
            await _context.SaveChangesAsync();
            _logger.LogInformation("Admin {AdminId} added medicine {MedicineId} ({Name})", CurrentAdminId, m.MedicineId, m.MedicineName);
            TempData["Msg"] = $"Đã thêm thuốc \"{m.MedicineName}\" vào hệ thống.";
            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            var m = await _context.Medicines.FindAsync(id);
            if (m == null) return NotFound();
            return View("Form", new MedicineFormVm
            {
                MedicineId = m.MedicineId,
                MedicineName = m.MedicineName,
                Ingredient = m.Ingredient,
                Hds = m.Hds,
                Price = m.Price
            });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(MedicineFormVm vm)
        {
            var m = await _context.Medicines.FindAsync(vm.MedicineId);
            if (m == null) return NotFound();

            await CheckNameAsync(vm);
            if (!ModelState.IsValid) return View("Form", vm);

            m.MedicineName = vm.MedicineName.Trim();
            m.Ingredient = Clean(vm.Ingredient);
            m.Hds = Clean(vm.Hds);
            m.Price = vm.Price;
            await _context.SaveChangesAsync();
            _logger.LogInformation("Admin {AdminId} updated medicine {MedicineId}", CurrentAdminId, m.MedicineId);
            TempData["Msg"] = "Đã cập nhật thuốc.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var m = await _context.Medicines.FindAsync(id);
            if (m == null) return RedirectToAction(nameof(Index));

            // Thuốc đã được kê trong đơn thì không xóa để không làm mất lịch sử đơn thuốc
            var used = await _context.Prescriptions.CountAsync(p => p.MedicineId == id);
            if (used > 0)
            {
                TempData["Err"] = $"Không thể xóa \"{m.MedicineName}\": thuốc này đã được kê trong {used} đơn thuốc.";
                return RedirectToAction(nameof(Index));
            }

            _context.Medicines.Remove(m);
            await _context.SaveChangesAsync();
            _logger.LogInformation("Admin {AdminId} deleted medicine {MedicineId}", CurrentAdminId, id);
            TempData["Msg"] = $"Đã xóa thuốc \"{m.MedicineName}\".";
            return RedirectToAction(nameof(Index));
        }

        private async Task CheckNameAsync(MedicineFormVm vm)
        {
            var name = vm.MedicineName?.Trim() ?? "";
            if (name.Length > 0 && await _context.Medicines.AnyAsync(x => x.MedicineName == name && x.MedicineId != vm.MedicineId))
                ModelState.AddModelError(nameof(vm.MedicineName), "Thuốc này đã có trong hệ thống");
        }

        private static string? Clean(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
    }
}
