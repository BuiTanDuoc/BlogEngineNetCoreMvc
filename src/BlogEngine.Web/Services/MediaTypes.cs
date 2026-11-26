using Microsoft.AspNetCore.StaticFiles;

namespace BlogEngine.Web.Services;

/// <summary>Phân loại và kiểm tra các định dạng file được phép upload.</summary>
public static class MediaTypes
{
    public static readonly string[] Images = { ".jpg", ".jpeg", ".png", ".gif", ".webp" };
    // Chỉ nhận định dạng mọi trình duyệt phát được trực tiếp bằng thẻ <video>.
    public static readonly string[] Videos = { ".mp4", ".webm" };
    // Không nhận file có macro (.docm, .xlsm, .pptm) hay file thực thi/script/HTML/SVG.
    public static readonly string[] Documents = { ".pdf", ".doc", ".docx", ".xls", ".xlsx", ".ppt", ".pptx", ".txt", ".csv" };
    public static readonly string[] Archives = { ".zip", ".rar", ".7z" };

    public static readonly string[] All = Images.Concat(Videos).Concat(Documents).Concat(Archives).ToArray();

    /// <summary>File nào nên bị ép tải về (Content-Disposition: attachment) thay vì mở trong trình duyệt.</summary>
    public static bool ForceDownload(string ext) => Archives.Contains(ext) || (Documents.Contains(ext) && ext != ".pdf");

    /// <summary>"image" | "video" | "document" | "archive" | "other".</summary>
    public static string KindOf(string ext) =>
        Images.Contains(ext) ? "image" :
        Videos.Contains(ext) ? "video" :
        Documents.Contains(ext) ? "document" :
        Archives.Contains(ext) ? "archive" : "other";

    /// <summary>Kiểm tra chữ ký file (magic bytes) để không tin phần mở rộng do client gửi.</summary>
    public static bool MatchesSignature(string ext, byte[] h, int len)
    {
        if (len < 1) return false;
        bool Zip() => len >= 4 && h[0] == 'P' && h[1] == 'K' && (h[2] == 3 || h[2] == 5) && (h[3] == 4 || h[3] == 6);
        bool Ole() => len >= 8 && h[0] == 0xD0 && h[1] == 0xCF && h[2] == 0x11 && h[3] == 0xE0 && h[4] == 0xA1 && h[5] == 0xB1 && h[6] == 0x1A && h[7] == 0xE1;
        return ext switch
        {
            ".jpg" or ".jpeg" => len >= 3 && h[0] == 0xFF && h[1] == 0xD8 && h[2] == 0xFF,
            ".png" => len >= 4 && h[0] == 0x89 && h[1] == 0x50 && h[2] == 0x4E && h[3] == 0x47,
            ".gif" => len >= 4 && h[0] == 'G' && h[1] == 'I' && h[2] == 'F' && h[3] == '8',
            ".webp" => len >= 12 && h[0] == 'R' && h[1] == 'I' && h[2] == 'F' && h[3] == 'F'
                       && h[8] == 'W' && h[9] == 'E' && h[10] == 'B' && h[11] == 'P',
            ".pdf" => len >= 4 && h[0] == '%' && h[1] == 'P' && h[2] == 'D' && h[3] == 'F',
            ".mp4" => len >= 12 && h[4] == 'f' && h[5] == 't' && h[6] == 'y' && h[7] == 'p',
            ".webm" => len >= 4 && h[0] == 0x1A && h[1] == 0x45 && h[2] == 0xDF && h[3] == 0xA3,
            ".docx" or ".xlsx" or ".pptx" or ".zip" => Zip(),
            ".doc" or ".xls" or ".ppt" => Ole(),
            ".rar" => len >= 4 && h[0] == 'R' && h[1] == 'a' && h[2] == 'r' && h[3] == '!',
            ".7z" => len >= 6 && h[0] == 0x37 && h[1] == 0x7A && h[2] == 0xBC && h[3] == 0xAF && h[4] == 0x27 && h[5] == 0x1C,
            // Văn bản thuần: không có byte NUL trong phần đầu (loại file nhị phân đội lốt .txt).
            ".txt" or ".csv" => !h.Take(len).Contains((byte)0),
            _ => false
        };
    }

    /// <summary>Provider MIME cho static files, bổ sung các đuôi mà ASP.NET Core chưa biết (.rar, .7z).</summary>
    public static FileExtensionContentTypeProvider CreateContentTypeProvider()
    {
        var p = new FileExtensionContentTypeProvider();
        p.Mappings[".rar"] = "application/vnd.rar";
        p.Mappings[".7z"] = "application/x-7z-compressed";
        return p;
    }
}
