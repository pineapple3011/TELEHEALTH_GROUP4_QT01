using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using TELEHEALTH_GROUP4_QT01.Areas.Admin.Models;
using TELEHEALTH_GROUP4_QT01.Helpers;
using TELEHEALTH_GROUP4_QT01.Models;

namespace TELEHEALTH_GROUP4_QT01.Areas.Admin.Controllers
{
    // Quản lý tài khoản: bệnh nhân + bác sĩ (hồ sơ Patient/Doctor đi kèm bản ghi Account để đăng nhập)
    public class AccountsController : AdminBaseController
    {
        private readonly TelehealthContext _context;
        private readonly IWebHostEnvironment _env;

        // Băm mật khẩu bằng SHA256, xuất chuỗi hex chữ HOA (64 ký tự).
        // Phải trùng với DoctorDb.Hash ở phân hệ bác sĩ và dữ liệu có sẵn trong bảng Accounts.
        private static string HashPassword(string plain) =>
            Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(plain)));

        public AccountsController(TelehealthContext context, IWebHostEnvironment env)
        {
            _context = context;
            _env = env;
        }

        public IActionResult Index() => RedirectToAction(nameof(Patients));

        // =====================================================================
        //  BỆNH NHÂN
        // =====================================================================
        public async Task<IActionResult> Patients(string? q)
        {
            var query = _context.Patients.AsQueryable();
            if (!string.IsNullOrWhiteSpace(q))
            {
                var k = q.Trim();
                query = query.Where(p => p.FullName.Contains(k) || p.Email.Contains(k) || p.Phone.Contains(k));
            }

            var items = await query.OrderBy(p => p.FullName)
                .Select(p => new PatientRowVm
                {
                    PatientId = p.PatientId,
                    FullName = p.FullName,
                    Email = p.Email,
                    Phone = p.Phone,
                    Address = p.Address,
                    AppointmentCount = p.Appointments.Count(),
                    HasAccount = p.Accounts.Any(),
                    IsActive = p.Accounts.Any(a => a.IsActive)
                }).ToListAsync();

            return View(new PatientListVm { Q = q, Items = items });
        }

        [HttpGet]
        public IActionResult CreatePatient() => View("PatientForm", new PatientFormVm());

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> CreatePatient(PatientFormVm vm)
        {
            if (string.IsNullOrWhiteSpace(vm.Password))
                ModelState.AddModelError(nameof(vm.Password), "Vui lòng nhập mật khẩu cho tài khoản mới");
            await CheckPatientEmailAsync(vm);
            if (!ModelState.IsValid) return View("PatientForm", vm);

            var patient = new Patient
            {
                FullName = vm.FullName.Trim(),
                Email = vm.Email.Trim(),
                Phone = vm.Phone.Trim(),
                Address = Clean(vm.Address),
                EmergencyPhone = Clean(vm.EmergencyPhone),
                AdminId = CurrentAdminId
            };
            _context.Patients.Add(patient);
            _context.Accounts.Add(new Account
            {
                Email = patient.Email,
                PasswordHash = HashPassword(vm.Password!),
                Role = VaiTro.Patient,
                Patient = patient,            // EF tự gán PatientId, lưu cùng 1 SaveChanges (atomic)
                IsActive = vm.IsActive,
                CreatedAt = DateTime.Now
            });
            await _context.SaveChangesAsync();

            TempData["Msg"] = "Đã thêm bệnh nhân và tài khoản đăng nhập.";
            return RedirectToAction(nameof(Patients));
        }

        [HttpGet]
        public async Task<IActionResult> EditPatient(int id)
        {
            var p = await _context.Patients.Include(x => x.Accounts).FirstOrDefaultAsync(x => x.PatientId == id);
            if (p == null) return NotFound();
            return View("PatientForm", new PatientFormVm
            {
                PatientId = p.PatientId,
                FullName = p.FullName,
                Email = p.Email,
                Phone = p.Phone,
                Address = p.Address,
                EmergencyPhone = p.EmergencyPhone,
                IsActive = p.Accounts.Count == 0 || p.Accounts.Any(a => a.IsActive)
            });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> EditPatient(PatientFormVm vm)
        {
            var p = await _context.Patients.Include(x => x.Accounts).FirstOrDefaultAsync(x => x.PatientId == vm.PatientId);
            if (p == null) return NotFound();

            await CheckPatientEmailAsync(vm);
            var acc = p.Accounts.FirstOrDefault();
            if (acc == null && string.IsNullOrWhiteSpace(vm.Password))
                ModelState.AddModelError(nameof(vm.Password), "Bệnh nhân này chưa có tài khoản đăng nhập, vui lòng nhập mật khẩu để tạo");
            if (!ModelState.IsValid) return View("PatientForm", vm);

            p.FullName = vm.FullName.Trim();
            p.Email = vm.Email.Trim();
            p.Phone = vm.Phone.Trim();
            p.Address = Clean(vm.Address);
            p.EmergencyPhone = Clean(vm.EmergencyPhone);

            if (acc == null)
            {
                _context.Accounts.Add(new Account
                {
                    Email = p.Email,
                    PasswordHash = HashPassword(vm.Password!),
                    Role = VaiTro.Patient,
                    PatientId = p.PatientId,
                    IsActive = vm.IsActive,
                    CreatedAt = DateTime.Now
                });
            }
            else
            {
                acc.Email = p.Email;
                acc.IsActive = vm.IsActive;
                if (!string.IsNullOrWhiteSpace(vm.Password)) acc.PasswordHash = HashPassword(vm.Password);
            }
            await _context.SaveChangesAsync();

            TempData["Msg"] = "Đã cập nhật bệnh nhân.";
            return RedirectToAction(nameof(Patients));
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> DeletePatient(int id)
        {
            var p = await _context.Patients.FindAsync(id);
            if (p == null) return RedirectToAction(nameof(Patients));

            if (await _context.Appointments.AnyAsync(a => a.PatientId == id))
            {
                TempData["Err"] = $"Không thể xóa \"{p.FullName}\" vì đã có lịch khám. Hãy khóa tài khoản thay vì xóa.";
                return RedirectToAction(nameof(Patients));
            }
            _context.Accounts.RemoveRange(_context.Accounts.Where(a => a.PatientId == id));
            _context.Patients.Remove(p);
            await _context.SaveChangesAsync();

            TempData["Msg"] = $"Đã xóa bệnh nhân \"{p.FullName}\".";
            return RedirectToAction(nameof(Patients));
        }

        private async Task CheckPatientEmailAsync(PatientFormVm vm)
        {
            var email = vm.Email?.Trim() ?? "";
            if (email.Length == 0) return;
            var dup = await _context.Patients.AnyAsync(x => x.Email == email && x.PatientId != vm.PatientId)
                   || await _context.Accounts.AnyAsync(x => x.Email == email && x.PatientId != vm.PatientId);
            if (dup) ModelState.AddModelError(nameof(vm.Email), "Email này đã được sử dụng");
        }

        // =====================================================================
        //  BÁC SĨ
        // =====================================================================
        public async Task<IActionResult> Doctors(string? q, int? specId)
        {
            var query = _context.Doctors.AsQueryable();
            if (specId != null) query = query.Where(d => d.SpecId == specId);
            if (!string.IsNullOrWhiteSpace(q))
            {
                var k = q.Trim();
                query = query.Where(d => d.FullName.Contains(k) || d.Email.Contains(k) || d.Phone.Contains(k));
            }

            var items = await query.OrderBy(d => d.FullName)
                .Select(d => new DoctorRowVm
                {
                    DoctorId = d.DoctorId,
                    FullName = d.FullName,
                    Email = d.Email,
                    Phone = d.Phone,
                    Specialty = d.Spec.SpecialtyName,
                    Exp = d.Exp,
                    Status = d.Current,
                    AvatarUrl = d.AvatarUrl,
                    AppointmentCount = d.Appointments.Count(),
                    HasAccount = d.Accounts.Any(),
                    IsActive = d.Accounts.Any(a => a.IsActive)
                }).ToListAsync();

            return View(new DoctorListVm { Q = q, SpecId = specId, Specs = await SpecItemsAsync(specId), Items = items });
        }

        [HttpGet]
        public async Task<IActionResult> CreateDoctor()
            => View("DoctorForm", new DoctorFormVm { Specs = await SpecItemsAsync(null) });

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateDoctor(DoctorFormVm vm)
        {
            if (string.IsNullOrWhiteSpace(vm.Password))
                ModelState.AddModelError(nameof(vm.Password), "Vui lòng nhập mật khẩu cho tài khoản mới");
            await CheckDoctorAsync(vm);
            if (!ModelState.IsValid)
            {
                vm.Specs = await SpecItemsAsync(vm.SpecId);
                return View("DoctorForm", vm);
            }

            string? avatar = null;   // chỉ nhận ảnh từ file tải lên, không tin đường dẫn gửi từ form
            if (vm.AvatarFile is { Length: > 0 }) avatar = await SaveAvatarAsync(vm.AvatarFile);

            var doctor = new TELEHEALTH_GROUP4_QT01.Models.Doctor
            {
                FullName = vm.FullName.Trim(),
                Email = vm.Email.Trim(),
                Phone = vm.Phone.Trim(),
                SpecId = vm.SpecId,
                Exp = vm.Exp,
                Current = string.IsNullOrWhiteSpace(vm.Current) ? TrangThaiBacSi.Available : vm.Current,
                AvatarUrl = avatar,
                AdminId = CurrentAdminId
            };
            _context.Doctors.Add(doctor);
            _context.Accounts.Add(new Account
            {
                Email = doctor.Email,
                PasswordHash = HashPassword(vm.Password!),
                Role = VaiTro.Doctor,
                Doctor = doctor,
                IsActive = vm.IsActive,
                CreatedAt = DateTime.Now
            });
            await _context.SaveChangesAsync();

            TempData["Msg"] = "Đã thêm bác sĩ và tài khoản đăng nhập.";
            return RedirectToAction(nameof(Doctors));
        }

        [HttpGet]
        public async Task<IActionResult> EditDoctor(int id)
        {
            var d = await _context.Doctors.Include(x => x.Accounts).FirstOrDefaultAsync(x => x.DoctorId == id);
            if (d == null) return NotFound();
            return View("DoctorForm", new DoctorFormVm
            {
                DoctorId = d.DoctorId,
                FullName = d.FullName,
                Email = d.Email,
                Phone = d.Phone,
                SpecId = d.SpecId,
                Exp = d.Exp,
                Current = d.Current,
                AvatarUrl = d.AvatarUrl,
                IsActive = d.Accounts.Count == 0 || d.Accounts.Any(a => a.IsActive),
                Specs = await SpecItemsAsync(d.SpecId)
            });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> EditDoctor(DoctorFormVm vm)
        {
            var d = await _context.Doctors.Include(x => x.Accounts).FirstOrDefaultAsync(x => x.DoctorId == vm.DoctorId);
            if (d == null) return NotFound();

            await CheckDoctorAsync(vm);
            var acc = d.Accounts.FirstOrDefault();
            if (acc == null && string.IsNullOrWhiteSpace(vm.Password))
                ModelState.AddModelError(nameof(vm.Password), "Bác sĩ này chưa có tài khoản đăng nhập, vui lòng nhập mật khẩu để tạo");
            if (!ModelState.IsValid)
            {
                vm.Specs = await SpecItemsAsync(vm.SpecId);
                return View("DoctorForm", vm);
            }

            if (vm.AvatarFile is { Length: > 0 }) d.AvatarUrl = await SaveAvatarAsync(vm.AvatarFile);
            d.FullName = vm.FullName.Trim();
            d.Email = vm.Email.Trim();
            d.Phone = vm.Phone.Trim();
            d.SpecId = vm.SpecId;
            d.Exp = vm.Exp;
            d.Current = string.IsNullOrWhiteSpace(vm.Current) ? TrangThaiBacSi.Available : vm.Current;

            if (acc == null)
            {
                _context.Accounts.Add(new Account
                {
                    Email = d.Email,
                    PasswordHash = HashPassword(vm.Password!),
                    Role = VaiTro.Doctor,
                    DoctorId = d.DoctorId,
                    IsActive = vm.IsActive,
                    CreatedAt = DateTime.Now
                });
            }
            else
            {
                acc.Email = d.Email;
                acc.IsActive = vm.IsActive;
                if (!string.IsNullOrWhiteSpace(vm.Password)) acc.PasswordHash = HashPassword(vm.Password);
            }
            await _context.SaveChangesAsync();

            TempData["Msg"] = "Đã cập nhật bác sĩ.";
            return RedirectToAction(nameof(Doctors));
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteDoctor(int id)
        {
            var d = await _context.Doctors.FindAsync(id);
            if (d == null) return RedirectToAction(nameof(Doctors));

            if (await _context.Appointments.AnyAsync(a => a.DoctorId == id))
            {
                TempData["Err"] = $"Không thể xóa \"{d.FullName}\" vì đã có lịch khám. Hãy khóa tài khoản thay vì xóa.";
                return RedirectToAction(nameof(Doctors));
            }
            _context.Schedules.RemoveRange(_context.Schedules.Where(s => s.DoctorId == id));
            _context.Accounts.RemoveRange(_context.Accounts.Where(a => a.DoctorId == id));
            _context.Doctors.Remove(d);
            await _context.SaveChangesAsync();

            TempData["Msg"] = $"Đã xóa bác sĩ \"{d.FullName}\".";
            return RedirectToAction(nameof(Doctors));
        }

        private async Task CheckDoctorAsync(DoctorFormVm vm)
        {
            var email = vm.Email?.Trim() ?? "";
            if (email.Length > 0)
            {
                var dup = await _context.Doctors.AnyAsync(x => x.Email == email && x.DoctorId != vm.DoctorId)
                       || await _context.Accounts.AnyAsync(x => x.Email == email && x.DoctorId != vm.DoctorId);
                if (dup) ModelState.AddModelError(nameof(vm.Email), "Email này đã được sử dụng");
            }
            if (vm.SpecId > 0 && !await _context.Specialties.AnyAsync(s => s.SpecId == vm.SpecId))
                ModelState.AddModelError(nameof(vm.SpecId), "Chuyên khoa không tồn tại");
            if (vm.AvatarFile is { Length: > 0 })
            {
                var ext = Path.GetExtension(vm.AvatarFile.FileName).ToLowerInvariant();
                if (!AvatarExt.Contains(ext)) ModelState.AddModelError(nameof(vm.AvatarFile), "Ảnh phải là JPG, PNG hoặc WEBP");
                else if (vm.AvatarFile.Length > 2 * 1024 * 1024) ModelState.AddModelError(nameof(vm.AvatarFile), "Ảnh tối đa 2MB");
            }
        }

        private static readonly string[] AvatarExt = { ".jpg", ".jpeg", ".png", ".webp" };

        private async Task<string> SaveAvatarAsync(IFormFile f)
        {
            var dir = Path.Combine(_env.WebRootPath, "uploads", "doctors");
            Directory.CreateDirectory(dir);
            var name = Guid.NewGuid().ToString("N") + Path.GetExtension(f.FileName).ToLowerInvariant();
            await using var fs = new FileStream(Path.Combine(dir, name), FileMode.Create);
            await f.CopyToAsync(fs);
            return "/uploads/doctors/" + name;
        }

        private async Task<List<SelectListItem>> SpecItemsAsync(int? selected)
            => (await _context.Specialties.OrderBy(s => s.SpecialtyName).ToListAsync())
                .Select(s => new SelectListItem(s.SpecialtyName, s.SpecId.ToString(), s.SpecId == selected)).ToList();

        private static string? Clean(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
    }
}