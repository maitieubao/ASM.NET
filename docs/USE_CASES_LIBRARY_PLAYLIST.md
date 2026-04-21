# Tài Liệu Use Case: Library & Playlist

## Tổng Quan

Module **Library & Playlist** cho phép người dùng (Customer) quản lý thư viện nhạc cá nhân của mình. Đây là một trong những tính năng cốt lõi của VibeMusic, bao gồm 8 use case chính được thể hiện trong sơ đồ use case.

**Actor**: Customer (người dùng đã đăng nhập)

**Yêu cầu xác thực**: Tất cả 8 use case đều yêu cầu người dùng đã đăng nhập (`[Authorize]`).

---

## Kiến Trúc Xử Lý

```
Browser (Customer)
    │
    ▼
PlaylistController          ← Tầng Presentation (VibeMusic/)
    │ inject
    ▼
IPlaylistService            ← Interface (VibeMusic.Application/Interfaces/)
    │ implement
    ▼
PlaylistService             ← Business Logic (VibeMusic.Application/Services/)
    │ inject
    ▼
IUnitOfWork → Repository<Playlist>
              Repository<PlaylistSong>
              Repository<SongLike>
    │ implement
    ▼
AppDbContext → PostgreSQL   ← Tầng Infrastructure (VibeMusic.Infrastructure/)
```

**Các class tham gia chính**:

| Class | Project | Vai trò |
|---|---|---|
| `PlaylistController` | VibeMusic | Nhận HTTP request, trả HTTP response |
| `InteractionController` | VibeMusic | Xử lý like/unlike bài hát |
| `BaseController` | VibeMusic | Cung cấp `CurrentUserId`, `IsAdmin`, `SuccessResponse()` |
| `IPlaylistService` | VibeMusic.Application | Hợp đồng nghiệp vụ playlist |
| `PlaylistService` | VibeMusic.Application | Triển khai toàn bộ logic playlist |
| `IInteractionService` | VibeMusic.Application | Hợp đồng nghiệp vụ tương tác (like, history) |
| `InteractionService` | VibeMusic.Application | Triển khai logic like/unlike |
| `Playlist` | VibeMusic.Domain | Entity bảng `playlists` |
| `PlaylistSong` | VibeMusic.Domain | Entity bảng `playlistsongs` (quan hệ nhiều-nhiều) |
| `SongLike` | VibeMusic.Domain | Entity bảng `songlikes` |
| `PlaylistDto` | VibeMusic.Application | DTO truyền dữ liệu playlist |
| `SongDto` | VibeMusic.Application | DTO truyền dữ liệu bài hát |

---

## Use Case 1: Xem Danh Sách Playlist

### Mô tả
Customer xem toàn bộ danh sách playlist cá nhân của mình, kèm theo các playlist nổi bật (featured) do admin tạo.

### Luồng xử lý

```
GET /Playlist/Index
    │
    ▼
PlaylistController.Index()
    ├── Kiểm tra CurrentUserId != null (nếu null → redirect Login)
    ├── _playlistService.GetUserPlaylistsAsync(userId)
    │       └── Query: SELECT * FROM playlists WHERE userid = @userId AND is_deleted = false
    │               ORDER BY createdat DESC
    └── ViewBag.FeaturedPlaylists = _playlistService.GetFeaturedPlaylistsAsync()
            └── Query: SELECT * FROM playlists WHERE isfeatured = true AND is_deleted = false
                    ORDER BY createdat DESC
    │
    ▼
View: Playlist/Index.cshtml
    ├── Hiển thị danh sách playlist cá nhân
    └── Hiển thị playlist nổi bật (Top Hits, Trending, New Releases...)
```

