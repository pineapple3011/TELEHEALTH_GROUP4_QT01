using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using TELEHEALTH_GROUP4_QT01.Security;

namespace TELEHEALTH_GROUP4_QT01.Areas.Admin.Controllers
{
    /// <summary>
    /// Lớp cha của mọi controller khu vực quản trị.
    /// [Authorize(Roles = Admin)] được kiểm tra ở SERVER: chưa đăng nhập => về trang đăng nhập, sai vai trò => 403.
    /// </summary>
    [Area("Admin")]
    [Authorize(Roles = AppRoles.Admin)]
    public abstract class AdminBaseController : Controller
    {
        /// <summary>AdminID của quản trị viên đang đăng nhập, lấy từ claim do server cấp (không nhận từ client).</summary>
        protected int CurrentAdminId { get; private set; }

        public override void OnActionExecuting(ActionExecutingContext context)
        {
            if (!int.TryParse(User.FindFirstValue(AppClaims.AdminId), out var id) || id <= 0)
            {
                context.Result = Forbid();
                return;
            }

            CurrentAdminId = id;
            base.OnActionExecuting(context);
        }

        // Khoảng ngày mặc định cho các báo cáo: từ đầu tháng (lùi monthsBack tháng) đến hôm nay
        protected static (DateOnly start, DateOnly end) DefaultRange(DateOnly? from, DateOnly? to, int monthsBack = 0)
        {
            var today = DateOnly.FromDateTime(DateTime.Today);
            var t = to ?? today;
            var f = from ?? new DateOnly(today.Year, today.Month, 1).AddMonths(-monthsBack);
            if (f > t) (f, t) = (t, f);
            return (f, t);
        }
    }
}
