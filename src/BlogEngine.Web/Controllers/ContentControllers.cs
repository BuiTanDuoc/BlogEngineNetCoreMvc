using BlogEngine.Web.Data;
using BlogEngine.Web.Data.Entities;
using BlogEngine.Web.Infrastructure;
using BlogEngine.Web.Models;
using BlogEngine.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BlogEngine.Web.Controllers;

public class PagesController : LocalizedController
{
    private readonly BlogDbContext _db;
    public PagesController(BlogDbContext db) => _db = db;

    [HttpGet("page/{slug}")]
    public async Task<IActionResult> Details(string slug)
    {
        var page = await _db.Pages.AsNoTracking().FirstOrDefaultAsync(p => p.Slug == slug && !p.IsDeleted);
        if (page is null || (!page.IsPublished && !User.IsStaff())) return NotFound();

        ViewBag.Children = await _db.Pages.AsNoTracking()
            .Where(p => p.ParentId == page.Id && p.IsPublished && !p.IsDeleted && p.ShowInList)
            .OrderBy(p => p.Title)
            .ToListAsync();

        return View(page);
    }
}

public class CategoriesController : LocalizedController
{
    private readonly BlogDbContext _db;
    private readonly ISettingsService _settings;

    public CategoriesController(BlogDbContext db, ISettingsService settings)
    {
        _db = db;
        _settings = settings;
    }

    [HttpGet("category/{slug}")]
    public async Task<IActionResult> Details(string slug, int page = 1)
    {
        // Tìm theo slug; fallback theo tên để link cũ dạng /category/Tên-Danh-Mục vẫn chạy.
        var category = await _db.Categories.AsNoTracking().FirstOrDefaultAsync(c => c.Slug == slug)
                       ?? await _db.Categories.AsNoTracking().FirstOrDefaultAsync(c => c.Name == slug);
        if (category is null) return NotFound();

        var settings = await _settings.GetAsync();
        var query = _db.Posts.AsNoTracking().Visible()
            .Where(p => p.Categories.Any(c => c.Id == category.Id))
            .OrderByDescending(p => p.DateCreated)
            .ToSummary();

        return View("~/Views/Shared/PostList.cshtml", new PostListViewModel
        {
            Posts = await PagedList<PostSummary>.CreateAsync(query, page, settings.PostsPerPage),
            Heading = L["Category: {0}", category.Name],
            SubHeading = category.Description,
            EmptyMessage = L["No posts in this category yet."]
        });
    }
}

public class TagsController : LocalizedController
{
    private readonly BlogDbContext _db;
    private readonly ISettingsService _settings;

    public TagsController(BlogDbContext db, ISettingsService settings)
    {
        _db = db;
        _settings = settings;
    }

    [HttpGet("tag/{slug}")]
    public async Task<IActionResult> Details(string slug, int page = 1)
    {
        var tag = await _db.Tags.AsNoTracking().FirstOrDefaultAsync(t => t.Slug == slug)
                  ?? await _db.Tags.AsNoTracking().FirstOrDefaultAsync(t => t.Name == slug);
        if (tag is null) return NotFound();

        var settings = await _settings.GetAsync();
        var query = _db.Posts.AsNoTracking().Visible()
            .Where(p => p.Tags.Any(t => t.Id == tag.Id))
            .OrderByDescending(p => p.DateCreated)
            .ToSummary();

        return View("~/Views/Shared/PostList.cshtml", new PostListViewModel
        {
            Posts = await PagedList<PostSummary>.CreateAsync(query, page, settings.PostsPerPage),
            Heading = L["Tag: {0}", tag.Name],
            EmptyMessage = L["No posts with this tag yet."]
        });
    }
}

public class AuthorController : LocalizedController
{
    private readonly BlogDbContext _db;
    private readonly ISettingsService _settings;

    public AuthorController(BlogDbContext db, ISettingsService settings)
    {
        _db = db;
        _settings = settings;
    }

    [HttpGet("author/{username}")]
    public async Task<IActionResult> Details(string username, int page = 1)
    {
        var author = await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.UserName == username && u.IsActive);
        if (author is null) return NotFound();

        var settings = await _settings.GetAsync();
        var query = _db.Posts.AsNoTracking().Visible()
            .Where(p => p.AuthorId == author.Id)
            .OrderByDescending(p => p.DateCreated)
            .ToSummary();

