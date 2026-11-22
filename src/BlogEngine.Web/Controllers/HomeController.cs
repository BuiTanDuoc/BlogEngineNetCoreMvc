using BlogEngine.Web.Data;
using BlogEngine.Web.Data.Entities;
using BlogEngine.Web.Infrastructure;
using BlogEngine.Web.Models;
using BlogEngine.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BlogEngine.Web.Controllers;

public class HomeController : LocalizedController
{
    private const int HomeLatestCount = 6;
    private const int HomeTopicCount = 6;
    private const int HeroCount = 3;
    private const int FeaturedCount = 3;

    private readonly BlogDbContext _db;
    private readonly ISettingsService _settings;

    public HomeController(BlogDbContext db, ISettingsService settings)
    {
        _db = db;
        _settings = settings;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index(int page = 1)
    {
        // Nếu có trang được đặt làm "front page" thì hiển thị nó ở trang chủ (giống BlogEngine gốc).
        // Trang front page chỉ dùng khi người xem được phép xem nó; nếu không thì rơi về danh sách bài như bình thường.
        var signedIn = User.IsSignedIn();
        var userId = signedIn ? User.GetUserId() : 0;
        var front = await _db.Pages.AsNoTracking()
            .FirstOrDefaultAsync(p => p.IsFrontPage && p.IsPublished && !p.IsDeleted
                                      && (p.Visibility == ContentVisibility.Public
                                          || (signedIn && p.Visibility == ContentVisibility.Private)
                                          || (signedIn && p.Visibility == ContentVisibility.Personal && p.AuthorId == userId)));
        if (front is not null)
            return View("~/Views/Pages/Details.cshtml", front);

        // Tương thích link phân trang cũ: /?page=2 -> danh sách bài viết.
        if (Request.Query.ContainsKey("page"))
            return await ListAsync(page);

        var visible = _db.Posts.AsNoTracking().Visible(User);

        var settings = await _settings.GetAsync();
        if (settings.IsBlk)
        {
            var ordered = visible.OrderByDescending(p => p.DateCreated);
            // Hero: bài Featured trước, thiếu thì bù bằng bài mới nhất.
            var hero = await visible.Where(p => p.IsFeatured)
                .OrderByDescending(p => p.DateCreated).Take(HeroCount).ToSummary().ToListAsync();
            if (hero.Count < HeroCount)
            {
                var heroIds = hero.Select(p => p.Id).ToList();
                hero.AddRange(await visible.Where(p => !heroIds.Contains(p.Id))
                    .OrderByDescending(p => p.DateCreated).Take(HeroCount - hero.Count).ToSummary().ToListAsync());
            }
            var blkTopics = await _db.Categories.AsNoTracking()
                .VisibleCategoryCards(User)
                .Where(c => c.Count > 0)
                .OrderByDescending(c => c.Count)
                .Take(HomeTopicCount)
                .ToListAsync();
            return View("Blk", new BlkHomeViewModel
            {
                Featured = hero,
                Categories = blkTopics,
                TotalPosts = await visible.CountAsync(),
                Posts = new PostListViewModel
                {
                    Posts = await PagedList<PostSummary>.CreateAsync(ordered.ToSummary(), 1, settings.PostsPerPage),
                    EmptyMessage = L["There are no posts yet."]
                }
            });
        }

        if (settings.IsIndigo)
        {
            var ordered = visible.OrderByDescending(p => p.DateCreated);
            return View("Indigo", new IndigoHomeViewModel
            {
                Featured = await ordered.Take(HeroCount).ToSummary().ToListAsync(),
                Posts = new PostListViewModel
                {
                    Posts = await PagedList<PostSummary>.CreateAsync(ordered.ToSummary(), 1, settings.PostsPerPage),
                    EmptyMessage = L["There are no posts yet."]
                }
            });
        }

        var featured = await visible.Where(p => p.IsFeatured)
            .OrderByDescending(p => p.DateCreated).Take(FeaturedCount).ToSummary().ToListAsync();
        var featuredIds = featured.Select(p => p.Id).ToList();
        // Bài đã nằm ở mục Featured thì không lặp lại ở mục Latest.
        var latest = await visible.Where(p => !featuredIds.Contains(p.Id))
            .OrderByDescending(p => p.DateCreated).Take(HomeLatestCount).ToSummary().ToListAsync();
        var topics = await _db.Categories.AsNoTracking()
            .VisibleCategoryCards(User)
            .Where(c => c.Count > 0)
            .OrderByDescending(c => c.Count)
            .Take(HomeTopicCount)
            .ToListAsync();

        return View(new HomeViewModel
        {
            Featured = featured,
            Latest = latest,
            Categories = topics,
            TotalPosts = await visible.CountAsync()
        });
    }

    [HttpGet("blog")]
    public Task<IActionResult> Blog(int page = 1) => ListAsync(page);

    private async Task<IActionResult> ListAsync(int page)
    {
        var settings = await _settings.GetAsync();
        var query = _db.Posts.AsNoTracking().Visible(User).OrderByDescending(p => p.DateCreated).ToSummary();

        var vm = new PostListViewModel
        {
            Posts = await PagedList<PostSummary>.CreateAsync(query, page, settings.PostsPerPage),
            Heading = L["All posts"],
            EmptyMessage = L["There are no posts yet."]
        };
        return View("~/Views/Shared/PostList.cshtml", vm);
    }
}
