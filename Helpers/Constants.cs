namespace TELEHEALTH_GROUP4_QT01.Helpers
{
    // !!! Thống nhất các chuỗi này với bạn làm phân hệ Bác sĩ và Admin
    public static class HinhThucKham
    {
        public const string Online = "Online";
        public const string Offline = "Offline";
        public const string CapCuu = "Cấp cứu";
    }

    public static class TrangThaiCa
    {
        public const string ChoXacNhan = "Chờ xác nhận";
        public const string DaXacNhan = "Đã xác nhận";
        public const string HoanThanh = "Hoàn thành";
        public const string DaHuy = "Đã hủy";
    }

    public static class TtGiaoDich
    {
        public const string ChoThanhToan = "Chờ thanh toán";
        public const string ChoXacNhan = "Chờ xác nhận";
        public const string DaThanhToan = "Đã thanh toán";
    }

    // Quy tắc vào phòng khám online (dùng chung cho bệnh nhân và bác sĩ):
    //  - chỉ ca Online, trạng thái "Đã xác nhận"
    //  - giao dịch thanh toán (nếu có) phải được admin duyệt = "Đã thanh toán"
    public static class PhongOnline
    {
        /// <returns>null nếu được vào phòng, ngược lại là lý do bị chặn.</returns>
        public static string? LyDoChan(string? hinhThuc, string? trangThaiCa, string? trangThaiGiaoDich)
        {
            if (hinhThuc != HinhThucKham.Online)
                return "Ca khám này không phải hình thức trực tuyến.";
            if (trangThaiCa == TrangThaiCa.DaHuy)
                return "Ca khám đã bị hủy.";
            if (trangThaiCa == TrangThaiCa.HoanThanh)
                return "Ca khám đã hoàn thành.";

            // Có giao dịch mà admin chưa duyệt (Chờ thanh toán / Chờ xác nhận) -> chặn
            if (trangThaiGiaoDich != null && trangThaiGiaoDich != TtGiaoDich.DaThanhToan)
                return "Quản trị viên chưa duyệt thanh toán của ca khám này nên chưa thể vào phòng khám online.";

            if (trangThaiCa != TrangThaiCa.DaXacNhan)
                return "Ca khám chưa được xác nhận nên chưa thể vào phòng khám online.";

            return null;
        }

        public static bool DuocVao(string? hinhThuc, string? trangThaiCa, string? trangThaiGiaoDich)
            => LyDoChan(hinhThuc, trangThaiCa, trangThaiGiaoDich) == null;
    }

    public static class PhiKham
    {
        // Tỷ lệ tiền cọc khi khám Offline (30%). Sửa theo quy định của nhóm
        public const decimal TyLeDatCoc = 0.3m;
    }
}
