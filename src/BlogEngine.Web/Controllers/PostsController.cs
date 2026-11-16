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
        var post = await LoadPostAsync(slug);
        if (post is null) return NotFound();

        return View(await BuildViewModelAsync(post, new CommentInput()));
    }

    [HttpPost("post/{slug}/comment")]
    [EnableRateLimiting("forms")]
    public async Task<IActionResult> Comment(string slug, CommentInput input)
    {
        var post = await LoadPostAsync(slug);
        if (post is null) return NotFound();

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

        if (settings.SendMailOnComment && !string.IsNullOrWhiteSpace(settings.Email))
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

        var now = DateTime.UtcNow;
        var post = await _db.Posts.FirstOrDefaultAsync(p => p.Slug == slug && p.IsPublished && !p.IsDeleted && p.DateCreated <= now);
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

    private async Task<Post?> LoadPostAsync(string slug)
    {
        var post = await _db.Posts.AsNoTracking()
            .Include(p => p.Author)
            .Include(p => p.Categories)
            .Include(p => p.Tags)
            .FirstOrDefaultAsync(p => p.Slug == slug && !p.IsDeleted);

        if (post is null) return null;

        var visible = post.IsPublished && post.DateCreated <= DateTime.UtcNow;
        // Bài nháp/hẹn giờ chỉ xem trước được khi đã đăng nhập vai trò quản trị/biên tập.
        return visible || User.IsStaff() ? post : null;
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

        var previous = await _db.Posts.AsNoTracking().Visible()
            .Where(p => p.DateCreated < post.DateCreated)
            .OrderByDescending(p => p.DateCreated)
            .Select(p => new Post { Id = p.Id, Title = p.Title, Slug = p.Slug })
            .FirstOrDefaultAsync();

        var next = await _db.Posts.AsNoTracking().Visible()
            .Where(p => p.DateCreated > post.DateCreated)
            .OrderBy(p => p.DateCreated)
            .Select(p => new Post { Id = p.Id, Title = p.Title, Slug = p.Slug })
            .FirstOrDefaultAsync();

        var related = new List<Post>();
        if (settings.EnableRelatedPosts && (post.Tags.Count > 0 || post.Categories.Count > 0))
        {
            var tagIds = post.Tags.Select(t => t.Id).ToList();
            var categoryIds = post.Categories.Select(c => c.Id).ToList();
            related = await _db.Posts.AsNoTracking().Visible()
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
