# BlogEngine.Mvc — BlogEngine.NET chuyển sang ASP.NET Core MVC (.NET 10) + EF Core + SQL Server

Bản viết lại BlogEngine.NET (ASP.NET WebForms / .NET Framework 4.x) thành ứng dụng **ASP.NET Core MVC trên .NET 10**,
truy cập dữ liệu bằng **EF Core 10** và lưu trên **SQL Server**.

> Đây là bản **viết lại**, không phải chuyển đổi tự động: WebForms (`.aspx/.ascx/.master`), provider model,
> admin AngularJS và Web API 2 không có tương đương trực tiếp trên .NET hiện đại.
> Xem mục "Ánh xạ tính năng" để biết cái gì giữ, cái gì thay, cái gì bỏ.

## Chạy thử

Yêu cầu: .NET 10 SDK, SQL Server (LocalDB, Express hoặc bản đầy đủ).

```bash
cd src/BlogEngine.Web
# sửa ConnectionStrings:DefaultConnection trong appsettings.json (hoặc dùng user-secrets)
dotnet run
```

Lần chạy đầu ứng dụng tự tạo database, role (`Administrators`, `Editors`), một category và một bài viết mẫu.

**Tài khoản quản trị**: tên `admin`. Mật khẩu lấy từ `Seed:AdminPassword`; nếu để trống, ứng dụng sinh mật khẩu ngẫu nhiên,
**in một lần trong log** (cảnh báo `Đã tạo tài khoản quản trị...`) và bắt đổi mật khẩu ở lần đăng nhập đầu.
(Bản gốc dùng `admin/admin` — đã bỏ vì không an toàn.) Khu quản trị: `/admin`.

```bash
dotnet user-secrets set "Seed:AdminPassword" "mat-khau-cua-ban"     # tuỳ chọn, trước lần chạy đầu
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Server=...;Database=...;..."
```

<p align="center">
  <a href="https://github.com/BuiTanDuoc/BlogEngineNetCoreMvc/blob/Master/Images/ScreenShoot.jpg" alt="Adsun Vietkey"></a>
</p>

## EF Core migrations

Chưa kèm sẵn file migration (môi trường tạo project không có `dotnet-ef`/NuGet). Khi chưa có migration, ứng dụng dùng
`EnsureCreated` — tiện để chạy thử, **không** nên dùng cho production. Trước khi triển khai:

```bash
dotnet tool install --global dotnet-ef
cd src/BlogEngine.Web
dotnet ef migrations add InitialCreate
dotnet ef database update            # hoặc để ứng dụng tự Migrate khi khởi động
```

Khi đã có migration, `DbInitializer` tự gọi `Migrate()` thay cho `EnsureCreated()`.
Nếu database đã được tạo bằng `EnsureCreated` thì phải xoá đi tạo lại bằng migration (hai cách không dùng lẫn nhau).

## Chuyển dữ liệu từ BlogEngine cũ

`database/legacy-data-migration.sql` chuyển users, roles, settings, categories (giữ cây), posts, tags, comments (giữ lồng nhau),
pages, blogroll từ các bảng `be_*`. **Script chưa được chạy thử trên SQL Server thật** — đọc kỹ phần hướng dẫn đầu file,
chạy trên bản sao; mặc định kết thúc bằng `ROLLBACK` để bạn kiểm tra số lượng trước khi `COMMIT`.

- Mật khẩu cũ (SHA-256 không salt) vẫn đăng nhập được và tự nâng cấp lên PBKDF2 ở lần đăng nhập đầu.
- Link cũ được giữ: `/post/ten-bai.aspx`, `/page/x.aspx`, `/default.aspx`, `/syndication.axd` → redirect 301 sang URL mới.

## Phạm vi xem của bài viết / trang (Visibility)

Mỗi bài viết và mỗi trang có thêm trường **Visibility** (Admin → Posts/Pages → ô "Visibility" ở khung Publish):

| Giá trị | Ai xem được |
|---|---|
| `Public` (mặc định) | Mọi người, kể cả khách chưa đăng nhập; có trong RSS/Atom và sitemap |
| `Private` | Mọi người dùng **đã đăng nhập** (kể cả tài khoản không có role) |
| `Personal` | **Chỉ người viết** — Administrators cũng không xem/sửa được bài của người khác |

Quy tắc:

- Visibility chỉ trả lời "ai được xem"; việc bài đã xuất bản/hẹn giờ/nháp vẫn do `IsPublished` + ngày đăng quyết định.
- Khách mở bài/trang Private hoặc Personal sẽ được chuyển tới trang đăng nhập rồi quay lại đúng URL; người đã đăng nhập nhưng
  không có quyền nhận 404 (không lộ là bài Personal có tồn tại).