### Dữ liệu trả về (`PlaylistDto`)
```csharp
public class PlaylistDto
{
    public int PlaylistId { get; set; }
    public string Title { get; set; }
    public string? Description { get; set; }
    public string? CoverImageUrl { get; set; }
    public bool IsFeatured { get; set; }
    public string? FeaturedType { get; set; }  // "TopHits", "Trending", "NewReleases"
    public string Visibility { get; set; }     // "Public" hoặc "Private"
    public List<SongDto> Songs { get; set; }   // Danh sách bài hát (lazy load)
}
```

### Điểm đáng chú ý
- `GetUserPlaylistsAsync` dùng `AsNoTracking()` và projection trực tiếp sang DTO — không load navigation properties không cần thiết
- Featured playlists có `UserId = null` (không thuộc về user nào, do admin tạo)

---

## Use Case 2: Tạo Playlist Mới

### Mô tả
Customer tạo một playlist mới với tên và mô tả tùy chọn.

### Luồng xử lý

```
POST /Playlist/Create
    Body: title="My Playlist", description="..."
    │
    ▼
PlaylistController.Create(title, description)
    ├── Kiểm tra CurrentUserId != null
    ├── Kiểm tra title không rỗng
    └── _playlistService.CreatePlaylistAsync(userId, title, description)
            ├── Tạo entity Playlist mới:
            │       UserId = userId
            │       Title = title
            │       IsFeatured = false
            │       Visibility = "Public"
            │       CreatedAt = DateTime.UtcNow
            ├── Repository<Playlist>().AddAsync(playlist)
            └── UnitOfWork.CompleteAsync() → INSERT INTO playlists
    │
    ▼
RedirectToAction("Index") → Quay về danh sách playlist
```

### Validation
- `title` không được rỗng (kiểm tra ở controller)
- Không giới hạn số lượng playlist mỗi user

### Điểm đáng chú ý
- Playlist mới luôn có `Visibility = "Public"` mặc định
- `IsFeatured = false` — chỉ admin mới tạo được featured playlist

---

## Use Case 3: Sửa Thông Tin Playlist

### Mô tả
Customer chỉnh sửa tên, mô tả, ảnh bìa, hoặc chế độ hiển thị (Public/Private) của playlist.

### Luồng xử lý

```
GET /Playlist/Edit/{id}
    │
    ▼
PlaylistController.Edit(id)
    └── _playlistService.GetPlaylistByIdAsync(id, userId, isAdmin=false)
            ├── Kiểm tra playlist tồn tại và chưa bị xóa
            ├── Nếu Visibility = "Private" và userId != owner → throw UnauthorizedAccessException
            └── Trả về PlaylistDto với danh sách bài hát
    │
    ▼
View: Playlist/Edit.cshtml (form chỉnh sửa)

POST /Playlist/Edit
    Body: PlaylistDto { PlaylistId, Title, Description, CoverImageUrl, Visibility }
    │
    ▼
PlaylistController.Edit(dto)
    └── _playlistService.UpdatePlaylistAsync(dto, userId, isAdmin=false)
            ├── Lấy playlist từ DB
            ├── Kiểm tra: playlist.UserId != userId && !isAdmin → throw UnauthorizedAccessException
            ├── Cập nhật: Title, Description, CoverImageUrl, Visibility
            └── UnitOfWork.CompleteAsync() → UPDATE playlists SET ...
    │
    ▼
RedirectToAction("Details", { id }) → Xem chi tiết playlist
```

### Phân quyền
- User chỉ sửa được playlist của chính mình (`playlist.UserId == userId`)
- Admin có thể sửa playlist của bất kỳ user nào (`isAdmin = true`)
- Vi phạm quyền → `UnauthorizedAccessException` → HTTP 403 Forbidden

---

## Use Case 4: Xóa Playlist

### Mô tả
Customer xóa một playlist. Hệ thống thực hiện **soft delete** (đánh dấu `IsDeleted = true`) thay vì xóa vật lý khỏi database.

### Luồng xử lý

