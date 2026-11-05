using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using TELEHEALTH_GROUP4_QT01.Models;
using TELEHEALTH_GROUP4_QT01.Models.ViewModels;
using TELEHEALTH_GROUP4_QT01.Security;

namespace TELEHEALTH_GROUP4_QT01.Controllers
{
    /// <summary>
    /// Quản trị tài khoản. [Authorize(Roles = Admin)] được kiểm tra ở SERVER cho mọi action:
    /// ẩn menu trên giao diện chỉ là làm đẹp, không phải bảo mật.
    /// </summary>
    [Authorize(Roles = AppRoles.Admin)]
    public class AdminController : Controller
    {
        private readonly TelehealthContext _db;
        private readonly IPasswordService _passwords;
        private readonly ILogger<AdminController> _logger;

        public AdminController(TelehealthContext db, IPasswordService passwords, ILogger<AdminController> logger)
        {
            _db = db;
            _passwords = passwords;
            _logger = logger;
        }

        // GET: /Admin/Users
        [HttpGet]
        public async Task<IActionResult> Users()
        {
            var myId = CurrentUserId();
            var now = DateTime.UtcNow;

            var accounts = await _db.UserAccounts.AsNoTracking()
                .Include(a => a.Patient).Include(a => a.Doctor).Include(a => a.Admin)
                .OrderBy(a => a.Role).ThenBy(a => a.Email)
                .ToListAsync();

            var rows = accounts.Select(a => new UserRowVm
            {
                UserId = a.UserId,
                Email = a.Email,
                Role = a.Role,
                DisplayName = a.Patient?.FullName ?? a.Doctor?.FullName ?? a.Admin?.FullName ?? "",
                IsActive = a.IsActive,
                IsLocked = a.LockoutEndUtc != null && a.LockoutEndUtc > now,
                IsSelf = a.UserId == myId,
                LastLogin = a.LastLoginUtc?.ToLocalTime().ToString("dd/MM/yyyy HH:mm") ?? "—"
            }).ToList();

            return View(rows);
        }

        // GET: /Admin/CreateDoctorAccount
        [HttpGet]
        public async Task<IActionResult> CreateDoctorAccount()
        {
            var vm = new CreateDoctorAccountVm { Doctors = await DoctorsWithoutAccountAsync() };
            return View(vm);
        }

        [HttpPost]
        public async Task<IActionResult> CreateDoctorAccount(CreateDoctorAccountVm vm)
        {
            if (vm.DoctorId != null)
            {
                var doctor = await _db.Doctors.FindAsync(vm.DoctorId.Value);
                if (doctor == null)
                {
                    ModelState.AddModelError(nameof(vm.DoctorId), "Bác sĩ không tồn tại.");
                }
                else
                {
                    var email = doctor.Email.Trim().ToLowerInvariant();

                    var pwError = PasswordPolicy.Validate(vm.Password, email);
                    if (pwError != null) ModelState.AddModelError(nameof(vm.Password), pwError);

                    if (await _db.UserAccounts.AnyAsync(a => a.DoctorId == doctor.DoctorId || a.Email == email))
                        ModelState.AddModelError(nameof(vm.DoctorId), "Bác sĩ này đã có tài khoản hoặc email đã được dùng.");

                    if (ModelState.IsValid)
                    {
                        _db.UserAccounts.Add(new UserAccount
                        {
                            Email = email,                 // email đăng nhập = email của hồ sơ bác sĩ
                            PasswordHash = _passwords.Hash(vm.Password),
                            Role = AppRoles.Doctor,        // vai trò do server gán
                            DoctorId = doctor.DoctorId
                        });
                        await _db.SaveChangesAsync();

                        _logger.LogInformation("Admin UserId={Admin} tạo tài khoản bác sĩ DoctorId={Doctor}", CurrentUserId(), doctor.DoctorId);
                        TempData["Msg"] = $"Đã tạo tài khoản cho bác sĩ {doctor.FullName} (đăng nhập bằng email {email}).";
                        return RedirectToAction(nameof(Users));
                    }
                }
            }

            vm.Password = "";
            vm.Doctors = await DoctorsWithoutAccountAsync();
            return View(vm);
        }

        // POST: /Admin/ToggleActive/5  – khoá / mở khoá tài khoản
        [HttpPost]
        public async Task<IActionResult> ToggleActive(int id)
        {
            if (id == CurrentUserId())
            {
                TempData["Err"] = "Không thể tự khoá tài khoản của chính mình.";
                return RedirectToAction(nameof(Users));
            }

            var acc = await _db.UserAccounts.FindAsync(id);
            if (acc == null) return NotFound();

            acc.IsActive = !acc.IsActive;
            acc.SecurityStamp = Guid.NewGuid().ToString("N");   // đá mọi phiên đăng nhập đang mở của tài khoản này
            acc.FailedLoginCount = 0;
            acc.LockoutEndUtc = null;
            await _db.SaveChangesAsync();

            _logger.LogWarning("Admin UserId={Admin} đặt IsActive={Active} cho UserId={Target}", CurrentUserId(), acc.IsActive, acc.UserId);
            TempData["Msg"] = acc.IsActive ? $"Đã mở khoá {acc.Email}." : $"Đã khoá {acc.Email}.";
            return RedirectToAction(nameof(Users));
        }

        // ---------------------------------------------------------------------
        private int CurrentUserId() =>
            int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : 0;

        private async Task<List<SelectListItem>> DoctorsWithoutAccountAsync()
        {
            var linked = await _db.UserAccounts
                .Where(a => a.DoctorId != null)
                .Select(a => a.DoctorId ?? 0)
                .ToListAsync();

            var doctors = await _db.Doctors.AsNoTracking()
                .Where(d => !linked.Contains(d.DoctorId))
                .OrderBy(d => d.FullName)
                .ToListAsync();

            return doctors.Select(d => new SelectListItem
            {
                Value = d.DoctorId.ToString(),
                Text = $"{d.FullName} ({d.Email})"
            }).ToList();
        }
    }
}
