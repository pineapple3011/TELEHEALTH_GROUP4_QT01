using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Data.SqlClient;
using TELEHEALTH_GROUP4_QT01.Areas.Doctor.Models;
using TELEHEALTH_GROUP4_QT01.Security;

namespace TELEHEALTH_GROUP4_QT01.Areas.Doctor.Helpers;

/// <summary>
/// Truy cập dữ liệu của phân hệ bác sĩ bằng ADO.NET (SQL thuần), bám đúng bảng trong TELEHEALTH.sql.
/// Không phụ thuộc DbContext nên không cần đổi gì ở phần bệnh nhân / admin.
/// Mọi truy vấn đều lọc theo DoctorID của bác sĩ đang đăng nhập.
/// </summary>
public class DoctorDb
{
    public const string Confirmed = "Đã xác nhận";
    public const string Pending = "Chờ xác nhận";
    public const string Completed = "Hoàn thành";
    public const string Cancelled = "Đã hủy";

    private readonly string _cs;

    public DoctorDb(IConfiguration cfg)
    {
        _cs = cfg.GetConnectionString("TelehealthContext")
              ?? cfg.GetConnectionString("DefaultConnection")
              ?? cfg.GetSection("ConnectionStrings").GetChildren().FirstOrDefault()?.Value
              ?? throw new InvalidOperationException("Chưa cấu hình ConnectionStrings trong appsettings.json");
    }

    // ---------- helpers ----------
    private SqlConnection Open()
    {
        var c = new SqlConnection(_cs);
        c.Open();
        return c;
    }

    private static string? S(SqlDataReader r, string col) => r[col] is DBNull ? null : Convert.ToString(r[col]);
    private static int I(SqlDataReader r, string col) => Convert.ToInt32(r[col]);
    private static DateTime D(SqlDataReader r, string col) => Convert.ToDateTime(r[col]);
    private static TimeSpan T(SqlDataReader r, string col) => r.GetFieldValue<TimeSpan>(r.GetOrdinal(col));

    private List<T> Query<T>(string sql, Func<SqlDataReader, T> map, params SqlParameter[] ps)
    {
        using var c = Open();
        using var cmd = new SqlCommand(sql, c);
        cmd.Parameters.AddRange(ps);
        using var r = cmd.ExecuteReader();
        var list = new List<T>();
        while (r.Read()) list.Add(map(r));
        return list;
    }

    private int Exec(string sql, params SqlParameter[] ps)
    {
        using var c = Open();
        using var cmd = new SqlCommand(sql, c);
        cmd.Parameters.AddRange(ps);
        return cmd.ExecuteNonQuery();
    }

    private object? Scalar(string sql, params SqlParameter[] ps)
    {
        using var c = Open();
        using var cmd = new SqlCommand(sql, c);
        cmd.Parameters.AddRange(ps);
        var v = cmd.ExecuteScalar();
        return v is DBNull ? null : v;
    }

    private static SqlParameter P(string name, object? v) => new(name, v ?? DBNull.Value);

    // ---------- đăng nhập / hồ sơ ----------
    public int? FindDoctorIdByEmail(string email) =>
        Scalar("SELECT DoctorID FROM Accounts WHERE Email=@e AND Role='Doctor' AND IsActive=1 AND DoctorID IS NOT NULL", P("@e", email)) is int id ? id : null;

    public DoctorProfile? GetDoctor(int doctorId) =>
        Query(@"SELECT d.DoctorID, d.SpecID, d.FullName, d.Email, d.Phone, d.EXP, d.[Current], d.AvatarUrl,
                       s.SpecialtyName, s.Description, s.BasePrice
                FROM Doctors d JOIN Specialties s ON s.SpecID=d.SpecID WHERE d.DoctorID=@d",
            r => new DoctorProfile
            {
                DoctorId = I(r, "DoctorID"),
                SpecId = I(r, "SpecID"),
                FullName = S(r, "FullName") ?? "",
                Email = S(r, "Email") ?? "",
                Phone = S(r, "Phone") ?? "",
                Exp = I(r, "EXP"),
                Current = S(r, "Current") ?? "Available",
                AvatarUrl = S(r, "AvatarUrl"),
                SpecialtyName = S(r, "SpecialtyName") ?? "",
                SpecialtyDescription = S(r, "Description"),
                BasePrice = Convert.ToDecimal(r["BasePrice"])
            }, P("@d", doctorId)).FirstOrDefault();

