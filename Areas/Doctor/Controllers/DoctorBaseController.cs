using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using TELEHEALTH_GROUP4_QT01.Areas.Doctor.Helpers;
using TELEHEALTH_GROUP4_QT01.Areas.Doctor.Models;

namespace TELEHEALTH_GROUP4_QT01.Areas.Doctor.Controllers;

/// <summary>
/// Lớp cha của mọi controller trong phân hệ bác sĩ (tương ứng AdminBaseController).
/// Xác định bác sĩ đang đăng nhập, nạp hồ sơ vào ViewBag.Doctor cho layout.
/// </summary>
[Area("Doctor")]
public abstract class DoctorBaseController : Controller
{
    /// <summary>Đổi cho đúng đường dẫn trang đăng nhập của dự án.</summary>
    protected const string LoginPath = "/Doctor/Auth/Login";

    protected readonly DoctorDb Db;
    protected int DoctorId { get; private set; }
    protected DoctorProfile Me { get; private set; } = new();

    protected DoctorBaseController(IConfiguration cfg) => Db = new DoctorDb(cfg);

    public override void OnActionExecuting(ActionExecutingContext context)
    {
        var id = ResolveDoctorId();
        var me = id == null ? null : Db.GetDoctor(id.Value);
        if (me == null)
        {
            var back = Uri.EscapeDataString(Request.Path + Request.QueryString);
            context.Result = Redirect($"{LoginPath}?returnUrl={back}");
            return;
        }
        DoctorId = me.DoctorId;
        Me = me;
        ViewBag.Doctor = me;
        base.OnActionExecuting(context);
    }

    /// <summary>
    /// Thứ tự dò tìm DoctorID: claim "DoctorID" -> Session "DoctorID" -> email (claim/Name/Session "Email") tra bảng Accounts với Role='Doctor'.
    /// </summary>
    private int? ResolveDoctorId()
    {
        if (int.TryParse(User.FindFirst("DoctorID")?.Value, out var fromClaim)) return fromClaim;

        string? sessionEmail = null;
        try
        {
            var s = HttpContext.Session;
            var n = s.GetInt32("DoctorID");
            if (n.HasValue) return n;
            if (int.TryParse(s.GetString("DoctorID"), out var fromStr)) return fromStr;
            sessionEmail = s.GetString("Email");
        }
        catch (InvalidOperationException) { /* chưa bật Session trong Program.cs */ }

        var email = User.FindFirst(ClaimTypes.Email)?.Value
                    ?? (User.Identity?.IsAuthenticated == true ? User.Identity.Name : null)
                    ?? sessionEmail;
        return string.IsNullOrWhiteSpace(email) ? null : Db.FindDoctorIdByEmail(email);
    }

    protected void Success(string msg) => TempData["Msg"] = msg;
    protected void Fail(string msg) => TempData["Err"] = msg;
}
