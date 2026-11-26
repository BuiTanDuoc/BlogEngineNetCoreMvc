namespace BlogEngine.Web.Services;

/// <summary>Cấu hình upload media (section "Media" trong appsettings.json).</summary>
public class MediaOptions
{
    /// <summary>Dung lượng tối đa của một video tải lên (MB). Ảnh và PDF vẫn giới hạn 10 MB.</summary>
    public int MaxVideoMb { get; set; } = 200;

    /// <summary>Dung lượng tối đa của file tài liệu/nén (zip, Office, PDF...) tải lên (MB).</summary>
    public int MaxFileMb { get; set; } = 50;

    public long MaxFileBytes => Math.Max(1, MaxFileMb) * 1024L * 1024L;

    /// <summary>Giới hạn body lớn nhất của một request upload.</summary>
    public long MaxRequestBytes => Math.Max(MaxVideoBytes, MaxFileBytes) + 1024 * 1024;

    public long MaxVideoBytes => Math.Max(1, MaxVideoMb) * 1024L * 1024L;
}