        return View("~/Views/Shared/PostList.cshtml", new PostListViewModel
        {
            Posts = await PagedList<PostSummary>.CreateAsync(query, page, settings.PostsPerPage),
            Heading = L["Posts by {0}", author.Name],
            SubHeading = author.Bio,
            EmptyMessage = L["This author has not published anything yet."]
        });
    }
}

public class ArchiveController : LocalizedController
{
    private readonly BlogDbContext _db;
    private readonly ISettingsService _settings;

    public ArchiveController(BlogDbContext db, ISettingsService settings)
    {
        _db = db;
        _settings = settings;
    }

    [HttpGet("archive")]
    public async Task<IActionResult> Index()
    {
        var settings = await _settings.GetAsync();
        var entries = await _db.Posts.AsNoTracking().Visible()
            .OrderByDescending(p => p.DateCreated)
            .Select(p => new ArchiveViewModel.Entry
            {
                Id = p.Id,
                Title = p.Title,
                Slug = p.Slug,
                DateCreated = p.DateCreated,
                CommentCount = p.Comments.Count(c => c.IsApproved && !c.IsSpam && !c.IsDeleted)
            })
            .ToListAsync();

        var groups = entries
            .GroupBy(e =>
            {
                var local = e.DateCreated.ToBlogTime(settings);
                return (local.Year, local.Month);
            })
            .ToList();

        return View(new ArchiveViewModel { Groups = groups });
    }

    [HttpGet("{year:int:range(1990,2100)}/{month:int:range(1,12)}")]
    public async Task<IActionResult> Month(int year, int month, int page = 1)
    {
        var settings = await _settings.GetAsync();
        var (start, end) = DateExtensions.MonthRangeUtc(year, month, settings);
        var title = new DateTime(year, month, 1).ToString("MMMM yyyy", System.Globalization.CultureInfo.CurrentCulture);
        return await ListRangeAsync(start, end, L["Archive: {0}", title], page, settings);
    }

    [HttpGet("{year:int:range(1990,2100)}")]
    public async Task<IActionResult> Year(int year, int page = 1)
    {
        var settings = await _settings.GetAsync();
        var (start, end) = DateExtensions.YearRangeUtc(year, settings);
        return await ListRangeAsync(start, end, L["Archive: {0}", year], page, settings);
    }

    private async Task<IActionResult> ListRangeAsync(DateTime start, DateTime end, string heading, int page, BlogSettings settings)
    {
        var query = _db.Posts.AsNoTracking().Visible()
            .Where(p => p.DateCreated >= start && p.DateCreated < end)
            .OrderByDescending(p => p.DateCreated)
            .ToSummary();

        return View("~/Views/Shared/PostList.cshtml", new PostListViewModel
        {
            Posts = await PagedList<PostSummary>.CreateAsync(query, page, settings.PostsPerPage),
            Heading = heading,
            EmptyMessage = L["No posts in this period."]
        });
    }
}

public class SearchController : LocalizedController
{
    private readonly BlogDbContext _db;
    private readonly ISettingsService _settings;

    public SearchController(BlogDbContext db, ISettingsService settings)
    {
        _db = db;
        _settings = settings;
    }

    [HttpGet("search")]
    public async Task<IActionResult> Index(string? q, int page = 1)
    {
        var settings = await _settings.GetAsync();
        var term = (q ?? "").Trim();
        ViewData["SearchTerm"] = term;

        if (term.Length < 2)
        {
            return View("~/Views/Shared/PostList.cshtml", new PostListViewModel
            {
                Posts = new PagedList<PostSummary> { Page = 1, PageSize = settings.PostsPerPage },
                Heading = L["Search"],
                EmptyMessage = L["Enter at least 2 characters to search."]
            });
        }

        var query = _db.Posts.AsNoTracking().Visible()
            .Where(p => p.Title.Contains(term)
                        || (p.Description != null && p.Description.Contains(term))
                        || p.Content.Contains(term)
                        || p.Tags.Any(t => t.Name.Contains(term)))
            .OrderByDescending(p => p.DateCreated)
            .ToSummary();

        var results = await PagedList<PostSummary>.CreateAsync(query, page, settings.PostsPerPage);
        return View("~/Views/Shared/PostList.cshtml", new PostListViewModel
        {
            Posts = results,
            Heading = L["Search results for “{0}”", term],
            SubHeading = L.Plural(results.TotalCount, "{0} result", "{0} results"),
            EmptyMessage = L["Nothing found. Try different keywords."]
        });
    }
}
