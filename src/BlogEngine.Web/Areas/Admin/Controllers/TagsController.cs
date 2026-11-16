using BlogEngine.Web.Data;
using BlogEngine.Web.Data.Entities;
using BlogEngine.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BlogEngine.Web.Areas.Admin.Controllers;

public class TagsController : AdminControllerBase
{
    private readonly BlogDbContext _db;
    public TagsController(BlogDbContext db) => _db = db;

    public async Task<IActionResult> Index()
    {
        var tags = await _db.Tags.AsNoTracking()
            .Select(t => new { t.Id, t.Name, t.Slug, Count = t.Posts.Count })
            .OrderBy(t => t.Name)
            .ToListAsync();
        return View(tags.Select(t => (t.Id, t.Name, t.Slug, t.Count)).ToList());
    }

    [HttpPost]
    public async Task<IActionResult> Rename(int id, string name)
    {
        name = (name ?? "").Trim();
        var slug = SlugHelper.Slugify(name, 80);
        if (name.Length == 0 || name.Length > 50 || slug.Length == 0)
        {
            FlashError(L["Tag name must be 1-50 characters."]);
            return RedirectToAction(nameof(Index));
        }

        var tag = await _db.Tags.FirstOrDefaultAsync(t => t.Id == id);
        if (tag is null) return NotFound();

        if (await _db.Tags.AnyAsync(t => t.Slug == slug && t.Id != id))
        {
            FlashError(L["Another tag already uses that name."]);
            return RedirectToAction(nameof(Index));
        }

        tag.Name = name;
        tag.Slug = slug;
        await _db.SaveChangesAsync();
        Flash(L["Tag renamed."]);
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> Delete(int id)
    {
        var tag = await _db.Tags.FirstOrDefaultAsync(t => t.Id == id);
        if (tag is null) return NotFound();
        _db.Tags.Remove(tag);
        await _db.SaveChangesAsync();
        Flash(L["Tag deleted."]);
        return RedirectToAction(nameof(Index));
    }
}
