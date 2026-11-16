namespace BlogEngine.Web.Data.Entities;

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
