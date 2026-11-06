using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using TELEHEALTH_GROUP4_QT01.Models;
using TELEHEALTH_GROUP4_QT01.Models.ViewModels;
using TELEHEALTH_GROUP4_QT01.Security;

namespace TELEHEALTH_GROUP4_QT01.Controllers
{
    /// <summary>
    /// Đăng nhập / đăng ký / đăng xuất DÙNG CHUNG cho cả 3 vai trò (Patient, Doctor, Admin),
    /// dựa trên bảng Accounts có sẵn. Sau khi đăng nhập hệ thống chuyển người dùng tới khu vực đúng vai trò.
    /// </summary>
    [AllowAnonymous]
    public class AccountController : Controller
    {
        private const int MaxFailedAttempts = 5;
        private static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);

        // Một thông báo chung cho mọi trường hợp thất bại => không lộ email nào đã đăng ký
        private const string LoginFailedMessage =
            "Email hoặc mật khẩu không đúng. Sau 5 lần nhập sai liên tiếp, tài khoản sẽ bị khoá tạm thời 15 phút.";

        private readonly TelehealthContext _db;
        private readonly IPasswordService _passwords;
        private readonly ILogger<AccountController> _logger;

        public AccountController(TelehealthContext db, IPasswordService passwords, ILogger<AccountController> logger)
        {
            _db = db;
            _passwords = passwords;
            _logger = logger;
        }

        // =====================================================================
        // ĐĂNG NHẬP
        // =====================================================================
        [HttpGet]
        public IActionResult Login(string? returnUrl = null)
        {
            if (User.Identity?.IsAuthenticated == true)
                return RedirectAfterLogin(returnUrl, User.FindFirstValue(ClaimTypes.Role));
            return View(new LoginVm { ReturnUrl = returnUrl });
        }

        [HttpPost, EnableRateLimiting("auth")]   // CSRF: đã bật toàn cục (AutoValidateAntiforgeryToken) trong Program.cs
        public async Task<IActionResult> Login(LoginVm vm)
        {
            if (!ModelState.IsValid) return LoginFailed(vm, null);

            var email = NormalizeEmail(vm.Email);
            var now = DateTime.UtcNow;

            var acc = await _db.Accounts
                .Include(a => a.Patient).Include(a => a.Doctor).Include(a => a.Admin)
                .FirstOrDefaultAsync(a => a.Email == email);

            var role = AppRoles.Normalize(acc?.Role);

            // Email không tồn tại / tài khoản bị khoá / vai trò lạ / đang bị khoá tạm: vẫn "đốt" thời gian
            // tương đương một lần băm mật khẩu để không lộ thông tin qua thời gian phản hồi.
            if (acc == null || role == null || !acc.IsActive || (acc.LockoutEndUtc != null && acc.LockoutEndUtc > now))
            {
                _passwords.BurnTime(vm.Password);
                return LoginFailed(vm, LoginFailedMessage);
            }

            var result = _passwords.Verify(acc.PasswordHash, vm.Password);
            if (result == PasswordVerifyResult.Failed)
            {
                acc.FailedLoginCount++;
                if (acc.FailedLoginCount >= MaxFailedAttempts)
                {
                    acc.LockoutEndUtc = now.Add(LockoutDuration);
                    acc.FailedLoginCount = 0;
                    _logger.LogWarning("Tài khoản AccountId={AccountId} bị khoá tạm do đăng nhập sai nhiều lần", acc.AccountId);
                }
                await _db.SaveChangesAsync();
                return LoginFailed(vm, LoginFailedMessage);
            }

            // Đăng nhập đúng
            if (result == PasswordVerifyResult.SuccessRehashNeeded)
                acc.PasswordHash = _passwords.Hash(vm.Password);   // nâng cấp hash SHA-256 cũ lên PBKDF2

            acc.FailedLoginCount = 0;
            acc.LockoutEndUtc = null;
            acc.LastLogin = DateTime.Now;
            await _db.SaveChangesAsync();

            await SignInAsync(acc, role);
            _logger.LogInformation("AccountId={AccountId} (Role={Role}) đăng nhập thành công", acc.AccountId, role);

            return RedirectAfterLogin(vm.ReturnUrl, role);
        }

        // =====================================================================
        // ĐĂNG KÝ (chỉ tạo tài khoản BỆNH NHÂN; không bao giờ nhận vai trò từ client)
        // Tài khoản bác sĩ / admin do Admin tạo ở khu vực quản trị.
        // =====================================================================
        [HttpGet]
        public IActionResult Register()
        {
            if (User.Identity?.IsAuthenticated == true) return RedirectToAction("Index", "Home", new { area = "" });
            return View(new RegisterVm());
        }

