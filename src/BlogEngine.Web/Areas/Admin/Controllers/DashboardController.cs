using BlogEngine.Web.Areas.Admin.Models;
using BlogEngine.Web.Data;
using BlogEngine.Web.Data.Entities;
using BlogEngine.Web.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BlogEngine.Web.Areas.Admin.Controllers;

public class DashboardController : AdminControllerBase
{
    private readonly BlogDbContext _db;
    public DashboardController(BlogDbContext db) => _db = db;

    public async Task<IActionResult> Index()
    {
        // Không đếm/hiển thị bài Personal của người khác (kể cả với Administrators).
        var uid = User.GetUserId();
        var posts = _db.Posts.AsNoTracking()
            .Where(p => p.Visibility != ContentVisibility.Personal || p.AuthorId == uid);
        var comments = _db.Comments.AsNoTracking()
            .Where(c => c.Post!.Visibility != ContentVisibility.Personal || c.Post.AuthorId == uid);

        var vm = new DashboardViewModel
        {
            PublishedPosts = await posts.CountAsync(p => p.IsPublished && !p.IsDeleted),
            DraftPosts = await posts.CountAsync(p => !p.IsPublished && !p.IsDeleted),
            Pages = await _db.Pages.CountAsync(p => !p.IsDeleted
                && (p.Visibility != ContentVisibility.Personal || p.AuthorId == uid)),
            Comments = await comments.CountAsync(c => c.IsApproved && !c.IsSpam && !c.IsDeleted),
            PendingComments = await comments.CountAsync(c => !c.IsApproved && !c.IsSpam && !c.IsDeleted),
            SpamComments = await comments.CountAsync(c => c.IsSpam && !c.IsDeleted),
            Users = await _db.Users.CountAsync(),
            UnreadMessages = await _db.ContactMessages.CountAsync(m => !m.IsRead),
            RecentPosts = await posts.Where(p => !p.IsDeleted).OrderByDescending(p => p.DateModified).Take(5).ToListAsync(),
            RecentComments = await comments.Include(c => c.Post).Where(c => !c.IsDeleted && !c.IsSpam)
                .OrderByDescending(c => c.Date).Take(5).ToListAsync()
        };
        return View(vm);
    }
}
