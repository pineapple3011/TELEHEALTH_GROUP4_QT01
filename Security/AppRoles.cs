namespace TELEHEALTH_GROUP4_QT01.Security
{
    /// <summary>Tên vai trò – chỉ định nghĩa MỘT chỗ để tránh gõ sai chuỗi.</summary>
    public static class AppRoles
    {
        public const string Patient = "Patient";
        public const string Doctor = "Doctor";
        public const string Admin = "Admin";
    }

    /// <summary>
    /// Tên các claim tuỳ chỉnh. Các claim này nằm trong cookie đã được mã hoá + ký
    /// (ASP.NET Data Protection) nên client không thể sửa; ngoài ra mỗi request
    /// còn được đối chiếu lại với DB (xem AuthCookieEvents).
    /// </summary>
    public static class AppClaims
    {
        public const string PatientId = "telehealth:patient_id";
        public const string DoctorId = "telehealth:doctor_id";
        public const string AdminId = "telehealth:admin_id";
        public const string SecurityStamp = "telehealth:security_stamp";
    }
}
