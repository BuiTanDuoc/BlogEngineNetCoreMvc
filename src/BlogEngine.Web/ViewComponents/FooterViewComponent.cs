using BlogEngine.Web.Data;
using BlogEngine.Web.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BlogEngine.Web.ViewComponents;

/// <summary>Các cột liên kết ở chân trang (thay thế widget sidebar cũ): chủ đề, bài mới, blogroll.</summary>
public class FooterViewComponent : ViewComponent
{
    private readonly BlogDbContext _db;
    public FooterViewComponent(BlogDbContext db) => _db = db;

    public async Task<IViewComponentResult> InvokeAsync()
    {
        var now = DateTime.UtcNow;

        var categories = await _db.Categories.AsNoTracking()
            .Select(c => new CategoryCard
            {
                Name = c.Name,
                Slug = c.Slug,
                Count = c.Posts.Count(p => p.IsPublished && !p.IsDeleted && p.DateCreated <= now)
            })
            .Where(c => c.Count > 0)
            .OrderByDescending(c => c.Count)
            .Take(8)
            .ToListAsync();

        var recent = await _db.Posts.AsNoTracking().Visible()
            .OrderByDescending(p => p.DateCreated)
            .Take(5)
            .Select(p => new { p.Title, p.Slug })
            .ToListAsync();

        var blogRoll = await _db.BlogRollItems.AsNoTracking()
            .OrderBy(b => b.SortIndex).ThenBy(b => b.Title).Take(8).ToListAsync();

        return View(new FooterViewModel
        {
            Categories = categories,
            RecentPosts = recent.Select(p => (p.Title, p.Slug)).ToList(),
            BlogRoll = blogRoll
        });
    }
}
