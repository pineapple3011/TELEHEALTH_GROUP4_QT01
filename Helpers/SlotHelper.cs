using TELEHEALTH_GROUP4_QT01.Models;

namespace TELEHEALTH_GROUP4_QT01.Helpers
{
    public static class SlotHelper
    {
        // Chia một ca làm việc (StartTime -> EndTime) thành các khung 30 phút, bỏ khung đã có người đặt
        public static List<TimeOnly> FreeSlots(Schedule lich, IEnumerable<Appointment> bookedInSchedule, int stepMinutes = 30)
        {
            var booked = bookedInSchedule
                .Where(c => c.Status != TrangThaiCa.DaHuy)
                .Select(c => Cv.T(c.AppointmentTime))
                .ToHashSet();

            var result = new List<TimeOnly>();
            var end = Cv.T(lich.EndTime);
            var t = Cv.T(lich.StartTime);

            while (true)
            {
                var next = t.AddMinutes(stepMinutes);
                if (next <= t || next > end) break;      // chặn vòng lặp khi qua nửa đêm
                if (!booked.Contains(t)) result.Add(t);
                t = next;
            }
            return result;
        }
    }
}
