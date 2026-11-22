using System.Threading.RateLimiting;
using BlogEngine.Web;
using BlogEngine.Web.Data;
using BlogEngine.Web.Data.Entities;
using BlogEngine.Web.Infrastructure;
using BlogEngine.Web.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// ---------- Database (EF Core + SQL Server) ----------
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Thiếu ConnectionStrings:DefaultConnection.");

builder.Services.AddDbContext<BlogDbContext>(options =>
    options.UseSqlServer(connectionString, sql => sql.EnableRetryOnFailure()));

// ---------- Services ----------
builder.Services.AddMemoryCache();
builder.Services.Configure<SmtpOptions>(builder.Configuration.GetSection("Smtp"));
builder.Services.AddScoped<ISettingsService, SettingsService>();
builder.Services.AddSingleton<IPasswordService, PasswordService>();
builder.Services.AddScoped<IEmailService, EmailService>();
builder.Services.AddSingleton<IPasswordResetTokenService, PasswordResetTokenService>();

// ---------- Authentication / Authorization ----------
builder.Services
    .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Cookie.Name = "be.auth";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
        options.LoginPath = "/account/login";
        options.AccessDeniedPath = "/account/denied";
        options.ExpireTimeSpan = TimeSpan.FromDays(14);
        options.SlidingExpiration = true;
    });

builder.Services.AddAuthorizationBuilder()
    .AddPolicy("Staff", p => p.RequireRole(RoleNames.Administrators, RoleNames.Editors))
    .AddPolicy("Admin", p => p.RequireRole(RoleNames.Administrators));

// ---------- MVC ----------
builder.Services.AddLocalization(o => o.ResourcesPath = "Resources");

builder.Services
    .AddControllersWithViews(options =>
    {
        // Mọi POST/PUT/DELETE đều bắt buộc anti-forgery token.
        options.Filters.Add(new AutoValidateAntiforgeryTokenAttribute());
    })
    .AddViewLocalization()
    .AddDataAnnotationsLocalization(o =>
        o.DataAnnotationLocalizerProvider = (_, factory) => factory.Create(typeof(SharedResource)));

builder.Services.Configure<RequestLocalizationOptions>(o =>
{
    o.DefaultRequestCulture = new RequestCulture("en");
    o.SupportedCultures = SupportedLanguages.Cultures;
    o.SupportedUICultures = SupportedLanguages.Cultures;
    // Ưu tiên: cookie người dùng chọn -> ngôn ngữ mặc định trong Settings.
    o.RequestCultureProviders = new List<IRequestCultureProvider>
    {
        new CookieRequestCultureProvider(),
        new SettingsRequestCultureProvider()
    };
});

// ---------- Rate limiting (chống spam comment/contact và brute-force đăng nhập) ----------
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    static string Key(HttpContext ctx) => ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown";

    options.AddPolicy("forms", ctx => RateLimitPartition.GetFixedWindowLimiter(Key(ctx), _ => new FixedWindowRateLimiterOptions
    {
        PermitLimit = 10,
        Window = TimeSpan.FromMinutes(1),
        QueueLimit = 0
    }));

    options.AddPolicy("login", ctx => RateLimitPartition.GetFixedWindowLimiter(Key(ctx), _ => new FixedWindowRateLimiterOptions
    {
        PermitLimit = 10,
        Window = TimeSpan.FromMinutes(5),
        QueueLimit = 0
    }));
});

builder.Services.AddResponseCompression(o =>
{
    o.EnableForHttps = true;
    o.Providers.Add<BrotliCompressionProvider>();
    o.Providers.Add<GzipCompressionProvider>();
});

// Chạy sau reverse proxy / IIS / load balancer: lấy đúng IP và scheme của client.
builder.Services.Configure<ForwardedHeadersOptions>(o =>
{
    o.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;

    // Mặc định chỉ tin proxy loopback. Khi chạy sau nginx/Docker/load balancer khác máy,
    // đặt Hosting:TrustForwardedHeaders=true (chỉ làm vậy nếu app KHÔNG mở trực tiếp ra Internet).
    if (builder.Configuration.GetValue<bool>("Hosting:TrustForwardedHeaders"))
    {
        o.KnownIPNetworks.Clear();
        o.KnownProxies.Clear();
    }
});

var app = builder.Build();

// ---------- Pipeline ----------
app.UseForwardedHeaders();

app.Use(async (context, next) =>
{
    var headers = context.Response.Headers;
    headers["X-Content-Type-Options"] = "nosniff";      // file upload không bị trình duyệt đoán sai kiểu nội dung
    headers["X-Frame-Options"] = "SAMEORIGIN";
    headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
    await next();
});

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/error");
    app.UseHsts();
}
else
{
    app.UseDeveloperExceptionPage();
}

app.UseStatusCodePagesWithReExecute("/error/{0}");
app.UseHttpsRedirection();
app.UseResponseCompression();

app.UseMiddleware<LegacyUrlMiddleware>();
app.UseStaticFiles();

app.UseRequestLocalization();

app.UseRouting();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "admin",
    pattern: "admin/{controller=Dashboard}/{action=Index}/{id?}",
    defaults: new { area = "Admin" });
app.MapControllers();

await DbInitializer.InitializeAsync(app.Services);

app.Run();
