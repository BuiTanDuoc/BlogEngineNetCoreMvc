using System.ComponentModel.DataAnnotations;
using BlogEngine.Web.Areas.Admin.Models;
using BlogEngine.Web.Data;
using BlogEngine.Web.Data.Entities;
using BlogEngine.Web.Infrastructure;
using BlogEngine.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BlogEngine.Web.Areas.Admin.Controllers;

[Authorize(Policy = "Admin")]
public class UsersController : AdminControllerBase
{
    private readonly BlogDbContext _db;
    private readonly IPasswordService _passwords;

    public UsersController(BlogDbContext db, IPasswordService passwords)
    {
        _db = db;
        _passwords = passwords;
    }

    public async Task<IActionResult> Index()
        => View(await _db.Users.AsNoTracking().Include(u => u.Roles).OrderBy(u => u.UserName).ToListAsync());

    [HttpGet]
    public async Task<IActionResult> Create()
    {
        await LoadRolesAsync();
        return View("Edit", new UserEditModel { Roles = { RoleNames.Editors } });
    }

    [HttpPost]
    public async Task<IActionResult> Create(UserEditModel model)
    {
        model.UserName = model.UserName?.Trim() ?? "";
        if (string.IsNullOrEmpty(model.Password))
            ModelState.AddModelError(nameof(model.Password), L["Password is required (min. 8 characters)."]);
        if (await _db.Users.AnyAsync(u => u.UserName == model.UserName))
            ModelState.AddModelError(nameof(model.UserName), L["This username is already taken."]);

        if (!ModelState.IsValid) { await LoadRolesAsync(); return View("Edit", model); }

        var user = new User { UserName = model.UserName, MustChangePassword = true };
        await ApplyAsync(user, model);
        user.PasswordHash = _passwords.Hash(user, model.Password!);
        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        Flash(L["User '{0}' created. They must change the password at first sign-in.", user.UserName]);
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var user = await _db.Users.AsNoTracking().Include(u => u.Roles).FirstOrDefaultAsync(u => u.Id == id);
        if (user is null) return NotFound();
        await LoadRolesAsync();
        return View(new UserEditModel
        {
            Id = user.Id, UserName = user.UserName, DisplayName = user.DisplayName, EmailAddress = user.EmailAddress,
            IsActive = user.IsActive, Bio = user.Bio, Roles = user.Roles.Select(r => r.Name).ToList()
        });
    }

    [HttpPost]
    public async Task<IActionResult> Edit(int id, UserEditModel model)
    {
        var user = await _db.Users.Include(u => u.Roles).FirstOrDefaultAsync(u => u.Id == id);
        if (user is null) return NotFound();

        // UserName và Password không sửa ở đây.
        ModelState.Remove(nameof(model.UserName));
        ModelState.Remove(nameof(model.Password));

        var isSelf = id == User.GetUserId();
        if (isSelf && (!model.IsActive || !model.Roles.Contains(RoleNames.Administrators)))
            ModelState.AddModelError("", L["You cannot deactivate yourself or remove your own administrator role."]);
        else if (!await StillHasAdminAsync(id, model))
            ModelState.AddModelError("", L["At least one active administrator is required."]);

        if (!ModelState.IsValid)
        {
            model.Id = id;
            model.UserName = user.UserName;
            await LoadRolesAsync();
            return View(model);
        }

        await ApplyAsync(user, model);
        await _db.SaveChangesAsync();
        Flash(L["User saved."]);
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> ResetPassword(int id, [Required(ErrorMessage = "The {0} field is required."), StringLength(100, MinimumLength = 8, ErrorMessage = "The field {0} must be a string with a minimum length of {2} and a maximum length of {1}.")] string newPassword)
    {
        if (!ModelState.IsValid)
        {
            FlashError(L["The new password must have at least 8 characters."]);
            return RedirectToAction(nameof(Edit), new { id });
        }

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == id);
        if (user is null) return NotFound();

        user.PasswordHash = _passwords.Hash(user, newPassword);
        user.MustChangePassword = id != User.GetUserId();
        user.FailedLoginCount = 0;
        user.LockoutEnd = null;
        await _db.SaveChangesAsync();

        Flash(L["Password reset. The user must choose a new password at next sign-in."]);
        return RedirectToAction(nameof(Edit), new { id });
    }

    [HttpPost]
    public async Task<IActionResult> Delete(int id)
    {
        if (id == User.GetUserId())
        {
            FlashError(L["You cannot delete your own account."]);
            return RedirectToAction(nameof(Index));
        }

        var user = await _db.Users.Include(u => u.Roles).FirstOrDefaultAsync(u => u.Id == id);
        if (user is null) return NotFound();

        if (await _db.Posts.AnyAsync(p => p.AuthorId == id))
        {
            FlashError(L["This user has posts. Deactivate the account instead of deleting it."]);
            return RedirectToAction(nameof(Index));
        }

        _db.Users.Remove(user);
        await _db.SaveChangesAsync();
        Flash(L["User deleted."]);
        return RedirectToAction(nameof(Index));
    }

    private async Task LoadRolesAsync()
        => ViewBag.AllRoles = await _db.Roles.AsNoTracking().OrderBy(r => r.Name).Select(r => r.Name).ToListAsync();

    private async Task<bool> StillHasAdminAsync(int editedId, UserEditModel model)
    {
        var editedStaysAdmin = model.IsActive && model.Roles.Contains(RoleNames.Administrators);
        if (editedStaysAdmin) return true;
        return await _db.Users.AnyAsync(u => u.Id != editedId && u.IsActive
                                             && u.Roles.Any(r => r.Name == RoleNames.Administrators));
    }

    private async Task ApplyAsync(User user, UserEditModel model)
    {
        user.DisplayName = model.DisplayName.NullIfEmpty();
        user.EmailAddress = model.EmailAddress.NullIfEmpty();
        user.Bio = model.Bio.NullIfEmpty();
        user.IsActive = model.IsActive;

        var names = model.Roles.Distinct().ToList();
        var roles = await _db.Roles.Where(r => names.Contains(r.Name)).ToListAsync();
        user.Roles.Clear();
        user.Roles.AddRange(roles);
    }
}
