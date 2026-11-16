using BlogEngine.Web.Areas.Admin.Models;
using BlogEngine.Web.Data;
using BlogEngine.Web.Data.Entities;
using BlogEngine.Web.Infrastructure;
using BlogEngine.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BlogEngine.Web.Areas.Admin.Controllers;

public class PagesController : AdminControllerBase
{
    private readonly BlogDbContext _db;
    public PagesController(BlogDbContext db) => _db = db;

    public async Task<IActionResult> Index(bool trash = false)
    {
        ViewBag.Trash = trash;
        var pages = await _db.Pages.AsNoTracking()
            .Include(p => p.Parent)
            .Where(p => p.IsDeleted == trash)
            .OrderBy(p => p.Title)
            .ToListAsync();
        return View(pages);
    }

    [HttpGet]
    public async Task<IActionResult> Create()
    {
        await LoadParentsAsync(null);
        return View("Edit", new PageEditModel());
    }

    [HttpPost]
    public async Task<IActionResult> Create(PageEditModel model)
    {
        if (!await ValidateAsync(model, null))
        {
            await LoadParentsAsync(null);
            return View("Edit", model);
        }

        var page = new Page();
        await ApplyAsync(page, model);
        _db.Pages.Add(page);
        await _db.SaveChangesAsync();
        Flash(L["Page created."]);
        return RedirectToAction(nameof(Edit), new { id = page.Id });
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var page = await _db.Pages.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id);
        if (page is null) return NotFound();

        await LoadParentsAsync(id);
        ViewBag.IsDeleted = page.IsDeleted;
        ViewBag.Slug = page.Slug;
        return View(new PageEditModel
        {
            Id = page.Id, Title = page.Title, Slug = page.Slug, Description = page.Description,
            Content = page.Content, Keywords = page.Keywords, IsPublished = page.IsPublished,
            IsFrontPage = page.IsFrontPage, ShowInList = page.ShowInList, ParentId = page.ParentId
        });
    }

    [HttpPost]
    public async Task<IActionResult> Edit(int id, PageEditModel model)
    {
        var page = await _db.Pages.FirstOrDefaultAsync(p => p.Id == id);
        if (page is null) return NotFound();

        if (!await ValidateAsync(model, id))
        {
            model.Id = id;
            await LoadParentsAsync(id);
            ViewBag.IsDeleted = page.IsDeleted;
            ViewBag.Slug = page.Slug;
            return View(model);
        }

        await ApplyAsync(page, model);
        await _db.SaveChangesAsync();
        Flash(L["Page saved."]);
        return RedirectToAction(nameof(Edit), new { id });
    }

    [HttpPost]
    public async Task<IActionResult> Trash(int id)
    {
        var page = await _db.Pages.FirstOrDefaultAsync(p => p.Id == id);
        if (page is null) return NotFound();
        page.IsDeleted = true;
        page.IsFrontPage = false;
        await _db.SaveChangesAsync();
        Flash(L["Page moved to trash."]);
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> Restore(int id)
    {
        var page = await _db.Pages.FirstOrDefaultAsync(p => p.Id == id);
        if (page is null) return NotFound();
        page.IsDeleted = false;
        await _db.SaveChangesAsync();
        Flash(L["Page restored."]);
        return RedirectToAction(nameof(Index), new { trash = true });
    }

    [HttpPost]
    public async Task<IActionResult> Delete(int id)
    {
        var page = await _db.Pages.Include(p => p.Children).FirstOrDefaultAsync(p => p.Id == id);
        if (page is null) return NotFound();
        if (!page.IsDeleted) { FlashError(L["Move the page to trash first."]); return RedirectToAction(nameof(Index)); }

        foreach (var child in page.Children) child.ParentId = null;
        _db.Pages.Remove(page);
        await _db.SaveChangesAsync();
        Flash(L["Page deleted permanently."]);
        return RedirectToAction(nameof(Index), new { trash = true });
    }

    private async Task LoadParentsAsync(int? excludeId)
    {
        ViewBag.Parents = await _db.Pages.AsNoTracking()
            .Where(p => !p.IsDeleted && p.Id != (excludeId ?? 0))
            .OrderBy(p => p.Title).ToListAsync();
    }

    private async Task<bool> ValidateAsync(PageEditModel model, int? id)
    {
        if (model.ParentId is int parentId && id is int self)
        {
            // Chặn vòng lặp cha-con: đi ngược chuỗi cha, không được gặp chính trang này.
            int? cursor = parentId;
            var guard = 0;
            while (cursor is int c && guard++ < 50)
            {
                if (c == self)
                {
                    ModelState.AddModelError(nameof(model.ParentId), L["A page cannot be its own ancestor."]);
                    break;
                }
                cursor = await _db.Pages.AsNoTracking().Where(p => p.Id == c).Select(p => p.ParentId).FirstOrDefaultAsync();
            }
        }
        return ModelState.IsValid;
    }

    private async Task ApplyAsync(Page page, PageEditModel model)
    {
        var currentId = page.Id;
        page.Title = model.Title.Trim();
        page.Description = model.Description.NullIfEmpty();
        page.Content = model.Content;
        page.Keywords = model.Keywords.NullIfEmpty();
        page.IsPublished = model.IsPublished;
        page.ShowInList = model.ShowInList;
        page.ParentId = model.ParentId;
        page.DateModified = DateTime.UtcNow;

        var baseSlug = SlugHelper.Slugify(model.Slug.NullIfEmpty() ?? page.Title, 200);
        page.Slug = await SlugHelper.EnsureUniqueAsync(baseSlug,
            s => _db.Pages.AnyAsync(p => p.Slug == s && p.Id != currentId), "page");

        page.IsFrontPage = model.IsFrontPage && model.IsPublished;
        if (page.IsFrontPage)
        {
            var others = await _db.Pages.Where(p => p.IsFrontPage && p.Id != currentId).ToListAsync();
            foreach (var o in others) o.IsFrontPage = false;
        }
    }
}
