using Microsoft.AspNetCore.Mvc;

namespace TELEHEALTH_GROUP4_QT01.Areas.Admin.Controllers
{
    [Area("Admin")]
    public abstract class AdminBaseController : Controller
    {
        // TẠM THỜI: cố định quản trị viên số 1 (phải có dòng AdminID = 1 trong bảng ADMIN).
        // Khi nhóm làm xong đăng nhập: lấy ID từ Claims và thêm [Authorize(Roles = "Admin")] vào class này.
        protected int CurrentAdminId => 1;

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
