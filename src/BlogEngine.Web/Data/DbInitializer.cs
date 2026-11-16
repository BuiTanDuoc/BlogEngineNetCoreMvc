using System.Security.Cryptography;
using BlogEngine.Web.Data.Entities;
using BlogEngine.Web.Infrastructure;
using BlogEngine.Web.Services;
using Microsoft.EntityFrameworkCore;

namespace BlogEngine.Web.Data;

public static class DbInitializer
{
    public static async Task InitializeAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var sp = scope.ServiceProvider;
        var db = sp.GetRequiredService<BlogDbContext>();
        var logger = sp.GetRequiredService<ILoggerFactory>().CreateLogger("DbInitializer");
        var config = sp.GetRequiredService<IConfiguration>();
        var passwords = sp.GetRequiredService<IPasswordService>();

        // Có migration thì áp dụng migration; chưa có thì tạo schema trực tiếp (tiện cho lần chạy đầu).
        if (db.Database.GetMigrations().Any())
            await db.Database.MigrateAsync();
        else
            await db.Database.EnsureCreatedAsync();

        await SeedRolesAsync(db);
        await SeedAdminAsync(db, passwords, config, logger);
        await SeedContentAsync(db);
        await NormalizeTaxonomySlugsAsync(db);
    }

    /// <summary>
    /// Dữ liệu chuyển từ BlogEngine cũ có slug tạm (có dấu/ký tự lạ) cho category và tag.
    /// Chuẩn hoá về ASCII một lần; link cũ dạng /category/Tên vẫn chạy nhờ fallback theo tên.
    /// </summary>
    private static async Task NormalizeTaxonomySlugsAsync(BlogDbContext db)
    {
        static bool IsClean(string slug) => slug.Length > 0 && slug.All(c => c is (>= 'a' and <= 'z') or (>= '0' and <= '9') or '-');

        var categories = await db.Categories.ToListAsync();
        if (categories.Any(c => !IsClean(c.Slug)))
        {
            var used = new HashSet<string>(categories.Where(c => IsClean(c.Slug)).Select(c => c.Slug));
            foreach (var c in categories.Where(c => !IsClean(c.Slug)))
            {
                var baseSlug = SlugHelper.Slugify(c.Name, 100);
                if (baseSlug.Length == 0) baseSlug = "category";
                var slug = baseSlug;
                for (var i = 2; !used.Add(slug); i++) slug = $"{baseSlug}-{i}";
                c.Slug = slug;
            }
            await db.SaveChangesAsync();
        }

        var tags = await db.Tags.ToListAsync();
        if (tags.Any(t => !IsClean(t.Slug)))
        {
            var used = new HashSet<string>(tags.Where(t => IsClean(t.Slug)).Select(t => t.Slug));
            foreach (var t in tags.Where(t => !IsClean(t.Slug)))
            {
                var baseSlug = SlugHelper.Slugify(t.Name, 80);
                if (baseSlug.Length == 0) baseSlug = "tag";
                var slug = baseSlug;
                for (var i = 2; !used.Add(slug); i++) slug = $"{baseSlug}-{i}";
                t.Slug = slug;
            }
            await db.SaveChangesAsync();
        }
    }

    private static async Task SeedRolesAsync(BlogDbContext db)
    {
        foreach (var name in new[] { RoleNames.Administrators, RoleNames.Editors })
        {
            if (!await db.Roles.AnyAsync(r => r.Name == name))
                db.Roles.Add(new Role { Name = name });
        }
        await db.SaveChangesAsync();
    }

    private static async Task SeedAdminAsync(BlogDbContext db, IPasswordService passwords, IConfiguration config, ILogger logger)
    {
        if (await db.Users.AnyAsync()) return;

        var userName = config["Seed:AdminUserName"];
        if (string.IsNullOrWhiteSpace(userName)) userName = "admin";

        var password = config["Seed:AdminPassword"];
        var generated = false;
        if (string.IsNullOrWhiteSpace(password))
        {
            password = GeneratePassword();
            generated = true;
        }

        var adminRole = await db.Roles.FirstAsync(r => r.Name == RoleNames.Administrators);
        var user = new User
        {
            UserName = userName!,
            DisplayName = "Administrator",
            EmailAddress = config["Seed:AdminEmail"].NullIfEmpty(),
            MustChangePassword = generated
        };
        user.PasswordHash = passwords.Hash(user, password!);
        user.Roles.Add(adminRole);
        db.Users.Add(user);
        await db.SaveChangesAsync();

        if (generated)
        {
            logger.LogWarning(
                "Đã tạo tài khoản quản trị '{User}' với mật khẩu tạm: {Password} — hãy đăng nhập và đổi mật khẩu ngay.",
                userName, password);
        }
        else
        {
            logger.LogInformation("Đã tạo tài khoản quản trị '{User}' từ cấu hình Seed.", userName);
        }
    }

    private static async Task SeedContentAsync(BlogDbContext db)
    {
        if (await db.Posts.AnyAsync() || await db.Categories.AnyAsync()) return;

        var author = await db.Users.OrderBy(u => u.Id).FirstAsync();
        var general = new Category { Name = "General", Slug = "general" };
        db.Categories.Add(general);

        db.Posts.Add(new Post
        {
            Title = "Welcome to your blog",
            Slug = "welcome-to-your-blog",
            Description = "Your new blog is up and running on ASP.NET Core.",
            Content = "<p>This is your first post. Sign in to the <a href=\"/admin\">admin area</a> to edit or delete it, " +
                      "write new posts, manage categories and moderate comments.</p>",
            IsPublished = true,
            AuthorId = author.Id,
            Categories = { general },
            Tags = { new Tag { Name = "welcome", Slug = "welcome" } }
        });

        await db.SaveChangesAsync();
    }

    private static string GeneratePassword()
    {
        const string alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnpqrstuvwxyz23456789";
        var chars = new char[16];
        for (var i = 0; i < chars.Length; i++)
            chars[i] = alphabet[RandomNumberGenerator.GetInt32(alphabet.Length)];
        return new string(chars);
    }
}
