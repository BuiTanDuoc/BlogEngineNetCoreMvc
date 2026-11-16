/* =============================================================================
   Chuyển dữ liệu từ BlogEngine.NET (bảng be_*) sang schema EF Core mới.

   !! SCRIPT NÀY CHƯA ĐƯỢC CHẠY THỬ TRÊN SQL SERVER THẬT. Hãy chạy trên BẢN SAO. !!

   Cách dùng:
   1. Restore database BlogEngine cũ cùng SQL Server với database mới (tên ví dụ: BlogEngineLegacy).
   2. Chạy ứng dụng mới MỘT LẦN để nó tạo schema + dữ liệu mẫu, rồi DỪNG ứng dụng.
      (Không đăng nhập, không tạo thêm nội dung - script sẽ từ chối nếu DB mới không còn "sạch".)
   3. Tìm/thay [BlogEngineLegacy] bên dưới bằng tên database cũ, chọn database MỚI, rồi chạy script.
      Script chạy trong một transaction và mặc định ROLLBACK. Đọc kết quả đếm ở cuối,
      nếu đúng thì đổi dòng "ROLLBACK" cuối file thành "COMMIT".
   4. Khởi động lại ứng dụng: slug của category/tag sẽ được chuẩn hoá tự động (bỏ dấu, ký tự lạ).

   Ghi chú:
   - Chỉ chuyển blog chính (IsPrimary = 1); chế độ multi-blog không còn được hỗ trợ.
   - Mật khẩu cũ (SHA-256) được giữ nguyên dưới dạng 'LEGACY:...' và tự nâng cấp lên PBKDF2 ở lần đăng nhập đầu.
   - Ngày giờ được coi là UTC. Nếu blog cũ cấu hình timezone khác 0, hãy chỉnh TimeZoneId trong Admin > Settings.
   - Không chuyển: file trong FileStore/App_Data, extension settings, referrers, quick notes, pingback/trackback,
     packages (theme/widget), rights/roles chi tiết (chỉ giữ tên role + gán role cho user).
   ============================================================================= */

SET NOCOUNT ON;
SET XACT_ABORT ON;

BEGIN TRANSACTION;

-- 0. Kiểm tra DB đích còn "sạch" ---------------------------------------------------------
IF (SELECT COUNT(*) FROM dbo.Users) > 1 OR (SELECT COUNT(*) FROM dbo.Posts) > 1 OR EXISTS (SELECT 1 FROM dbo.Comments)
    THROW 50000, 'Database đích đã có dữ liệu. Hãy dùng database mới chỉ vừa được ứng dụng khởi tạo.', 1;

DECLARE @BlogId uniqueidentifier =
    (SELECT TOP (1) BlogId FROM [BlogEngineLegacy].dbo.be_Blogs ORDER BY IsPrimary DESC, BlogRowId);

IF @BlogId IS NULL THROW 50001, 'Không tìm thấy blog trong be_Blogs của database cũ.', 1;

-- Xoá dữ liệu mẫu do DbInitializer tạo ra
DELETE FROM dbo.Posts;                                  -- kéo theo PostCategories/PostTags/Comments
DELETE FROM dbo.Tags;
DELETE FROM dbo.Categories;
DELETE FROM dbo.Users WHERE LastLoginTime IS NULL;      -- tài khoản admin mẫu (kéo theo UserRoles)

-- 1. Users + Roles ---------------------------------------------------------------------
INSERT INTO dbo.Users (UserName, DisplayName, EmailAddress, PasswordHash, IsActive, MustChangePassword,
                       CreatedAt, LastLoginTime, FailedLoginCount)
SELECT LEFT(u.UserName, 100), NULL, LEFT(u.EmailAddress, 255), N'LEGACY:' + u.[Password], 1, 0,
       SYSUTCDATETIME(), u.LastLoginTime, 0
FROM [BlogEngineLegacy].dbo.be_Users u
WHERE u.BlogID = @BlogId;

