using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Mvc;
using TELEHEALTH_GROUP4_QT01.Areas.Doctor.Models;

namespace TELEHEALTH_GROUP4_QT01.Areas.Doctor.Controllers;

/// <summary>Trang Lịch trực &amp; Trạng thái (bật/tắt Available/Busy).</summary>
public class SchedulesController : DoctorBaseController
{
    public SchedulesController(IConfiguration cfg) : base(cfg) { }

    public IActionResult Index() => View(new SchedulePageVm { Doctor = Me, Upcoming = Db.GetUpcomingSchedules(DoctorId) });

    [HttpPost, ValidateAntiForgeryToken]
    public IActionResult SetStatus(string status)
    {
        if (status != "Available" && status != "Busy") { Fail("Trạng thái không hợp lệ."); return RedirectToAction(nameof(Index)); }
        Db.SetStatus(DoctorId, status);
        Success(status == "Available" ? "Bạn đang ở trạng thái Sẵn sàng - bệnh nhân có thể đặt lịch." : "Bạn đang ở trạng thái Bận - bệnh nhân sẽ thấy bạn tạm ngưng nhận ca.");
        return RedirectToAction(nameof(Index));
    }

    /// <summary>Thêm một ca đơn lẻ (giữ lại để tương thích).</summary>
    [HttpPost, ValidateAntiForgeryToken]
    public IActionResult Create([Bind(Prefix = "Input")] ScheduleInput input)
    {
        if (!ModelState.IsValid) { Fail("Ngày hoặc giờ trực không hợp lệ."); return RedirectToAction(nameof(Index)); }
        var err = Db.AddSchedule(DoctorId, input.WorkDate, input.StartTime, input.EndTime);
        if (err != null) Fail(err); else Success("Đã thêm ca trực mới.");
        return RedirectToAction(nameof(Index));
    }

    /// <summary>Thêm hàng loạt: khoảng ngày x các thứ trong tuần x các khung giờ.</summary>
    [HttpPost, ValidateAntiForgeryToken]
    public IActionResult CreateBulk([Bind(Prefix = "Bulk")] ScheduleBulkInput input)
    {
        if (!ModelState.IsValid) { Fail("Vui lòng chọn khoảng ngày hợp lệ."); return RedirectToAction(nameof(Index)); }

        var from = input.FromDate.Date;
        var to = input.ToDate.Date;
        if (to < from) { Fail("Ngày kết thúc phải sau hoặc bằng ngày bắt đầu."); return RedirectToAction(nameof(Index)); }
        if ((to - from).TotalDays > 90) { Fail("Mỗi lần chỉ được tạo lịch trong tối đa 90 ngày."); return RedirectToAction(nameof(Index)); }
        if (input.Days.Count == 0) { Fail("Hãy chọn ít nhất một ngày trong tuần."); return RedirectToAction(nameof(Index)); }

        // Gom các khung giờ: ô đã tick + khung giờ tùy chỉnh (nếu nhập đủ cả hai giờ)
        var slots = new List<(TimeSpan Start, TimeSpan End)>();
        foreach (var s in input.Slots)
        {
            var parts = (s ?? "").Split('-');
            if (parts.Length == 2
                && TimeSpan.TryParseExact(parts[0], @"hh\:mm", CultureInfo.InvariantCulture, out var a)
                && TimeSpan.TryParseExact(parts[1], @"hh\:mm", CultureInfo.InvariantCulture, out var b))
                slots.Add((a, b));
        }
        if (!string.IsNullOrWhiteSpace(input.CustomStart) || !string.IsNullOrWhiteSpace(input.CustomEnd))
        {
            if (TimeSpan.TryParse(input.CustomStart, CultureInfo.InvariantCulture, out var cs)
                && TimeSpan.TryParse(input.CustomEnd, CultureInfo.InvariantCulture, out var ce))
                slots.Add((cs, ce));
            else { Fail("Khung giờ tùy chỉnh chưa đầy đủ hoặc không hợp lệ."); return RedirectToAction(nameof(Index)); }
        }
        slots = slots.Distinct().ToList();
        if (slots.Count == 0) { Fail("Hãy chọn ít nhất một khung giờ."); return RedirectToAction(nameof(Index)); }
        if (slots.Any(x => x.End <= x.Start)) { Fail("Giờ kết thúc phải sau giờ bắt đầu."); return RedirectToAction(nameof(Index)); }

        int added = 0, skipped = 0;
        for (var day = from; day <= to; day = day.AddDays(1))
        {
            if (!input.Days.Contains((int)day.DayOfWeek)) continue;
            foreach (var (start, end) in slots)
            {
                var err = Db.AddSchedule(DoctorId, day, start, end);   // trùng giờ / quá khứ -> bỏ qua
                if (err == null) added++; else skipped++;
            }
        }

        if (added == 0) Fail("Không có ca nào được thêm (các khung giờ đã tồn tại hoặc nằm trong quá khứ).");
        else Success($"Đã thêm {added} ca trực" + (skipped > 0 ? $", bỏ qua {skipped} ca do trùng giờ hoặc đã qua." : "."));
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public IActionResult Delete(int id)
    {
        var err = Db.DeleteSchedule(DoctorId, id);
        if (err != null) Fail(err); else Success("Đã xóa ca trực.");
        return RedirectToAction(nameof(Index));
    }
}