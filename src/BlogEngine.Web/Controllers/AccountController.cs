using System.Security.Cryptography;
using System.Text;
using BlogEngine.Web.Data;
using BlogEngine.Web.Data.Entities;
using BlogEngine.Web.Infrastructure;
using BlogEngine.Web.Models;
using BlogEngine.Web.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace BlogEngine.Web.Controllers;

[Route("account")]
public class AccountController : LocalizedController
{
    private const int MaxFailedAttempts = 5;
    private static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);

    private readonly BlogDbContext _db;
    private readonly IPasswordService _passwords;
    private readonly IPasswordResetTokenService _resetTokens;
    private readonly IEmailService _email;
    private readonly ISettingsService _settings;
    private readonly IConfiguration _config;
    private readonly IWebHostEnvironment _env;
    private readonly ILogger<AccountController> _logger;

    public AccountController(BlogDbContext db, IPasswordService passwords, IPasswordResetTokenService resetTokens,
        IEmailService email, ISettingsService settings, IConfiguration config, IWebHostEnvironment env,
        ILogger<AccountController> logger)
    {
        _db = db;
        _passwords = passwords;
        _resetTokens = resetTokens;
        _email = email;
        _settings = settings;
        _config = config;
        _env = env;
        _logger = logger;
    }

    [HttpGet("login")]
    public IActionResult Login(string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated == true) return RedirectAfterLogin(returnUrl, User.IsStaff());
        return View(new LoginInput { ReturnUrl = returnUrl });
    }

    [HttpPost("login")]
    [EnableRateLimiting("login")]
    public async Task<IActionResult> Login(LoginInput input)
    {
        if (!ModelState.IsValid) return View(input);

        var userName = input.UserName.Trim();
        var user = await _db.Users.Include(u => u.Roles).FirstOrDefaultAsync(u => u.UserName == userName);

        if (user is null || !user.IsActive)
        {
            // Băm giả để thời gian phản hồi không lộ việc tài khoản có tồn tại hay không.
            _passwords.Hash(new User(), input.Password);
            return Fail(input);
        }

        if (user.LockoutEnd is { } lockoutEnd && lockoutEnd > DateTime.UtcNow)
        {
            var minutes = Math.Max(1, (int)Math.Ceiling((lockoutEnd - DateTime.UtcNow).TotalMinutes));
            ModelState.AddModelError("", L.Plural(minutes, "Too many failed attempts. Try again in {0} minute.", "Too many failed attempts. Try again in {0} minutes.").Value);
            return View(input);
        }

        var check = _passwords.Verify(user, input.Password);
        if (check == PasswordCheck.Failed)
        {
            user.FailedLoginCount++;
            if (user.FailedLoginCount >= MaxFailedAttempts)
            {
                user.LockoutEnd = DateTime.UtcNow.Add(LockoutDuration);
                user.FailedLoginCount = 0;
            }
            await _db.SaveChangesAsync();
            return Fail(input);
        }

        user.FailedLoginCount = 0;
        user.LockoutEnd = null;
        user.LastLoginTime = DateTime.UtcNow;
        if (check == PasswordCheck.SuccessRehashNeeded)
            user.PasswordHash = _passwords.Hash(user, input.Password); // nâng cấp hash SHA-256 cũ lên PBKDF2
        await _db.SaveChangesAsync();

        var principal = ClaimsExtensions.BuildPrincipal(user, CookieAuthenticationDefaults.AuthenticationScheme);
        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal,
            new AuthenticationProperties { IsPersistent = input.RememberMe });

        if (user.MustChangePassword)
            return RedirectToAction(nameof(ChangePassword), new { returnUrl = input.ReturnUrl });

        return RedirectAfterLogin(input.ReturnUrl, user.Roles.Any(r => r.Name is RoleNames.Administrators or RoleNames.Editors));
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return Redirect("/");
    }

    [Authorize]
    [HttpGet("change-password")]
    public IActionResult ChangePassword(string? returnUrl = null)
    {
        ViewBag.ReturnUrl = returnUrl;
        ViewBag.Forced = User.HasClaim(c => c.Type == ClaimsExtensions.MustChangePasswordClaim);
        return View(new ChangePasswordInput());
    }

    [Authorize]
    [HttpPost("change-password")]
    [EnableRateLimiting("login")]
    public async Task<IActionResult> ChangePassword(ChangePasswordInput input, string? returnUrl = null)
    {
        ViewBag.ReturnUrl = returnUrl;
        ViewBag.Forced = User.HasClaim(c => c.Type == ClaimsExtensions.MustChangePasswordClaim);
        if (!ModelState.IsValid) return View(input);

        var user = await _db.Users.Include(u => u.Roles).FirstOrDefaultAsync(u => u.Id == User.GetUserId());
        if (user is null) return Challenge();

        if (_passwords.Verify(user, input.CurrentPassword) == PasswordCheck.Failed)
        {
            ModelState.AddModelError(nameof(input.CurrentPassword), L["Current password is incorrect."].Value);
            return View(input);
        }

        user.PasswordHash = _passwords.Hash(user, input.NewPassword);
        user.MustChangePassword = false;
        await _db.SaveChangesAsync();

        // Đăng nhập lại để cập nhật claim (bỏ cờ must_change_password).
        var principal = ClaimsExtensions.BuildPrincipal(user, CookieAuthenticationDefaults.AuthenticationScheme);
        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal);

        TempData["Message"] = L["Your password was changed."].Value;
        return RedirectAfterLogin(returnUrl, user.Roles.Any(r => r.Name is RoleNames.Administrators or RoleNames.Editors));
    }

    // ---------- Quên mật khẩu (chỉ Administrators / Editors, qua email) ----------

    [HttpGet("forgot-password")]
    public IActionResult ForgotPassword() => View(new ForgotPasswordInput());

    [HttpPost("forgot-password")]
    [EnableRateLimiting("login")]
    public async Task<IActionResult> ForgotPassword(ForgotPasswordInput input)
    {
        if (!ModelState.IsValid) return View(input);

        var key = input.Identifier.Trim();
        var users = await _db.Users.Include(u => u.Roles)
            .Where(u => u.IsActive && u.EmailAddress != null && (u.UserName == key || u.EmailAddress == key))
            .ToListAsync();

        var settings = await _settings.GetAsync();
        foreach (var user in users.Where(u => u.Roles.Any(r => r.Name is RoleNames.Administrators or RoleNames.Editors)))
        {
            var token = _resetTokens.Create(user);
            var path = Url.Action(nameof(ResetPassword), "Account", new { token }) ?? "/account/reset-password";
            var baseUrl = _config["Hosting:PublicBaseUrl"]?.TrimEnd('/') ?? $"{Request.Scheme}://{Request.Host}";
            var link = baseUrl + path;

            if (_env.IsDevelopment())
                _logger.LogInformation("Link đặt lại mật khẩu của {User}: {Link}", user.UserName, link);

            var minutes = (int)PasswordResetTokenService.Lifetime.TotalMinutes;
            var body =
                $"<p>{System.Net.WebUtility.HtmlEncode(L["Hello"].Value)} {System.Net.WebUtility.HtmlEncode(user.Name)},</p>" +
                $"<p>{System.Net.WebUtility.HtmlEncode(L["We received a request to reset your password. Click the link below to choose a new one:"].Value)}</p>" +
                $"<p><a href=\"{System.Net.WebUtility.HtmlEncode(link)}\">{System.Net.WebUtility.HtmlEncode(link)}</a></p>" +
                $"<p>{System.Net.WebUtility.HtmlEncode(string.Format(L["This link is valid for {0} minutes and can be used once. If you did not request it, ignore this email."].Value, minutes))}</p>";

            await _email.SendAsync(user.EmailAddress!, $"[{settings.Name}] {L["Reset your password"].Value}", body);
        }

        // Luôn trả cùng một thông báo để không lộ tài khoản nào tồn tại.
        TempData["Message"] = L["If the account exists and has an email address, a reset link has been sent."].Value;
        return RedirectToAction(nameof(Login));
    }

    [HttpGet("reset-password")]
    public IActionResult ResetPassword(string? token)
    {
        if (!_resetTokens.TryRead(token, out _, out _))
            return View("ResetPasswordInvalid");
        return View(new ResetPasswordInput { Token = token! });
    }

    [HttpPost("reset-password")]
    [EnableRateLimiting("login")]
    public async Task<IActionResult> ResetPassword(ResetPasswordInput input)
    {
        if (!_resetTokens.TryRead(input.Token, out var userId, out var fingerprint))
            return View("ResetPasswordInvalid");
        if (!ModelState.IsValid) return View(input);

        var user = await _db.Users.Include(u => u.Roles).FirstOrDefaultAsync(u => u.Id == userId);
        if (user is null || !user.IsActive
            || !user.Roles.Any(r => r.Name is RoleNames.Administrators or RoleNames.Editors)
            || !CryptographicOperations.FixedTimeEquals(
                   Encoding.UTF8.GetBytes(_resetTokens.Fingerprint(user)), Encoding.UTF8.GetBytes(fingerprint)))
            return View("ResetPasswordInvalid");

        user.PasswordHash = _passwords.Hash(user, input.NewPassword);
        user.MustChangePassword = false;
        user.FailedLoginCount = 0;
        user.LockoutEnd = null;
        await _db.SaveChangesAsync();

        TempData["Message"] = L["Your password was changed."].Value;
        return RedirectToAction(nameof(Login));
    }

    [HttpGet("denied")]
    public IActionResult Denied() => View();

    private IActionResult Fail(LoginInput input)
    {
        ModelState.AddModelError("", L["Invalid username or password."].Value);
        input.Password = "";
        return View(input);
    }

    private IActionResult RedirectAfterLogin(string? returnUrl, bool isStaff)
    {
        if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl)) return LocalRedirect(returnUrl);
        return isStaff ? Redirect("/admin") : Redirect("/");
    }
}
