using BlogEngine.Web.Areas.Admin.Models;
using BlogEngine.Web.Data;
using BlogEngine.Web.Data.Entities;
using BlogEngine.Web.Infrastructure;
using BlogEngine.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BlogEngine.Web.Areas.Admin.Controllers;

public class PostsController : AdminControllerBase
{
    private const int PageSize = 20;
    private readonly BlogDbContext _db;
    private readonly ISettingsService _settings;

    public PostsController(BlogDbContext db, ISettingsService settings)
    {
        _db = db;
        _settings = settings;
    }

    public async Task<IActionResult> Index(string status = "all", string? q = null, int page = 1)
    {
        var baseQuery = OwnPosts();

        var vm = new Models.PostListViewModel
        {
            Posts = await PagedList<Post>.CreateAsync(Filter(baseQuery, status, q)
                .Include(p => p.Author).Include(p => p.Categories)
                .OrderByDescending(p => p.DateCreated), page, PageSize),
            Status = status,
            Query = q,
            PublishedCount = await baseQuery.CountAsync(p => p.IsPublished && !p.IsDeleted),
            DraftCount = await baseQuery.CountAsync(p => !p.IsPublished && !p.IsDeleted),
            TrashCount = await baseQuery.CountAsync(p => p.IsDeleted)
        };
        return View(vm);
    }

    [HttpGet]
    public async Task<IActionResult> Create()
    {
        await LoadLookupsAsync();
        return View("Edit", new PostEditModel { IsPublished = false });
    }

    [HttpPost]
    public async Task<IActionResult> Create(PostEditModel model)
    {
        if (!ModelState.IsValid)
        {
            await LoadLookupsAsync();
            return View("Edit", model);
        }

        var post = new Post { AuthorId = User.GetUserId() };
        await ApplyAsync(post, model);
        _db.Posts.Add(post);
        await _db.SaveChangesAsync();

        Flash(L["Post created."]);
        return RedirectToAction(nameof(Edit), new { id = post.Id });
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var post = await _db.Posts.AsNoTracking()
            .Include(p => p.Categories).Include(p => p.Tags)
            .FirstOrDefaultAsync(p => p.Id == id);
        if (post is null || IsHiddenPersonal(post)) return NotFound();
        if (!CanModify(post)) return Forbid();

        var settings = await _settings.GetAsync();
        await LoadLookupsAsync();
        ViewBag.Slug = post.Slug;
        ViewBag.IsDeleted = post.IsDeleted;

        return View(new PostEditModel
        {
            Id = post.Id,
            Title = post.Title,
            Slug = post.Slug,
            Description = post.Description,
            Content = post.Content,
            CategoryIds = post.Categories.Select(c => c.Id).ToList(),
            Tags = string.Join(", ", post.Tags.Select(t => t.Name)),
            IsPublished = post.IsPublished,
            Visibility = post.Visibility,
            IsCommentEnabled = post.IsCommentEnabled,
            IsFeatured = post.IsFeatured,
            DateCreated = post.DateCreated.ToBlogTime(settings)
        });
    }

    [HttpPost]
    public async Task<IActionResult> Edit(int id, PostEditModel model)
    {
        var post = await _db.Posts.Include(p => p.Categories).Include(p => p.Tags).FirstOrDefaultAsync(p => p.Id == id);
        if (post is null || IsHiddenPersonal(post)) return NotFound();
        if (!CanModify(post)) return Forbid();

        // Administrator sửa bài của người khác thì không được biến nó thành Personal (sẽ làm bài biến mất với mọi người trừ tác giả).
        if (model.Visibility == ContentVisibility.Personal && post.AuthorId != User.GetUserId())
            ModelState.AddModelError(nameof(model.Visibility), L["Only the author can make a post personal."]);

        if (!ModelState.IsValid)
        {
            model.Id = id;
            await LoadLookupsAsync();
            ViewBag.Slug = post.Slug;
            ViewBag.IsDeleted = post.IsDeleted;
            return View(model);
        }

        await ApplyAsync(post, model);
        await _db.SaveChangesAsync();

        Flash(L["Post saved."]);
        return RedirectToAction(nameof(Edit), new { id });
    }

