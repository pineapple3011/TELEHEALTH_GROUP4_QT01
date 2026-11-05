using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TELEHEALTH_GROUP4_QT01.Models;
using TELEHEALTH_GROUP4_QT01.Security;
using TELEHEALTH_GROUP4_QT01.Services;

namespace TELEHEALTH_GROUP4_QT01.Controllers
{
    /// <summary>
    /// Phục vụ file tải lên (ảnh triệu chứng, biên lai...) – hồ sơ y tế là dữ liệu nhạy cảm
    /// nên KHÔNG để file trong wwwroot. Mỗi lần tải đều kiểm tra quyền ở server:
    ///   Patient: chỉ file thuộc lịch khám của chính mình
    ///   Doctor : chỉ file thuộc lịch khám của mình
    ///   Admin  : tất cả
    /// </summary>
    [Authorize]
    public class FilesController : Controller
    {
        private readonly TelehealthContext _db;
        private readonly IFileStorageService _storage;

        public FilesController(TelehealthContext db, IFileStorageService storage)
        {
            _db = db;
            _storage = storage;
        }

        // GET: /Files/Download/5
        [HttpGet]
        public async Task<IActionResult> Download(int id)
        {
            var file = await _db.AppointmentFiles.AsNoTracking()
                .Include(f => f.Appoint)
                .FirstOrDefaultAsync(f => f.FileId == id);

            // Không có quyền cũng trả 404 (không để lộ file nào tồn tại)
            if (file == null || !CanAccess(file.Appoint)) return NotFound();

            var stored = _storage.Open(file.FilePath);
            if (stored == null) return NotFound();

            Response.Headers["X-Content-Type-Options"] = "nosniff";
            Response.Headers["Cache-Control"] = "private, no-store";
            // File tải lên không bao giờ được chạy script/ nhúng tài nguyên khi mở trực tiếp
            Response.Headers["Content-Security-Policy"] = "default-src 'none'; img-src 'self'; style-src 'unsafe-inline'; sandbox";

            // Ảnh: hiển thị trong trình duyệt. PDF: buộc tải xuống (PDF có thể chứa script).
            // Content-Type do SERVER quyết định theo whitelist, không lấy từ client.
            if (stored.IsImage)
                return File(stored.Stream, stored.ContentType);

            return File(stored.Stream, stored.ContentType, $"telehealth-{id}.pdf");
        }

        private bool CanAccess(Appointment appointment)
        {
            if (User.IsInRole(AppRoles.Admin)) return true;

            if (User.IsInRole(AppRoles.Patient))
                return int.TryParse(User.FindFirstValue(AppClaims.PatientId), out var pid) && appointment.PatientId == pid;

            if (User.IsInRole(AppRoles.Doctor))
                return int.TryParse(User.FindFirstValue(AppClaims.DoctorId), out var did) && appointment.DoctorId == did;

            return false;
        }
    }
}
