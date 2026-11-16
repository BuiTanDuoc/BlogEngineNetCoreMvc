using BlogEngine.Web.Areas.Admin.Models;
using BlogEngine.Web.Data;
using BlogEngine.Web.Data.Entities;
using BlogEngine.Web.Infrastructure;
using BlogEngine.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BlogEngine.Web.Areas.Admin.Controllers;

public class CategoriesController : AdminControllerBase
{
    private readonly BlogDbContext _db;
    public CategoriesController(BlogDbContext db) => _db = db;

    public async Task<IActionResult> Index()
    {
        ViewBag.Counts = await _db.Categories.AsNoTracking()
            .Select(c => new { c.Id, Count = c.Posts.Count(p => !p.IsDeleted) })
            .ToDictionaryAsync(x => x.Id, x => x.Count);
        var categories = await _db.Categories.AsNoTracking().Include(c => c.Parent).OrderBy(c => c.Name).ToListAsync();
        return View(categories);
    }

    [HttpGet]
    public async Task<IActionResult> Create()
    {
        await LoadParentsAsync(null);
        return View("Edit", new CategoryEditModel());
    }

    [HttpPost]
    public async Task<IActionResult> Create(CategoryEditModel model)
    {
        if (!ModelState.IsValid) { await LoadParentsAsync(null); return View("Edit", model); }

        var category = new Category();
        await ApplyAsync(category, model);
        _db.Categories.Add(category);
        await _db.SaveChangesAsync();
        Flash(L["Category created."]);
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var c = await _db.Categories.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        if (c is null) return NotFound();
        await LoadParentsAsync(id);
        return View(new CategoryEditModel { Id = c.Id, Name = c.Name, Slug = c.Slug, Description = c.Description, ParentId = c.ParentId });
    }

    [HttpPost]
    public async Task<IActionResult> Edit(int id, CategoryEditModel model)
    {
        var category = await _db.Categories.FirstOrDefaultAsync(x => x.Id == id);
        if (category is null) return NotFound();

        if (model.ParentId == id) ModelState.AddModelError(nameof(model.ParentId), L["A category cannot be its own parent."]);
        else if (model.ParentId is int pid && await IsDescendantAsync(pid, id))
            ModelState.AddModelError(nameof(model.ParentId), L["The parent cannot be a child of this category."]);

        if (!ModelState.IsValid) { model.Id = id; await LoadParentsAsync(id); return View(model); }

        await ApplyAsync(category, model);
        await _db.SaveChangesAsync();
        Flash(L["Category saved."]);
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> Delete(int id)
    {
        var category = await _db.Categories.Include(c => c.Children).FirstOrDefaultAsync(c => c.Id == id);
        if (category is null) return NotFound();

        foreach (var child in category.Children) child.ParentId = category.ParentId;
        _db.Categories.Remove(category);
        await _db.SaveChangesAsync();
        Flash(L["Category deleted. Posts were kept."]);
        return RedirectToAction(nameof(Index));
    }

    private async Task<bool> IsDescendantAsync(int candidateId, int ancestorId)
    {
        int? cursor = candidateId;
        var guard = 0;
        while (cursor is int c && guard++ < 50)
        {
            if (c == ancestorId) return true;
            cursor = await _db.Categories.AsNoTracking().Where(x => x.Id == c).Select(x => x.ParentId).FirstOrDefaultAsync();
        }
        return false;
    }

    private async Task LoadParentsAsync(int? excludeId)
        => ViewBag.Parents = await _db.Categories.AsNoTracking()
            .Where(c => c.Id != (excludeId ?? 0)).OrderBy(c => c.Name).ToListAsync();

    private async Task ApplyAsync(Category category, CategoryEditModel model)
    {
        var currentId = category.Id;
        category.Name = model.Name.Trim();
        category.Description = model.Description.NullIfEmpty();
        category.ParentId = model.ParentId;

        var baseSlug = SlugHelper.Slugify(model.Slug.NullIfEmpty() ?? category.Name, 100);
        category.Slug = await SlugHelper.EnsureUniqueAsync(baseSlug,
            s => _db.Categories.AnyAsync(c => c.Slug == s && c.Id != currentId), "category");
    }
}
