using System;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Configuration;
using TELEHEALTH_GROUP4_QT01.Areas.Doctor.Helpers;
using TELEHEALTH_GROUP4_QT01.Areas.Doctor.Models;
using TELEHEALTH_GROUP4_QT01.Security;

namespace TELEHEALTH_GROUP4_QT01.Areas.Doctor.Controllers;

/// <summary>
/// Lớp cha của mọi controller trong phân hệ bác sĩ (tương ứng AdminBaseController).
/// [Authorize(Roles = Doctor)]: chưa đăng nhập => về trang đăng nhập chung (/Account/Login), sai vai trò => 403.
/// DoctorID lấy từ claim "DoctorID" do server cấp lúc đăng nhập – không đọc từ form/query/session do client điều khiển.
/// </summary>
[Area("Doctor")]
[Authorize(Roles = AppRoles.Doctor)]
public abstract class DoctorBaseController : Controller
{
    protected readonly DoctorDb Db;
    protected int DoctorId { get; private set; }
    protected DoctorProfile Me { get; private set; } = new();

    protected DoctorBaseController(IConfiguration cfg) => Db = new DoctorDb(cfg);

    public override void OnActionExecuting(ActionExecutingContext context)
    {
        var me = int.TryParse(User.FindFirstValue(AppClaims.DoctorId), out var id) ? Db.GetDoctor(id) : null;
        if (me == null)
        {
            context.Result = Forbid();
            return;
        }
        DoctorId = me.DoctorId;
        Me = me;
        ViewBag.Doctor = me;
        base.OnActionExecuting(context);
    }

    protected void Success(string msg) => TempData["Msg"] = msg;
    protected void Fail(string msg) => TempData["Err"] = msg;
}
