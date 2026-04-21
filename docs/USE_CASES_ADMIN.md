# Tài Liệu Use Case: Admin

## Tổng Quan

Module **Admin** cung cấp toàn bộ công cụ quản trị hệ thống VibeMusic. Admin có quyền truy cập đặc biệt vào tất cả dữ liệu và có thể thực hiện các thao tác mà user thường không được phép.

**Actor**: Admin (quản trị viên hệ thống)

**Yêu cầu xác thực**: Tất cả use cases đều yêu cầu `[Authorize(Roles = "Admin")]` — user thường bị từ chối HTTP 403.

**Lưu ý về "UseCase5"**: Trong sơ đồ use case, use case thứ 5 (nằm giữa "Xem danh sách người dùng" và "Xem danh sách báo cáo") là **Quản lý người dùng** — bao gồm khóa/mở khóa tài khoản, cấp/thu hồi Premium, và xóa người dùng.

---

## Kiến Trúc Xử Lý

```
Browser (Admin)
    │
    ▼
AdminSongController        ← Quản lý bài hát
AdminArtistController      ← Quản lý nghệ sĩ
AdminTaxonomyController    ← Quản lý thể loại/danh mục
AdminUserController        ← Quản lý người dùng
AdminReportController      ← Xem và xử lý báo cáo
AdminSupportController     ← Xóa bình luận vi phạm
AdminDashboardController   ← Thống kê doanh thu và lượt nghe
    │ inject
    ▼
ISongService / IArtistService / IGenreService / ICategoryService
IUserService / IDashboardService / ICommentService
    │ implement
    ▼
Services (VibeMusic.Application/Services/)
    │ inject
    ▼
IUnitOfWork → Repository<T> → AppDbContext → PostgreSQL
```

**Các class tham gia chính**:

| Class | Project | Vai trò |
|---|---|---|
| `AdminSongController` | VibeMusic | CRUD bài hát, toggle premium/explicit |
| `AdminArtistController` | VibeMusic | CRUD nghệ sĩ, refresh bio, toggle verified |
| `AdminTaxonomyController` | VibeMusic | CRUD thể loại (genre) và danh mục (category) |
| `AdminUserController` | VibeMusic | Xem, khóa, cấp premium, xóa người dùng |
| `AdminReportController` | VibeMusic | Xem, xử lý, dismiss báo cáo |
| `AdminSupportController` | VibeMusic | Xóa bình luận vi phạm |
| `AdminDashboardController` | VibeMusic | Dashboard thống kê, sync lượt nghe |
| `BaseController` | VibeMusic | Cung cấp `CurrentUserId`, `IsAdmin`, `SuccessResponse()` |
| `ISongService` | VibeMusic.Application | Hợp đồng nghiệp vụ bài hát |
| `IArtistService` | VibeMusic.Application | Hợp đồng nghiệp vụ nghệ sĩ |
| `IUserService` | VibeMusic.Application | Hợp đồng nghiệp vụ người dùng |
| `IDashboardService` | VibeMusic.Application | Hợp đồng thống kê hệ thống |
| `DashboardDto` | VibeMusic.Application | DTO tổng hợp số liệu dashboard |

---

## Use Case 1: Thêm / Sửa / Xóa Bài Hát

### Mô tả
Admin quản lý toàn bộ thư viện bài hát: thêm mới, chỉnh sửa thông tin, xóa, và toggle trạng thái Premium/Explicit.

### Luồng xử lý — Thêm bài hát

```
GET /AdminSong/Create
    └── ViewBag.Genres = _genreService.GetAllGenresAsync()
    └── return View(new SongDto())

POST /AdminSong/Create (SongDto)
    ├── Validate ModelState
    ├── _songService.CreateSongAsync(dto)
    │       └── INSERT INTO songs (title, youtubevideoid, albumid, ...)
    └── TempData["Success"] → RedirectToAction("Index")
```

