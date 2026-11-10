using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Mvc;
using TELEHEALTH_GROUP4_QT01.Areas.Doctor.Helpers;
using TELEHEALTH_GROUP4_QT01.Areas.Doctor.Models;

namespace TELEHEALTH_GROUP4_QT01.Areas.Doctor.Controllers;

/// <summary>Trang Ca khám trong ngày: xem, xác nhận, từ chối ca của bác sĩ.</summary>
public class AppointmentsController : DoctorBaseController
{
    public AppointmentsController(IConfiguration cfg) : base(cfg) { }

    public IActionResult Index(DateTime? date)
    {
        var day = (date ?? DateTime.Today).Date;
        var vm = new DayPageVm
        {
            Date = day,
            Rows = Db.GetAppointmentsByDate(DoctorId, day),
            PendingElsewhere = Db.PendingOutside(DoctorId, day)
        };
        if (vm.Rows.Count == 0) vm.NextDateWithAppointments = Db.NextAppointmentDate(DoctorId, day.AddDays(1));
        return View(vm);
    }

    public IActionResult Details(int id)
    {
        var appt = Db.GetAppointment(DoctorId, id);
        if (appt == null) return NotFound();
        return View(new AppointmentDetailVm
        {
            Appointment = appt,
            Patient = Db.GetPatient(appt.PatientId) ?? new PatientInfo { FullName = appt.PatientName, Phone = appt.PatientPhone },
            Files = Db.GetFiles(id),
            Payment = Db.GetPayment(id)
        });
    }

    public IActionResult All(string? status)
    {
        var rows = Db.GetAllAppointments(DoctorId);
        var valid = new[] { "all", "pending", "confirmed", "completed", "cancelled" };
        // Mặc định mở tab "Cần xác nhận" nếu đang có ca chờ, ngược lại mở "Tất cả"
        ViewBag.Status = valid.Contains(status ?? "") ? status : (rows.Any(r => r.IsPending) ? "pending" : "all");
        return View(rows);
    }

    public IActionResult Room(int id)
    {
        var appt = Db.GetAppointment(DoctorId, id);
        if (appt == null) return NotFound();
        // Chỉ vào phòng khi ca Online đã xác nhận VÀ admin đã duyệt thanh toán
        var blocked = appt.RoomBlockedReason;
        if (blocked != null)
        {
            Fail(blocked);
            return RedirectToAction(nameof(Details), new { id });
        }
        return View(appt);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public IActionResult Confirm(int id, DateTime? date, string? back)
    {
        if (Db.ChangeStatus(DoctorId, id, DoctorDb.Confirmed, DoctorDb.Pending)) Success("Đã xác nhận ca khám.");
        else Fail("Không thể xác nhận ca này (đã đổi trạng thái hoặc không thuộc về bạn).");
        if (back == "all") return RedirectToAction(nameof(All));
        return RedirectToAction(nameof(Index), new { date = date?.ToString("yyyy-MM-dd") });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public IActionResult Reject(int id, DateTime? date, string? back)
    {
        if (Db.ChangeStatus(DoctorId, id, DoctorDb.Cancelled, DoctorDb.Pending, DoctorDb.Confirmed)) Success("Đã hủy ca khám.");
        else Fail("Không thể hủy ca này (ca đã hoàn thành, đã hủy hoặc không thuộc về bạn).");
        if (back == "all") return RedirectToAction(nameof(All));
        return RedirectToAction(nameof(Index), new { date = date?.ToString("yyyy-MM-dd") });
    }
}
