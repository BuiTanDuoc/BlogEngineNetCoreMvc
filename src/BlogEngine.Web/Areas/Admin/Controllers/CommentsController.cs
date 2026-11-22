using BlogEngine.Web.Areas.Admin.Models;
using BlogEngine.Web.Data;
using BlogEngine.Web.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BlogEngine.Web.Areas.Admin.Controllers;

public class CommentsController : AdminControllerBase
{
    private const int PageSize = 25;
    private readonly BlogDbContext _db;
    public CommentsController(BlogDbContext db) => _db = db;

    /// <summary>Comment trên bài Personal của người khác không được thấy/duyệt (kể cả Administrators).</summary>
    private IQueryable<Data.Entities.Comment> AccessibleComments()
    {
        var uid = User.GetUserId();
        return _db.Comments.Where(c => c.Post!.Visibility != Data.Entities.ContentVisibility.Personal || c.Post.AuthorId == uid);
    }

    public async Task<IActionResult> Index(string filter = "pending", int page = 1)
    {
        var all = AccessibleComments().AsNoTracking();
        var query = filter switch
        {
            "approved" => all.Where(c => c.IsApproved && !c.IsSpam && !c.IsDeleted),
            "spam" => all.Where(c => c.IsSpam && !c.IsDeleted),
            "trash" => all.Where(c => c.IsDeleted),
            _ => all.Where(c => !c.IsApproved && !c.IsSpam && !c.IsDeleted)
        };

        var comments = await PagedList<Data.Entities.Comment>.CreateAsync(query.OrderByDescending(c => c.Date), page, PageSize);
        var postIds = comments.Items.Select(c => c.PostId).Distinct().ToList();
        var posts = await _db.Posts.AsNoTracking().Where(p => postIds.Contains(p.Id))
            .Select(p => new { p.Id, p.Title, p.Slug }).ToListAsync();

        return View(new CommentListViewModel
        {
            Comments = comments,
            Posts = posts.ToDictionary(p => p.Id, p => (p.Title, p.Slug)),
            Filter = filter,
            PendingCount = await all.CountAsync(c => !c.IsApproved && !c.IsSpam && !c.IsDeleted),
            ApprovedCount = await all.CountAsync(c => c.IsApproved && !c.IsSpam && !c.IsDeleted),
            SpamCount = await all.CountAsync(c => c.IsSpam && !c.IsDeleted),
            TrashCount = await all.CountAsync(c => c.IsDeleted)
        });
    }

    [HttpPost] public Task<IActionResult> Approve(int id, string filter) => Update(id, filter, c => { c.IsApproved = true; c.IsSpam = false; });
    [HttpPost] public Task<IActionResult> Unapprove(int id, string filter) => Update(id, filter, c => c.IsApproved = false);
    [HttpPost] public Task<IActionResult> Spam(int id, string filter) => Update(id, filter, c => { c.IsSpam = true; c.IsApproved = false; });
    [HttpPost] public Task<IActionResult> NotSpam(int id, string filter) => Update(id, filter, c => { c.IsSpam = false; c.IsApproved = true; });
    [HttpPost] public Task<IActionResult> Trash(int id, string filter) => Update(id, filter, c => c.IsDeleted = true);
    [HttpPost] public Task<IActionResult> Restore(int id, string filter) => Update(id, filter, c => c.IsDeleted = false);

    private async Task<IActionResult> Update(int id, string filter, Action<Data.Entities.Comment> change)
    {
        var comment = await AccessibleComments().FirstOrDefaultAsync(c => c.Id == id);
        if (comment is null) return NotFound();

        change(comment);
        comment.ModeratedBy = User.Identity?.Name;
        await _db.SaveChangesAsync();
        return RedirectToAction(nameof(Index), new { filter });
    }
}
