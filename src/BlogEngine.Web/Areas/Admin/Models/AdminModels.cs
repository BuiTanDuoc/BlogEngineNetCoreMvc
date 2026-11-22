using System.ComponentModel.DataAnnotations;
using BlogEngine.Web.Data.Entities;
using BlogEngine.Web.Infrastructure;

namespace BlogEngine.Web.Areas.Admin.Models;

public class DashboardViewModel
{
    public int PublishedPosts { get; init; }
    public int DraftPosts { get; init; }
    public int Pages { get; init; }
    public int Comments { get; init; }
    public int PendingComments { get; init; }
    public int SpamComments { get; init; }
    public int Users { get; init; }
    public int UnreadMessages { get; init; }
    public List<Post> RecentPosts { get; init; } = new();
    public List<Comment> RecentComments { get; init; } = new();
}

public class PostEditModel
{
    public int? Id { get; set; }

    [Required(ErrorMessage = "The {0} field is required."), StringLength(255, ErrorMessage = "The field {0} must be a string with a maximum length of {1}.")]
    [Display(Name = "Title")]
    public string Title { get; set; } = "";

    [StringLength(200, ErrorMessage = "The field {0} must be a string with a maximum length of {1}."), Display(Name = "Slug (URL)")]
    public string? Slug { get; set; }

    [Display(Name = "Description / excerpt")]
    public string? Description { get; set; }

    [Required(ErrorMessage = "The {0} field is required.")]
    [Display(Name = "Content")]
    public string Content { get; set; } = "";

    public List<int> CategoryIds { get; set; } = new();

    [Display(Name = "Tags (comma separated)")]
    public string? Tags { get; set; }

    [Display(Name = "Published")]
    public bool IsPublished { get; set; }

    [Display(Name = "Visibility")]
    public ContentVisibility Visibility { get; set; } = ContentVisibility.Public;

    [Display(Name = "Allow comments")]
    public bool IsCommentEnabled { get; set; } = true;

    [Display(Name = "Featured")]
    public bool IsFeatured { get; set; }

    /// <summary>Ngày đăng theo múi giờ của blog. Để trống = bây giờ. Đặt ngày tương lai để hẹn giờ.</summary>
    [Display(Name = "Publish date")]
    public DateTime? DateCreated { get; set; }
}

public class PostListViewModel
{
    public required PagedList<Post> Posts { get; init; }
    public string Status { get; init; } = "all";
    public string? Query { get; init; }
    public int PublishedCount { get; init; }
    public int DraftCount { get; init; }
    public int TrashCount { get; init; }
}

public class PageEditModel
{
    public int? Id { get; set; }

    [Required(ErrorMessage = "The {0} field is required."), StringLength(255, ErrorMessage = "The field {0} must be a string with a maximum length of {1}.")]
    [Display(Name = "Title")]
    public string Title { get; set; } = "";

    [StringLength(200, ErrorMessage = "The field {0} must be a string with a maximum length of {1}."), Display(Name = "Slug (URL)")]
    public string? Slug { get; set; }

    [Display(Name = "Description")]
    public string? Description { get; set; }

    [Required(ErrorMessage = "The {0} field is required.")]
    [Display(Name = "Content")]
    public string Content { get; set; } = "";

    [StringLength(500, ErrorMessage = "The field {0} must be a string with a maximum length of {1}.")]
    [Display(Name = "Keywords")]
    public string? Keywords { get; set; }

    [Display(Name = "Published")] public bool IsPublished { get; set; }
    [Display(Name = "Visibility")] public ContentVisibility Visibility { get; set; } = ContentVisibility.Public;
    [Display(Name = "Use as front page")] public bool IsFrontPage { get; set; }
    [Display(Name = "Show in menu")] public bool ShowInList { get; set; } = true;
    [Display(Name = "Parent page")] public int? ParentId { get; set; }
}

public class CategoryEditModel
{
    public int? Id { get; set; }

    [Required(ErrorMessage = "The {0} field is required."), StringLength(100, ErrorMessage = "The field {0} must be a string with a maximum length of {1}.")]
    [Display(Name = "Name")]
    public string Name { get; set; } = "";

    [StringLength(120, ErrorMessage = "The field {0} must be a string with a maximum length of {1}.")]
    [Display(Name = "Slug (URL)")]
    public string? Slug { get; set; }

    [StringLength(500, ErrorMessage = "The field {0} must be a string with a maximum length of {1}.")]
    [Display(Name = "Description")]
    public string? Description { get; set; }

    [Display(Name = "Parent category")]
    public int? ParentId { get; set; }
}

public class CommentListViewModel
{
    public required PagedList<Comment> Comments { get; init; }
    public Dictionary<int, (string Title, string Slug)> Posts { get; init; } = new();
    public string Filter { get; init; } = "pending";
    public int PendingCount { get; init; }
    public int ApprovedCount { get; init; }
    public int SpamCount { get; init; }
    public int TrashCount { get; init; }
}

public class UserEditModel
{
    public int? Id { get; set; }

    [Required(ErrorMessage = "The {0} field is required."), StringLength(100, ErrorMessage = "The field {0} must be a string with a maximum length of {1}.")]
    [RegularExpression(@"^[A-Za-z0-9._@\-]+$", ErrorMessage = "Use letters, digits and . _ - @ only.")]
    [Display(Name = "Username")]
    public string UserName { get; set; } = "";

    [StringLength(100, ErrorMessage = "The field {0} must be a string with a maximum length of {1}."), Display(Name = "Display name")]
    public string? DisplayName { get; set; }

    [EmailAddress, StringLength(255, ErrorMessage = "The field {0} must be a string with a maximum length of {1}.")]
    [Display(Name = "Email address")]
    public string? EmailAddress { get; set; }

    [StringLength(100, MinimumLength = 8, ErrorMessage = "The field {0} must be a string with a minimum length of {2} and a maximum length of {1}."), DataType(DataType.Password)]
    [Display(Name = "Password")]
    public string? Password { get; set; }

    [Display(Name = "Active")] public bool IsActive { get; set; } = true;

    [Display(Name = "Bio")]
    public string? Bio { get; set; }

    public List<string> Roles { get; set; } = new();
}

public class BlogRollEditModel
{
    public int? Id { get; set; }

    [Required(ErrorMessage = "The {0} field is required."), StringLength(255, ErrorMessage = "The field {0} must be a string with a maximum length of {1}."), Display(Name = "Title")] public string Title { get; set; } = "";
    [Required(ErrorMessage = "The {0} field is required."), Url, StringLength(500, ErrorMessage = "The field {0} must be a string with a maximum length of {1}."), Display(Name = "Blog URL")] public string BlogUrl { get; set; } = "";
    [Url, StringLength(500, ErrorMessage = "The field {0} must be a string with a maximum length of {1}."), Display(Name = "Feed URL")] public string? FeedUrl { get; set; }
    [StringLength(500, ErrorMessage = "The field {0} must be a string with a maximum length of {1}."), Display(Name = "Description")] public string? Description { get; set; }
    [StringLength(100, ErrorMessage = "The field {0} must be a string with a maximum length of {1}."), Display(Name = "XFN relationship")] public string? Xfn { get; set; }
    [Display(Name = "Sort order")] public int SortIndex { get; set; }
}

public class MediaFile
{
    public required string RelativePath { get; init; }
    public required string Url { get; init; }
    public long Size { get; init; }
    public DateTime Modified { get; init; }
    public bool IsImage { get; init; }
}