-- Hồ sơ tác giả (be_Profiles)
UPDATE us SET DisplayName = LEFT(p.SettingValue, 100)
FROM dbo.Users us JOIN [BlogEngineLegacy].dbo.be_Profiles p
  ON p.UserName = us.UserName AND p.BlogID = @BlogId AND p.SettingName = N'DisplayName' AND NULLIF(p.SettingValue, N'') IS NOT NULL;

UPDATE us SET Bio = p.SettingValue
FROM dbo.Users us JOIN [BlogEngineLegacy].dbo.be_Profiles p
  ON p.UserName = us.UserName AND p.BlogID = @BlogId AND p.SettingName = N'AboutMe' AND NULLIF(p.SettingValue, N'') IS NOT NULL;

UPDATE us SET PhotoUrl = LEFT(p.SettingValue, 500)
FROM dbo.Users us JOIN [BlogEngineLegacy].dbo.be_Profiles p
  ON p.UserName = us.UserName AND p.BlogID = @BlogId AND p.SettingName = N'PhotoUrl' AND NULLIF(p.SettingValue, N'') IS NOT NULL;

INSERT INTO dbo.Roles (Name)
SELECT DISTINCT LEFT(r.[Role], 100)
FROM [BlogEngineLegacy].dbo.be_Roles r
WHERE r.BlogID = @BlogId AND r.[Role] <> N'Anonymous'
  AND NOT EXISTS (SELECT 1 FROM dbo.Roles x WHERE x.Name = r.[Role]);

-- Tên cột của bảng nối do EF đặt theo quy ước: UsersId / RolesId
INSERT INTO dbo.UserRoles (UsersId, RolesId)
SELECT DISTINCT u.Id, r.Id
FROM [BlogEngineLegacy].dbo.be_UserRoles ur
JOIN dbo.Users u ON u.UserName = ur.UserName
JOIN dbo.Roles r ON r.Name = ur.[Role]
WHERE ur.BlogID = @BlogId;

DECLARE @FallbackUserId int = COALESCE(
    (SELECT TOP (1) u.Id FROM dbo.Users u JOIN dbo.UserRoles ur ON ur.UsersId = u.Id
       JOIN dbo.Roles r ON r.Id = ur.RolesId AND r.Name = N'Administrators' ORDER BY u.Id),
    (SELECT TOP (1) Id FROM dbo.Users ORDER BY Id));

IF @FallbackUserId IS NULL THROW 50002, 'Database cũ không có user nào.', 1;

-- 2. Settings ----------------------------------------------------------------------------
INSERT INTO dbo.Settings (Name, [Value])
SELECT LOWER(s.SettingName), s.SettingValue
FROM [BlogEngineLegacy].dbo.be_Settings s
WHERE s.BlogId = @BlogId AND s.SettingValue IS NOT NULL
  AND LOWER(s.SettingName) IN (N'name', N'description', N'email', N'postsperpage', N'postsperfeed',
      N'numberofrecentposts', N'numberofrecentcomments', N'iscommentsenabled',
      N'enablecommentsmoderation', N'dayscommentsareenabled', N'enablerating', N'enablerelatedposts',
      N'sendmailoncomment', N'emailsubjectprefix', N'trackingscript', N'contactformmessage', N'contactthankmessage');

INSERT INTO dbo.Settings (Name, [Value])
SELECT N'culture', s.SettingValue
FROM [BlogEngineLegacy].dbo.be_Settings s
WHERE s.BlogId = @BlogId AND LOWER(s.SettingName) = N'language' AND NULLIF(s.SettingValue, N'') IS NOT NULL;

-- 3. Categories (giữ cây cha-con) --------------------------------------------------------
CREATE TABLE #Cat (OldId uniqueidentifier NOT NULL PRIMARY KEY, NewId int NOT NULL);

MERGE dbo.Categories AS t
USING (SELECT CategoryID, CategoryName, [Description]
       FROM [BlogEngineLegacy].dbo.be_Categories WHERE BlogID = @BlogId) AS s
