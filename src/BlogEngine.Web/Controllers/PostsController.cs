using BlogEngine.Web.Data;
using BlogEngine.Web.Data.Entities;
using BlogEngine.Web.Infrastructure;
using BlogEngine.Web.Models;
using BlogEngine.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace BlogEngine.Web.Controllers;

public class PostsController : LocalizedController
{
    private readonly BlogDbContext _db;
    private readonly ISettingsService _settings;
    private readonly IEmailService _email;

    public PostsController(BlogDbContext db, ISettingsService settings, IEmailService email)
    {
        _db = db;
        _settings = settings;
        _email = email;
    }

    [HttpGet("post/{slug}")]
    public async Task<IActionResult> Details(string slug)
    {
        var (post, denied) = await ResolvePostAsync(slug, challengeAnonymous: true);
        if (post is null) return denied!;

        return View(await BuildViewModelAsync(post, new CommentInput()));
    }

    [HttpPost("post/{slug}/comment")]
    [EnableRateLimiting("forms")]
    public async Task<IActionResult> Comment(string slug, CommentInput input)
    {
        // POST không "challenge" sang trang đăng nhập (ReturnUrl sẽ trỏ vào URL POST): người không xem được bài thì cũng không có form để gửi.
        var (post, denied) = await ResolvePostAsync(slug, challengeAnonymous: false);
        if (post is null) return denied!;

        var settings = await _settings.GetAsync();
        if (!CanComment(post, settings)) return BadRequest(L["Comments are closed for this post."].Value);

        // Honeypot: giả vờ thành công để bot không biết bị chặn.
        if (!string.IsNullOrEmpty(input.Website2))
            return RedirectToPost(slug);

        if (!ModelState.IsValid)
        {
            Response.StatusCode = StatusCodes.Status400BadRequest;
            return View(nameof(Details), await BuildViewModelAsync(post, input));
        }

        if (input.ParentId is int parentId &&
            !await _db.Comments.AnyAsync(c => c.Id == parentId && c.PostId == post.Id && c.IsApproved && !c.IsSpam && !c.IsDeleted))
        {
            input.ParentId = null;
        }

        var isStaff = User.IsStaff();
        var comment = new Comment
        {
            PostId = post.Id,
            ParentId = input.ParentId,
            Author = input.Name.Trim(),
            Email = input.Email.Trim(),
            Website = input.Website.ToSafeHttpUrl(),
            Content = input.Content.Trim(),
            Ip = HttpContext.Connection.RemoteIpAddress?.ToString(),
            Date = DateTime.UtcNow,
            IsApproved = !settings.EnableCommentsModeration || isStaff
        };
        _db.Comments.Add(comment);
        await _db.SaveChangesAsync();

        // Bài Personal chỉ người viết được biết: không gửi nội dung/tiêu đề ra email của blog.
        if (settings.SendMailOnComment && !string.IsNullOrWhiteSpace(settings.Email)
            && post.Visibility != ContentVisibility.Personal)
        {
            var link = $"{Request.Scheme}://{Request.Host}/post/{post.Slug}#id_{comment.Id}";
            var body = $"<p>New comment by <b>{System.Net.WebUtility.HtmlEncode(comment.Author)}</b> " +
                       $"({System.Net.WebUtility.HtmlEncode(comment.Email)}) on " +
                       $"<a href=\"{link}\">{System.Net.WebUtility.HtmlEncode(post.Title)}</a>:</p>" +
                       $"<blockquote>{comment.Content.ToSafeCommentHtml()}</blockquote>" +
                       (comment.IsApproved ? "" : "<p><i>This comment is waiting for moderation.</i></p>");
            await _email.SendAsync(settings.Email, $"{settings.EmailSubjectPrefix}: new comment on {post.Title}", body);
        }

        TempData["CommentMessage"] = comment.IsApproved
            ? L["Thank you, your comment was added."].Value
            : L["Thank you, your comment is awaiting moderation."].Value;
        return Redirect(Url.Action(nameof(Details), new { slug })! + "#comments");
    }

    [HttpPost("post/{slug}/rate")]
    [EnableRateLimiting("forms")]
    public async Task<IActionResult> Rate(string slug, int rating)
    {
        var settings = await _settings.GetAsync();
        if (!settings.EnableRating || rating is < 1 or > 5) return RedirectToPost(slug);

        // Visible(User): không chấm điểm được bài mà mình không có quyền xem (Private/Personal).
        var post = await _db.Posts.Visible(User).FirstOrDefaultAsync(p => p.Slug == slug);
        if (post is null) return NotFound();

        var cookieName = $"rated-{post.Id}";
        if (!Request.Cookies.ContainsKey(cookieName))
        {
            post.Rating = (post.Rating * post.Raters + rating) / (post.Raters + 1);
            post.Raters += 1;
            await _db.SaveChangesAsync();

            Response.Cookies.Append(cookieName, "1", new CookieOptions
            {
                Expires = DateTimeOffset.UtcNow.AddYears(1),
                HttpOnly = true,
                SameSite = SameSiteMode.Lax,
                IsEssential = true
            });
        }

        return RedirectToPost(slug);
    }

