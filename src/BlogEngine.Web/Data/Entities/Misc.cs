namespace BlogEngine.Web.Data.Entities;

/// <summary>Cặp key/value cho cấu hình blog (tương đương bảng be_Settings).</summary>
public class Setting
{
    public string Name { get; set; } = "";
    public string? Value { get; set; }
}

public class BlogRollItem
{
    public int Id { get; set; }
    public string Title { get; set; } = "";
    public string? Description { get; set; }
    public string BlogUrl { get; set; } = "";
    public string? FeedUrl { get; set; }
    public string? Xfn { get; set; }
    public int SortIndex { get; set; }
}

public class ContactMessage
{
    public int Id { get; set; }
    public DateTime Date { get; set; } = DateTime.UtcNow;
    public string Name { get; set; } = "";
    public string Email { get; set; } = "";
    public string? Subject { get; set; }
    public string Message { get; set; } = "";
    public string? Ip { get; set; }
    public bool IsRead { get; set; }
}