ON 1 = 0
WHEN NOT MATCHED THEN
    INSERT (Name, Slug, [Description])
    VALUES (LEFT(ISNULL(NULLIF(s.CategoryName, N''), N'Category'), 100),
            N'cat-' + CONVERT(nvarchar(36), s.CategoryID),   -- slug tạm; ứng dụng chuẩn hoá khi khởi động
            LEFT(s.[Description], 500))
OUTPUT s.CategoryID, inserted.Id INTO #Cat (OldId, NewId);

UPDATE c SET ParentId = pm.NewId
FROM dbo.Categories c
JOIN #Cat cm ON cm.NewId = c.Id
JOIN [BlogEngineLegacy].dbo.be_Categories lc ON lc.CategoryID = cm.OldId
JOIN #Cat pm ON pm.OldId = lc.ParentID;

-- 4. Posts -------------------------------------------------------------------------------
CREATE TABLE #Post (OldId uniqueidentifier NOT NULL PRIMARY KEY, NewId int NOT NULL);

;WITH src AS (
    SELECT p.*,
           LEFT(COALESCE(NULLIF(LTRIM(RTRIM(p.Slug)), N''), N'post-' + CONVERT(nvarchar(36), p.PostID)), 240) AS BaseSlug
    FROM [BlogEngineLegacy].dbo.be_Posts p
    WHERE p.BlogID = @BlogId
), ranked AS (
    SELECT src.*, ROW_NUMBER() OVER (PARTITION BY BaseSlug ORDER BY DateCreated, PostRowID) AS rn
    FROM src
)
MERGE dbo.Posts AS t
USING (
    SELECT r.PostID,
           ISNULL(r.Title, N'(untitled)') AS Title,
           CASE WHEN r.rn = 1 THEN r.BaseSlug ELSE r.BaseSlug + N'-' + CONVERT(nvarchar(10), r.rn) END AS Slug,
           r.[Description], ISNULL(r.PostContent, N'') AS Content,
           ISNULL(r.DateCreated, SYSUTCDATETIME()) AS DateCreated,
           ISNULL(r.DateModified, ISNULL(r.DateCreated, SYSUTCDATETIME())) AS DateModified,
           COALESCE(u.Id, @FallbackUserId) AS AuthorId,
           ISNULL(r.IsPublished, 0) AS IsPublished, ISNULL(r.IsCommentEnabled, 1) AS IsCommentEnabled,
           ISNULL(r.Raters, 0) AS Raters, CONVERT(float, ISNULL(r.Rating, 0)) AS Rating, r.IsDeleted
    FROM ranked r
    LEFT JOIN dbo.Users u ON u.UserName = r.Author
) AS s
ON 1 = 0
WHEN NOT MATCHED THEN
    INSERT (Title, Slug, [Description], Content, DateCreated, DateModified, AuthorId,
            IsPublished, IsCommentEnabled, IsDeleted, Raters, Rating)
    VALUES (LEFT(s.Title, 255), s.Slug, s.[Description], s.Content, s.DateCreated, s.DateModified, s.AuthorId,
            s.IsPublished, s.IsCommentEnabled, s.IsDeleted, s.Raters, s.Rating)
OUTPUT s.PostID, inserted.Id INTO #Post (OldId, NewId);

-- PostCategories (cột nối do EF đặt tên: CategoriesId / PostsId)
INSERT INTO dbo.PostCategories (CategoriesId, PostsId)
SELECT DISTINCT cm.NewId, pm.NewId
FROM [BlogEngineLegacy].dbo.be_PostCategory pc
JOIN #Post pm ON pm.OldId = pc.PostID
JOIN #Cat cm ON cm.OldId = pc.CategoryID
WHERE pc.BlogID = @BlogId;

-- 5. Tags (be_PostTag lưu chuỗi tag cho từng bài) -------------------------------------------
;WITH legacy AS (
    SELECT pt.PostID,
           LEFT(LTRIM(RTRIM(pt.Tag)), 50) AS TagName,
           LEFT(LOWER(REPLACE(LTRIM(RTRIM(pt.Tag)), N' ', N'-')), 80) AS TagSlug
    FROM [BlogEngineLegacy].dbo.be_PostTag pt
    WHERE pt.BlogID = @BlogId AND NULLIF(LTRIM(RTRIM(pt.Tag)), N'') IS NOT NULL
)
INSERT INTO dbo.Tags (Name, Slug)
SELECT MIN(TagName), TagSlug FROM legacy GROUP BY TagSlug;

