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

/// <summary>Form Khám bệnh &amp; Chẩn đoán: bệnh án + bảng kê đơn thuốc.</summary>
public class ConsultationsController : DoctorBaseController
{
    public ConsultationsController(IConfiguration cfg) : base(cfg) { }

    private ExamVm? BuildVm(int appointId, ExamInput? posted = null)
    {
        var appt = Db.GetAppointment(DoctorId, appointId);
        if (appt == null) return null;
        var saved = Db.LoadExam(appointId);
        return new ExamVm
        {
            Appointment = appt,
            Patient = Db.GetPatient(appt.PatientId) ?? new PatientInfo { FullName = appt.PatientName, Phone = appt.PatientPhone },
            Files = Db.GetFiles(appointId),
            Medicines = Db.GetMedicines(),
            Input = posted ?? saved,
            HasRecord = appt.HasRecord
        };
    }

    public IActionResult Exam(int id)
    {
        var vm = BuildVm(id);
        if (vm == null) return NotFound();
        if (vm.Appointment.IsCancelled || vm.Appointment.IsPending)
        {
            Fail(vm.Appointment.IsPending ? "Hãy xác nhận ca khám trước khi bắt đầu khám." : "Ca khám đã bị hủy.");
            return RedirectToAction("Index", "Appointments", new { date = vm.Appointment.Date.ToString("yyyy-MM-dd") });
        }
        return View(vm);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public IActionResult Save(ExamInput input, string mode = "draft")
    {
        input.Items ??= new();
        input.Diagnosis = (input.Diagnosis ?? "").Trim();

        // gộp các dòng trùng thuốc (khóa chính Prescription = RecordID + MedicineID)
        input.Items = input.Items
            .Where(i => i.MedicineId > 0)
            .GroupBy(i => i.MedicineId)
            .Select(g => { var f = g.First(); f.Quantity = g.Sum(x => x.Quantity); return f; })
            .ToList();

        var errors = new List<string>();
        if (input.Diagnosis.Length == 0) errors.Add("Vui lòng nhập chẩn đoán.");
        var medIds = Db.GetMedicines().Select(m => m.MedicineId).ToHashSet();
        foreach (var it in input.Items)
        {
            it.Dosage = (it.Dosage ?? "").Trim();
            it.Hds = string.IsNullOrWhiteSpace(it.Hds) ? null : it.Hds.Trim();
            if (!medIds.Contains(it.MedicineId)) errors.Add("Có thuốc không tồn tại trong danh mục.");
            if (it.Quantity < 1) errors.Add("Số lượng thuốc phải từ 1 trở lên.");
            if (it.Dosage.Length == 0) errors.Add("Mỗi thuốc cần ghi liều dùng.");
            if (it.Dosage.Length > 100) errors.Add("Liều dùng tối đa 100 ký tự.");
            if (it.Hds?.Length > 255) errors.Add("Hướng dẫn sử dụng tối đa 255 ký tự.");
        }
        var appt = Db.GetAppointment(DoctorId, input.AppointId);
        if (appt == null) return NotFound();
        if (input.FollowUpDate.HasValue && input.FollowUpDate.Value.Date < appt.Date.Date) errors.Add("Ngày tái khám không được trước ngày khám.");

        if (errors.Count > 0)
        {
            Fail(string.Join(" ", errors.Distinct()));
            var back = BuildVm(input.AppointId, input);
            return back == null ? NotFound() : View("Exam", back);
        }

        var err = Db.SaveExam(DoctorId, input, complete: mode == "complete");
        if (err != null) { Fail(err); return RedirectToAction("Index", "Appointments"); }

        Success(mode == "complete" ? "Đã hoàn thành ca khám và lưu bệnh án." : "Đã lưu nháp bệnh án.");
        return mode == "complete"
            ? RedirectToAction("Details", "Appointments", new { id = input.AppointId })
            : RedirectToAction(nameof(Exam), new { id = input.AppointId });
    }
}
