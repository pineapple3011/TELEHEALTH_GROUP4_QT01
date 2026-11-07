using System.Globalization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TELEHEALTH_GROUP4_QT01.Areas.Admin.Helpers;
using TELEHEALTH_GROUP4_QT01.Areas.Admin.Models;
using TELEHEALTH_GROUP4_QT01.Helpers;
using TELEHEALTH_GROUP4_QT01.Models;

namespace TELEHEALTH_GROUP4_QT01.Areas.Admin.Controllers
{
    // Thống kê doanh thu (chỉ tính giao dịch "Đã thanh toán") + duyệt giao dịch chờ xác nhận
    public class RevenueController : AdminBaseController
    {
        private readonly TelehealthContext _context;
        private readonly ILogger<RevenueController> _logger;

        public RevenueController(TelehealthContext context, ILogger<RevenueController> logger)
        {
            _context = context;
            _logger = logger;
        }

        // GET: /Admin/Revenue?from=...&to=...   (mặc định: 6 tháng gần nhất)
        public async Task<IActionResult> Index(DateOnly? from, DateOnly? to)
        {
            var (f, t) = DefaultRange(from, to, monthsBack: 5);
            var paid = await LoadPaidAsync(f, t);

            var vm = new RevenueVm
            {
                From = f,
                To = t,
                Total = paid.Sum(x => x.Amount),
                Count = paid.Count
            };

            // Theo tháng (đủ các tháng trong khoảng, tháng không có doanh thu = 0)
            for (var m = new DateTime(f.Year, f.Month, 1); m <= t.ToDateTime(TimeOnly.MinValue); m = m.AddMonths(1))
            {
                var inMonth = paid.Where(x => x.PaymentTime.Year == m.Year && x.PaymentTime.Month == m.Month).ToList();
                vm.ByMonth.Add(new RevenueItem
                {
                    Label = m.ToString("MM/yyyy", CultureInfo.InvariantCulture),
                    Total = inMonth.Sum(x => x.Amount),
                    Count = inMonth.Count
                });
            }

            vm.BySpecialty = Group(paid, x => x.Specialty);
            vm.ByDoctor = Group(paid, x => x.Doctor).Take(10).ToList();
            vm.ByType = Group(paid, x => x.BookingType);

            // Giao dịch chờ xác nhận (bệnh nhân đã gửi biên lai)
            vm.Pending = await _context.Payments
                .Where(p => p.Status == TtGiaoDich.ChoXacNhan)
                .OrderBy(p => p.PaymentId)
                .Select(p => new PendingPaymentRow
                {
                    PaymentId = p.PaymentId,
                    AppointId = p.AppointId,
                    Time = p.PaymentTime,
                    Amount = p.Amount,
                    Method = p.PaymentMethod,
                    Patient = p.Appoint.Patient.FullName,
                    Doctor = p.Appoint.Doctor.FullName,
                    ReceiptFileId = _context.AppointmentFiles
                        .Where(x => x.AppointId == p.AppointId && x.FileType == "Biên lai")
                        .OrderByDescending(x => x.FileId)
                        .Select(x => (int?)x.FileId).FirstOrDefault()
                }).ToListAsync();
            vm.PendingAmount = vm.Pending.Sum(x => x.Amount);

            return View(vm);
        }

        // POST: xác nhận đã nhận tiền -> giao dịch "Đã thanh toán"; lịch hẹn đang "Chờ xác nhận" chuyển "Đã xác nhận"
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Confirm(int id)
        {
            var p = await _context.Payments.Include(x => x.Appoint).FirstOrDefaultAsync(x => x.PaymentId == id);
            if (p == null || p.Status != TtGiaoDich.ChoXacNhan) return RedirectToAction(nameof(Index));

            p.Status = TtGiaoDich.DaThanhToan;
            p.PaymentTime = DateTime.Now;
            if (p.Appoint.Status == TrangThaiCa.ChoXacNhan) p.Appoint.Status = TrangThaiCa.DaXacNhan;
            await _context.SaveChangesAsync();
            _logger.LogInformation("Admin confirmed payment {PaymentId} of appointment {AppointId}", p.PaymentId, p.AppointId);
            TempData["Msg"] = $"Đã xác nhận thanh toán cho lịch khám #{p.AppointId}.";
            return RedirectToAction(nameof(Index));
        }

        // POST: biên lai không hợp lệ -> trả về "Chờ thanh toán" để bệnh nhân gửi lại
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Reject(int id)
        {
            var p = await _context.Payments.FindAsync(id);
            if (p != null && p.Status == TtGiaoDich.ChoXacNhan)
            {
                p.Status = TtGiaoDich.ChoThanhToan;
                await _context.SaveChangesAsync();
                _logger.LogWarning("Admin rejected receipt of payment {PaymentId} (appointment {AppointId})", p.PaymentId, p.AppointId);
                TempData["Msg"] = $"Đã từ chối biên lai của lịch khám #{p.AppointId}, bệnh nhân cần gửi lại.";
            }
            return RedirectToAction(nameof(Index));
        }

        // GET: xuất danh sách giao dịch đã thanh toán ra CSV
        public async Task<IActionResult> ExportCsv(DateOnly? from, DateOnly? to)
        {
            var (f, t) = DefaultRange(from, to, monthsBack: 5);
            var rows = await LoadPaidAsync(f, t);
            var bytes = CsvBuilder.Build(
                new[] { "Mã giao dịch", "Mã lịch khám", "Thời gian", "Bệnh nhân", "Bác sĩ", "Chuyên khoa", "Hình thức khám", "Phương thức", "Số tiền" },
                rows.Select(r => new[]
                {
                    r.PaymentId.ToString(), r.AppointId.ToString(), r.PaymentTime.ToString("dd/MM/yyyy HH:mm"),
                    r.Patient, r.Doctor, r.Specialty, r.BookingType, r.Method, ((long)r.Amount).ToString()
                }));
            return File(bytes, "text/csv", $"doanh-thu_{f:yyyyMMdd}_{t:yyyyMMdd}.csv");
        }

        private async Task<List<PaidRow>> LoadPaidAsync(DateOnly f, DateOnly t)
        {
            var fromDt = f.ToDateTime(TimeOnly.MinValue);
            var toDt = t.AddDays(1).ToDateTime(TimeOnly.MinValue);
            return await _context.Payments
                .Where(p => p.Status == TtGiaoDich.DaThanhToan && p.PaymentTime >= fromDt && p.PaymentTime < toDt)
                .OrderBy(p => p.PaymentTime)
                .Select(p => new PaidRow
                {
                    PaymentId = p.PaymentId,
                    AppointId = p.AppointId,
                    PaymentTime = p.PaymentTime,
                    Amount = p.Amount,
                    Method = p.PaymentMethod,
                    Patient = p.Appoint.Patient.FullName,
                    Doctor = p.Appoint.Doctor.FullName,
                    Specialty = p.Appoint.Doctor.Spec.SpecialtyName,
                    BookingType = p.Appoint.BookingType
                }).ToListAsync();
        }

        private static List<RevenueItem> Group(List<PaidRow> rows, Func<PaidRow, string> key)
            => rows.GroupBy(key)
                   .Select(g => new RevenueItem { Label = g.Key, Total = g.Sum(x => x.Amount), Count = g.Count() })
                   .OrderByDescending(x => x.Total).ToList();
    }
}
