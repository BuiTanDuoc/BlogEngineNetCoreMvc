namespace BlogEngine.Web.Data.Entities;

/// <summary>
/// Phạm vi xem của bài viết / trang (độc lập với trạng thái xuất bản IsPublished).
/// Giá trị được lưu dạng số trong DB — KHÔNG đổi số thứ tự các giá trị đã có.
/// </summary>
public enum ContentVisibility
{
    /// <summary>Công khai: ai cũng xem được (kể cả khách chưa đăng nhập, RSS, sitemap).</summary>
    Public = 0,

    /// <summary>Riêng tư: chỉ người dùng đã đăng nhập mới xem được.</summary>
    Private = 1,

    /// <summary>Cá nhân: chỉ chính người viết mới xem được (kể cả Administrators cũng không).</summary>
    Personal = 2
}

public class Post
{
    public int Id { get; set; }
    public string Title { get; set; } = "";
    public string Slug { get; set; } = "";

    /// <summary>Tóm tắt hiển thị ở danh sách bài viết / meta description.</summary>
    public string? Description { get; set; }
    public string Content { get; set; } = "";

    public DateTime DateCreated { get; set; } = DateTime.UtcNow;
    public DateTime DateModified { get; set; } = DateTime.UtcNow;

    public int AuthorId { get; set; }
    public User? Author { get; set; }

    public bool IsPublished { get; set; }

    /// <summary>Ai được xem bài này (Public / Private / Personal). Mặc định Public.</summary>
    public ContentVisibility Visibility { get; set; } = ContentVisibility.Public;

    public bool IsCommentEnabled { get; set; } = true;

    /// <summary>Bài nổi bật: được ghim ở mục "Featured" trên trang chủ.</summary>
    public bool IsFeatured { get; set; }

    /// <summary>Đã chuyển vào thùng rác (soft delete, giống BlogEngine gốc).</summary>
    public bool IsDeleted { get; set; }

    public int Raters { get; set; }
    public double Rating { get; set; }

    public List<Category> Categories { get; set; } = new();
    public List<Tag> Tags { get; set; } = new();
    public List<Comment> Comments { get; set; } = new();
}

public class Page
{
    public int Id { get; set; }
    public string Title { get; set; } = "";
    public string Slug { get; set; } = "";
    public string? Description { get; set; }
    public string Content { get; set; } = "";
    public string? Keywords { get; set; }

    public DateTime DateCreated { get; set; } = DateTime.UtcNow;
    public DateTime DateModified { get; set; } = DateTime.UtcNow;

    public bool IsPublished { get; set; }

    /// <summary>Ai được xem trang này (Public / Private / Personal). Mặc định Public.</summary>
    public ContentVisibility Visibility { get; set; } = ContentVisibility.Public;

    /// <summary>
    /// Người tạo trang. Cần cho Visibility = Personal. Trang có sẵn từ trước khi có tính năng này
    /// là null (và luôn Public); được gán khi người dùng chuyển trang sang Personal.
    /// </summary>
    public int? AuthorId { get; set; }
    public User? Author { get; set; }

    public bool IsFrontPage { get; set; }
    public bool ShowInList { get; set; } = true;
    public bool IsDeleted { get; set; }

    public int? ParentId { get; set; }
    public Page? Parent { get; set; }
    public List<Page> Children { get; set; } = new();
}

public class Category
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Slug { get; set; } = "";
    public string? Description { get; set; }

    public int? ParentId { get; set; }
    public Category? Parent { get; set; }
    public List<Category> Children { get; set; } = new();

    public List<Post> Posts { get; set; } = new();
}

public class Tag
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Slug { get; set; } = "";
    public List<Post> Posts { get; set; } = new();
}

public class Comment
{
    public int Id { get; set; }

    public int PostId { get; set; }
    public Post? Post { get; set; }

    /// <summary>Trả lời cho comment khác (comment lồng nhau).</summary>
    public int? ParentId { get; set; }
    public Comment? Parent { get; set; }
    public List<Comment> Replies { get; set; } = new();

    public DateTime Date { get; set; } = DateTime.UtcNow;
    public string Author { get; set; } = "";
    public string? Email { get; set; }
    public string? Website { get; set; }
    public string Content { get; set; } = "";
    public string? Ip { get; set; }

    public bool IsApproved { get; set; }
    public bool IsSpam { get; set; }
    public bool IsDeleted { get; set; }
    public string? ModeratedBy { get; set; }
}
