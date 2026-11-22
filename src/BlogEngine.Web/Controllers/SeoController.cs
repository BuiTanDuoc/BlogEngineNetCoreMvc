using System.Text;
using System.Xml.Linq;
using BlogEngine.Web.Data;
using BlogEngine.Web.Data.Entities;
using BlogEngine.Web.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BlogEngine.Web.Controllers;

public class SeoController : Controller
{
    private readonly BlogDbContext _db;
    public SeoController(BlogDbContext db) => _db = db;

    private string BaseUrl => $"{Request.Scheme}://{Request.Host}";

    [HttpGet("robots.txt")]
    public IActionResult Robots()
    {
        var sb = new StringBuilder();
        sb.AppendLine("User-agent: *");
        sb.AppendLine("Disallow: /admin/");
        sb.AppendLine("Disallow: /account/");
        sb.AppendLine("Disallow: /search");
        sb.AppendLine($"Sitemap: {BaseUrl}/sitemap.xml");
        return Content(sb.ToString(), "text/plain", Encoding.UTF8);
    }

    [HttpGet("sitemap.xml")]
    [ResponseCache(Duration = 3600)]
    public async Task<IActionResult> Sitemap()
    {
        XNamespace ns = "http://www.sitemaps.org/schemas/sitemap/0.9";
        var urlset = new XElement(ns + "urlset");

        void Add(string path, DateTime? lastMod = null)
        {
            var url = new XElement(ns + "url", new XElement(ns + "loc", BaseUrl + path));
            if (lastMod is { } d)
                url.Add(new XElement(ns + "lastmod", DateTime.SpecifyKind(d, DateTimeKind.Utc).ToString("yyyy-MM-dd")));
            urlset.Add(url);
        }

        Add("/");
        Add("/archive");
        Add("/contact");

        var posts = await _db.Posts.AsNoTracking().Visible()
            .OrderByDescending(p => p.DateCreated)
            .Select(p => new { p.Slug, p.DateModified })
            .ToListAsync();
        foreach (var p in posts) Add($"/post/{Uri.EscapeDataString(p.Slug)}", p.DateModified);

        // Sitemap dành cho bot/khách: chỉ liệt kê nội dung Public (Visible() không truyền viewer = khách).
        var pages = await _db.Pages.AsNoTracking().Visible()
            .Select(p => new { p.Slug, p.DateModified })
            .ToListAsync();
        foreach (var p in pages) Add($"/page/{Uri.EscapeDataString(p.Slug)}", p.DateModified);

        var now = DateTime.UtcNow;
        var categories = await _db.Categories.AsNoTracking()
            .Where(c => c.Posts.Any(p => p.IsPublished && !p.IsDeleted && p.DateCreated <= now
                                         && p.Visibility == ContentVisibility.Public))
            .Select(c => c.Slug)
            .ToListAsync();
        foreach (var slug in categories) Add($"/category/{Uri.EscapeDataString(slug)}");

        var tags = await _db.Tags.AsNoTracking()
            .Where(t => t.Posts.Any(p => p.IsPublished && !p.IsDeleted && p.DateCreated <= now
                                         && p.Visibility == ContentVisibility.Public))
            .Select(t => t.Slug)
            .ToListAsync();
        foreach (var slug in tags) Add($"/tag/{Uri.EscapeDataString(slug)}");

        var doc = new XDocument(new XDeclaration("1.0", "utf-8", null), urlset);
        return Content(doc.Declaration + Environment.NewLine + doc.Root, "application/xml", Encoding.UTF8);
    }
}
