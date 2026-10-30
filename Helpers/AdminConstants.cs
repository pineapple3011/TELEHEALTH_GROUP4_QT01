namespace TELEHEALTH_GROUP4_QT01.Helpers
{
    // Giá trị cột Account.Role. !!! Đối chiếu với CHECK constraint (nếu có) trong bảng Accounts
    public static class VaiTro
    {
        public const string Admin = "Admin";
        public const string Doctor = "Doctor";
        public const string Patient = "Patient";
    }

    // Giá trị cột Doctors.Current. "Available" khớp với Cv.IsAvailable ở phân hệ bệnh nhân.
    // !!! Thống nhất "Busy"/"Working" với bạn làm phân hệ Bác sĩ nếu họ dùng chuỗi khác
    public static class TrangThaiBacSi
    {
        public const string Available = "Available";
        public const string Busy = "Busy";
        public const string Working = "Working";

        public static readonly string[] All = { Available, Busy, Working };

        public static string Label(string? s) => s switch
        {
            null or "" => "Chưa rõ",
            "Available" => "Đang nhận khám",
            "Busy" => "Bận",
            "Working" => "Đang làm việc",
            _ => s
        };
    }
}