### Luồng xử lý — Sửa bài hát

```
GET /AdminSong/Edit/{id}
    ├── _songService.GetSongByIdAsync(id)
    └── ViewBag.Genres = _genreService.GetAllGenresAsync()

POST /AdminSong/Edit (SongDto)
    ├── Validate ModelState
    ├── _songService.UpdateSongAsync(dto)
    │       └── UPDATE songs SET title=..., albumid=..., ...
    └── TempData["Success"] → RedirectToAction("Index")
```

### Luồng xử lý — Xóa bài hát (Soft Delete)

```
POST /AdminSong/Delete/{id}
    ├── _songService.DeleteSongAsync(id)
    │       └── UPDATE songs SET is_deleted = true
    └── TempData["Success"] → RedirectToAction("Index")
```

### Toggle Premium / Explicit (AJAX)

```
POST /AdminSong/TogglePremium/{id}
    └── _songService.TogglePremiumStatusAsync(id)
            └── UPDATE songs SET ispremiumonly = NOT ispremiumonly WHERE songid = @id
    └── return Json({ success: true })

POST /AdminSong/ToggleExplicit/{id}
    └── _songService.ToggleExplicitStatusAsync(id)
            └── UPDATE songs SET isexplicit = NOT isexplicit WHERE songid = @id
    └── return Json({ success: true })
```

Toggle dùng **Direct SQL** (không load entity) để tối ưu hiệu suất — tránh round-trip load → modify → save.

### Điểm đáng chú ý
- `SearchAlbums(term)` — AJAX endpoint tìm album khi gán bài hát vào album, tránh load toàn bộ danh sách
- Bài hát bị xóa dùng soft delete (`is_deleted = true`) — không mất dữ liệu, vẫn có thể khôi phục

---

## Use Case 2: Thêm / Sửa / Xóa Nghệ Sĩ

### Mô tả
Admin quản lý thông tin nghệ sĩ, bao gồm cả tính năng đặc biệt: làm mới tiểu sử từ Wikipedia và toggle trạng thái xác minh.

### Luồng xử lý — CRUD cơ bản

```
POST /AdminArtist/Create (ArtistDto)
    ├── _artistService.CreateArtistAsync(dto)
    │       ├── Nếu Bio rỗng: gọi Wikipedia API lấy tiểu sử tự động
    │       └── INSERT INTO artists (name, bio, country, avatarurl, ...)
    └── TempData["Success"] → RedirectToAction("Index")

POST /AdminArtist/Edit (ArtistDto)
    └── _artistService.UpdateArtistAsync(dto)
            └── UPDATE artists SET name=..., bio=..., ...

POST /AdminArtist/Delete/{id}
    └── _artistService.DeleteArtistAsync(id)
            └── UPDATE artists SET is_deleted = true (soft delete)
```

### Làm mới tiểu sử từ Wikipedia

```
POST /AdminArtist/RefreshArtistBio/{id}
    └── _artistService.RefreshArtistBioAsync(id)
            ├── Gọi Wikipedia API: GetArtistBioAsync(artist.Name)
            ├── Nếu tìm thấy:
            │       UPDATE artists SET bio=..., wikipediaurl=...
            │       return bio text
            └── Nếu không tìm thấy: return null
    ├── Nếu null: TempData["Error"] = "Could not refresh..."
    └── Nếu có: TempData["Success"] = "Artist biography refreshed..."
    └── RedirectToAction("Edit", { id })
```

### Toggle Verified (AJAX)

```
POST /AdminArtist/ToggleVerified/{id}
    └── _artistService.ToggleVerifiedStatusAsync(id)
            ├── UPDATE artists SET isverified = NOT isverified
            └── _cache.Remove("artist_details_{id}") ← Xóa cache
    └── return Json({ success: true })
```

Xóa cache sau khi toggle để trang chi tiết nghệ sĩ hiển thị trạng thái mới ngay lập tức.

---

