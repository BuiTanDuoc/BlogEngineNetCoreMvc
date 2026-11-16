using BlogEngine.Web.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace BlogEngine.Web.Data;

public class BlogDbContext : DbContext
{
    public BlogDbContext(DbContextOptions<BlogDbContext> options) : base(options) { }

    public DbSet<Post> Posts => Set<Post>();
    public DbSet<Page> Pages => Set<Page>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Tag> Tags => Set<Tag>();
    public DbSet<Comment> Comments => Set<Comment>();
    public DbSet<User> Users => Set<User>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<Setting> Settings => Set<Setting>();
    public DbSet<BlogRollItem> BlogRollItems => Set<BlogRollItem>();
    public DbSet<ContactMessage> ContactMessages => Set<ContactMessage>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<Post>(e =>
        {
            e.ToTable("Posts");
            e.Property(x => x.Title).HasMaxLength(255).IsRequired();
            e.Property(x => x.Slug).HasMaxLength(255).IsRequired();
            e.Property(x => x.Content).IsRequired();
            e.HasIndex(x => x.Slug).IsUnique();
            e.HasIndex(x => new { x.IsPublished, x.IsDeleted, x.DateCreated });

            e.HasOne(x => x.Author).WithMany(u => u.Posts)
                .HasForeignKey(x => x.AuthorId).OnDelete(DeleteBehavior.Restrict);

            e.HasMany(x => x.Categories).WithMany(c => c.Posts).UsingEntity(j => j.ToTable("PostCategories"));
            e.HasMany(x => x.Tags).WithMany(t => t.Posts).UsingEntity(j => j.ToTable("PostTags"));
        });

        b.Entity<Page>(e =>
        {
            e.ToTable("Pages");
            e.Property(x => x.Title).HasMaxLength(255).IsRequired();
            e.Property(x => x.Slug).HasMaxLength(255).IsRequired();
            e.Property(x => x.Keywords).HasMaxLength(500);
            e.HasIndex(x => x.Slug).IsUnique();
            e.HasOne(x => x.Parent).WithMany(p => p.Children)
                .HasForeignKey(x => x.ParentId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<Category>(e =>
        {
            e.ToTable("Categories");
            e.Property(x => x.Name).HasMaxLength(100).IsRequired();
            e.Property(x => x.Slug).HasMaxLength(120).IsRequired();
            e.Property(x => x.Description).HasMaxLength(500);
            e.HasIndex(x => x.Slug).IsUnique();
            e.HasOne(x => x.Parent).WithMany(p => p.Children)
                .HasForeignKey(x => x.ParentId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<Tag>(e =>
        {
            e.ToTable("Tags");
            e.Property(x => x.Name).HasMaxLength(50).IsRequired();
            e.Property(x => x.Slug).HasMaxLength(80).IsRequired();
            e.HasIndex(x => x.Slug).IsUnique();
        });

        b.Entity<Comment>(e =>
        {
            e.ToTable("Comments");
            e.Property(x => x.Author).HasMaxLength(100).IsRequired();
            e.Property(x => x.Email).HasMaxLength(255);
            e.Property(x => x.Website).HasMaxLength(255);
            e.Property(x => x.Ip).HasMaxLength(50);
            e.Property(x => x.ModeratedBy).HasMaxLength(100);
            e.Property(x => x.Content).IsRequired();
            e.HasIndex(x => new { x.PostId, x.IsApproved, x.IsSpam, x.IsDeleted });

            e.HasOne(x => x.Post).WithMany(p => p.Comments)
                .HasForeignKey(x => x.PostId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Parent).WithMany(c => c.Replies)
                .HasForeignKey(x => x.ParentId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<User>(e =>
        {
            e.ToTable("Users");
            e.Property(x => x.UserName).HasMaxLength(100).IsRequired();
            e.Property(x => x.DisplayName).HasMaxLength(100);
            e.Property(x => x.EmailAddress).HasMaxLength(255);
            e.Property(x => x.PasswordHash).HasMaxLength(500).IsRequired();
            e.Property(x => x.PhotoUrl).HasMaxLength(500);
            e.HasIndex(x => x.UserName).IsUnique();

            e.HasMany(x => x.Roles).WithMany(r => r.Users).UsingEntity(j => j.ToTable("UserRoles"));
        });

        b.Entity<Role>(e =>
        {
            e.ToTable("Roles");
            e.Property(x => x.Name).HasMaxLength(100).IsRequired();
            e.HasIndex(x => x.Name).IsUnique();
        });

        b.Entity<Setting>(e =>
        {
            e.ToTable("Settings");
            e.HasKey(x => x.Name);
            e.Property(x => x.Name).HasMaxLength(100);
        });

        b.Entity<BlogRollItem>(e =>
        {
            e.ToTable("BlogRollItems");
            e.Property(x => x.Title).HasMaxLength(255).IsRequired();
            e.Property(x => x.BlogUrl).HasMaxLength(500).IsRequired();
            e.Property(x => x.FeedUrl).HasMaxLength(500);
            e.Property(x => x.Xfn).HasMaxLength(100);
        });

        b.Entity<ContactMessage>(e =>
        {
            e.ToTable("ContactMessages");
            e.Property(x => x.Name).HasMaxLength(100).IsRequired();
            e.Property(x => x.Email).HasMaxLength(255).IsRequired();
            e.Property(x => x.Subject).HasMaxLength(255);
            e.Property(x => x.Ip).HasMaxLength(50);
            e.Property(x => x.Message).IsRequired();
        });
    }
}
