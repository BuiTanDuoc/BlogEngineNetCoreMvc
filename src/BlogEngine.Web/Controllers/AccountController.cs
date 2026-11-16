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

    public AccountController(BlogDbContext db, IPasswordService passwords)
    {
        _db = db;
        _passwords = passwords;
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