## Use Case 3: Thêm / Sửa / Xóa Thể Loại

### Mô tả
Admin quản lý **Genre** (thể loại nhạc: Pop, Rock, Jazz...) và **Category** (danh mục: Nhạc Việt, Nhạc Nước Ngoài...) — hai loại phân loại độc lập trong hệ thống.

### Luồng xử lý — Genre

```
GET /AdminTaxonomy/Index
    ├── _genreService.GetAllGenresAsync()
    └── _categoryService.GetAllCategoriesAsync()
    └── return View(AdminTaxonomyViewModel { Genres, Categories })

POST /AdminTaxonomy/CreateGenre (GenreDto)
    ├── _genreService.CreateGenreAsync(dto)
    │       └── INSERT INTO genres (name, description)
    └── TempData["Success"] → RedirectToAction("Index")

POST /AdminTaxonomy/EditGenre (GenreDto)
    └── _genreService.UpdateGenreAsync(dto)
            └── UPDATE genres SET name=..., description=...

POST /AdminTaxonomy/DeleteGenre/{id}
    └── _genreService.DeleteGenreAsync(id)
            └── DELETE FROM genres WHERE genreid = @id
```

### Luồng xử lý — Category

Tương tự Genre nhưng dùng `ICategoryService` và bảng `categories`.

### Điểm đáng chú ý
- Genre và Category được hiển thị **cùng một trang** (`AdminTaxonomy/Index`) — admin quản lý cả hai từ một nơi
- Genre dùng **hard delete** (xóa vật lý) — khác với Song/Artist dùng soft delete
- Nếu genre đang được dùng bởi bài hát, service sẽ throw exception → `TempData["Error"]` hiển thị lỗi

---

## Use Case 4: Xem Danh Sách Người Dùng

### Mô tả
Admin xem danh sách tất cả người dùng với phân trang và tìm kiếm, xem chi tiết từng user bao gồm lịch sử nghe, bài hát yêu thích và playlist.

### Luồng xử lý

```
GET /AdminUser/Index?page=1&pageSize=10&searchTerm=...
    ├── _userService.GetPaginatedUsersAsync(page, pageSize, searchTerm)
    │       └── SELECT * FROM users WHERE is_deleted = false
    │               AND (username LIKE @term OR email LIKE @term)
    │               ORDER BY createdat DESC
    │               OFFSET @skip LIMIT @pageSize
    └── _subscriptionService.GetActivePlansAsync()
            └── (Để hiển thị dropdown cấp Premium)
    └── return View(AdminUserListViewModel)

GET /AdminUser/Details/{id}
    ├── _userService.GetUserByIdAsync(id)
    ├── _userService.GetUserListeningHistoryAsync(id)
    ├── _interactionService.GetLikedSongIdsAsync(id)
    ├── _songService.GetSongsByIdsAsync(likedSongIds)
    └── _playlistService.GetUserPlaylistsAsync(id)
    └── ViewBag.ListeningHistory, ViewBag.LikedSongs, ViewBag.Playlists
```

---

## Use Case 5: Quản Lý Người Dùng

### Mô tả
Admin thực hiện các thao tác quản lý trực tiếp trên tài khoản người dùng: khóa/mở khóa, cấp/thu hồi Premium, và xóa tài khoản. Có cơ chế **self-protection** ngăn admin tự tác động lên tài khoản của chính mình.

### Khóa / Mở khóa tài khoản

```
POST /AdminUser/ToggleUserLock/{id}
    ├── Kiểm tra: id == CurrentAdminId → TempData["Error"] + Redirect (self-protection)
    └── _userService.ToggleUserLockAsync(id)
            ├── SELECT user WHERE userid = @id AND is_deleted = false
            ├── user.IsLocked = !user.IsLocked
            └── UPDATE users SET islocked = @newValue
    └── TempData["Success"/"Error"] → RedirectToAction("Details", { id })
```

### Cấp Premium thủ công

