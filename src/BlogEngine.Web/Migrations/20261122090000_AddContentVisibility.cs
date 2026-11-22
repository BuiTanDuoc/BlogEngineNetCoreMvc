using BlogEngine.Web.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BlogEngine.Web.Migrations
{
    /// <summary>
    /// Phạm vi xem của bài viết/trang (Visibility: 0=Public, 1=Private, 2=Personal) và Pages.AuthorId (người tạo trang,
    /// cần cho trang Personal). Dữ liệu cũ giữ nguyên hành vi: mọi bài/trang là Public, trang cũ chưa có người tạo (NULL).
    /// </summary>
    [DbContext(typeof(BlogDbContext))]
    [Migration("20261122090000_AddContentVisibility")]
    public partial class AddContentVisibility : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Visibility",
                table: "Posts",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "AuthorId",
                table: "Pages",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Visibility",
                table: "Pages",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_Pages_AuthorId",
                table: "Pages",
                column: "AuthorId");

            migrationBuilder.AddForeignKey(
                name: "FK_Pages_Users_AuthorId",
                table: "Pages",
                column: "AuthorId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Pages_Users_AuthorId",
                table: "Pages");

            migrationBuilder.DropIndex(
                name: "IX_Pages_AuthorId",
                table: "Pages");

            migrationBuilder.DropColumn(
                name: "Visibility",
                table: "Pages");

            migrationBuilder.DropColumn(
                name: "AuthorId",
                table: "Pages");

            migrationBuilder.DropColumn(
                name: "Visibility",
                table: "Posts");
        }
    }
}
