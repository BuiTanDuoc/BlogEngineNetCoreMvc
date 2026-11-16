using BlogEngine.Web.Data;
using BlogEngine.Web.Data.Entities;
using BlogEngine.Web.Infrastructure;
using BlogEngine.Web.Models;
using BlogEngine.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace BlogEngine.Web.Controllers;

public class ContactController : LocalizedController
{
    private readonly BlogDbContext _db;
    private readonly ISettingsService _settings;
    private readonly IEmailService _email;

    public ContactController(BlogDbContext db, ISettingsService settings, IEmailService email)
    {
        _db = db;
        _settings = settings;
        _email = email;
    }

    [HttpGet("contact")]
    public async Task<IActionResult> Index()
    {
        ViewBag.Settings = await _settings.GetAsync();
        return View(new ContactInput());
    }

    [HttpPost("contact")]
    [EnableRateLimiting("forms")]
    public async Task<IActionResult> Index(ContactInput input)
    {
        var settings = await _settings.GetAsync();
        ViewBag.Settings = settings;

        if (!string.IsNullOrEmpty(input.Website2))
            return RedirectToAction(nameof(Thanks)); // honeypot

        if (!ModelState.IsValid) return View(input);

        var message = new ContactMessage
        {
            Name = input.Name.Trim(),
            Email = input.Email.Trim(),
            Subject = input.Subject.NullIfEmpty(),
            Message = input.Message.Trim(),
            Ip = HttpContext.Connection.RemoteIpAddress?.ToString()
        };
        _db.ContactMessages.Add(message);
        await _db.SaveChangesAsync();

        if (!string.IsNullOrWhiteSpace(settings.Email))
        {
            var subject = (message.Subject ?? "Contact form").Replace("\r", " ").Replace("\n", " ");
            var body = $"<p><b>{System.Net.WebUtility.HtmlEncode(message.Name)}</b> " +
                       $"&lt;{System.Net.WebUtility.HtmlEncode(message.Email)}&gt; wrote:</p>" +
                       $"<blockquote>{message.Message.ToSafeCommentHtml()}</blockquote>";
            await _email.SendAsync(settings.Email, $"{settings.EmailSubjectPrefix}: {subject}", body, replyTo: message.Email);
        }

        return RedirectToAction(nameof(Thanks));
    }

    [HttpGet("contact/thanks")]
    public async Task<IActionResult> Thanks()
    {
        ViewBag.Settings = await _settings.GetAsync();
        return View();
    }
}