```
POST /AdminUser/GrantPremium?id={userId}&planId={planId}
    └── _userService.GrantPremiumByPlanAsync(userId, planId)
            ├── Lấy plan từ DB
            ├── user.IsPremium = true
            ├── Tạo UserSubscription mới với EndDate = now + DurationDays
            └── UPDATE users + INSERT INTO usersubscriptions
    └── TempData["Success"] → RedirectToAction("Index")
```

### Thu hồi Premium

```
POST /AdminUser/RevokePremium?id={userId}
    └── _userService.RevokePremiumAsync(userId)
            ├── user.IsPremium = false
            ├── Tìm UserSubscription đang active → IsActive = false
            └── UPDATE users + UPDATE usersubscriptions
    └── TempData["Success"] → RedirectToAction("Index")
```

### Xóa tài khoản (Soft Delete)

```
POST /AdminUser/Delete/{id}
    ├── Kiểm tra: id == CurrentAdminId → TempData["Error"] + Redirect (self-protection)
    └── _userService.DeleteUserAsync(id)
            └── UPDATE users SET is_deleted = true
    └── TempData["Success"] → RedirectToAction("Index")
```

### Cơ chế Self-Protection
```csharp
private int CurrentAdminId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");

// Trong ToggleUserLock và Delete:
if (id == CurrentAdminId)
{
    TempData["Error"] = "You cannot lock/delete your own administrative account.";
    return RedirectToAction(...);
}
```
Admin không thể tự khóa hoặc xóa tài khoản của chính mình — tránh tình huống mất quyền truy cập hệ thống.

---

## Use Case 6: Xem Danh Sách Báo Cáo

### Mô tả
Admin xem danh sách các báo cáo vi phạm từ người dùng, với khả năng lọc theo trạng thái và tìm kiếm.

### Luồng xử lý

```
GET /AdminReport/Index?page=1&status=Pending&searchTerm=...
    ├── Nếu status = "All" → status = null (lấy tất cả)
    ├── Nếu có searchTerm:
    │       _dashboardService.GetAllReportsAsync()
    │       → Filter in-memory theo UserName, TargetName, TargetType, Reason, Details
    │       → Phân trang thủ công
    └── Nếu không có searchTerm:
            _dashboardService.GetPaginatedReportsAsync(page, pageSize, status)
            → SQL query với filter và pagination
    └── return View(AdminReportListViewModel)

GET /AdminReport/Details/{id}
    └── _dashboardService.GetReportByIdAsync(id)
    └── return View(ReportDto)
```

### Tại sao có 2 luồng query?
- **Không có searchTerm**: Dùng SQL pagination — hiệu quả với dữ liệu lớn
- **Có searchTerm**: Load tất cả rồi filter in-memory — đơn giản hơn nhưng chỉ phù hợp khi số lượng báo cáo không quá lớn. Đây là trade-off giữa đơn giản và hiệu suất.

---

## Use Case 7: Xử Lý Báo Cáo

### Mô tả
Admin xử lý báo cáo vi phạm theo 2 hướng: **Resolve** (xử lý, có thể kèm hành động như xóa nội dung) hoặc **Dismiss** (bác bỏ báo cáo không hợp lệ).

### Luồng xử lý

```
POST /AdminReport/Resolve?id={reportId}&takeAction=true/false
    └── _dashboardService.ResolveReportAsync(id, takeAction)
            ├── Cập nhật Report.Status = "Resolved"
            └── Nếu takeAction = true: thực hiện hành động (xóa nội dung vi phạm)
    └── TempData["Success"] → RedirectToAction("Index", { status, searchTerm })

POST /AdminReport/Dismiss?id={reportId}
    └── _dashboardService.DismissReportAsync(id)
            └── Cập nhật Report.Status = "Dismissed"
    └── TempData["Success"] → RedirectToAction("Index", { status, searchTerm })
```

