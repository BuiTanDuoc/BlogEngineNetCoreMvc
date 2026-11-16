using System.ComponentModel.DataAnnotations;
using BlogEngine.Web.Data.Entities;
using BlogEngine.Web.Infrastructure;

namespace BlogEngine.Web.Models;

public class NameSlug
{
    public string Name { get; set; } = "";
    public string Slug { get; set; } = "";
}

/// <summary>Dữ liệu tối thiểu của bài viết để render danh sách (tránh load cả cây entity).</summary>
public class PostSummary
{
    public int Id { get; set; }
    public string Title { get; set; } = "";
    public string Slug { get; set; } = "";
    public string? Description { get; set; }
    public string Content { get; set; } = "";
    public DateTime DateCreated { get; set; }
    public string AuthorUserName { get; set; } = "";
    public string? AuthorDisplayName { get; set; }
    public bool IsCommentEnabled { get; set; }
    public bool IsFeatured { get; set; }
    public int CommentCount { get; set; }
    public double Rating { get; set; }
    public int Raters { get; set; }
    public List<NameSlug> Categories { get; set; } = new();
    public List<NameSlug> Tags { get; set; } = new();

    public string AuthorName => string.IsNullOrWhiteSpace(AuthorDisplayName) ? AuthorUserName : AuthorDisplayName!;
    public int ReadingMinutes => Content.ReadingMinutes();
    /// <summary>Ảnh đầu tiên trong bài (nếu có) — thumbnail cho danh sách bài viết (cả hai giao diện).</summary>
    public string? ImageUrl => Content.FirstImageUrl();
}

public static class PostQueries
{
    /// <summary>Bài đã publish, chưa xoá và đã đến giờ đăng.</summary>
    public static IQueryable<Post> Visible(this IQueryable<Post> query)
    {
        var now = DateTime.UtcNow;
        return query.Where(p => p.IsPublished && !p.IsDeleted && p.DateCreated <= now);
    }

    public static IQueryable<PostSummary> ToSummary(this IQueryable<Post> query) =>
        query.Select(p => new PostSummary
        {
            Id = p.Id,
            Title = p.Title,
            Slug = p.Slug,
            Description = p.Description,
            Content = p.Content,
            DateCreated = p.DateCreated,
            AuthorUserName = p.Author!.UserName,
            AuthorDisplayName = p.Author!.DisplayName,
            IsCommentEnabled = p.IsCommentEnabled,
            IsFeatured = p.IsFeatured,
            Rating = p.Rating,
            Raters = p.Raters,
            CommentCount = p.Comments.Count(c => c.IsApproved && !c.IsSpam && !c.IsDeleted),
            Categories = p.Categories.Select(c => new NameSlug { Name = c.Name, Slug = c.Slug }).ToList(),
            Tags = p.Tags.Select(t => new NameSlug { Name = t.Name, Slug = t.Slug }).ToList()
        });
}

public class PostListViewModel
{
    public required PagedList<PostSummary> Posts { get; init; }
    public string? Heading { get; init; }
    public string? SubHeading { get; init; }
    public string? EmptyMessage { get; init; }
}

public class CommentNode
{
    public required Comment Comment { get; init; }
    public List<CommentNode> Replies { get; } = new();
}

public class CommentInput
{
    [Required(ErrorMessage = "The {0} field is required."), StringLength(100, ErrorMessage = "The field {0} must be a string with a maximum length of {1}.")]
    [Display(Name = "Name")]
    public string Name { get; set; } = "";

    [Required(ErrorMessage = "The {0} field is required."), EmailAddress, StringLength(255, ErrorMessage = "The field {0} must be a string with a maximum length of {1}.")]
    [Display(Name = "Email (not published)")]
    public string Email { get; set; } = "";

    [StringLength(255, ErrorMessage = "The field {0} must be a string with a maximum length of {1}.")]
    [Display(Name = "Website")]
    public string? Website { get; set; }

    [Required(ErrorMessage = "The {0} field is required."), StringLength(4000, MinimumLength = 2, ErrorMessage = "The field {0} must be a string with a minimum length of {2} and a maximum length of {1}.")]
    [Display(Name = "Comment")]
    public string Content { get; set; } = "";

    public int? ParentId { get; set; }

    /// <summary>Honeypot: người thật không thấy ô này, bot thường điền vào.</summary>
    public string? Website2 { get; set; }
}

