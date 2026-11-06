using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using TELEHEALTH_GROUP4_QT01.Security;

namespace TELEHEALTH_GROUP4_QT01.Controllers
{
    /// <summary>
    /// Lớp cha của mọi controller dành cho BỆNH NHÂN.
    /// [Authorize(Roles = Patient)] được áp cho cả lớp con: chưa đăng nhập => chuyển tới trang đăng nhập,
    /// đăng nhập nhưng sai vai trò (Doctor/Admin) => 403.
    /// </summary>
    [Authorize(Roles = AppRoles.Patient)]
    public abstract class PatientBaseController : Controller
    {
        /// <summary>
        /// ID bệnh nhân lấy từ claim do SERVER cấp lúc đăng nhập (nằm trong cookie đã mã hoá).
        /// Tuyệt đối không đọc ID này từ form / query string / header do client gửi.
        /// </summary>
        protected int CurrentPatientId { get; private set; }

        public override void OnActionExecuting(ActionExecutingContext context)
        {
            if (!int.TryParse(User.FindFirstValue(AppClaims.PatientId), out var id) || id <= 0)
            {
                context.Result = Forbid();
                return;
            }

            CurrentPatientId = id;
            base.OnActionExecuting(context);
        }
    }
}