### Trạng thái báo cáo
| Status | Ý nghĩa |
|---|---|
| `Pending` | Chờ admin xử lý (mặc định khi user gửi báo cáo) |
| `Resolved` | Admin đã xử lý (có hoặc không có hành động) |
| `Dismissed` | Admin bác bỏ — báo cáo không hợp lệ |

---

## Use Case 8: Xóa Bình Luận Vi Phạm

### Mô tả
Admin xem toàn bộ bình luận trong hệ thống và xóa các bình luận vi phạm. Khác với user chỉ xóa được bình luận của mình, admin xóa được bình luận của bất kỳ ai.

### Luồng xử lý

```
GET /AdminSupport/Comments
    └── _commentService.GetAllCommentsAsync()
            └── SELECT c.*, u.username, s.title
                FROM comments c
                JOIN users u ON c.userid = u.userid
                JOIN songs s ON c.songid = s.songid
                ORDER BY c.createdat DESC
    └── return View(IEnumerable<CommentDto>)

POST /AdminSupport/DeleteComment/{id}
    └── _commentService.DeleteCommentAsync(id, userId: null)
            ├── userId = null → bỏ qua kiểm tra quyền sở hữu
            └── DELETE FROM comments WHERE commentid = @id
    └── TempData["Success"] → RedirectToAction("Comments")
```

### Cơ chế Admin Bypass
```csharp
public async Task DeleteCommentAsync(int commentId, int? userId = null)
{
    var comment = await _unitOfWork.Repository<Comment>().GetByIdAsync(commentId);
    if (comment == null) return;

    // userId = null → Admin bypass, xóa mà không kiểm tra quyền sở hữu
    if (userId != null && comment.UserId != userId) return;

    _unitOfWork.Repository<Comment>().Remove(comment);
    await _unitOfWork.CompleteAsync();
}
```
Cùng một method `DeleteCommentAsync` phục vụ cả user (truyền `userId`) và admin (truyền `null`).

---

## Use Case 9: Xem Thống Kê Doanh Thu

### Mô tả
Admin xem dashboard tổng hợp các số liệu tài chính và hoạt động của hệ thống, được cache để tối ưu hiệu suất.

### Luồng xử lý

```
GET /Admin (hoặc /AdminDashboard)
    ├── Kiểm tra cache: "AdminDashboardStats" (TTL 10 phút, sliding 2 phút)
    ├── Nếu cache miss:
    │       _dashboardService.GetStatsAsync()
    │           ├── TotalRevenue: SUM(amount) FROM payments WHERE status='Success'
    │           ├── MonthlyRevenue: SUM(amount) WHERE paymentdate >= đầu tháng
    │           ├── TotalUsers, PremiumUsersCount, NewUsers24h
    │           ├── TotalSongs, TotalPlaylists, TotalPlays
    │           ├── PendingReports: COUNT(*) WHERE status='Pending'
    │           ├── TopSongs: TOP 10 bài hát theo PlayCount
    │           ├── TopArtists: TOP 10 nghệ sĩ theo tổng lượt nghe
    │           ├── PlayHistory: 30 ngày gần nhất (biểu đồ)
    │           └── RecentPayments: 10 giao dịch gần nhất
    │       _cache.Set("AdminDashboardStats", stats, cacheOptions)
    └── return View(DashboardDto)
```

### Dữ liệu hiển thị (`DashboardDto`)
```csharp
public class DashboardDto
{
    public decimal TotalRevenue { get; set; }      // Tổng doanh thu
    public decimal MonthlyRevenue { get; set; }    // Doanh thu tháng này
    public int TotalUsers { get; set; }            // Tổng người dùng
    public int PremiumUsersCount { get; set; }     // Số Premium users
    public int NewUsers24h { get; set; }           // Người dùng mới 24h
    public int TotalSongs { get; set; }            // Tổng bài hát
    public long TotalPlays { get; set; }           // Tổng lượt nghe
    public int PendingReports { get; set; }        // Báo cáo chờ xử lý
    public IEnumerable<TopSongDto> TopSongs { get; set; }
    public IEnumerable<TopArtistDto> TopArtists { get; set; }
    public IEnumerable<DailyPlayCountDto> PlayHistory { get; set; }
    public IEnumerable<PaymentDto> RecentPayments { get; set; }
}
```