public class PostDetailsViewModel
{
    public required Post Post { get; init; }
    public Post? Previous { get; init; }
    public Post? Next { get; init; }
    public List<CommentNode> Comments { get; init; } = new();
    public int CommentCount { get; init; }
    public List<Post> Related { get; init; } = new();
    public bool CanComment { get; init; }
    public CommentInput Input { get; init; } = new();
}

public class ArchiveViewModel
{
    public class Entry
    {
        public int Id { get; set; }
        public string Title { get; set; } = "";
        public string Slug { get; set; } = "";
        public DateTime DateCreated { get; set; }
        public int CommentCount { get; set; }
    }

    public List<IGrouping<(int Year, int Month), Entry>> Groups { get; init; } = new();
}

public class ContactInput
{
    [Required(ErrorMessage = "The {0} field is required."), StringLength(100, ErrorMessage = "The field {0} must be a string with a maximum length of {1}."), Display(Name = "Name")] public string Name { get; set; } = "";
    [Required(ErrorMessage = "The {0} field is required."), EmailAddress, StringLength(255, ErrorMessage = "The field {0} must be a string with a maximum length of {1}."), Display(Name = "Email")] public string Email { get; set; } = "";
    [StringLength(255, ErrorMessage = "The field {0} must be a string with a maximum length of {1}."), Display(Name = "Subject")] public string? Subject { get; set; }
    [Required(ErrorMessage = "The {0} field is required."), StringLength(5000, MinimumLength = 5, ErrorMessage = "The field {0} must be a string with a minimum length of {2} and a maximum length of {1}."), Display(Name = "Message")] public string Message { get; set; } = "";
    public string? Website2 { get; set; }
}

public class LoginInput
{
    [Required(ErrorMessage = "The {0} field is required."), Display(Name = "Username")] public string UserName { get; set; } = "";
    [Required(ErrorMessage = "The {0} field is required."), DataType(DataType.Password)] public string Password { get; set; } = "";
    [Display(Name = "Remember me")] public bool RememberMe { get; set; }
    public string? ReturnUrl { get; set; }
}

public class ChangePasswordInput
{
    [Required(ErrorMessage = "The {0} field is required."), DataType(DataType.Password), Display(Name = "Current password")]
    public string CurrentPassword { get; set; } = "";

    [Required(ErrorMessage = "The {0} field is required."), StringLength(100, MinimumLength = 8, ErrorMessage = "The field {0} must be a string with a minimum length of {2} and a maximum length of {1}."), DataType(DataType.Password), Display(Name = "New password")]
    public string NewPassword { get; set; } = "";

    [Required(ErrorMessage = "The {0} field is required."), DataType(DataType.Password), Compare(nameof(NewPassword), ErrorMessage = "'{0}' and '{1}' do not match."), Display(Name = "Confirm new password")]
    public string ConfirmPassword { get; set; } = "";
}

public class CategoryCard
{
    public string Name { get; init; } = "";
    public string Slug { get; init; } = "";
    public string? Description { get; init; }
    public int Count { get; init; }
}

public class HomeViewModel
{
    public List<CategoryCard> Categories { get; init; } = new();
    /// <summary>Bài được đánh dấu Featured (tối đa vài bài mới nhất) — hiển thị riêng ở trang chủ.</summary>
    public List<PostSummary> Featured { get; init; } = new();
    public List<PostSummary> Latest { get; init; } = new();
    public int TotalPosts { get; init; }
}

/// <summary>Trang chủ giao diện Indigo: dải bài nổi bật + danh sách bài có phân trang.</summary>
public class IndigoHomeViewModel
{
    public List<PostSummary> Featured { get; init; } = new();
    public required PostListViewModel Posts { get; init; }
}

public class FooterViewModel
{
    public List<CategoryCard> Categories { get; init; } = new();
    public List<(string Title, string Slug)> RecentPosts { get; init; } = new();
    public List<BlogRollItem> BlogRoll { get; init; } = new();
}

/// <summary>Dữ liệu cho SidebarViewComponent (Views/Shared/Components/Sidebar/Default.cshtml).</summary>
public class SidebarViewModel
{
    public List<Category> Categories { get; init; } = new();
    public Dictionary<int, int> CategoryCounts { get; init; } = new();
    public List<(string Title, string Slug)> RecentPosts { get; init; } = new();
    public List<(string Author, string PostTitle, string PostSlug, int CommentId, string Excerpt)> RecentComments { get; init; } = new();
    public List<(string Name, string Slug, int Count)> Tags { get; init; } = new();
    public List<(int Year, int Month, int Count)> Months { get; init; } = new();
    public List<BlogRollItem> BlogRoll { get; init; } = new();
}