```
POST /Playlist/Delete
    Body: playlistId=5
    │
    ▼
PlaylistController.Delete(playlistId)
    └── _playlistService.DeletePlaylistAsync(playlistId, userId, isAdmin=false)
            ├── Lấy playlist: WHERE playlistid = @id AND is_deleted = false
            ├── Nếu không tìm thấy → return (không throw exception)
            ├── Kiểm tra: playlist.UserId != userId && !isAdmin → throw UnauthorizedAccessException
            ├── playlist.IsDeleted = true
            └── UnitOfWork.CompleteAsync() → UPDATE playlists SET is_deleted = true
    │
    ▼
SuccessResponse({ success: true }) → JSON response cho AJAX call
```

### Tại sao dùng Soft Delete?
- Dữ liệu không bị mất vĩnh viễn — có thể khôi phục nếu cần
- Tránh cascade delete phức tạp với `PlaylistSong`
- Audit trail: biết khi nào playlist bị xóa
- Tất cả queries đều có `WHERE is_deleted = false` để lọc ra

### Xử lý lỗi
```csharp
try {
    await _playlistService.DeletePlaylistAsync(playlistId, userId, IsAdmin);
    return SuccessResponse(new { success = true });
}
catch (UnauthorizedAccessException) {
    return Forbid();  // HTTP 403
}
catch (Exception) {
    return BadRequestResponse("An error occurred while deleting the playlist.");
}
```

---

## Use Case 5: Thêm Bài Hát Vào Playlist

### Mô tả
Customer thêm một bài hát vào playlist. Hỗ trợ 3 cách thêm:
1. Thêm bằng `SongId` (bài hát đã có trong hệ thống)
2. Thêm bằng `YoutubeId` (import bài hát từ YouTube rồi thêm)
3. Thêm vào nhiều playlist cùng lúc

### Luồng xử lý — Thêm bằng SongId

```
POST /Playlist/AddSong?playlistId=5&songId=42
    │
    ▼
PlaylistController.AddSong(playlistId, songId)
    └── _playlistService.AddSongToPlaylistAsync(5, 42, userId, isAdmin=false)
            ├── Lấy playlist, kiểm tra quyền sở hữu
            ├── Kiểm tra bài hát đã có trong playlist chưa
            │       SELECT * FROM playlistsongs WHERE playlistid=5 AND songid=42
            ├── Nếu chưa có:
            │       Tính position tiếp theo:
            │           SELECT MAX(position) FROM playlistsongs WHERE playlistid=5
            │       INSERT INTO playlistsongs (playlistid, songid, position, addedat)
            └── UnitOfWork.CompleteAsync()
    │
    ▼
SuccessResponse({ success: true })
```

### Luồng xử lý — Thêm bằng YoutubeId (với retry)

```
POST /Playlist/AddSongByYoutubeId?playlistId=5&youtubeId=dQw4w9WgXcQ
    │
    ▼
PlaylistController.AddSongByYoutubeId(playlistId, youtubeId)
    ├── Retry loop (tối đa 3 lần, backoff 1s/2s):
    │       song = await _songService.GetOrCreateByYoutubeIdAsync(youtubeId)
    │           ├── Tìm bài hát trong DB theo YoutubeVideoId
    │           ├── Nếu chưa có: gọi YouTube API → import metadata → lưu vào DB
    │           └── Trả về SongDto
    └── Nếu song != null:
            AddSongToPlaylistAsync(playlistId, song.SongId, userId)
    │
    ▼
SuccessResponse({ success: true, songTitle: "..." })
```

### Tại sao có retry?
Npgsql (PostgreSQL driver) có thể gặp transient errors khi import bài hát mới (concurrent inserts). Retry với exponential backoff giải quyết vấn đề này mà không cần user thao tác lại.

### Cơ chế tránh duplicate
`AddSongToPlaylistAsync` kiểm tra `PlaylistSong` đã tồn tại trước khi insert — đảm bảo mỗi bài hát chỉ xuất hiện một lần trong playlist.

---