### Caching Strategy
- **TTL tuyệt đối**: 10 phút — dữ liệu không bao giờ cũ hơn 10 phút
- **Sliding expiration**: 2 phút — nếu không có request trong 2 phút, cache bị xóa sớm
- **Manual refresh**: Admin có thể nhấn "Làm mới" → `POST /Admin/RefreshStats` → xóa cache ngay lập tức

---

## Use Case 10: Xem Thống Kê Lượt Nghe

### Mô tả
Admin xem thống kê lượt nghe thực tế và có thể đồng bộ lại dữ liệu `PlayCount` từ lịch sử nghe nhạc thực tế (thay vì số liệu ảo từ YouTube).

### Xem thống kê lượt nghe

Dữ liệu lượt nghe được hiển thị trong Dashboard (`/Admin`) qua:
- **`PlayHistory`**: Biểu đồ lượt nghe theo ngày (30 ngày gần nhất)
- **`TopSongs`**: Top 10 bài hát được nghe nhiều nhất
- **`TopArtists`**: Top 10 nghệ sĩ theo tổng lượt nghe
- **`TotalPlays`**: Tổng lượt nghe toàn hệ thống

### Đồng bộ lượt nghe từ lịch sử thực tế

```
POST /Admin/SyncPlayCountFromHistory
    ├── Đếm lượt nghe thực từ ListeningHistory:
    │       SELECT songid, COUNT(*) as count
    │       FROM listeninghistory
    │       GROUP BY songid
    ├── Lấy tất cả songs chưa bị xóa
    ├── So sánh và cập nhật PlayCount:
    │       Nếu song.PlayCount != realCount:
    │           song.PlayCount = realCount
    │           UPDATE songs SET playcount = @realCount
    ├── _cache.Remove("AdminDashboardStats") ← Xóa cache
    └── TempData["Success"] = "Đã đồng bộ {N} bài hát..."
```

### Tại sao cần đồng bộ?
Khi import bài hát từ YouTube, hệ thống có thể dùng `ViewCount / 10000` làm `PlayCount` ban đầu (số liệu ảo). Sau khi hệ thống hoạt động, `ListeningHistory` ghi lại lượt nghe thực tế trong app. `SyncPlayCountFromHistory` cập nhật lại `PlayCount` về số liệu thực, đảm bảo thống kê chính xác.

### Reset toàn bộ lượt nghe

```
POST /Admin/ResetAllPlayCounts
    └── ExecuteSqlRawAsync("UPDATE songs SET playcount = 0 WHERE is_deleted = false")
    └── _cache.Remove("AdminDashboardStats")
    └── _cache.Remove("universal_play_counts")
    └── TempData["Success"] = "Đã reset {N} bài hát về 0"
```

Dùng raw SQL để reset hàng loạt — hiệu quả hơn nhiều so với load từng entity và update.

---

## Use Case 11: Quản Lý View Count

### Mô tả
Admin quản lý nguồn view count cho từng bài hát: thiết lập `Priority_Source` để kiểm soát nguồn nào được hiển thị cho người dùng, và trigger đồng bộ thủ công từ YouTube/Deezer khi cần cập nhật ngay lập tức.

**Actor**: Admin  
**Preconditions**: Admin đã đăng nhập, bài hát tồn tại trong hệ thống

### Luồng xử lý — Thiết lập Priority Source

