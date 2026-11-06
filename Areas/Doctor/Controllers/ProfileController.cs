using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Mvc;
using TELEHEALTH_GROUP4_QT01.Areas.Doctor.Models;
using TELEHEALTH_GROUP4_QT01.Security;
using TELEHEALTH_GROUP4_QT01.Services;

namespace TELEHEALTH_GROUP4_QT01.Areas.Doctor.Controllers;

/// <summary>Trang Hồ sơ cá nhân bác sĩ.</summary>
public class ProfileController : DoctorBaseController
{
    private static readonly string[] AllowedExt = { ".jpg", ".jpeg", ".png", ".webp" };
    private readonly IWebHostEnvironment _env;
    private readonly IFileStorageService _files;

    public ProfileController(IConfiguration cfg, IWebHostEnvironment env, IFileStorageService files) : base(cfg)
    {
        _env = env;
        _files = files;
    }

    public IActionResult Index() => View(new ProfilePageVm { Doctor = Me, Stats = Db.GetStats(DoctorId) });

    public IActionResult Edit() => View(new ProfileEditVm { Doctor = Me, Input = new ProfileEditInput { Phone = Me.Phone } });

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit([Bind(Prefix = "Input")] ProfileEditInput input)
    {
        string? avatarUrl = null;
        if (input.Avatar is { Length: > 0 })
        {
            var avatarError = await _files.ValidateAvatarAsync(input.Avatar);
            if (avatarError != null) ModelState.AddModelError("Input.Avatar", avatarError);
        }
        if (!ModelState.IsValid) return View(new ProfileEditVm { Doctor = Me, Input = input });

        if (input.Avatar is { Length: > 0 })
        {
            var ext = Path.GetExtension(input.Avatar.FileName).ToLowerInvariant();
            var dir = Path.Combine(_env.WebRootPath, "uploads", "doctors");
            Directory.CreateDirectory(dir);
            var name = $"bs{DoctorId}_{Guid.NewGuid():N}{ext}";
            await using (var fs = System.IO.File.Create(Path.Combine(dir, name)))
                await input.Avatar.CopyToAsync(fs);
            avatarUrl = "/uploads/doctors/" + name;
        }

        Db.UpdateProfile(DoctorId, input.Phone.Trim(), avatarUrl);
        Success("Đã cập nhật hồ sơ.");
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public IActionResult ChangePassword(ChangePasswordInput pw)
    {
        if (!ModelState.IsValid)
        {
            Fail(string.Join(" ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage)));
            return RedirectToAction(nameof(Edit));
        }
        var policyError = PasswordPolicy.Validate(pw.NewPassword, Me.Email);
        if (policyError != null)
        {
            Fail(policyError);
            return RedirectToAction(nameof(Edit));
        }
        if (Db.ChangePassword(DoctorId, pw.CurrentPassword, pw.NewPassword)) Success("Đã đổi mật khẩu.");
        else Fail("Mật khẩu hiện tại không đúng.");
        return RedirectToAction(nameof(Edit));
    }
}
