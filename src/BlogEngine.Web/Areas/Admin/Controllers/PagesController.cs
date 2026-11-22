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

    /// <summary>
    /// Các trang người dùng hiện tại được phép thấy/sửa: mọi trang TRỪ trang Personal của người khác
    /// (Personal = chỉ người tạo mới thấy, kể cả admin). Mọi truy vấn theo id phải đi qua đây để trả 404 thay vì lộ trang.
    /// </summary>
    private IQueryable<Page> Accessible()
    {
        var uid = User.GetUserId();
        return _db.Pages.Where(p => p.Visibility != ContentVisibility.Personal || p.AuthorId == uid);
    }

    public async Task<IActionResult> Index(bool trash = false)
    {
        ViewBag.Trash = trash;
        var pages = await Accessible().AsNoTracking()
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

        var page = new Page { AuthorId = User.GetUserId() };
        await ApplyAsync(page, model);
        _db.Pages.Add(page);
        await _db.SaveChangesAsync();
        Flash(L["Page created."]);
        return RedirectToAction(nameof(Edit), new { id = page.Id });
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var page = await Accessible().AsNoTracking().FirstOrDefaultAsync(p => p.Id == id);
        if (page is null) return NotFound();

        await LoadParentsAsync(id);
        ViewBag.IsDeleted = page.IsDeleted;
        ViewBag.Slug = page.Slug;
        return View(new PageEditModel
        {
            Id = page.Id, Title = page.Title, Slug = page.Slug, Description = page.Description,
            Content = page.Content, Keywords = page.Keywords, IsPublished = page.IsPublished, Visibility = page.Visibility,
            IsFrontPage = page.IsFrontPage, ShowInList = page.ShowInList, ParentId = page.ParentId
        });
    }

    [HttpPost]
    public async Task<IActionResult> Edit(int id, PageEditModel model)
    {
        var page = await Accessible().FirstOrDefaultAsync(p => p.Id == id);
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
        var page = await Accessible().FirstOrDefaultAsync(p => p.Id == id);
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
        var page = await Accessible().FirstOrDefaultAsync(p => p.Id == id);
        if (page is null) return NotFound();
        page.IsDeleted = false;
        await _db.SaveChangesAsync();
        Flash(L["Page restored."]);
        return RedirectToAction(nameof(Index), new { trash = true });
    }

    [HttpPost]
    public async Task<IActionResult> Delete(int id)
    {
        var page = await Accessible().Include(p => p.Children).FirstOrDefaultAsync(p => p.Id == id);
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
        ViewBag.Parents = await Accessible().AsNoTracking()
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

        // Trang chủ (front page) thay thế trang chủ của cả blog nên phải công khai.
        if (model.IsFrontPage && model.Visibility != ContentVisibility.Public)
            ModelState.AddModelError(nameof(model.Visibility), L["The front page must be public."]);

        // Chỉ người tạo mới được đặt trang là Personal (trang cũ chưa có người tạo thì người đặt sẽ trở thành chủ).
        if (id is int pageId && model.Visibility == ContentVisibility.Personal)
        {
            var owner = await _db.Pages.AsNoTracking().Where(p => p.Id == pageId).Select(p => p.AuthorId).FirstOrDefaultAsync();
            if (owner is int ownerId && ownerId != User.GetUserId())
                ModelState.AddModelError(nameof(model.Visibility), L["Only the author can make a page personal."]);
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
        page.Visibility = model.Visibility;
        // Trang cũ chưa có người tạo: người đặt nó thành Personal trở thành chủ (nếu không sẽ không ai xem được).
        if (page.Visibility == ContentVisibility.Personal && page.AuthorId is null)
            page.AuthorId = User.GetUserId();
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