```
GET /AdminSong/Edit/{id}
    ├── _songService.GetSongByIdAsync(id)
    ├── Hiển thị section "View Count Management":
    │       ├── Gọi GET /api/songs/{id}/view-count → hiển thị view count hiện tại và nguồn gốc
    │       └── Dropdown Priority Source: "Auto (Cao nhất)", "Internal", "YouTube", "Deezer"
    └── return View(SongDto)

POST /api/admin/songs/{id}/priority-source (body: { source: "YouTube" | "Deezer" | "Internal" | null })
    ├── [Authorize(Roles = "Admin")]
    ├── Kiểm tra song tồn tại → 404 nếu không tìm thấy
    ├── _viewCountService.UpdatePrioritySourceAsync(id, source)
    │       ├── UPDATE songs SET priority_source = @source WHERE songid = @id
    │       └── _cache.Remove("viewcount_{id}") ← Invalidate cache
    └── return Json({ success: true })
```

### Luồng xử lý — Manual Sync View Count

```
POST /api/admin/songs/{id}/sync-view-count
    ├── [Authorize(Roles = "Admin")]
    ├── Kiểm tra song tồn tại → 404 nếu không tìm thấy
    ├── _backgroundQueue.Enqueue(() => _syncService.SyncAllSourcesAsync(id, ct))
    └── return HTTP 202 Accepted

[Background Job — chạy nền]
    └── _syncService.SyncAllSourcesAsync(id, ct)
            ├── SyncSongAsync(id, ViewCountSource.YouTube):
            │       ├── Lấy song.YoutubeVideoId
            │       ├── Gọi IYoutubeService.GetVideoDetailsAsync(youtubeVideoId)
            │       ├── Lấy ViewCount từ response
            │       ├── Upsert ExternalViewCount (SongId, Source=YouTube):
            │       │       UPDATE nếu đã có, INSERT nếu chưa có
            │       └── _cache.Remove("viewcount_{id}") ← Invalidate cache
            └── SyncSongAsync(id, ViewCountSource.Deezer):
                    ├── Lấy song.DeezerTrackId
                    ├── Gọi IDeezerService.SearchTrackAsync(deezerTrackId)
                    ├── Lấy Popularity rank từ response
                    ├── Upsert ExternalViewCount (SongId, Source=Deezer)
                    └── _cache.Remove("viewcount_{id}") ← Invalidate cache
```

### Luồng thay thế (Alternative Flows)

```
[Song không có YoutubeVideoId]
    SyncSongAsync(id, YouTube)
        └── YoutubeVideoId == null hoặc rỗng
            → Bỏ qua sync YouTube
            → return SyncResult { Success = false, ErrorMessage = "Song has no YoutubeVideoId" }

[API lỗi trong quá trình sync]
    SyncSongAsync(id, source)
        └── IYoutubeService / IDeezerService throw exception
            ├── Ghi log error (ILogger)
            ├── Giữ nguyên giá trị cũ trong external_view_counts (không update)
            └── return SyncResult { Success = false, ErrorMessage = ex.Message }

[Song không tồn tại]
    POST /api/admin/songs/{id}/sync-view-count
        └── return HTTP 404 Not Found

[User không có role Admin]
    POST /api/admin/songs/{id}/sync-view-count
        └── return HTTP 403 Forbidden
```

### Postconditions
- View count trong bảng `external_view_counts` được cập nhật với giá trị mới nhất từ YouTube/Deezer
- Cache `"viewcount_{songId}"` bị invalidate — lần query tiếp theo sẽ lấy dữ liệu mới từ DB
- `priority_source` trong bảng `songs` phản ánh lựa chọn của admin
- Nếu API lỗi: dữ liệu cũ được giữ nguyên, lỗi được ghi vào log

### Kiến trúc xử lý

