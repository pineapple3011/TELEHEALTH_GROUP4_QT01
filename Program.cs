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

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    // Chỉ đưa /api/... vào tài liệu, bỏ qua các trang MVC thường (tránh lỗi và rối)
    c.DocInclusionPredicate((_, api) =>
        api.RelativePath?.StartsWith("api/", StringComparison.OrdinalIgnoreCase) == true);

    // Tùy chọn: hiển thị phần mô tả /// <summary> trong Swagger (xem Bước 5)
    var xml = Path.Combine(AppContext.BaseDirectory, $"{typeof(Program).Assembly.GetName().Name}.xml");
    if (File.Exists(xml)) c.IncludeXmlComments(xml);
});

builder.Services.AddAntiforgery(options =>
{
    options.Cookie.Name = "Telehealth.Antiforgery";
    options.Cookie.HttpOnly = true;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    options.Cookie.SameSite = SameSiteMode.Strict;
});

builder.Services.AddSession(options =>
{
    options.Cookie.HttpOnly = true;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    options.Cookie.SameSite = SameSiteMode.Lax;
});

// ---------------------------------------------------------------------------
// Cơ sở dữ liệu – chuỗi kết nối lấy từ cấu hình (user-secrets / biến môi trường), KHÔNG gán cứng
//   dotnet user-secrets set "ConnectionStrings:TelehealthContext" "..."
//   hoặc biến môi trường: ConnectionStrings__TelehealthContext
// (DoctorDb cũng đọc cùng chuỗi này)
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
builder.Services.AddMemoryCache();   // AuthCookieEvents nhớ kết quả kiểm tra phiên 30 giây
builder.Services.AddScoped<AuthCookieEvents>();
builder.Services.Configure<BankOptions>(builder.Configuration.GetSection(BankOptions.SectionName));

// ---------------------------------------------------------------------------
// Xác thực bằng cookie + phân quyền theo vai trò (kiểm tra ở server) – dùng chung cho Patient/Doctor/Admin
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
app.UseSession();

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

// Header bảo mật. Chưa đặt script-src/style-src vì layout hiện có script/style nội tuyến và Google Translate;
// các chỉ thị dưới đây không cần sửa giao diện nhưng vẫn chặn clickjacking, nhúng plugin và form gửi ra ngoài.
app.Use(async (context, next) =>
{
    var headers = context.Response.Headers;
    headers["Content-Security-Policy"] = "object-src 'none'; base-uri 'self'; form-action 'self'; frame-ancestors 'self'";
    headers["X-Content-Type-Options"] = "nosniff";
    headers["X-Frame-Options"] = "SAMEORIGIN";
    headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
    headers["Permissions-Policy"] =
        "camera=(self \"https://meet.jit.si\"), " +
        "microphone=(self \"https://meet.jit.si\"), " +
        "display-capture=(self \"https://meet.jit.si\"), " +
        "geolocation=(self)";
    await next();
});

if (app.Environment.IsDevelopment() || app.Configuration.GetValue<bool>("Swagger:Enabled"))
{
    app.UseSwagger();
    app.UseSwaggerUI(o => o.DocumentTitle = "Telehealth API");
}

app.UseRouting();

app.UseRateLimiter();
app.UseAuthentication();    // PHẢI đứng trước UseAuthorization
app.UseAuthorization();

app.MapStaticAssets();
app.MapControllerRoute(
    name: "areas",
    pattern: "{area:exists}/{controller=Dashboard}/{action=Index}/{id?}")
    .WithStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

// Tạo tài khoản Admin từ cấu hình (nếu có cấu hình Seed:Admin và chưa có tài khoản dùng email đó)
using (var scope = app.Services.CreateScope())
{
    await AccountSeeder.SeedAdminAsync(scope.ServiceProvider, app.Configuration, app.Logger);
}

app.Run();
