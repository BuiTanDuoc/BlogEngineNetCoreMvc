using BlogEngine.Web.Data;
using BlogEngine.Web.Data.Entities;
using BlogEngine.Web.Infrastructure;
using BlogEngine.Web.Models;
using BlogEngine.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BlogEngine.Web.ViewComponents;

/// <summary>Thay thế các widget của BlogEngine (Categories, Recent posts/comments, Tag cloud, Month list, Blogroll).</summary>
public class SidebarViewComponent : ViewComponent
{
    private readonly BlogDbContext _db;
    private readonly ISettingsService _settings;

    public SidebarViewComponent(BlogDbContext db, ISettingsService settings)
    {
        _db = db;
        _settings = settings;
    }

    public async Task<IViewComponentResult> InvokeAsync()
    {
        var settings = await _settings.GetAsync();
        var now = DateTime.UtcNow;
        var viewer = UserClaimsPrincipal;
        var signedIn = viewer.IsSignedIn();
        var userId = signedIn ? viewer.GetUserId() : 0;

        var categoryRows = await _db.Categories.AsNoTracking()
            .VisibleCategoryCards(viewer)
            .Where(c => c.Count > 0)
            .OrderBy(c => c.Name)
            .ToListAsync();

        var recentPosts = await _db.Posts.AsNoTracking().Visible(viewer)
            .OrderByDescending(p => p.DateCreated)
            .Take(settings.NumberOfRecentPosts)
            .Select(p => new { p.Title, p.Slug })
            .ToListAsync();

        var recentComments = await _db.Comments.AsNoTracking()
            .Where(c => c.IsApproved && !c.IsSpam && !c.IsDeleted
                        && c.Post!.IsPublished && !c.Post.IsDeleted && c.Post.DateCreated <= now
                        && (c.Post.Visibility == ContentVisibility.Public
                            || (signedIn && c.Post.Visibility == ContentVisibility.Private)
                            || (signedIn && c.Post.Visibility == ContentVisibility.Personal && c.Post.AuthorId == userId)))
            .OrderByDescending(c => c.Date)
            .Take(settings.NumberOfRecentComments)
            .Select(c => new { c.Id, c.Author, c.Content, PostTitle = c.Post!.Title, PostSlug = c.Post.Slug })
            .ToListAsync();

        var tags = await _db.Tags.AsNoTracking()
            .Select(t => new
            {
                t.Name,
                t.Slug,
                Count = t.Posts.Count(p => p.IsPublished && !p.IsDeleted && p.DateCreated <= now
                    && (p.Visibility == ContentVisibility.Public
                        || (signedIn && p.Visibility == ContentVisibility.Private)
                        || (signedIn && p.Visibility == ContentVisibility.Personal && p.AuthorId == userId)))
            })
            .Where(x => x.Count > 0)
            .OrderByDescending(x => x.Count)
            .Take(30)
            .ToListAsync();

        var dates = await _db.Posts.AsNoTracking().Visible(viewer)
            .Select(p => p.DateCreated)
            .ToListAsync();
        var months = dates
            .Select(d => d.ToBlogTime(settings))
            .GroupBy(d => (d.Year, d.Month))
            .OrderByDescending(g => g.Key.Year).ThenByDescending(g => g.Key.Month)
            .Take(12)
            .Select(g => (g.Key.Year, g.Key.Month, g.Count()))
            .ToList();

        var blogRoll = await _db.BlogRollItems.AsNoTracking().OrderBy(b => b.SortIndex).ThenBy(b => b.Title).ToListAsync();

        var vm = new SidebarViewModel
        {
            Categories = categoryRows.Select(c => new Category { Id = c.Id, Name = c.Name, Slug = c.Slug }).ToList(),
            CategoryCounts = categoryRows.ToDictionary(c => c.Id, c => c.Count),
            RecentPosts = recentPosts.Select(p => (p.Title, p.Slug)).ToList(),
            RecentComments = recentComments
                .Select(c => (c.Author, c.PostTitle, c.PostSlug, c.Id, c.Content.Excerpt(60)))
                .ToList(),
            Tags = tags.OrderBy(t => t.Name).Select(t => (t.Name, t.Slug, t.Count)).ToList(),
            Months = months,
            BlogRoll = blogRoll
        };
        return View(vm);
    }
}