```
Browser (Admin)
    │
    ▼
AdminViewCountController   ← POST /api/admin/songs/{id}/sync-view-count
ViewCountController        ← GET /api/songs/{id}/view-count (public)
    │ inject
    ▼
IViewCountService          ← GetViewCountAsync, UpdatePrioritySourceAsync
IExternalViewCountSyncService ← SyncSongAsync, SyncAllSourcesAsync
IBackgroundQueue           ← Enqueue background jobs
    │ inject
    ▼
IYoutubeService / IDeezerService ← External API calls
IUnitOfWork → Repository<ExternalViewCount> → AppDbContext → PostgreSQL
IMemoryCache               ← Cache key: "viewcount_{songId}", TTL: 5 phút
```

### Dữ liệu liên quan

| Bảng | Cột quan trọng | Mô tả |
|---|---|---|
| `songs` | `priority_source` (int?) | Nguồn ưu tiên: null=auto, 0=Internal, 1=YouTube, 2=Deezer |
| `external_view_counts` | `song_id`, `source`, `view_count`, `last_updated` | View count từ từng nguồn |
| `external_view_counts` | unique index `(song_id, source)` | Mỗi bài hát chỉ có 1 record per nguồn |

---

## Bảng Tóm Tắt Use Cases

| Use Case | Controller | Endpoint chính | Yêu cầu |
|---|---|---|---|
| Thêm/Sửa/Xóa bài hát | `AdminSongController` | `/AdminSong/Create`, `/Edit`, `/Delete` | Admin |
| Thêm/Sửa/Xóa nghệ sĩ | `AdminArtistController` | `/AdminArtist/Create`, `/Edit`, `/Delete` | Admin |
| Thêm/Sửa/Xóa thể loại | `AdminTaxonomyController` | `/AdminTaxonomy/CreateGenre`, `/EditGenre`, `/DeleteGenre` | Admin |
| Xem danh sách người dùng | `AdminUserController` | `/AdminUser/Index`, `/Details/{id}` | Admin |
| Quản lý người dùng | `AdminUserController` | `/ToggleUserLock`, `/GrantPremium`, `/RevokePremium`, `/Delete` | Admin |
| Xem danh sách báo cáo | `AdminReportController` | `/AdminReport/Index`, `/Details/{id}` | Admin |
| Xử lý báo cáo | `AdminReportController` | `/AdminReport/Resolve`, `/Dismiss` | Admin |
| Xóa bình luận vi phạm | `AdminSupportController` | `/AdminSupport/Comments`, `/DeleteComment` | Admin |
| Xem thống kê doanh thu | `AdminDashboardController` | `/Admin` (Dashboard) | Admin |
| Xem thống kê lượt nghe | `AdminDashboardController` | `/Admin/SyncPlayCountFromHistory` | Admin |
| Quản lý View Count | `AdminViewCountController` / `ViewCountController` | `/api/admin/songs/{id}/sync-view-count`, `/api/admin/songs/{id}/priority-source`, `/api/songs/{id}/view-count` | Admin (sync/priority), Public (get) |

---

## Cơ Chế Bảo Mật Admin

### 1. Role-based Authorization
Tất cả admin controllers đều có `[Authorize(Roles = "Admin")]` ở class level:
```csharp
[Authorize(Roles = "Admin")]
public class AdminSongController : BaseController { ... }
```
User thường truy cập → HTTP 403 Forbidden.

### 2. Anti-CSRF Protection
Tất cả POST actions đều có `[ValidateAntiForgeryToken]`:
```csharp
[HttpPost]
[ValidateAntiForgeryToken]
public async Task<IActionResult> Delete(int id, ...) { ... }
```
Ngăn chặn Cross-Site Request Forgery attacks.

### 3. Self-Protection Invariant
Admin không thể tự khóa hoặc xóa tài khoản của chính mình:
```csharp
if (id == CurrentAdminId)
{
    TempData["Error"] = "You cannot lock/delete your own administrative account.";
    return RedirectToAction(...);
}
```

### 4. Soft Delete cho nội dung quan trọng
Song, Artist, User đều dùng soft delete (`is_deleted = true`) — dữ liệu không bị mất vĩnh viễn, có thể khôi phục nếu cần.