INSERT INTO dbo.PostTags (PostsId, TagsId)
SELECT DISTINCT pm.NewId, tg.Id
FROM [BlogEngineLegacy].dbo.be_PostTag pt
JOIN #Post pm ON pm.OldId = pt.PostID
JOIN dbo.Tags tg ON tg.Slug = LEFT(LOWER(REPLACE(LTRIM(RTRIM(pt.Tag)), N' ', N'-')), 80)
WHERE pt.BlogID = @BlogId AND NULLIF(LTRIM(RTRIM(pt.Tag)), N'') IS NOT NULL;

-- 6. Comments (2 bước để giữ comment lồng nhau) ------------------------------------------------
CREATE TABLE #Cmt (OldId uniqueidentifier NOT NULL PRIMARY KEY, NewId int NOT NULL);

MERGE dbo.Comments AS t
USING (
    SELECT c.PostCommentID, pm.NewId AS PostId, c.CommentDate,
           LEFT(ISNULL(NULLIF(c.Author, N''), N'Anonymous'), 100) AS Author,
           LEFT(c.Email, 255) AS Email, LEFT(c.Website, 255) AS Website,
           ISNULL(c.Comment, N'') AS Content, LEFT(c.Ip, 50) AS Ip,
           ISNULL(c.IsApproved, 1) AS IsApproved, c.IsSpam, c.IsDeleted, LEFT(c.ModeratedBy, 100) AS ModeratedBy
    FROM [BlogEngineLegacy].dbo.be_PostComment c
    JOIN #Post pm ON pm.OldId = c.PostID
    WHERE c.BlogID = @BlogId
) AS s
ON 1 = 0
WHEN NOT MATCHED THEN
    INSERT (PostId, [Date], Author, Email, Website, Content, Ip, IsApproved, IsSpam, IsDeleted, ModeratedBy)
    VALUES (s.PostId, s.CommentDate, s.Author, s.Email, s.Website, s.Content, s.Ip,
            s.IsApproved, s.IsSpam, s.IsDeleted, s.ModeratedBy)
OUTPUT s.PostCommentID, inserted.Id INTO #Cmt (OldId, NewId);

UPDATE c SET ParentId = pm.NewId
FROM dbo.Comments c
JOIN #Cmt cm ON cm.NewId = c.Id
JOIN [BlogEngineLegacy].dbo.be_PostComment lc ON lc.PostCommentID = cm.OldId
JOIN #Cmt pm ON pm.OldId = lc.ParentCommentID;   -- comment gốc có ParentCommentID rỗng nên không khớp -> giữ NULL

-- 7. Pages ---------------------------------------------------------------------------------
CREATE TABLE #Page (OldId uniqueidentifier NOT NULL PRIMARY KEY, NewId int NOT NULL);