        [HttpPost, EnableRateLimiting("auth")]
        public async Task<IActionResult> Register(RegisterVm vm)
        {
            var email = NormalizeEmail(vm.Email);

            // Kiểm tra chính sách mật khẩu ở server
            var pwError = PasswordPolicy.Validate(vm.Password, email);
            if (pwError != null) ModelState.AddModelError(nameof(vm.Password), pwError);

            if (!ModelState.IsValid) return RegisterFailed(vm);

            // Email đã dùng (cả tài khoản lẫn hồ sơ bệnh nhân cũ chưa có tài khoản).
            // Cố ý KHÔNG tự "nhận" hồ sơ cũ: nếu không có bước xác minh email thì ai cũng có thể chiếm hồ sơ y tế của người khác.
            if (await _db.Accounts.AnyAsync(a => a.Email == email) ||
                await _db.Patients.AnyAsync(p => p.Email == email))
            {
                ModelState.AddModelError(nameof(vm.Email), "Không thể tạo tài khoản với email này.");
                return RegisterFailed(vm);
            }

            var patient = new Patient
            {
                FullName = vm.FullName.Trim(),
                Email = email,
                Phone = vm.Phone.Trim(),
                Address = NullIfBlank(vm.Address),
                EmergencyPhone = NullIfBlank(vm.EmergencyPhone)
            };

            _db.Accounts.Add(new Account
            {
                Email = email,
                PasswordHash = _passwords.Hash(vm.Password),
                Role = AppRoles.Patient,      // gán cứng ở server
                Patient = patient,            // EF tự thêm Patient rồi Account trong cùng một transaction
                IsActive = true,
                CreatedAt = DateTime.Now
            });

            try
            {
                await _db.SaveChangesAsync();
            }
            catch (DbUpdateException ex) when (IsUniqueViolation(ex))
            {
                // Hai người đăng ký cùng email cùng lúc: unique index ở DB là chốt chặn cuối
                ModelState.AddModelError(nameof(vm.Email), "Không thể tạo tài khoản với email này.");
                return RegisterFailed(vm);
            }

            TempData["Msg"] = "Đăng ký thành công. Vui lòng đăng nhập.";
            return RedirectToAction(nameof(Login));
        }

        // =====================================================================
        // ĐĂNG XUẤT (POST + CSRF token: không thể bị ép đăng xuất bằng thẻ <img src="/Account/Logout">)
        // =====================================================================
        [HttpPost]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction("Index", "Home", new { area = "" });
        }

        [HttpGet]
        public IActionResult AccessDenied()
        {
            Response.StatusCode = StatusCodes.Status403Forbidden;
            return View();
        }

        // =====================================================================
        // HÀM PHỤ
        // =====================================================================
        private async Task SignInAsync(Account acc, string role)
        {
            var displayName = acc.Patient?.FullName ?? acc.Doctor?.FullName ?? acc.Admin?.FullName ?? acc.Email;

            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, acc.AccountId.ToString()),
                new(ClaimTypes.Name, displayName),
                new(ClaimTypes.Email, acc.Email),
                new(ClaimTypes.Role, role),                           // vai trò lấy từ DB, không phải từ client
                new(AppClaims.SecurityStamp, acc.SecurityStamp)
            };
            if (acc.PatientId is int pid) claims.Add(new Claim(AppClaims.PatientId, pid.ToString()));
            if (acc.DoctorId is int did) claims.Add(new Claim(AppClaims.DoctorId, did.ToString()));
            if (acc.AdminId is int aid) claims.Add(new Claim(AppClaims.AdminId, aid.ToString()));

            var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                new ClaimsPrincipal(identity),
                new AuthenticationProperties { IsPersistent = false });   // cookie phiên: đóng trình duyệt là hết
        }

        private IActionResult RedirectAfterLogin(string? returnUrl, string? role)
        {
            // Chống open redirect: chỉ chấp nhận đường dẫn nội bộ của chính website
            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
                return LocalRedirect(returnUrl);

            if (role == AppRoles.Admin) return RedirectToAction("Index", "Dashboard", new { area = "Admin" });
            if (role == AppRoles.Doctor) return RedirectToAction("Index", "Appointments", new { area = "Doctor" });
            return RedirectToAction("Index", "Home", new { area = "" });
        }

        private IActionResult LoginFailed(LoginVm vm, string? message)
        {
            if (message != null) ModelState.AddModelError("", message);
            vm.Password = "";
            return View(vm);
        }

        private IActionResult RegisterFailed(RegisterVm vm)
        {
            vm.Password = "";
            vm.ConfirmPassword = "";
            return View(vm);
        }

        private static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();

        private static string? NullIfBlank(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

        private static bool IsUniqueViolation(DbUpdateException ex) =>
            ex.InnerException is Microsoft.Data.SqlClient.SqlException sql && (sql.Number == 2601 || sql.Number == 2627);
    }
}
