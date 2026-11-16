using BlogEngine.Web.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BlogEngine.Web.ViewComponents;

/// <summary>Menu trang tĩnh (tương đương PageMenu của BlogEngine).</summary>
public class NavViewComponent : ViewComponent
{
    private readonly BlogDbContext _db;
    public NavViewComponent(BlogDbContext db) => _db = db;

    public async Task<IViewComponentResult> InvokeAsync()
    {
        var pages = await _db.Pages.AsNoTracking()
            .Where(p => p.IsPublished && !p.IsDeleted && p.ShowInList && p.ParentId == null && !p.IsFrontPage)
            .OrderBy(p => p.Title)
            .Select(p => new Data.Entities.Page { Title = p.Title, Slug = p.Slug })
            .ToListAsync();
        return View(pages);
    }
}
