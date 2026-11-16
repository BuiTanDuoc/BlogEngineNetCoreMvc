using BlogEngine.Web.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BlogEngine.Web.Migrations
{
    /// <summary>Thêm cột Posts.IsFeatured (bài nổi bật hiển thị ở trang chủ).</summary>
    [DbContext(typeof(BlogDbContext))]
    [Migration("20261116090000_AddPostIsFeatured")]
    public partial class AddPostIsFeatured : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsFeatured",
                table: "Posts",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsFeatured",
                table: "Posts");
        }
    }
}