## Use Case 6: Xóa Bài Hát Khỏi Playlist

### Mô tả
Customer xóa một bài hát khỏi playlist và hệ thống tự động cập nhật lại thứ tự (position) của các bài còn lại.

### Luồng xử lý

```
POST /Playlist/RemoveSong?playlistId=5&songId=42
    │
    ▼
PlaylistController.RemoveSong(playlistId, songId)
    └── _playlistService.RemoveSongFromPlaylistAsync(5, 42, userId, isAdmin=false)
            ├── Lấy playlist, kiểm tra quyền (AsNoTracking — chỉ đọc)
            ├── Lấy PlaylistSong cần xóa
            ├── Lưu lại position của bài bị xóa (deletedPos)
            ├── Repository<PlaylistSong>().Remove(existing)
            ├── UnitOfWork.CompleteAsync() → DELETE FROM playlistsongs
            └── Bulk re-index (1 SQL command thay vì N updates):
                    ExecuteSqlRawAsync(
                        "UPDATE playlistsongs SET position = position - 1
                         WHERE playlistid = @id AND position > @deletedPos"
                    )
    │
    ▼
SuccessResponse({ success: true })
```

### Tối ưu hóa re-indexing
Thay vì load tất cả `PlaylistSong` còn lại và update từng cái (N queries), hệ thống dùng một câu SQL duy nhất để giảm position của tất cả bài hát phía sau bài bị xóa. Đây là ví dụ điển hình của việc "đẩy logic xuống database" để tối ưu hiệu suất.

---

## Use Case 7: Xem Danh Sách Yêu Thích

### Mô tả
Customer xem tất cả bài hát đã like, được hiển thị dưới dạng một "playlist ảo" với phân trang.

### Luồng xử lý

```
GET /Playlist/LikedSongs?page=1
    │
    ▼
PlaylistController.LikedSongs(page)
    ├── Kiểm tra CurrentUserId != null
    ├── _interactionService.GetLikedSongIdsPaginatedAsync(userId, page, pageSize=20)
    │       └── Query: SELECT songid FROM songlikes WHERE userid = @userId
    │               ORDER BY createdat DESC
    │               OFFSET @skip LIMIT @pageSize
    │               + COUNT(*) for total
    └── _songService.GetSongsByIdsPaginatedAsync(likedSongIds, 1, pageSize)
            └── Query: SELECT * FROM songs WHERE songid IN (@ids) AND is_deleted = false
    │
    ▼
View: Playlist/LikedSongs.cshtml
    ├── Hiển thị danh sách bài hát đã thích
    ├── ViewBag.CurrentPage, ViewBag.TotalPages (phân trang)
    └── Model: PlaylistDto { Title = "Bài hát đã thích", Songs = [...] }
```

### Tại sao dùng 2 queries thay vì 1 JOIN?
- Query 1 lấy danh sách `songId` đã like (nhẹ, chỉ lấy ID)
- Query 2 lấy chi tiết bài hát theo ID list
- Cách này cho phép phân trang chính xác theo thứ tự like (không phải thứ tự bài hát)
- Tránh N+1 problem khi load thông tin bài hát

---

## Use Case 8: Thêm / Bỏ Bài Hát Yêu Thích

### Mô tả
Customer toggle trạng thái yêu thích của một bài hát — nếu chưa like thì like, nếu đã like thì bỏ like.

### Luồng xử lý

```
POST /Interaction/ToggleLike
    Body: { songId: 42 }
    │
    ▼
InteractionController.ToggleLike(songId)
    └── _interactionService.ToggleLikeAsync(userId, songId)
            ├── Kiểm tra SongLike đã tồn tại chưa:
            │       SELECT * FROM songlikes WHERE userid=@userId AND songid=@songId
            ├── Nếu chưa có (chưa like):
            │       INSERT INTO songlikes (userid, songid, createdat)
            │       return true (đã like)
            └── Nếu đã có (đã like):
                    DELETE FROM songlikes WHERE userid=@userId AND songid=@songId
                    return false (đã bỏ like)
    │
    ▼
SuccessResponse({ isLiked: true/false, likeCount: N })
```

