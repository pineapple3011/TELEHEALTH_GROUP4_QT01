using System.Globalization;

namespace TELEHEALTH_GROUP4_QT01.Helpers
{
    // Các hàm chuyển kiểu nhỏ, giúp code chạy được dù cột trong DB cho phép null hay không
    public static class Cv
    {
        public static int I(int? v) => v ?? 0;
        public static decimal M(decimal? v) => v ?? 0m;
        public static DateOnly D(DateOnly? v) => v ?? default;
        public static TimeOnly T(TimeOnly? v) => v ?? default;

        // Định dạng ngày/giờ để hiển thị (hỗ trợ cả DateOnly, DateTime, TimeOnly, TimeSpan)
        public static string F(object? v, string format)
        {
            if (v == null) return "";
            if (v is TimeSpan ts) return ts.ToString(@"hh\:mm");
            if (v is IFormattable f) return f.ToString(format, null);
            return v.ToString() ?? "";
        }

        // Đọc thuộc tính theo tên, thử lần lượt nhiều tên (phòng trường hợp cột bị gõ sai chính tả trong DB)
        public static string Prop(object? o, params string[] names)
        {
            if (o == null) return "";
            foreach (var n in names)
            {
                var p = o.GetType().GetProperty(n);
                if (p != null) return Convert.ToString(p.GetValue(o)) ?? "";
            }
            return "";
        }

        // Hiển thị trạng thái bác sĩ cho dễ đọc
        public static string DoctorStatus(string? s) =>
            string.Equals(s, "Available", StringComparison.OrdinalIgnoreCase) ? "Đang nhận khám"
            : string.IsNullOrWhiteSpace(s) ? "" : s;

        public static bool IsAvailable(string? s) =>
            string.Equals(s, "Available", StringComparison.OrdinalIgnoreCase);

        // "20" -> "20 năm"
        public static string Years(string? s) =>
            int.TryParse(s, out var n) ? $"{n} năm" : (s ?? "");

        public static string Money(decimal v) =>
            v.ToString("N0", CultureInfo.InvariantCulture).Replace(",", ".") + " đ";
    }
}