    public void SetStatus(int doctorId, string status) =>
        Exec("UPDATE Doctors SET [Current]=@s WHERE DoctorID=@d", P("@s", status), P("@d", doctorId));

    public void UpdateProfile(int doctorId, string phone, string? avatarUrl)
    {
        if (avatarUrl == null) Exec("UPDATE Doctors SET Phone=@p WHERE DoctorID=@d", P("@p", phone), P("@d", doctorId));
        else Exec("UPDATE Doctors SET Phone=@p, AvatarUrl=@a WHERE DoctorID=@d", P("@p", phone), P("@a", avatarUrl), P("@d", doctorId));
    }

    public bool ChangePassword(int doctorId, string current, string next)
    {
        var stored = Scalar("SELECT PasswordHash FROM Accounts WHERE DoctorID=@d AND Role='Doctor'", P("@d", doctorId)) as string;
        var passwords = new Pbkdf2PasswordService();
        if (stored == null || passwords.Verify(stored, current) == PasswordVerifyResult.Failed) return false;
        Exec("UPDATE Accounts SET PasswordHash=@h WHERE DoctorID=@d AND Role='Doctor'", P("@h", passwords.Hash(next)), P("@d", doctorId));
        return true;
    }

    public ProfileStats GetStats(int doctorId)
    {
        using var c = Open();
        using var cmd = new SqlCommand(@"
            SELECT (SELECT COUNT(*) FROM Appointments WHERE DoctorID=@d),
                   (SELECT COUNT(*) FROM Appointments WHERE DoctorID=@d AND Status=N'Hoàn thành'),
                   (SELECT COUNT(DISTINCT PatientID) FROM Appointments WHERE DoctorID=@d AND Status<>N'Đã hủy'),
                   (SELECT COUNT(*) FROM Schedules WHERE DoctorID=@d AND WorkDate>=CAST(GETDATE() AS date))", c);
        cmd.Parameters.AddWithValue("@d", doctorId);
        using var r = cmd.ExecuteReader();
        r.Read();
        return new ProfileStats { TotalAppointments = r.GetInt32(0), Completed = r.GetInt32(1), Patients = r.GetInt32(2), UpcomingShifts = r.GetInt32(3) };
    }

    // ---------- lịch trực ----------
    public List<ScheduleRow> GetUpcomingSchedules(int doctorId) =>
        Query(@"SELECT s.ScheduleID, s.WorkDate, s.StartTime, s.EndTime,
                       (SELECT COUNT(*) FROM Appointments a WHERE a.ScheduleID=s.ScheduleID AND a.Status<>N'Đã hủy') AS Active
                FROM Schedules s WHERE s.DoctorID=@d AND s.WorkDate>=CAST(GETDATE() AS date)
                ORDER BY s.WorkDate, s.StartTime",
            r => new ScheduleRow { ScheduleId = I(r, "ScheduleID"), WorkDate = D(r, "WorkDate"), StartTime = T(r, "StartTime"), EndTime = T(r, "EndTime"), ActiveAppointments = I(r, "Active") },
            P("@d", doctorId));

    /// <returns>null nếu thêm được, ngược lại là thông báo lỗi.</returns>
    public string? AddSchedule(int doctorId, DateTime date, TimeSpan start, TimeSpan end)
    {
        if (end <= start) return "Giờ kết thúc phải sau giờ bắt đầu.";
        if (date.Date < DateTime.Today) return "Không thể tạo lịch trực trong quá khứ.";
        var overlap = Scalar(@"SELECT COUNT(*) FROM Schedules WHERE DoctorID=@d AND WorkDate=@w AND StartTime<@e AND EndTime>@s",
            P("@d", doctorId), P("@w", date.Date), P("@s", start), P("@e", end));
        if (Convert.ToInt32(overlap) > 0) return "Khung giờ này trùng với một ca trực đã có trong ngày.";
        Exec("INSERT Schedules(DoctorID,WorkDate,StartTime,EndTime) VALUES(@d,@w,@s,@e)",
            P("@d", doctorId), P("@w", date.Date), P("@s", start), P("@e", end));
        return null;
    }

    public string? DeleteSchedule(int doctorId, int scheduleId)
    {
        var active = Scalar(@"SELECT COUNT(*) FROM Appointments a JOIN Schedules s ON s.ScheduleID=a.ScheduleID
                              WHERE s.ScheduleID=@s AND s.DoctorID=@d AND a.Status<>N'Đã hủy'", P("@s", scheduleId), P("@d", doctorId));
        if (Convert.ToInt32(active) > 0) return "Khung giờ này đang có bệnh nhân đặt lịch nên không thể xóa.";
        // Các ca đã hủy còn tham chiếu ScheduleID -> gỡ liên kết trước khi xóa để không vướng khóa ngoại
        Exec(@"UPDATE a SET a.ScheduleID=NULL FROM Appointments a JOIN Schedules s ON s.ScheduleID=a.ScheduleID
               WHERE s.ScheduleID=@s AND s.DoctorID=@d", P("@s", scheduleId), P("@d", doctorId));
        Exec("DELETE FROM Schedules WHERE ScheduleID=@s AND DoctorID=@d", P("@s", scheduleId), P("@d", doctorId));
        return null;
    }

    // ---------- ca khám ----------
    private const string ApptSelect = @"
        SELECT a.AppointID, a.PatientID, p.FullName, p.Phone, a.AppointmentDate, a.AppointmentTime, a.BookingType,
               a.EmergencyLocation, a.TriageLevel, a.Status, c.ClinicName,
               (SELECT COUNT(*) FROM AppointmentFiles f WHERE f.AppointID=a.AppointID) AS FileCount,
               (SELECT TOP 1 pay.Status FROM Payment pay WHERE pay.AppointID=a.AppointID ORDER BY pay.PaymentID DESC) AS PayStatus,
               CASE WHEN EXISTS(SELECT 1 FROM MedicalRecords m WHERE m.AppointID=a.AppointID) THEN 1 ELSE 0 END AS HasRecord
        FROM Appointments a
        JOIN Patients p ON p.PatientID=a.PatientID
        LEFT JOIN Clinics c ON c.ClinicID=a.ClinicID ";

    private static AppointmentRowVm MapAppt(SqlDataReader r) => new()
    {
        AppointId = I(r, "AppointID"),
        PatientId = I(r, "PatientID"),
        PatientName = S(r, "FullName") ?? "",
        PatientPhone = S(r, "Phone") ?? "",
        Date = D(r, "AppointmentDate"),
        Time = T(r, "AppointmentTime"),
        BookingType = S(r, "BookingType") ?? "",
        EmergencyLocation = S(r, "EmergencyLocation"),
        TriageLevel = S(r, "TriageLevel"),
        Status = S(r, "Status") ?? "",
        ClinicName = S(r, "ClinicName"),
        FileCount = I(r, "FileCount"),
        PaymentStatus = S(r, "PayStatus"),
        HasRecord = I(r, "HasRecord") == 1
    };

    public List<AppointmentRowVm> GetAppointmentsByDate(int doctorId, DateTime date) =>
        Query(ApptSelect + "WHERE a.DoctorID=@d AND a.AppointmentDate=@w ORDER BY a.AppointmentTime", MapAppt, P("@d", doctorId), P("@w", date.Date));

    public AppointmentRowVm? GetAppointment(int doctorId, int appointId) =>
        Query(ApptSelect + "WHERE a.DoctorID=@d AND a.AppointID=@a", MapAppt, P("@d", doctorId), P("@a", appointId)).FirstOrDefault();

    public List<AppointmentRowVm> GetAllAppointments(int doctorId) =>
        Query(ApptSelect + "WHERE a.DoctorID=@d ORDER BY a.AppointmentDate DESC, a.AppointmentTime DESC", MapAppt, P("@d", doctorId));

    public DateTime? NextAppointmentDate(int doctorId, DateTime from) =>
        Scalar("SELECT MIN(AppointmentDate) FROM Appointments WHERE DoctorID=@d AND AppointmentDate>=@f AND Status<>N'Đã hủy'", P("@d", doctorId), P("@f", from.Date)) as DateTime?;

    public int PendingOutside(int doctorId, DateTime date) =>
        Convert.ToInt32(Scalar("SELECT COUNT(*) FROM Appointments WHERE DoctorID=@d AND Status=N'Chờ xác nhận' AND AppointmentDate<>@w", P("@d", doctorId), P("@w", date.Date)));

    public PatientInfo? GetPatient(int patientId) =>
        Query("SELECT PatientID, FullName, Email, Phone, Address, EmergencyPhone FROM Patients WHERE PatientID=@p",
            r => new PatientInfo { PatientId = I(r, "PatientID"), FullName = S(r, "FullName") ?? "", Email = S(r, "Email") ?? "", Phone = S(r, "Phone") ?? "", Address = S(r, "Address"), EmergencyPhone = S(r, "EmergencyPhone") },
            P("@p", patientId)).FirstOrDefault();

    public List<AppointmentFileVm> GetFiles(int appointId) =>
        Query("SELECT FileID, FilePath, FileType, UploadDate FROM AppointmentFiles WHERE AppointID=@a ORDER BY UploadDate",
            r => new AppointmentFileVm { FileId = I(r, "FileID"), FilePath = S(r, "FilePath") ?? "", FileType = S(r, "FileType") ?? "", UploadDate = D(r, "UploadDate") },
            P("@a", appointId));

    public PaymentVm? GetPayment(int appointId) =>
        Query("SELECT TOP 1 Amount, PaymentMethod, Status, PaymentTime FROM Payment WHERE AppointID=@a ORDER BY PaymentID DESC",
            r => new PaymentVm { Amount = Convert.ToDecimal(r["Amount"]), Method = S(r, "PaymentMethod") ?? "", Status = S(r, "Status") ?? "", PaymentTime = D(r, "PaymentTime") },
            P("@a", appointId)).FirstOrDefault();

    /// <summary>Đổi trạng thái nếu ca thuộc bác sĩ và đang ở một trong các trạng thái cho phép.</summary>
    public bool ChangeStatus(int doctorId, int appointId, string to, params string[] allowedFrom)
    {
        var inList = string.Join(",", allowedFrom.Select((_, i) => "@f" + i));
        var ps = new List<SqlParameter> { P("@t", to), P("@a", appointId), P("@d", doctorId) };
        ps.AddRange(allowedFrom.Select((f, i) => P("@f" + i, f)));
        return Exec($"UPDATE Appointments SET Status=@t WHERE AppointID=@a AND DoctorID=@d AND Status IN ({inList})", ps.ToArray()) > 0;
    }

    // ---------- khám bệnh, bệnh án, đơn thuốc ----------
    public List<MedicineOption> GetMedicines() =>
        Query("SELECT MedicineID, MedicineName, HDS, Price FROM Medicines ORDER BY MedicineName",
            r => new MedicineOption { MedicineId = I(r, "MedicineID"), Name = S(r, "MedicineName") ?? "", Hds = S(r, "HDS"), Price = Convert.ToDecimal(r["Price"]) });

    public ExamInput LoadExam(int appointId)
    {
        var input = new ExamInput { AppointId = appointId };
        using var c = Open();
        int? recordId = null;
        using (var cmd = new SqlCommand("SELECT RecordID, Diagnosis, FollowUpDate FROM MedicalRecords WHERE AppointID=@a", c))
        {
            cmd.Parameters.AddWithValue("@a", appointId);
            using var r = cmd.ExecuteReader();
            if (r.Read())
            {
                recordId = I(r, "RecordID");
                input.Diagnosis = S(r, "Diagnosis") ?? "";
                input.FollowUpDate = r["FollowUpDate"] is DBNull ? null : D(r, "FollowUpDate");
            }
        }
        if (recordId != null)
        {
            using var cmd = new SqlCommand("SELECT MedicineID, Quantity, Dosage, HDS FROM Prescription WHERE RecordID=@r ORDER BY MedicineID", c);
            cmd.Parameters.AddWithValue("@r", recordId.Value);
            using var r = cmd.ExecuteReader();
            while (r.Read())
                input.Items.Add(new PrescriptionItemInput { MedicineId = I(r, "MedicineID"), Quantity = I(r, "Quantity"), Dosage = S(r, "Dosage") ?? "", Hds = S(r, "HDS") });
        }
        return input;
    }

    /// <summary>Lưu bệnh án + đơn thuốc trong một transaction. complete=true thì chuyển ca sang "Hoàn thành".</summary>
    /// <returns>null nếu thành công, ngược lại là thông báo lỗi.</returns>
    public string? SaveExam(int doctorId, ExamInput input, bool complete)
    {
        using var c = Open();
        using var tx = c.BeginTransaction();
        try
        {
            string? status;
            using (var cmd = new SqlCommand("SELECT Status FROM Appointments WHERE AppointID=@a AND DoctorID=@d", c, tx))
            {
                cmd.Parameters.AddWithValue("@a", input.AppointId);
                cmd.Parameters.AddWithValue("@d", doctorId);
                status = cmd.ExecuteScalar() as string;
            }
            if (status == null) { tx.Rollback(); return "Không tìm thấy ca khám."; }
            if (status != Confirmed && status != Completed) { tx.Rollback(); return "Chỉ khám được những ca đã xác nhận."; }

            int recordId;
            using (var cmd = new SqlCommand("SELECT RecordID FROM MedicalRecords WHERE AppointID=@a", c, tx))
            {
                cmd.Parameters.AddWithValue("@a", input.AppointId);
                var existing = cmd.ExecuteScalar();
                if (existing == null)
                {
                    using var ins = new SqlCommand("INSERT MedicalRecords(AppointID,Diagnosis,FollowUpDate) OUTPUT INSERTED.RecordID VALUES(@a,@dg,@f)", c, tx);
                    ins.Parameters.AddWithValue("@a", input.AppointId);
                    ins.Parameters.AddWithValue("@dg", input.Diagnosis);
                    ins.Parameters.AddWithValue("@f", (object?)input.FollowUpDate?.Date ?? DBNull.Value);
                    recordId = (int)ins.ExecuteScalar()!;
                }
                else
                {
                    recordId = Convert.ToInt32(existing);
                    using var upd = new SqlCommand("UPDATE MedicalRecords SET Diagnosis=@dg, FollowUpDate=@f WHERE RecordID=@r", c, tx);
                    upd.Parameters.AddWithValue("@r", recordId);
                    upd.Parameters.AddWithValue("@dg", input.Diagnosis);
                    upd.Parameters.AddWithValue("@f", (object?)input.FollowUpDate?.Date ?? DBNull.Value);
                    upd.ExecuteNonQuery();
                }
            }

            using (var del = new SqlCommand("DELETE FROM Prescription WHERE RecordID=@r", c, tx))
            {
                del.Parameters.AddWithValue("@r", recordId);
                del.ExecuteNonQuery();
            }
            foreach (var it in input.Items)
            {
                using var ins = new SqlCommand("INSERT Prescription(RecordID,MedicineID,Quantity,Dosage,HDS) VALUES(@r,@m,@q,@dos,@h)", c, tx);
                ins.Parameters.AddWithValue("@r", recordId);
                ins.Parameters.AddWithValue("@m", it.MedicineId);
                ins.Parameters.AddWithValue("@q", it.Quantity);
                ins.Parameters.AddWithValue("@dos", it.Dosage);
                ins.Parameters.AddWithValue("@h", (object?)it.Hds ?? DBNull.Value);
                ins.ExecuteNonQuery();
            }

            if (complete)
            {
                using var up = new SqlCommand("UPDATE Appointments SET Status=N'Hoàn thành' WHERE AppointID=@a AND DoctorID=@d", c, tx);
                up.Parameters.AddWithValue("@a", input.AppointId);
                up.Parameters.AddWithValue("@d", doctorId);
                up.ExecuteNonQuery();
            }
            tx.Commit();
            return null;
        }
        catch
        {
            tx.Rollback();
            throw;
        }
    }

}
