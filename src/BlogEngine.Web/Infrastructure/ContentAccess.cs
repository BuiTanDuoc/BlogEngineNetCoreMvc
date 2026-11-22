using System.Security.Claims;
using BlogEngine.Web.Data.Entities;

namespace BlogEngine.Web.Infrastructure;

/// <summary>
/// Quy tắc "ai được xem bài viết / trang" — MỘT chỗ duy nhất.
/// <list type="bullet">
///   <item><b>Public</b>: mọi người (kể cả khách, RSS, sitemap).</item>
///   <item><b>Private</b>: mọi người dùng đã đăng nhập (kể cả tài khoản không có role).</item>
///   <item><b>Personal</b>: chỉ chính người viết. Administrators cũng không xem được nội dung của người khác.</item>
/// </list>
/// Visibility chỉ quyết định "ai", còn việc bài đã xuất bản / hẹn giờ / nháp vẫn do IsPublished + DateCreated quyết định.
/// Khi sửa quy tắc ở đây, nhớ sửa luôn các biểu thức EF tương ứng (PostQueries.Visible, PageQueries.Visible,
/// PostQueries.VisibleCategoryCards, SidebarViewComponent) vì EF không gọi được hàm C# trong câu truy vấn.
/// </summary>
public static class ContentAccess
{
    public static bool IsSignedIn(this ClaimsPrincipal? user) => user?.Identity?.IsAuthenticated == true;

    /// <summary>Người xem này có được xem nội dung có phạm vi <paramref name="visibility"/> do <paramref name="authorId"/> viết không.</summary>
    public static bool CanView(this ClaimsPrincipal? user, ContentVisibility visibility, int? authorId) => visibility switch
    {
        ContentVisibility.Public => true,
        ContentVisibility.Private => user.IsSignedIn(),
        ContentVisibility.Personal => user.IsSignedIn() && authorId is > 0 && user!.GetUserId() == authorId,
        _ => false
    };
}
