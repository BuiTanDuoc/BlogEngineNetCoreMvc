namespace BlogEngine.Web.Data.Entities;

public static class RoleNames
{
    public const string Administrators = "Administrators";
    public const string Editors = "Editors";
}

public class User
{
    public int Id { get; set; }
    public string UserName { get; set; } = "";
    public string? DisplayName { get; set; }
    public string? EmailAddress { get; set; }

    /// <summary>
    /// Hash của ASP.NET Core Identity (PBKDF2). Với dữ liệu migrate từ BlogEngine cũ,
    /// giá trị có tiền tố "LEGACY:" (SHA-256 base64) và được nâng cấp tự động ở lần đăng nhập đầu tiên.
    /// </summary>
    public string PasswordHash { get; set; } = "";

    public bool IsActive { get; set; } = true;
    public bool MustChangePassword { get; set; }

    public string? Bio { get; set; }
    public string? PhotoUrl { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastLoginTime { get; set; }

    public int FailedLoginCount { get; set; }
    public DateTime? LockoutEnd { get; set; }

    public List<Role> Roles { get; set; } = new();
    public List<Post> Posts { get; set; } = new();

    public string Name => string.IsNullOrWhiteSpace(DisplayName) ? UserName : DisplayName!;
}

public class Role
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public List<User> Users { get; set; } = new();
}