;WITH src AS (
    SELECT p.*,
           LEFT(COALESCE(NULLIF(LTRIM(RTRIM(p.Slug)), N''), N'page-' + CONVERT(nvarchar(36), p.PageID)), 240) AS BaseSlug
    FROM [BlogEngineLegacy].dbo.be_Pages p
    WHERE p.BlogID = @BlogId
), ranked AS (
    SELECT src.*, ROW_NUMBER() OVER (PARTITION BY BaseSlug ORDER BY DateCreated, PageRowID) AS rn FROM src
)
MERGE dbo.Pages AS t
USING (
    SELECT r.PageID, ISNULL(r.Title, N'(untitled)') AS Title,
           CASE WHEN r.rn = 1 THEN r.BaseSlug ELSE r.BaseSlug + N'-' + CONVERT(nvarchar(10), r.rn) END AS Slug,
           r.[Description], ISNULL(r.PageContent, N'') AS Content, LEFT(r.Keywords, 500) AS Keywords,
           ISNULL(r.DateCreated, SYSUTCDATETIME()) AS DateCreated,
           ISNULL(r.DateModified, ISNULL(r.DateCreated, SYSUTCDATETIME())) AS DateModified,
           ISNULL(r.IsPublished, 0) AS IsPublished, ISNULL(r.IsFrontPage, 0) AS IsFrontPage,
           ISNULL(r.ShowInList, 1) AS ShowInList, r.IsDeleted
    FROM ranked r
) AS s
ON 1 = 0
WHEN NOT MATCHED THEN
    INSERT (Title, Slug, [Description], Content, Keywords, DateCreated, DateModified,
            IsPublished, IsFrontPage, ShowInList, IsDeleted)
    VALUES (LEFT(s.Title, 255), s.Slug, s.[Description], s.Content, s.Keywords, s.DateCreated, s.DateModified,
            s.IsPublished, s.IsFrontPage, s.ShowInList, s.IsDeleted)
OUTPUT s.PageID, inserted.Id INTO #Page (OldId, NewId);

UPDATE p SET ParentId = pm.NewId
FROM dbo.Pages p
JOIN #Page cm ON cm.NewId = p.Id
JOIN [BlogEngineLegacy].dbo.be_Pages lp ON lp.PageID = cm.OldId
JOIN #Page pm ON pm.OldId = lp.Parent;

-- Chỉ cho phép đúng một front page
UPDATE dbo.Pages SET IsFrontPage = 0
WHERE IsFrontPage = 1 AND Id <> (SELECT MIN(Id) FROM dbo.Pages WHERE IsFrontPage = 1);

-- 8. Blogroll ----------------------------------------------------------------------------------
INSERT INTO dbo.BlogRollItems (Title, [Description], BlogUrl, FeedUrl, Xfn, SortIndex)
SELECT LEFT(Title, 255), LEFT([Description], 500), LEFT(BlogUrl, 500), LEFT(FeedUrl, 500), LEFT(Xfn, 100), SortIndex
FROM [BlogEngineLegacy].dbo.be_BlogRollItems
WHERE BlogId = @BlogId;

-- 9. Kết quả đếm: so sánh với database cũ trước khi COMMIT ----------------------------------------
SELECT N'Users' AS [Table], (SELECT COUNT(*) FROM dbo.Users) AS NewCount,
       (SELECT COUNT(*) FROM [BlogEngineLegacy].dbo.be_Users WHERE BlogID = @BlogId) AS LegacyCount
UNION ALL SELECT N'Posts', (SELECT COUNT(*) FROM dbo.Posts), (SELECT COUNT(*) FROM [BlogEngineLegacy].dbo.be_Posts WHERE BlogID = @BlogId)
UNION ALL SELECT N'Pages', (SELECT COUNT(*) FROM dbo.Pages), (SELECT COUNT(*) FROM [BlogEngineLegacy].dbo.be_Pages WHERE BlogID = @BlogId)
UNION ALL SELECT N'Categories', (SELECT COUNT(*) FROM dbo.Categories), (SELECT COUNT(*) FROM [BlogEngineLegacy].dbo.be_Categories WHERE BlogID = @BlogId)
UNION ALL SELECT N'Comments', (SELECT COUNT(*) FROM dbo.Comments),
       (SELECT COUNT(*) FROM [BlogEngineLegacy].dbo.be_PostComment c WHERE c.BlogID = @BlogId AND c.PostID IN (SELECT OldId FROM #Post))
UNION ALL SELECT N'BlogRoll', (SELECT COUNT(*) FROM dbo.BlogRollItems), (SELECT COUNT(*) FROM [BlogEngineLegacy].dbo.be_BlogRollItems WHERE BlogId = @BlogId);

DROP TABLE #Cat; DROP TABLE #Post; DROP TABLE #Cmt; DROP TABLE #Page;

ROLLBACK TRANSACTION;   -- <-- đổi thành COMMIT TRANSACTION sau khi đã kiểm tra kết quả
