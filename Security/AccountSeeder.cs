using Microsoft.EntityFrameworkCore;
using TELEHEALTH_GROUP4_QT01.Models;

namespace TELEHEALTH_GROUP4_QT01.Security
{
    /// <summary>
    /// Tạo tài khoản Admin từ cấu hình (user-secrets / biến môi trường):
    ///   Seed:Admin:Email, Seed:Admin:Password, Seed:Admin:FullName, Seed:Admin:Phone
    /// Mật khẩu KHÔNG nằm trong mã nguồn. Chỉ tạo khi chưa có tài khoản nào dùng email đó.
    /// </summary>
    public static class AccountSeeder
    {
        public static async Task SeedAdminAsync(IServiceProvider sp, IConfiguration cfg, ILogger logger)
        {
            var email = cfg["Seed:Admin:Email"]?.Trim().ToLowerInvariant();
            var password = cfg["Seed:Admin:Password"];
            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrEmpty(password)) return;

            try
            {
                var db = sp.GetRequiredService<TelehealthContext>();
                var hasher = sp.GetRequiredService<IPasswordService>();

                if (await db.Accounts.AnyAsync(a => a.Email == email)) return;

                var err = PasswordPolicy.Validate(password, email);
                if (err != null)
                {
                    logger.LogError("Seed admin bị bỏ qua vì mật khẩu không đạt chính sách: {Error}", err);
                    return;
                }

                var admin = await db.Admins.FirstOrDefaultAsync(a => a.Email == email)
                            ?? new TELEHEALTH_GROUP4_QT01.Models.Admin
                            {
                                FullName = cfg["Seed:Admin:FullName"] ?? "Quản trị viên",
                                Email = email,
                                Phone = cfg["Seed:Admin:Phone"] ?? "0000000000"
                            };

                db.Accounts.Add(new Account
                {
                    Email = email,
                    PasswordHash = hasher.Hash(password),
                    Role = AppRoles.Admin,
                    Admin = admin,
                    IsActive = true,
                    CreatedAt = DateTime.Now
                });
                await db.SaveChangesAsync();
                logger.LogInformation("Đã tạo tài khoản Admin từ cấu hình: {Email}", email);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Không thể tạo tài khoản Admin từ cấu hình. Đã chạy Database/01_Accounts_Auth.sql chưa?");
            }
        }
    }
}
