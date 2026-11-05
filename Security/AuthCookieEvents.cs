using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using TELEHEALTH_GROUP4_QT01.Models;

namespace TELEHEALTH_GROUP4_QT01.Security
{
    /// <summary>
    /// Mỗi request có cookie đăng nhập đều được đối chiếu lại với DB:
    ///  - tài khoản bị khoá / xoá  => đăng xuất ngay
    ///  - vai trò hoặc SecurityStamp đổi (admin đổi quyền, đổi mật khẩu) => đăng xuất ngay
    /// Nhờ vậy quyền luôn do SERVER quyết định, cookie cũ không thể "giữ" quyền cũ.
    /// </summary>
    public sealed class AuthCookieEvents : CookieAuthenticationEvents
    {
        private readonly TelehealthContext _db;

        public AuthCookieEvents(TelehealthContext db) => _db = db;

        public override async Task ValidatePrincipal(CookieValidatePrincipalContext context)
        {
            var principal = context.Principal;
            var idText = principal?.FindFirstValue(ClaimTypes.NameIdentifier);
            var stamp = principal?.FindFirstValue(AppClaims.SecurityStamp);
            var role = principal?.FindFirstValue(ClaimTypes.Role);

            bool valid = false;
            if (int.TryParse(idText, out var userId) && stamp != null && role != null)
            {
                var acc = await _db.UserAccounts.AsNoTracking()
                    .Where(a => a.UserId == userId)
                    .Select(a => new { a.IsActive, a.SecurityStamp, a.Role })
                    .FirstOrDefaultAsync(context.HttpContext.RequestAborted);

                valid = acc != null && acc.IsActive && acc.SecurityStamp == stamp && acc.Role == role;
            }

            if (!valid)
            {
                context.RejectPrincipal();
                await context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            }
        }
    }
}
