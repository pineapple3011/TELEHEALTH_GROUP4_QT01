using Microsoft.EntityFrameworkCore;
using TELEHEALTH_GROUP4_QT01.Areas.Admin.Models;
using TELEHEALTH_GROUP4_QT01.Helpers;
using TELEHEALTH_GROUP4_QT01.Models;

namespace TELEHEALTH_GROUP4_QT01.Areas.Admin.Helpers
{
    // Phát hiện bác sĩ để trạng thái "Bận / Đang làm việc" nhưng không thực sự xử lý bệnh nhân.
    // DB không lưu lịch sử đổi trạng thái nên suy ra từ lịch khám + hồ sơ bệnh án trong ngày.
    public static class DoctorStatusAnalyzer
    {
        public static async Task<List<DoctorStatusRow>> AnalyzeAsync(TelehealthContext db, int thresholdHours = 3)
        {
            var now = DateTime.Now;
            var today = DateOnly.FromDateTime(now);
            var dayStart = now.Date;
            var dayEnd = dayStart.AddDays(1);

            var docs = await db.Doctors
                .Where(d => d.Current != TrangThaiBacSi.Available)
                .Select(d => new { d.DoctorId, d.FullName, Spec = d.Spec.SpecialtyName, d.Current })
                .ToListAsync();
            var ids = docs.Select(d => d.DoctorId).ToList();
            if (ids.Count == 0) return new();

            var appts = await db.Appointments
                .Where(a => ids.Contains(a.DoctorId) && a.AppointmentDate == today
                         && (a.Status == TrangThaiCa.DaXacNhan || a.Status == TrangThaiCa.HoanThanh))
                .Select(a => new { a.DoctorId, a.AppointmentTime, a.Status, HasRecord = a.MedicalRecord != null })
                .ToListAsync();

            var lastRecords = (await db.MedicalRecords
                .Where(r => ids.Contains(r.Appoint.DoctorId))
                .GroupBy(r => r.Appoint.DoctorId)
                .Select(g => new { DoctorId = g.Key, Last = g.Max(r => r.CreateAt) })
                .ToListAsync()).ToDictionary(x => x.DoctorId, x => x.Last);

            var todayRecords = (await db.MedicalRecords
                .Where(r => ids.Contains(r.Appoint.DoctorId) && r.CreateAt >= dayStart && r.CreateAt < dayEnd)
                .GroupBy(r => r.Appoint.DoctorId)
                .Select(g => new { DoctorId = g.Key, N = g.Count() })
                .ToListAsync()).ToDictionary(x => x.DoctorId, x => x.N);

            // Mốc giờ (chỉ so sánh trong cùng ngày để tránh lỗi khi vừa qua nửa đêm)
            TimeOnly? Cut(TimeSpan back)
            {
                var c = now - back;
                return c.Date == now.Date ? TimeOnly.FromDateTime(c) : null;
            }
            var overdueCut = Cut(TimeSpan.FromMinutes(30));
            var thresholdCut = Cut(TimeSpan.FromHours(thresholdHours));

            var rows = new List<DoctorStatusRow>();
            foreach (var d in docs)
            {
                var mine = appts.Where(a => a.DoctorId == d.DoctorId).ToList();
                lastRecords.TryGetValue(d.DoctorId, out var last);
                todayRecords.TryGetValue(d.DoctorId, out var nRec);

                var row = new DoctorStatusRow
                {
                    DoctorId = d.DoctorId,
                    DoctorName = d.FullName,
                    Specialty = d.Spec,
                    Status = d.Current,
                    TodayCases = mine.Count,
                    TodayRecords = nRec,
                    LastRecordAt = last == default ? null : last,
                    OverdueNoRecord = overdueCut == null ? 0
                        : mine.Count(a => a.Status == TrangThaiCa.DaXacNhan && !a.HasRecord && a.AppointmentTime < overdueCut.Value)
                };

                if (mine.Count == 0)
                    row.Reasons.Add("Hôm nay không có ca khám nào (đã xác nhận / hoàn thành)");
                if (row.OverdueNoRecord > 0)
                    row.Reasons.Add($"{row.OverdueNoRecord} ca đã quá giờ hơn 30 phút nhưng chưa có hồ sơ bệnh án");
                if (mine.Count > 0 && thresholdCut != null
                    && mine.Any(a => a.AppointmentTime < thresholdCut.Value)
                    && (row.LastRecordAt == null || now - row.LastRecordAt.Value > TimeSpan.FromHours(thresholdHours)))
                    row.Reasons.Add($"Không tạo hồ sơ nào trong {thresholdHours} giờ qua");

                rows.Add(row);
            }
            return rows.OrderByDescending(r => r.Suspicious).ThenBy(r => r.DoctorName).ToList();
        }
    }
}