- Bài/trang không Public bị ẩn khỏi danh sách (trang chủ, chuyên mục, tag, tác giả, lưu trữ, tìm kiếm), sidebar, footer, menu trang,
  RSS/Atom, sitemap, "bài liên quan"/bài trước-sau, bộ đếm số bài của chuyên mục/tag và "comment mới nhất" — với những người không có quyền xem.
- Trang front page bắt buộc `Public`. Trang tạo trước khi có tính năng này chưa có `AuthorId` (luôn `Public`); người đầu tiên đặt nó thành
  `Personal` sẽ trở thành chủ trang.
- Quy tắc nằm ở **một chỗ**: `Infrastructure/ContentAccess.cs` (kiểm tra một bài) và `PostQueries.Visible` / `PageQueries.Visible`
  / `PostQueries.VisibleCategoryCards` trong `Models/PublicModels.cs` (lọc truy vấn). Khi thêm truy vấn bài viết mới, luôn dùng
  `.Visible(User)`; không truyền viewer = chỉ bài Public (an toàn khi quên).
- Lưu ý: file tải lên (`wwwroot/uploads`) vẫn truy cập được bằng URL trực tiếp, nên ảnh/tệp nhúng trong bài Private/Personal
  không được bảo vệ bởi tính năng này.

Cập nhật database: `dotnet ef database update` (hoặc chạy ứng dụng, `DbInitializer` tự `Migrate()`) để áp dụng migration
`AddContentVisibility` (thêm `Posts.Visibility`, `Pages.Visibility`, `Pages.AuthorId`; dữ liệu cũ mặc định `Public`).

## Cấu hình

| Mục | Ở đâu |
|---|---|
| Chuỗi kết nối | `ConnectionStrings:DefaultConnection` |
| SMTP (mail comment/liên hệ) | `Smtp:*` trong appsettings/user-secrets/biến môi trường — **không** lưu trong DB như bản cũ |
| URL công khai dùng trong link email đặt lại mật khẩu (tùy chọn) | `Hosting:PublicBaseUrl` (vd `https://blog.example.com`); để trống = lấy từ request |
| Tin cậy `X-Forwarded-*` (chạy sau nginx/Docker) | `Hosting:TrustForwardedHeaders=true` (chỉ khi app không mở trực tiếp ra Internet) |
| Tên blog, số bài/trang, comment, múi giờ, culture... | Admin → Settings (lưu bảng `Settings`) |

## Cấu trúc

```
src/BlogEngine.Web
  Data/            BlogDbContext, Entities, DbInitializer (migrate + seed)
  Services/        SettingsService, PasswordService, SlugHelper, EmailService
  Controllers/     Home, Posts, Pages, Categories, Tags, Author, Archive, Search, Contact, Feed, Seo, Account, Error
  ViewComponents/  Footer (cột liên kết chân trang, thay widget), Nav (menu trang)
  Resources/       SharedResource.vi.resx (bản dịch tiếng Việt; khoá = câu tiếng Anh)
  Areas/Admin/     Dashboard, Posts, Pages, Categories, Tags, Comments, Messages, Media, Users, BlogRoll, Settings
  Views/, wwwroot/ Razor views, CSS/JS thuần (không phụ thuộc thư viện ngoài)
database/          legacy-data-migration.sql
tools/             check-translations.py (kiểm tra thiếu/thừa bản dịch)
```

## Giao diện (theme)

Theme mặc định lấy cảm hứng từ phong cách của coder000.com (nền giấy ấm, một cột dễ đọc, hero lớn kèm thẻ "terminal",
card bài viết, footer nhiều cột). CSS/HTML tự viết, không dùng tài nguyên của trang đó và không phụ thuộc thư viện ngoài.

- **Sáng/tối**: nút ở header. Mặc định theo hệ điều hành; lựa chọn của người dùng lưu trong `localStorage` và áp dụng
  trước khi render (không bị nháy). Khu admin dùng chung cơ chế này.
- Màu/kích thước nằm ở biến CSS đầu `wwwroot/css/site.css` (`:root` và `:root[data-theme="dark"]`) — đổi accent chỉ cần sửa `--accent`.
- Trang chủ: hero (tên + mô tả blog từ Admin → Settings), khối chủ đề, 6 bài mới nhất. `/blog` là danh sách đầy đủ có phân trang.
  Nếu đặt một Page làm "front page" thì trang đó thay thế trang chủ.

### Theme "BLK Design" (phong cách BLK Design System Pro)