    /// <summary>Chuyển vào thùng rác (có thể khôi phục).</summary>
    [HttpPost]
    public async Task<IActionResult> Trash(int id)
    {
        var post = await _db.Posts.FirstOrDefaultAsync(p => p.Id == id);
        if (post is null || IsHiddenPersonal(post)) return NotFound();
        if (!CanModify(post)) return Forbid();

        post.IsDeleted = true;
        post.DateModified = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        Flash(L["Post moved to trash."]);
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> Restore(int id)
    {
        var post = await _db.Posts.FirstOrDefaultAsync(p => p.Id == id);
        if (post is null || IsHiddenPersonal(post)) return NotFound();
        if (!CanModify(post)) return Forbid();

        post.IsDeleted = false;
        post.DateModified = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        Flash(L["Post restored."]);
        return RedirectToAction(nameof(Index), new { status = "trash" });
    }

    /// <summary>Xoá vĩnh viễn một bài đang nằm trong thùng rác (kèm comment).</summary>
    [HttpPost]
    public async Task<IActionResult> Delete(int id)
    {
        var post = await _db.Posts.Include(p => p.Comments).FirstOrDefaultAsync(p => p.Id == id);
        if (post is null || IsHiddenPersonal(post)) return NotFound();
        if (!CanModify(post)) return Forbid();
        if (!post.IsDeleted)
        {
            FlashError(L["Move the post to trash first."]);
            return RedirectToAction(nameof(Index));
        }

        // Comment trả lời comment khác dùng FK Restrict -> gỡ liên kết cha trước khi xoá.
        foreach (var c in post.Comments) c.ParentId = null;
        await _db.SaveChangesAsync();

        _db.Posts.Remove(post);
        await _db.SaveChangesAsync();
        Flash(L["Post deleted permanently."]);
        return RedirectToAction(nameof(Index), new { status = "trash" });
    }

    // ---------- helpers ----------

    private IQueryable<Post> OwnPosts()
    {
        var query = _db.Posts.AsNoTracking();
        var uid = User.GetUserId();
        // Editors chỉ thấy bài của mình; Administrators thấy tất cả TRỪ bài Personal của người khác
        // (Personal = chỉ người viết mới thấy, kể cả admin).
        return User.IsAdministrator()
            ? query.Where(p => p.Visibility != ContentVisibility.Personal || p.AuthorId == uid)
            : query.Where(p => p.AuthorId == uid);
    }

    private static IQueryable<Post> Filter(IQueryable<Post> query, string status, string? q)
    {
        query = status switch
        {
            "published" => query.Where(p => p.IsPublished && !p.IsDeleted),
            "draft" => query.Where(p => !p.IsPublished && !p.IsDeleted),
            "trash" => query.Where(p => p.IsDeleted),
            _ => query.Where(p => !p.IsDeleted)
        };
        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim();
            query = query.Where(p => p.Title.Contains(term) || p.Content.Contains(term));
        }
        return query;
    }

    private bool CanModify(Post post) => User.IsAdministrator() || post.AuthorId == User.GetUserId();

    /// <summary>Bài Personal của người khác: coi như không tồn tại (404) để không lộ cả sự tồn tại lẫn tiêu đề.</summary>
    private bool IsHiddenPersonal(Post post) =>
        post.Visibility == ContentVisibility.Personal && post.AuthorId != User.GetUserId();

    private async Task LoadLookupsAsync()
    {
        ViewBag.Categories = await _db.Categories.AsNoTracking().OrderBy(c => c.Name).ToListAsync();
        ViewBag.Settings = await _settings.GetAsync();
    }

    private async Task ApplyAsync(Post post, PostEditModel model)
    {
        var settings = await _settings.GetAsync();
        var now = DateTime.UtcNow;
        var currentId = post.Id;

        post.Title = model.Title.Trim();
        post.Description = model.Description.NullIfEmpty();
        post.Content = model.Content;
        post.IsPublished = model.IsPublished;
        post.Visibility = model.Visibility;
        post.IsCommentEnabled = model.IsCommentEnabled;
        post.IsFeatured = model.IsFeatured;
        post.DateModified = now;

        var baseSlug = SlugHelper.Slugify(model.Slug.NullIfEmpty() ?? post.Title, 200);
        post.Slug = await SlugHelper.EnsureUniqueAsync(baseSlug,
            s => _db.Posts.AnyAsync(p => p.Slug == s && p.Id != currentId), "post");

        if (model.DateCreated is { } local)
        {
            try
            {
                post.DateCreated = TimeZoneInfo.ConvertTimeToUtc(
                    DateTime.SpecifyKind(local, DateTimeKind.Unspecified), settings.GetTimeZone());
            }
            catch (ArgumentException)
            {
                post.DateCreated = DateTime.SpecifyKind(local, DateTimeKind.Utc);
            }
        }
        else if (currentId == 0)
        {
            post.DateCreated = now;
        }

        // Categories
        post.Categories.Clear();
        if (model.CategoryIds.Count > 0)
        {
            var ids = model.CategoryIds.Distinct().ToList();
            var categories = await _db.Categories.Where(c => ids.Contains(c.Id)).ToListAsync();
            post.Categories.AddRange(categories);
        }

        // Tags
        post.Tags.Clear();
        var names = (model.Tags ?? "")
            .Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(t => t.Length > 50 ? t[..50] : t)
            .Where(t => t.Length > 0)
            .DistinctBy(t => SlugHelper.Slugify(t, 80))
            .Take(30)
            .ToList();

        foreach (var name in names)
        {
            var slug = SlugHelper.Slugify(name, 80);
            if (slug.Length == 0) continue;
            var tag = await _db.Tags.FirstOrDefaultAsync(t => t.Slug == slug)
                      ?? _db.Tags.Local.FirstOrDefault(t => t.Slug == slug);
            if (tag is null)
            {
                tag = new Tag { Name = name, Slug = slug };
                _db.Tags.Add(tag);
            }
            if (!post.Tags.Contains(tag)) post.Tags.Add(tag);
        }
    }
}
