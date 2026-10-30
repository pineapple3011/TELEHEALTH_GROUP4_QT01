using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using TELEHEALTH_GROUP4_QT01.Areas.Admin.Models;
using TELEHEALTH_GROUP4_QT01.Models;

namespace TELEHEALTH_GROUP4_QT01.Areas.Admin.Controllers
{
    public class ClinicsController : AdminBaseController
    {
        private readonly TelehealthContext _context;
        public ClinicsController(TelehealthContext context) => _context = context;

        public async Task<IActionResult> Index(int? specId)
        {
            var query = _context.Clinics.AsQueryable();
            if (specId != null) query = query.Where(c => c.SpecId == specId);

            var items = await query.OrderBy(c => c.Spec.SpecialtyName).ThenBy(c => c.ClinicName)
                .Select(c => new ClinicRowVm
                {
                    ClinicId = c.ClinicId,
                    Name = c.ClinicName,
                    Location = c.Location,
                    Specialty = c.Spec.SpecialtyName,
                    AppointmentCount = c.Appointments.Count()
                }).ToListAsync();

            return View(new ClinicListVm { SpecId = specId, Specs = await SpecItemsAsync(specId), Items = items });
        }

        [HttpGet]
        public async Task<IActionResult> Create(int? specId)
            => View("Form", new ClinicFormVm { SpecId = specId ?? 0, Specs = await SpecItemsAsync(specId) });

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(ClinicFormVm vm)
        {
            await CheckAsync(vm);
            if (!ModelState.IsValid)
            {
                vm.Specs = await SpecItemsAsync(vm.SpecId);
                return View("Form", vm);
            }
            _context.Clinics.Add(new Clinic
            {
                ClinicName = vm.ClinicName.Trim(), Location = vm.Location.Trim(), SpecId = vm.SpecId
            });
            await _context.SaveChangesAsync();
            TempData["Msg"] = "Đã thêm phòng khám.";
            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            var c = await _context.Clinics.FindAsync(id);
            if (c == null) return NotFound();
            return View("Form", new ClinicFormVm
            {
                ClinicId = c.ClinicId, ClinicName = c.ClinicName, Location = c.Location, SpecId = c.SpecId,
                Specs = await SpecItemsAsync(c.SpecId)
            });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(ClinicFormVm vm)
        {
            var c = await _context.Clinics.FindAsync(vm.ClinicId);
            if (c == null) return NotFound();

            await CheckAsync(vm);
            if (!ModelState.IsValid)
            {
                vm.Specs = await SpecItemsAsync(vm.SpecId);
                return View("Form", vm);
            }
            c.ClinicName = vm.ClinicName.Trim();
            c.Location = vm.Location.Trim();
            c.SpecId = vm.SpecId;
            await _context.SaveChangesAsync();
            TempData["Msg"] = "Đã cập nhật phòng khám.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var c = await _context.Clinics.FindAsync(id);
            if (c == null) return RedirectToAction(nameof(Index));

            if (await _context.Appointments.AnyAsync(a => a.ClinicId == id))
            {
                TempData["Err"] = $"Không thể xóa \"{c.ClinicName}\" vì đã có lịch khám tại phòng này.";
                return RedirectToAction(nameof(Index));
            }
            _context.Clinics.Remove(c);
            await _context.SaveChangesAsync();
            TempData["Msg"] = $"Đã xóa phòng khám \"{c.ClinicName}\".";
            return RedirectToAction(nameof(Index));
        }

        private async Task CheckAsync(ClinicFormVm vm)
        {
            if (vm.SpecId > 0 && !await _context.Specialties.AnyAsync(s => s.SpecId == vm.SpecId))
                ModelState.AddModelError(nameof(vm.SpecId), "Chuyên khoa không tồn tại");
        }

        private async Task<List<SelectListItem>> SpecItemsAsync(int? selected)
            => (await _context.Specialties.OrderBy(s => s.SpecialtyName).ToListAsync())
                .Select(s => new SelectListItem(s.SpecialtyName, s.SpecId.ToString(), s.SpecId == selected)).ToList();
    }
}