Chọn ở Admin → Settings → Appearance → Theme (`blk`). Nền xanh navy, card tím than có bóng nổi, màu nhấn magenta → tím,
nút gradient chéo chữ in hoa, header trong suốt rồi đặc lại khi cuộn, chân trang gradient tròn. Mặc định **tối**
(nút sáng/tối ở header vẫn dùng được; chưa chọn thì là tối).

- Files: `Views/Shared/_LayoutBlk.cshtml`, `Views/Home/Blk.cshtml` (hero + 3 bài nổi bật + chủ đề + danh sách), nhánh `IsBlk` trong
  `Views/Shared/_PostCard.cshtml`, `wwwroot/css/blk.css` (class tiền tố `bk-`), đoạn JS cuối `wwwroot/js/site.js`.
- Đổi màu: biến `--bk-primary`, `--bk-primary-states`, `--accent`… ở đầu `blk.css` (`:root` = sáng, `:root[data-theme="dark"]` = tối).
- CSS/HTML tự viết lấy cảm hứng từ BLK, **không** copy mã/font/icon của BLK Design System Pro (đây là sản phẩm có bản quyền);
  font khai báo là Poppins nhưng không nhúng — thiếu thì rơi về font hệ thống. Muốn dùng Poppins, tự thêm `<link>` Google Fonts hoặc file font
  vào `_LayoutBlk.cshtml`.
- Dùng chung sidebar, trang bài viết, form, bình luận… với các theme khác (được style lại bằng `.bk …` trong `blk.css`).

## Đa ngôn ngữ (English / Tiếng Việt)

- Nút `EN | VI` ở header (và ở sidebar admin) lưu lựa chọn vào cookie. Chưa chọn thì dùng **Default language**
  trong Admin → Settings. Ngày giờ, số và tên tháng theo ngôn ngữ đang chọn.
- Dịch **giao diện** (public + admin + thông báo lỗi/validation). **Nội dung** bài viết/trang/chuyên mục do bạn nhập nên giữ nguyên
  một ngôn ngữ (chưa có bài viết đa ngôn ngữ).
- Cách hoạt động: khoá dịch chính là câu tiếng Anh viết trong code (`@L["Sign in"]`); thiếu bản dịch thì hiện câu tiếng Anh.
  Bản dịch ở `Resources/SharedResource.vi.resx`. Thêm chuỗi mới → thêm mục tương ứng vào file này, rồi chạy
  `python3 tools/check-translations.py` (báo chuỗi thiếu, thừa, sai placeholder `{0}`, hoặc hai khoá chỉ khác hoa/thường —
  tên resource không phân biệt hoa/thường).
- Thêm ngôn ngữ mới (ví dụ `fr`): tạo `SharedResource.fr.resx`, thêm `"fr"` vào `SupportedLanguages.Codes` và `DisplayName`
  (`Infrastructure/Localization.cs`).
- Dùng ICU để định dạng ngày theo ngôn ngữ: image Docker `aspnet` đã có sẵn; trên Alpine cần cài `icu-libs`.

## Ánh xạ tính năng

| BlogEngine.NET cũ | Bản mới |
|---|---|
| WebForms pages/master/ascx, theme động | Razor views + 1 layout, theme "Paper & Terminal" có chuyển sáng/tối (xem mục Giao diện). Theme cũ **không** chuyển |
| Widget zone / sidebar | Bỏ sidebar; `FooterViewComponent` ở chân trang: chủ đề, bài mới, blogroll. Trang chủ có khối "Browse by topic". Widget *recent comments*, *tag cloud*, *month list* **không còn** (tag/archive vẫn truy cập qua trang bài viết và `/archive`) |
| Provider (XML/DB/…) + 6 loại DB | Chỉ SQL Server qua EF Core |
| `be_*` bảng, khoá GUID + `BlogId` | Schema chuẩn hoá: khoá `int`, FK thật, many-to-many cho post↔category/tag |
| Multi-blog (`be_Blogs`) | **Bỏ**, chỉ còn một blog |
| Membership/Role provider, rights chi tiết | Cookie auth + 2 role: `Administrators` (toàn quyền), `Editors` (viết bài/trang của mình, duyệt comment). Rights chi tiết **bỏ** |
| Mật khẩu SHA-256 | PBKDF2 (ASP.NET Core Identity `PasswordHasher`), tự nâng cấp hash cũ, khoá tạm 15 phút sau 5 lần sai |
| Admin AngularJS + Web API | Admin MVC + soạn thảo HTML cơ bản (toolbar + upload ảnh + preview an toàn trong iframe sandbox) |
| File manager (DB/App_Data) | Media: upload ảnh/PDF vào `wwwroot/uploads` (kiểm tra magic bytes, 10 MB) |
| Comment (nested, moderation, spam flag) | Giữ. Comment là văn bản thuần (encode HTML), honeypot + rate limit, Gravatar tuỳ chọn |
| Rating bài viết | Giữ (1 lượt/trình duyệt qua cookie) |
| Search, archive, category, tag, author, contact | Giữ; contact lưu DB + gửi mail nếu cấu hình SMTP |
| RSS/Atom (`syndication.axd`), sitemap, robots | `/feed`, `/feed/atom`, `/sitemap.xml`, `/robots.txt` |
| Trash (soft delete) cho post/page/comment | Giữ |
| Bài hẹn giờ, nháp xem trước | Giữ (ngày đăng tương lai = hẹn giờ; staff xem trước bài chưa public) |
| Đa ngôn ngữ (resx 60+ ngôn ngữ) | **English + Tiếng Việt** (xem mục Đa ngôn ngữ). Các ngôn ngữ khác của bản cũ không chuyển |

