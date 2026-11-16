using System.Text;
using System.Xml;
using System.Xml.Linq;
using BlogEngine.Web.Data;
using BlogEngine.Web.Infrastructure;
using BlogEngine.Web.Models;
using BlogEngine.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BlogEngine.Web.Controllers;

public class FeedController : Controller
{
    private static readonly XNamespace ContentNs = "http://purl.org/rss/1.0/modules/content/";
    private static readonly XNamespace AtomNs = "http://www.w3.org/2005/Atom";

    private readonly BlogDbContext _db;
    private readonly ISettingsService _settings;

    public FeedController(BlogDbContext db, ISettingsService settings)
    {
        _db = db;
        _settings = settings;
    }

    private string BaseUrl => $"{Request.Scheme}://{Request.Host}";

    [HttpGet("feed")]
    [ResponseCache(Duration = 600)]
    public async Task<IActionResult> Rss()
    {
        var settings = await _settings.GetAsync();
        var posts = await LoadPostsAsync(settings);

        var channel = new XElement("channel",
            new XElement("title", Clean(settings.Name)),
            new XElement("link", BaseUrl + "/"),
            new XElement("description", Clean(settings.Description)),
            new XElement("language", settings.Culture),
            new XElement("lastBuildDate", (posts.FirstOrDefault()?.DateModified ?? DateTime.UtcNow).ToString("R")),
            new XElement(AtomNs + "link",
                new XAttribute("href", BaseUrl + "/feed"),
                new XAttribute("rel", "self"),
                new XAttribute("type", "application/rss+xml")));

        foreach (var p in posts)
        {
            var link = $"{BaseUrl}/post/{p.Slug}";
            var item = new XElement("item",
                new XElement("title", Clean(p.Title)),
                new XElement("link", link),
                new XElement("guid", new XAttribute("isPermaLink", "true"), link),
                new XElement("pubDate", DateTime.SpecifyKind(p.DateCreated, DateTimeKind.Utc).ToString("R")),
                new XElement("description", Clean(string.IsNullOrWhiteSpace(p.Description) ? p.Content.Excerpt(400) : p.Description)),
                new XElement(ContentNs + "encoded", new XCData(Clean(p.Content).Replace("]]>", "]]&gt;"))));

            foreach (var c in p.Categories)
                item.Add(new XElement("category", Clean(c.Name)));

            channel.Add(item);
        }

        var doc = new XDocument(
            new XDeclaration("1.0", "utf-8", null),
            new XElement("rss",
                new XAttribute("version", "2.0"),
                new XAttribute(XNamespace.Xmlns + "content", ContentNs),
                new XAttribute(XNamespace.Xmlns + "atom", AtomNs),
                channel));

        return Xml(doc, "application/rss+xml");
    }

    [HttpGet("feed/atom")]
    [ResponseCache(Duration = 600)]
    public async Task<IActionResult> Atom()
    {
        var settings = await _settings.GetAsync();
        var posts = await LoadPostsAsync(settings);

        var feed = new XElement(AtomNs + "feed",
            new XElement(AtomNs + "title", Clean(settings.Name)),
            new XElement(AtomNs + "subtitle", Clean(settings.Description)),
            new XElement(AtomNs + "link", new XAttribute("href", BaseUrl + "/")),
            new XElement(AtomNs + "link", new XAttribute("href", BaseUrl + "/feed/atom"), new XAttribute("rel", "self")),
            new XElement(AtomNs + "id", BaseUrl + "/"),
            new XElement(AtomNs + "updated", Iso(posts.FirstOrDefault()?.DateModified ?? DateTime.UtcNow)));

        foreach (var p in posts)
        {
            var link = $"{BaseUrl}/post/{p.Slug}";
            feed.Add(new XElement(AtomNs + "entry",
                new XElement(AtomNs + "title", Clean(p.Title)),
                new XElement(AtomNs + "link", new XAttribute("href", link)),
                new XElement(AtomNs + "id", link),
                new XElement(AtomNs + "published", Iso(p.DateCreated)),
                new XElement(AtomNs + "updated", Iso(p.DateModified)),
                new XElement(AtomNs + "author", new XElement(AtomNs + "name", Clean(p.Author?.Name ?? ""))),
                new XElement(AtomNs + "summary", Clean(string.IsNullOrWhiteSpace(p.Description) ? p.Content.Excerpt(400) : p.Description)),
                new XElement(AtomNs + "content", new XAttribute("type", "html"), Clean(p.Content))));
        }

        var doc = new XDocument(new XDeclaration("1.0", "utf-8", null), feed);
        return Xml(doc, "application/atom+xml");
    }

    private Task<List<Data.Entities.Post>> LoadPostsAsync(BlogSettings settings) =>
        _db.Posts.AsNoTracking().Visible()
            .Include(p => p.Author)
            .Include(p => p.Categories)
            .OrderByDescending(p => p.DateCreated)
            .Take(settings.PostsPerFeed)
            .ToListAsync();

    private static string Iso(DateTime utc) => DateTime.SpecifyKind(utc, DateTimeKind.Utc).ToString("yyyy-MM-ddTHH:mm:ssZ");

    /// <summary>Loại ký tự không hợp lệ trong XML 1.0 (làm vỡ feed nếu có trong nội dung bài).</summary>
    private static string Clean(string? text)
    {
        if (string.IsNullOrEmpty(text)) return "";
        var sb = new StringBuilder(text.Length);
        foreach (var c in text)
            if (XmlConvert.IsXmlChar(c) || char.IsSurrogate(c)) sb.Append(c);
        return sb.ToString();
    }

    private IActionResult Xml(XDocument doc, string contentType)
    {
        using var writer = new Utf8StringWriter();
        doc.Save(writer);
        return Content(writer.ToString(), $"{contentType}; charset=utf-8", Encoding.UTF8);
    }

    private sealed class Utf8StringWriter : StringWriter
    {
        public override Encoding Encoding => Encoding.UTF8;
    }
}
