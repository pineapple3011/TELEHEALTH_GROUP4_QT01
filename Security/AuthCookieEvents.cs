using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using TELEHEALTH_GROUP4_QT01.Models;

namespace TELEHEALTH_GROUP4_QT01.Security
{
    /// <summary>
    /// Mỗi request có cookie đăng nhập đều được đối chiếu lại với bảng Accounts:
    ///  - tài khoản bị khoá / xoá  => đăng xuất
    ///  - vai trò hoặc SecurityStamp đổi (admin đổi quyền, reset mật khẩu, khoá/mở khoá) => đăng xuất
    /// Nhờ vậy quyền luôn do SERVER quyết định, cookie cũ không thể "giữ" quyền cũ.
    /// Kết quả kiểm tra được nhớ 30 giây để không truy vấn DB cho từng file CSS/JS/ảnh;
    /// nghĩa là việc khoá tài khoản có hiệu lực trong tối đa 30 giây.
    /// </summary>
    public sealed class AuthCookieEvents : CookieAuthenticationEvents
    {
        private static readonly TimeSpan CacheFor = TimeSpan.FromSeconds(30);

        private readonly TelehealthContext _db;
        private readonly IMemoryCache _cache;

        public AuthCookieEvents(TelehealthContext db, IMemoryCache cache)
        {
            _db = db;
            _cache = cache;
        }

        public override Task RedirectToLogin(RedirectContext<CookieAuthenticationOptions> context)
        {
            if (context.Request.Path.StartsWithSegments("/api"))
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return Task.CompletedTask;
            }
            return base.RedirectToLogin(context);
        }

        public override Task RedirectToAccessDenied(RedirectContext<CookieAuthenticationOptions> context)
        {
            if (context.Request.Path.StartsWithSegments("/api"))
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                return Task.CompletedTask;
            }
            return base.RedirectToAccessDenied(context);
        }

        public override async Task ValidatePrincipal(CookieValidatePrincipalContext context)
        {
            var principal = context.Principal;
            var idText = principal?.FindFirstValue(ClaimTypes.NameIdentifier);
            var stamp = principal?.FindFirstValue(AppClaims.SecurityStamp);
            var role = principal?.FindFirstValue(ClaimTypes.Role);

            bool valid = false;
            if (int.TryParse(idText, out var accountId) && stamp != null && role != null)
            {
                var key = $"auth:{accountId}:{stamp}:{role}";
                if (!_cache.TryGetValue(key, out valid))
                {
                    var acc = await _db.Accounts.AsNoTracking()
                        .Where(a => a.AccountId == accountId)
                        .Select(a => new { a.IsActive, a.SecurityStamp, a.Role })
                        .FirstOrDefaultAsync(context.HttpContext.RequestAborted);

                    valid = acc != null
                            && acc.IsActive
                            && acc.SecurityStamp == stamp
                            && AppRoles.Normalize(acc.Role) == role;

                    _cache.Set(key, valid, CacheFor);
                }
            }

            if (!valid)
            {
                context.RejectPrincipal();
                await context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            }
        }
    }
}
