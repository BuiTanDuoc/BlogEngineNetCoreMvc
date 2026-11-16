using BlogEngine.Web.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BlogEngine.Web.Areas.Admin.Controllers;

/// <summary>Hộp thư từ form liên hệ.</summary>
public class MessagesController : AdminControllerBase
{
    private readonly BlogDbContext _db;
    public MessagesController(BlogDbContext db) => _db = db;

    public async Task<IActionResult> Index()
        => View(await _db.ContactMessages.AsNoTracking().OrderByDescending(m => m.Date).Take(200).ToListAsync());

    [HttpPost]
    public async Task<IActionResult> MarkRead(int id)
    {
        var m = await _db.ContactMessages.FirstOrDefaultAsync(x => x.Id == id);
        if (m is null) return NotFound();
        m.IsRead = true;
        await _db.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> Delete(int id)
    {
        var m = await _db.ContactMessages.FirstOrDefaultAsync(x => x.Id == id);
        if (m is null) return NotFound();
        _db.ContactMessages.Remove(m);
        await _db.SaveChangesAsync();
        Flash(L["Message deleted."]);
        return RedirectToAction(nameof(Index));
    }
}