### Cập nhật UI real-time
Frontend JavaScript nhận response và cập nhật icon tim (❤️/🤍) ngay lập tức mà không cần reload trang — đây là AJAX call.

### Kiểm tra trạng thái like khi load playlist
Khi `GetPlaylistByIdAsync` load danh sách bài hát, nó inline kiểm tra trạng thái like:
```csharp
Songs = p.PlaylistSongs
    .Select(ps => new SongDto
    {
        SongId = ps.Song.SongId,
        Title = ps.Song.Title,
        // Inline check: bài hát này có được user hiện tại like không?
        IsLiked = userId.HasValue && ps.Song.SongLikes.Any(l => l.UserId == userId.Value)
    })
```
EF Core dịch `Any(...)` thành `EXISTS (SELECT 1 FROM songlikes WHERE ...)` — một subquery hiệu quả.

---

## Tính Năng Bổ Sung: Sắp Xếp Lại Thứ Tự Bài Hát

Mặc dù không xuất hiện trong sơ đồ use case, `PlaylistController` còn hỗ trợ:

```
POST /Playlist/Reorder
    Body: { playlistId: 5, sortedSongIds: [42, 15, 7, 33] }
    │
    ▼
PlaylistService.ReorderSongsAsync(playlistId, sortedSongIds, userId)
    ├── Kiểm tra quyền sở hữu
    ├── Load tất cả PlaylistSong của playlist
    └── Cập nhật position theo thứ tự mới:
            for (int i = 0; i < sortedSongIds.Count; i++)
                playlistSong.Position = i;
            UnitOfWork.CompleteAsync()
```

---

## Bảng Tóm Tắt Use Cases

| Use Case | HTTP Method | Endpoint | Service Method | Database Operation |
|---|---|---|---|---|
| Xem danh sách playlist | GET | `/Playlist/Index` | `GetUserPlaylistsAsync` | SELECT với filter |
| Tạo playlist mới | POST | `/Playlist/Create` | `CreatePlaylistAsync` | INSERT |
| Sửa thông tin playlist | POST | `/Playlist/Edit` | `UpdatePlaylistAsync` | UPDATE |
| Xóa playlist | POST | `/Playlist/Delete` | `DeletePlaylistAsync` | UPDATE (soft delete) |
| Thêm bài hát | POST | `/Playlist/AddSong` | `AddSongToPlaylistAsync` | INSERT + SELECT MAX |
| Xóa bài hát | POST | `/Playlist/RemoveSong` | `RemoveSongFromPlaylistAsync` | DELETE + bulk UPDATE |
| Xem yêu thích | GET | `/Playlist/LikedSongs` | `GetLikedSongIdsPaginatedAsync` | SELECT với pagination |
| Toggle yêu thích | POST | `/Interaction/ToggleLike` | `ToggleLikeAsync` | INSERT hoặc DELETE |

---

## Cơ Chế Phân Quyền

Tất cả các thao tác thay đổi dữ liệu đều kiểm tra quyền sở hữu ở **service layer** (không phải controller):

```csharp
// Trong PlaylistService — pattern nhất quán cho mọi thao tác
if (playlist.UserId != userId && !isAdmin)
    throw new UnauthorizedAccessException("Bạn không có quyền...");
```

Controller bắt exception và trả về HTTP 403:
```csharp
catch (UnauthorizedAccessException) {
    return Forbid();  // HTTP 403 Forbidden
}
```

**Tại sao kiểm tra ở service layer thay vì controller?**
- Service có thể được gọi từ nhiều nơi (controller, background job, admin panel)
- Đảm bảo quyền được kiểm tra dù gọi từ đâu
- Dễ test: chỉ cần test service, không cần test controller
