using System.ComponentModel.DataAnnotations;

namespace BlogEngine.Web.Services;

/// <summary>
/// Cấu hình blog. Mỗi property được lưu thành một dòng trong bảng Settings
/// (key = tên property viết thường, trùng với tên key của bảng be_Settings cũ).
/// </summary>
public class BlogSettings
{
    [Required(ErrorMessage = "The {0} field is required."), StringLength(100, ErrorMessage = "The field {0} must be a string with a maximum length of {1}.")]
    [Display(Name = "Blog name")]
    public string Name { get; set; } = "My Blog";

    [StringLength(500, ErrorMessage = "The field {0} must be a string with a maximum length of {1}.")]
    [Display(Name = "Description")]
    public string Description { get; set; } = "Short description of the blog";

    [EmailAddress, Display(Name = "Notification email")]
    public string Email { get; set; } = "";

    [Range(1, 100, ErrorMessage = "The field {0} must be between {1} and {2}."), Display(Name = "Posts per page")]
    public int PostsPerPage { get; set; } = 10;

    [Range(1, 100, ErrorMessage = "The field {0} must be between {1} and {2}."), Display(Name = "Posts per feed")]
    public int PostsPerFeed { get; set; } = 10;

    [Range(1, 50, ErrorMessage = "The field {0} must be between {1} and {2}."), Display(Name = "Recent posts in sidebar")]
    public int NumberOfRecentPosts { get; set; } = 10;

    [Range(1, 50, ErrorMessage = "The field {0} must be between {1} and {2}."), Display(Name = "Recent comments in sidebar")]
    public int NumberOfRecentComments { get; set; } = 10;

    [Display(Name = "Enable comments")]
    public bool IsCommentsEnabled { get; set; } = true;

    [Display(Name = "Moderate comments before publishing")]
    public bool EnableCommentsModeration { get; set; }

    [Range(0, 3650, ErrorMessage = "The field {0} must be between {1} and {2}."), Display(Name = "Close comments after N days (0 = never)")]
    public int DaysCommentsAreEnabled { get; set; }

    [Display(Name = "Show Gravatar avatars")]
    public bool EnableGravatar { get; set; } = true;

    [Display(Name = "Enable post ratings")]
    public bool EnableRating { get; set; } = true;

    [Display(Name = "Show related posts")]
    public bool EnableRelatedPosts { get; set; } = true;

    [Display(Name = "Email me when a comment is added")]
    public bool SendMailOnComment { get; set; } = true;

    [StringLength(50, ErrorMessage = "The field {0} must be a string with a maximum length of {1}."), Display(Name = "Email subject prefix")]
    public string EmailSubjectPrefix { get; set; } = "Weblog";

    [Display(Name = "Time zone (IANA or Windows id)")]
    public string TimeZoneId { get; set; } = "UTC";

    /// <summary>Ngôn ngữ giao diện mặc định ("en" hoặc "vi"). Người dùng có thể tự chọn lại bằng nút đổi ngôn ngữ.</summary>
    [Display(Name = "Default language")]
    public string Culture { get; set; } = "en";

    [Display(Name = "Contact form intro (HTML)")]
    public string ContactFormMessage { get; set; } = "<p>I will answer the mail as soon as I can.</p>";

    [Display(Name = "Contact form thank-you message (HTML)")]
    public string ContactThankMessage { get; set; } = "<h1>Thank you</h1><p>The message was sent.</p>";

    /// <summary>Giao diện công khai: "indigo" (mặc định), "blk" hoặc "paper". Xem <see cref="SiteThemes"/>.</summary>
    [Display(Name = "Theme")]
    public string Theme { get; set; } = SiteThemes.Indigo;

    [Display(Name = "Facebook URL")]
    public string SocialFacebook { get; set; } = "";

    [Display(Name = "YouTube URL")]
    public string SocialYoutube { get; set; } = "";

    [Display(Name = "Chat URL")]
    public string SocialChat { get; set; } = "";

    /// <summary>Getter-only nên SettingsService (chỉ lưu property có setter) bỏ qua.</summary>
    public bool IsIndigo => string.Equals(Theme, SiteThemes.Indigo, StringComparison.OrdinalIgnoreCase);

    /// <summary>Giao diện BLK (phong cách BLK Design System Pro: nền tối, card phát sáng, nút gradient).</summary>
    public bool IsBlk => string.Equals(Theme, SiteThemes.Blk, StringComparison.OrdinalIgnoreCase);

    [Display(Name = "Tracking script (inserted before </body>)")]
    public string TrackingScript { get; set; } = "";

    public BlogSettings Clone() => (BlogSettings)MemberwiseClone();
}

/// <summary>Danh sách giao diện công khai có thể chọn trong Admin &gt; Settings.</summary>
public static class SiteThemes
{
    public const string Paper = "paper";
    public const string Indigo = "indigo";
    public const string Blk = "blk";

    public static readonly IReadOnlyList<(string Key, string Name)> All = new[]
    {
        (Indigo, "Indigo"),
        (Blk, "BLK Design"),
        (Paper, "Paper & Terminal")
    };

    public static bool IsValid(string? key) =>
        All.Any(t => string.Equals(t.Key, key, StringComparison.OrdinalIgnoreCase));
}
