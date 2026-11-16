using BlogEngine.Web.Areas.Admin.Models;
using BlogEngine.Web.Data;
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
        var posts = _db.Posts.AsNoTracking();
        var comments = _db.Comments.AsNoTracking();

        var vm = new DashboardViewModel
        {
            PublishedPosts = await posts.CountAsync(p => p.IsPublished && !p.IsDeleted),
            DraftPosts = await posts.CountAsync(p => !p.IsPublished && !p.IsDeleted),
            Pages = await _db.Pages.CountAsync(p => !p.IsDeleted),
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
