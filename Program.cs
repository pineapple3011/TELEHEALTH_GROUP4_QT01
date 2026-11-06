using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TELEHEALTH_GROUP4_QT01.Models;
using TELEHEALTH_GROUP4_QT01.Security;
using TELEHEALTH_GROUP4_QT01.Services;

var builder = WebApplication.CreateBuilder(args);

// ---------------------------------------------------------------------------
// MVC + CSRF
// AutoValidateAntiforgeryToken: MỌI request POST/PUT/DELETE/PATCH đều bắt buộc có token chống CSRF
// (GET không cần). Form dùng tag helper (<form asp-action=...>) tự chèn token.
// ---------------------------------------------------------------------------
builder.Services.AddControllersWithViews(options =>
{
    options.Filters.Add(new AutoValidateAntiforgeryTokenAttribute());
});

builder.Services.AddAntiforgery(options =>
{
    options.Cookie.Name = "Telehealth.Antiforgery";
    options.Cookie.HttpOnly = true;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    options.Cookie.SameSite = SameSiteMode.Strict;
});

// ---------------------------------------------------------------------------
// Cơ sở dữ liệu – chuỗi kết nối lấy từ cấu hình (user-secrets / biến môi trường), KHÔNG gán cứng
//   dotnet user-secrets set "ConnectionStrings:TelehealthContext" "..."
//   hoặc biến môi trường: ConnectionStrings__TelehealthContext
// ---------------------------------------------------------------------------
var connectionString = builder.Configuration.GetConnectionString("TelehealthContext")
    ?? throw new InvalidOperationException(
        "Thiếu ConnectionStrings:TelehealthContext. Hãy cấu hình bằng 'dotnet user-secrets' (dev) " +
        "hoặc biến môi trường ConnectionStrings__TelehealthContext (production). Xem HUONG_DAN.md.");

builder.Services.AddDbContext<TelehealthContext>(x => x.UseSqlServer(connectionString));

// ---------------------------------------------------------------------------
// Dịch vụ của ứng dụng
// ---------------------------------------------------------------------------
builder.Services.AddSingleton<IPasswordService, Pbkdf2PasswordService>();
builder.Services.AddSingleton<IFileStorageService, FileStorageService>();
builder.Services.AddScoped<AuthCookieEvents>();
builder.Services.Configure<BankOptions>(builder.Configuration.GetSection(BankOptions.SectionName));

// ---------------------------------------------------------------------------
// Xác thực bằng cookie + phân quyền theo vai trò (kiểm tra ở server)
// ---------------------------------------------------------------------------
builder.Services
    .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Cookie.Name = "Telehealth.Auth";
        options.Cookie.HttpOnly = true;                              // JS không đọc được cookie => giảm rủi ro XSS đánh cắp phiên
        options.Cookie.SecurePolicy = CookieSecurePolicy.Always;     // chỉ gửi qua HTTPS
        options.Cookie.SameSite = SameSiteMode.Lax;                  // giảm CSRF (kết hợp antiforgery token)
        options.LoginPath = "/Account/Login";
        options.AccessDeniedPath = "/Account/AccessDenied";
        options.ExpireTimeSpan = TimeSpan.FromHours(2);
        options.SlidingExpiration = true;
        options.EventsType = typeof(AuthCookieEvents);               // đối chiếu lại DB ở mỗi request
    });

builder.Services.AddAuthorization();

// ---------------------------------------------------------------------------
// Giới hạn tần suất (rate limit) cho đăng nhập / đăng ký: 10 request / phút / IP
// ---------------------------------------------------------------------------
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("auth", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 10,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            }));
});

var app = builder.Build();

// ---------------------------------------------------------------------------
// Pipeline
// ---------------------------------------------------------------------------
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();

// Header bảo mật. Ở Development dùng chế độ Report-Only (chỉ báo lỗi ở Console, không chặn)
// để không làm hỏng công cụ hot-reload / Browser Link của Visual Studio.
var cspHeaderName = app.Environment.IsDevelopment()
    ? "Content-Security-Policy-Report-Only"
    : "Content-Security-Policy";

const string csp =
    "default-src 'self'; " +
    "script-src 'self'; " +
    "style-src 'self' 'unsafe-inline'; " +          // template Alazea dùng nhiều style="..." nội tuyến
    "img-src 'self' data: https://img.vietqr.io; " + // ảnh QR thanh toán VietQR
    "font-src 'self' data:; " +
    "connect-src 'self'; " +
    "object-src 'none'; " +
    "base-uri 'self'; " +
    "form-action 'self'; " +
    "frame-ancestors 'none'";                        // chống clickjacking

app.Use(async (context, next) =>
{
    var headers = context.Response.Headers;
    headers[cspHeaderName] = csp;
    headers["X-Content-Type-Options"] = "nosniff";
    headers["X-Frame-Options"] = "DENY";
    headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
    headers["Permissions-Policy"] = "camera=(self), microphone=(self), geolocation=(self)";
    await next();
});

app.UseRouting();

app.UseRateLimiter();
app.UseAuthentication();    // PHẢI đứng trước UseAuthorization
app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

// Tạo tài khoản Admin đầu tiên từ cấu hình (nếu có và chưa có Admin nào)
using (var scope = app.Services.CreateScope())
{
    await AccountSeeder.SeedAdminAsync(scope.ServiceProvider, app.Configuration, app.Logger);
}

app.Run();
