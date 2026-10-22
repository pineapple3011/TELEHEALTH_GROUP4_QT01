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

    public static class PhiKham
    {
        // Tỷ lệ tiền cọc khi khám Offline (30%). Sửa theo quy định của nhóm
        public const decimal TyLeDatCoc = 0.3m;
    }
}