    private IActionResult RedirectToPost(string slug) => Redirect(Url.Action(nameof(Details), new { slug })!);

    /// <summary>
    /// Tìm bài theo slug và kiểm tra quyền xem. Trả về (bài, null) nếu được xem, hoặc (null, kết quả từ chối).
    /// <list type="bullet">
    ///   <item>Không có / đã xoá / nháp hoặc hẹn giờ mà người xem không phải staff → 404 (như cũ).</item>
    ///   <item>Không đủ quyền theo Visibility: khách → chuyển tới trang đăng nhập (nếu cho phép), người đã đăng nhập → 404
    ///   (không để lộ là bài Personal có tồn tại).</item>
    /// </list>
    /// </summary>
    private async Task<(Post? Post, IActionResult? Denied)> ResolvePostAsync(string slug, bool challengeAnonymous)
    {
        var post = await _db.Posts.AsNoTracking()
            .Include(p => p.Author)
            .Include(p => p.Categories)
            .Include(p => p.Tags)
            .FirstOrDefaultAsync(p => p.Slug == slug && !p.IsDeleted);

        if (post is null) return (null, NotFound());

        // Bài nháp/hẹn giờ chỉ xem trước được khi đã đăng nhập vai trò quản trị/biên tập.
        var live = post.IsPublished && post.DateCreated <= DateTime.UtcNow;
        if (!live && !User.IsStaff()) return (null, NotFound());

        if (!User.CanView(post.Visibility, post.AuthorId))
        {
            if (!User.IsSignedIn() && challengeAnonymous) return (null, Challenge());
            return (null, NotFound());
        }

        // Bài không công khai: cấm proxy/CDN/trình duyệt dùng chung lưu lại bản sao.
        if (post.Visibility != ContentVisibility.Public)
            Response.Headers.CacheControl = "private, no-store";

        return (post, null);
    }

    private static bool CanComment(Post post, BlogSettings settings) =>
        settings.IsCommentsEnabled
        && post.IsCommentEnabled
        && (settings.DaysCommentsAreEnabled == 0 || post.DateCreated.AddDays(settings.DaysCommentsAreEnabled) > DateTime.UtcNow);

    private async Task<PostDetailsViewModel> BuildViewModelAsync(Post post, CommentInput input)
    {
        var settings = await _settings.GetAsync();

        var comments = await _db.Comments.AsNoTracking()
            .Where(c => c.PostId == post.Id && c.IsApproved && !c.IsSpam && !c.IsDeleted)
            .OrderBy(c => c.Date)
            .ToListAsync();

        var nodes = comments.ToDictionary(c => c.Id, c => new CommentNode { Comment = c });
        var roots = new List<CommentNode>();
        foreach (var c in comments)
        {
            if (c.ParentId is int pid && nodes.TryGetValue(pid, out var parent))
                parent.Replies.Add(nodes[c.Id]);
            else
                roots.Add(nodes[c.Id]);
        }

        var previous = await _db.Posts.AsNoTracking().Visible(User)
            .Where(p => p.DateCreated < post.DateCreated)
            .OrderByDescending(p => p.DateCreated)
            .Select(p => new Post { Id = p.Id, Title = p.Title, Slug = p.Slug })
            .FirstOrDefaultAsync();

        var next = await _db.Posts.AsNoTracking().Visible(User)
            .Where(p => p.DateCreated > post.DateCreated)
            .OrderBy(p => p.DateCreated)
            .Select(p => new Post { Id = p.Id, Title = p.Title, Slug = p.Slug })
            .FirstOrDefaultAsync();

        var related = new List<Post>();
        if (settings.EnableRelatedPosts && (post.Tags.Count > 0 || post.Categories.Count > 0))
        {
            var tagIds = post.Tags.Select(t => t.Id).ToList();
            var categoryIds = post.Categories.Select(c => c.Id).ToList();
            related = await _db.Posts.AsNoTracking().Visible(User)
                .Where(p => p.Id != post.Id &&
                            (p.Tags.Any(t => tagIds.Contains(t.Id)) || p.Categories.Any(c => categoryIds.Contains(c.Id))))
                .OrderByDescending(p => p.DateCreated)
                .Take(5)
                .Select(p => new Post { Id = p.Id, Title = p.Title, Slug = p.Slug })
                .ToListAsync();
        }

        return new PostDetailsViewModel
        {
            Post = post,
            Previous = previous,
            Next = next,
            Comments = roots,
            CommentCount = comments.Count,
            Related = related,
            CanComment = CanComment(post, settings),
            Input = input
        };
    }
}
