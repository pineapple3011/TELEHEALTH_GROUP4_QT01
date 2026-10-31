using System;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using TELEHEALTH_GROUP4_QT01.Areas.Doctor.Helpers;
using TELEHEALTH_GROUP4_QT01.Areas.Doctor.Models;

namespace TELEHEALTH_GROUP4_QT01.Areas.Doctor.Controllers;

/// <summary>Đăng nhập / đăng xuất cho phân hệ bác sĩ (không kế thừa DoctorBaseController vì chưa có phiên đăng nhập).</summary>
[Area("Doctor")]
public class AuthController : Controller
{
    private readonly DoctorDb _db;
    public AuthController(IConfiguration cfg) => _db = new DoctorDb(cfg);

    [HttpGet]
    public IActionResult Login(string? returnUrl)
    {
        if (CurrentDoctorId() != null) return RedirectToAction("Index", "Appointments");
        return View(new DoctorLoginVm { ReturnUrl = returnUrl });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public IActionResult Login(DoctorLoginVm vm)
    {
        if (!ModelState.IsValid) return View(vm);

        var id = _db.Authenticate(vm.Email, vm.Password);
        if (id == null)
        {
            ModelState.AddModelError("", "Email hoặc mật khẩu không đúng, hoặc tài khoản không phải của bác sĩ.");
            vm.Password = "";
            return View(vm);
        }

        try { HttpContext.Session.SetInt32("DoctorID", id.Value); }
        catch (InvalidOperationException)
        {
            ModelState.AddModelError("", "Ứng dụng chưa bật Session. Thêm builder.Services.AddSession() và app.UseSession() vào Program.cs.");
            return View(vm);
        }

        if (!string.IsNullOrEmpty(vm.ReturnUrl) && Url.IsLocalUrl(vm.ReturnUrl)) return Redirect(vm.ReturnUrl);
        return RedirectToAction("Index", "Appointments");
    }

    [HttpGet]
    public IActionResult Logout()
    {
        try { HttpContext.Session.Remove("DoctorID"); } catch (InvalidOperationException) { }
        return RedirectToAction(nameof(Login));
    }

    private int? CurrentDoctorId()
    {
        try { return HttpContext.Session.GetInt32("DoctorID"); } catch (InvalidOperationException) { return null; }
    }
}

public class DoctorLoginVm
{
    [Required(ErrorMessage = "Nhập email")] [EmailAddress(ErrorMessage = "Email không hợp lệ")] public string Email { get; set; } = "";
    [Required(ErrorMessage = "Nhập mật khẩu")] public string Password { get; set; } = "";
    public string? ReturnUrl { get; set; }
}
