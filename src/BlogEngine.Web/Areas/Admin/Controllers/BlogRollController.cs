using BlogEngine.Web.Areas.Admin.Models;
using BlogEngine.Web.Data;
using BlogEngine.Web.Data.Entities;
using BlogEngine.Web.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BlogEngine.Web.Areas.Admin.Controllers;

[Authorize(Policy = "Admin")]
public class BlogRollController : AdminControllerBase
{
    private readonly BlogDbContext _db;
    public BlogRollController(BlogDbContext db) => _db = db;

    public async Task<IActionResult> Index()
        => View(await _db.BlogRollItems.AsNoTracking().OrderBy(b => b.SortIndex).ThenBy(b => b.Title).ToListAsync());

    [HttpGet]
    public IActionResult Create() => View("Edit", new BlogRollEditModel { Xfn = "contact" });

    [HttpPost]
    public async Task<IActionResult> Create(BlogRollEditModel model)
    {
        Validate(model);
        if (!ModelState.IsValid) return View("Edit", model);

        var item = new BlogRollItem();
        Apply(item, model);
        _db.BlogRollItems.Add(item);
        await _db.SaveChangesAsync();
        Flash(L["Link added."]);
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var b = await _db.BlogRollItems.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        if (b is null) return NotFound();
        return View(new BlogRollEditModel
        {
            Id = b.Id, Title = b.Title, BlogUrl = b.BlogUrl, FeedUrl = b.FeedUrl,
            Description = b.Description, Xfn = b.Xfn, SortIndex = b.SortIndex
        });
    }

    [HttpPost]
    public async Task<IActionResult> Edit(int id, BlogRollEditModel model)
    {
        var item = await _db.BlogRollItems.FirstOrDefaultAsync(x => x.Id == id);
        if (item is null) return NotFound();

        Validate(model);
        if (!ModelState.IsValid) { model.Id = id; return View(model); }

        Apply(item, model);
        await _db.SaveChangesAsync();
        Flash(L["Link saved."]);
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> Delete(int id)
    {
        var item = await _db.BlogRollItems.FirstOrDefaultAsync(x => x.Id == id);
        if (item is null) return NotFound();
        _db.BlogRollItems.Remove(item);
        await _db.SaveChangesAsync();
        Flash(L["Link deleted."]);
        return RedirectToAction(nameof(Index));
    }

    private void Validate(BlogRollEditModel model)
    {
        if (model.BlogUrl.ToSafeHttpUrl() is null)
            ModelState.AddModelError(nameof(model.BlogUrl), L["Enter a valid http(s) URL."]);
        if (!string.IsNullOrWhiteSpace(model.FeedUrl) && model.FeedUrl.ToSafeHttpUrl() is null)
            ModelState.AddModelError(nameof(model.FeedUrl), L["Enter a valid http(s) URL."]);
    }

    private static void Apply(BlogRollItem item, BlogRollEditModel model)
    {
        item.Title = model.Title.Trim();
        item.BlogUrl = model.BlogUrl.ToSafeHttpUrl()!;
        item.FeedUrl = model.FeedUrl.ToSafeHttpUrl();
        item.Description = model.Description.NullIfEmpty();
        item.Xfn = model.Xfn.NullIfEmpty();
        item.SortIndex = model.SortIndex;
    }
}
