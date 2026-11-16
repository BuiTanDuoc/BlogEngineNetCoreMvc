using BlogEngine.Web.Areas.Admin.Models;
using BlogEngine.Web.Infrastructure;
using BlogEngine.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BlogEngine.Web.Areas.Admin.Controllers;

/// <summary>Quản lý file upload (thay cho File Manager lưu trong DB/App_Data của BlogEngine cũ).</summary>
public class MediaController : AdminControllerBase
{
    private const long MaxBytes = 10 * 1024 * 1024;
    private static readonly string[] ImageExtensions = { ".jpg", ".jpeg", ".png", ".gif", ".webp" };
    private static readonly string[] AllowedExtensions = ImageExtensions.Append(".pdf").ToArray();

    private readonly string _root;

    public MediaController(IWebHostEnvironment env)
    {
        var webRoot = env.WebRootPath ?? Path.Combine(env.ContentRootPath, "wwwroot");
        _root = Path.GetFullPath(Path.Combine(webRoot, "uploads"));
    }

    public IActionResult Index()
    {
        Directory.CreateDirectory(_root);
        var files = new DirectoryInfo(_root)
            .EnumerateFiles("*", SearchOption.AllDirectories)
            .Where(f => AllowedExtensions.Contains(f.Extension.ToLowerInvariant()))
            .OrderByDescending(f => f.LastWriteTimeUtc)
            .Take(300)
            .Select(f =>
            {
                var rel = Path.GetRelativePath(_root, f.FullName).Replace('\\', '/');
                return new MediaFile
                {
                    RelativePath = rel,
                    Url = "/uploads/" + string.Join('/', rel.Split('/').Select(Uri.EscapeDataString)),
                    Size = f.Length,
                    Modified = f.LastWriteTimeUtc,
                    IsImage = ImageExtensions.Contains(f.Extension.ToLowerInvariant())
                };
            })
            .ToList();
        return View(files);
    }

    [HttpPost]
    [RequestSizeLimit(MaxBytes + 1024 * 1024)]
    public async Task<IActionResult> Upload(List<IFormFile> files)
    {
        var ok = 0;
        foreach (var file in files)
        {
            var (url, error) = await SaveAsync(file);
            if (url is null) FlashError($"{file.FileName}: {error}");
            else ok++;
        }
        if (ok > 0) Flash(L.Plural(ok, "{0} file uploaded.", "{0} files uploaded."));
        return RedirectToAction(nameof(Index));
    }

    /// <summary>Endpoint cho nút chèn ảnh trong trình soạn thảo (AJAX).</summary>
    [HttpPost]
    [RequestSizeLimit(MaxBytes + 1024 * 1024)]
    public async Task<IActionResult> UploadImage(IFormFile file)
    {
        if (file is null) return BadRequest(new { error = L["No file."].Value });
        if (!ImageExtensions.Contains(Path.GetExtension(file.FileName).ToLowerInvariant()))
            return BadRequest(new { error = L["Only image files are allowed."].Value });

        var (url, error) = await SaveAsync(file);
        return url is null ? BadRequest(new { error }) : Json(new { url });
    }

    [HttpPost]
    [Authorize(Policy = "Admin")]
    public IActionResult Delete(string path)
    {
        var full = Path.GetFullPath(Path.Combine(_root, path ?? ""));
        // Chặn path traversal: file phải nằm trong thư mục uploads.
        if (!full.StartsWith(_root + Path.DirectorySeparatorChar, StringComparison.Ordinal) || !System.IO.File.Exists(full))
            return NotFound();

        System.IO.File.Delete(full);
        Flash(L["File deleted."]);
        return RedirectToAction(nameof(Index));
    }

    private async Task<(string? Url, string? Error)> SaveAsync(IFormFile file)
    {
        if (file.Length == 0) return (null, L["Empty file."].Value);
        if (file.Length > MaxBytes) return (null, L["File is larger than 10 MB."].Value);

        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!AllowedExtensions.Contains(ext)) return (null, L["Unsupported file type (images and PDF only)."].Value);

        // Kiểm tra chữ ký file (magic bytes) để không tin phần mở rộng do client gửi.
        var header = new byte[12];
        await using (var probe = file.OpenReadStream())
        {
            var read = await probe.ReadAsync(header.AsMemory(0, header.Length));
            if (!MatchesSignature(ext, header, read)) return (null, L["File content does not match its extension."].Value);
        }

        var now = DateTime.UtcNow;
        var dir = Path.Combine(_root, now.ToString("yyyy"), now.ToString("MM"));
        Directory.CreateDirectory(dir);

        var baseName = SlugHelper.Slugify(Path.GetFileNameWithoutExtension(file.FileName), 60);
        if (baseName.Length == 0) baseName = "file";
        var fileName = $"{baseName}-{Guid.NewGuid().ToString("N")[..6]}{ext}";

        await using (var target = System.IO.File.Create(Path.Combine(dir, fileName)))
            await file.CopyToAsync(target);

        return ($"/uploads/{now:yyyy}/{now:MM}/{fileName}", null);
    }

    private static bool MatchesSignature(string ext, byte[] h, int len)
    {
        if (len < 4) return false;
        return ext switch
        {
            ".jpg" or ".jpeg" => h[0] == 0xFF && h[1] == 0xD8 && h[2] == 0xFF,
            ".png" => h[0] == 0x89 && h[1] == 0x50 && h[2] == 0x4E && h[3] == 0x47,
            ".gif" => h[0] == 'G' && h[1] == 'I' && h[2] == 'F' && h[3] == '8',
            ".webp" => len >= 12 && h[0] == 'R' && h[1] == 'I' && h[2] == 'F' && h[3] == 'F'
                       && h[8] == 'W' && h[9] == 'E' && h[10] == 'B' && h[11] == 'P',
            ".pdf" => h[0] == '%' && h[1] == 'P' && h[2] == 'D' && h[3] == 'F',
            _ => false
        };
    }
}