**Không chuyển** (cần làm lại nếu bạn dùng): MetaWeblog/Windows Live Writer API, pingback/trackback, BlogML import/export,
OPML/FOAF/APML/SIOC, extension (Akismet, BBCode, Smilies, Recaptcha, Newsletter…), package/theme installer, quick notes,
referrer tracking, user registration công khai (tạo user từ Admin → Users), Custom fields.

## Bảo mật (so với bản cũ)

- Anti-forgery bắt buộc cho mọi POST; cookie `HttpOnly`, `SameSite=Lax`.
- Rate limit cho comment/liên hệ/đăng nhập; chặn open-redirect khi login; security header cơ bản (`nosniff`, `X-Frame-Options`).
- Nội dung bài/trang do staff nhập là **HTML thô** (như bản gốc) — chỉ cấp role Editors cho người tin cậy. Chưa có Content-Security-Policy
  vì `TrackingScript` và nội dung bài có thể chứa script hợp lệ.
- **Quên mật khẩu** (`/account/forgot-password`): chỉ áp dụng cho tài khoản Administrators/Editors đang hoạt động **và có email**; cần cấu hình `Smtp:*`.
  Link chứa token Data Protection (không lưu DB), hết hạn sau 1 giờ và chỉ dùng được một lần (đổi mật khẩu là token cũ mất hiệu lực).
  Luôn trả cùng một thông báo dù tài khoản có tồn tại hay không. Ở môi trường Development, link được ghi vào log để thử khi chưa có SMTP.
  Khi triển khai thật, hãy lưu **khóa Data Protection** bền vững (và dùng chung nếu chạy nhiều instance), nếu không link sẽ hỏng sau khi restart.
- Liên kết mạng xã hội ở footer (Indigo/BLK): Facebook, YouTube, Chat (Messenger/Zalo/Telegram...) — nhập ở Admin → Settings → Appearance.
- Tìm kiếm dùng `LIKE` qua EF; blog rất lớn nên cân nhắc SQL Server Full-Text Search.

## Trạng thái kiểm chứng (nói rõ)

Môi trường tạo project này **không có SQL Server và không tải được NuGet** (nên không có EF Core thật). Đã làm:

- ✅ Toàn bộ C# và 40 Razor view biên dịch trên .NET 10 SDK (với thư viện giả lập API EF Core).
- ✅ **Chạy toàn bộ ứng dụng thật** (Program.cs, routing, Razor, cookie auth, anti-forgery, localization) trên kho dữ liệu in-memory
  thay cho EF, rồi thao tác bằng trình duyệt headless (Chromium): trang chủ/bài viết/archive/search/contact/404, đổi sáng/tối
  (lưu + theo hệ điều hành), đổi EN/VI, gửi bình luận (kể cả XSS bị encode), validation dịch đúng, đăng nhập/đăng xuất admin,
  trang quản trị. Ảnh chụp theme sáng/tối, desktop/mobile đã được xem và chỉnh.
- ✅ Test logic thuần đạt: slug có dấu tiếng Việt, hash mật khẩu cũ→mới, lọc URL, encode comment, redirect link cũ, múi giờ,
  số ít/số nhiều, bản dịch.
- ❌ **Chưa chạy với SQL Server + EF Core thật**: các truy vấn EF có thể còn lỗi dịch sang SQL, `EnsureCreated`/migration,
  khoá ngoại/cascade, và script `database/legacy-data-migration.sql` đều chưa được thử. Hãy build, chạy và duyệt các trang chính
  trên database thật trước khi dùng.
- Chưa có project test tự động (các test trên chạy bằng harness tạm, không kèm trong repo).
