namespace TELEHEALTH_GROUP4_QT01.Security
{
    /// <summary>Tên vai trò (khớp giá trị cột Accounts.Role). Chỉ định nghĩa MỘT chỗ để tránh gõ sai.</summary>
    public static class AppRoles
    {
        public const string Patient = "Patient";
        public const string Doctor = "Doctor";
        public const string Admin = "Admin";

        /// <summary>Chuẩn hoá giá trị Role lấy từ DB (không phân biệt hoa/thường). Trả về null nếu không hợp lệ.</summary>
        public static string? Normalize(string? role)
        {
            var r = role?.Trim();
            if (string.Equals(r, Patient, StringComparison.OrdinalIgnoreCase)) return Patient;
            if (string.Equals(r, Doctor, StringComparison.OrdinalIgnoreCase)) return Doctor;
            if (string.Equals(r, Admin, StringComparison.OrdinalIgnoreCase)) return Admin;
            return null;
        }
    }

    /// <summary>
    /// Tên các claim. Các claim nằm trong cookie đã được mã hoá + ký (ASP.NET Data Protection)
    /// nên client không thể sửa; ngoài ra mỗi request còn được đối chiếu lại với DB (AuthCookieEvents).
    /// "DoctorID" trùng tên claim mà DoctorBaseController vẫn dùng từ trước.
    /// </summary>
    public static class AppClaims
    {
        public const string PatientId = "PatientID";
        public const string DoctorId = "DoctorID";
        public const string AdminId = "AdminID";
        public const string SecurityStamp = "telehealth:security_stamp";
    }
}
