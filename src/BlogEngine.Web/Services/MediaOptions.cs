namespace BlogEngine.Web.Services;

/// <summary>Cấu hình upload media (section "Media" trong appsettings.json).</summary>
public class MediaOptions
{
    /// <summary>Dung lượng tối đa của một video tải lên (MB). Ảnh và PDF vẫn giới hạn 10 MB.</summary>
    public int MaxVideoMb { get; set; } = 200;

    public long MaxVideoBytes => Math.Max(1, MaxVideoMb) * 1024L * 1024L;
}
