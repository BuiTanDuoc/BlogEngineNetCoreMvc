using BlogEngine.Web.Areas.Admin.Models;
using BlogEngine.Web.Infrastructure;
using BlogEngine.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace BlogEngine.Web.Areas.Admin.Controllers;

/// <summary>Quản lý file upload (thay cho File Manager lưu trong DB/App_Data của BlogEngine cũ).</summary>
public class MediaController : AdminControllerBase
{
    private const long MaxImageBytes = 10 * 1024 * 1024; // ảnh

    private readonly string _root;
    private readonly MediaOptions _options;

    public MediaController(IWebHostEnvironment env, IOptions<MediaOptions> options)
    {
        _options = options.Value;
        var webRoot = env.WebRootPath ?? Path.Combine(env.ContentRootPath, "wwwroot");
        _root = Path.GetFullPath(Path.Combine(webRoot, "uploads"));
    }

    private IEnumerable<MediaFile> Scan()
    {
        Directory.CreateDirectory(_root);
        return new DirectoryInfo(_root)
            .EnumerateFiles("*", SearchOption.AllDirectories)
            .Where(f => MediaTypes.All.Contains(f.Extension.ToLowerInvariant()))
            .OrderByDescending(f => f.LastWriteTimeUtc)
            .Select(f =>
            {
                var rel = Path.GetRelativePath(_root, f.FullName).Replace('\\', '/');
                var ext = f.Extension.ToLowerInvariant();
                var kind = MediaTypes.KindOf(ext);
                return new MediaFile
                {
                    RelativePath = rel,
                    Url = "/uploads/" + string.Join('/', rel.Split('/').Select(Uri.EscapeDataString)),
                    Size = f.Length,
                    Modified = f.LastWriteTimeUtc,
                    IsImage = kind == "image",
                    IsVideo = kind == "video",
                    Kind = kind,
                    Name = DisplayName(f.Name),
                    Ext = ext.TrimStart('.').ToUpperInvariant()
                };
            });
    }

    /// <summary>"bao-cao-thang-11-a1b2c3.docx" → "bao-cao-thang-11.docx".</summary>
    private static string DisplayName(string storedName)
    {
        var ext = Path.GetExtension(storedName);
        var baseName = Path.GetFileNameWithoutExtension(storedName);
        baseName = System.Text.RegularExpressions.Regex.Replace(baseName, "-[0-9a-f]{6}$", "");
        return baseName + ext;
    }

    /// <param name="type">all | image | document | archive | video</param>
    public IActionResult Index(string? type, string? q)
    {
        type = (type ?? "all").ToLowerInvariant();
        var files = Scan();
        if (type is "image" or "document" or "archive" or "video") files = files.Where(f => f.Kind == type);
        if (!string.IsNullOrWhiteSpace(q))
            files = files.Where(f => f.Name.Contains(q.Trim(), StringComparison.OrdinalIgnoreCase));

        ViewBag.MaxVideoMb = _options.MaxVideoMb;
        ViewBag.MaxFileMb = _options.MaxFileMb;
        ViewBag.Type = type;
        ViewBag.Query = q;
        return View(files.Take(500).ToList());
    }

    /// <summary>Danh sách file cho hộp thoại chọn file trong trình soạn thảo. kind: image | video | file (tài liệu + nén) | all.</summary>
    [HttpGet]
    public IActionResult Browse(string? kind)
    {
        var files = Scan();
        files = (kind ?? "all").ToLowerInvariant() switch
        {
            "image" => files.Where(f => f.Kind == "image"),
            "video" => files.Where(f => f.Kind == "video"),
            "file" => files.Where(f => f.Kind is "document" or "archive"),
            _ => files
        };
        return Json(files.Take(200).Select(f => new { url = f.Url, name = f.Name, size = f.Size, kind = f.Kind }));
    }

    // Giới hạn body của /admin/media/upload* được nâng bằng middleware trong Program.cs
    // (phải làm trước khi bộ lọc anti-forgery đọc form, nên không dùng [RequestSizeLimit] ở đây).
    [HttpPost]
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
    public async Task<IActionResult> UploadImage(IFormFile file)
    {
        if (file is null) return BadRequest(new { error = L["No file."].Value });
        if (!MediaTypes.Images.Contains(Path.GetExtension(file.FileName).ToLowerInvariant()))
            return BadRequest(new { error = L["Only image files are allowed."].Value });

        var (url, error) = await SaveAsync(file);
        return url is null ? BadRequest(new { error }) : Json(new { url });
    }

    /// <summary>Endpoint cho nút chèn video trong trình soạn thảo (AJAX).</summary>
    [HttpPost]
    public async Task<IActionResult> UploadVideo(IFormFile file)
    {
        if (file is null) return BadRequest(new { error = L["No file."].Value });
        if (!MediaTypes.Videos.Contains(Path.GetExtension(file.FileName).ToLowerInvariant()))
            return BadRequest(new { error = L["Only video files are allowed."].Value });

        var (url, error) = await SaveAsync(file);
        return url is null ? BadRequest(new { error }) : Json(new { url });
    }

    /// <summary>Endpoint cho nút chèn link tải file (zip, Office, PDF...) trong trình soạn thảo (AJAX).</summary>
    [HttpPost]
    public async Task<IActionResult> UploadFile(IFormFile file)
    {
        if (file is null) return BadRequest(new { error = L["No file."].Value });

        var (url, error) = await SaveAsync(file);
        if (url is null) return BadRequest(new { error });
        return Json(new { url, name = Path.GetFileName(file.FileName), size = file.Length, kind = MediaTypes.KindOf(Path.GetExtension(url).ToLowerInvariant()) });
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

        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!MediaTypes.All.Contains(ext)) return (null, L["Unsupported file type."].Value);

        var kind = MediaTypes.KindOf(ext);
        if (kind == "video")
        {
            if (file.Length > _options.MaxVideoBytes) return (null, L["Video is larger than {0} MB.", _options.MaxVideoMb].Value);
        }
        else if (kind == "image")
        {
            if (file.Length > MaxImageBytes) return (null, L["File is larger than 10 MB."].Value);
        }
        else if (file.Length > _options.MaxFileBytes)
        {
            return (null, L["File is larger than {0} MB.", _options.MaxFileMb].Value);
        }

        // Kiểm tra chữ ký file (magic bytes) để không tin phần mở rộng do client gửi.
        var header = new byte[12];
        await using (var probe = file.OpenReadStream())
        {
            var read = await probe.ReadAsync(header.AsMemory(0, header.Length));
            if (!MediaTypes.MatchesSignature(ext, header, read)) return (null, L["File content does not match its extension."].Value);
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
}
