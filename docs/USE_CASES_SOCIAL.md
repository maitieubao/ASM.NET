# Tài Liệu Use Case: Social

## Tổng Quan

Module **Social** cho phép người dùng (Customer) tương tác với cộng đồng trên nền tảng VibeMusic. Bao gồm 7 use case: quản lý bình luận bài hát và theo dõi nghệ sĩ.

**Actor**: Customer (người dùng đã đăng nhập)

**Yêu cầu xác thực**: Tất cả 7 use case đều yêu cầu `[Authorize]`. Riêng xem bình luận (`GetSongComments`) cho phép `[AllowAnonymous]` — khách vãng lai vẫn đọc được nhưng không thể tương tác.

---

## Kiến Trúc Xử Lý

```
Browser (Customer)
    │
    ▼
CommentController          ← Bình luận (VibeMusic/Controllers/)
ArtistController           ← Theo dõi nghệ sĩ (VibeMusic/Controllers/)
    │ inject
    ▼
ICommentService            ← Interface bình luận (VibeMusic.Application/Interfaces/)
IArtistService             ← Interface nghệ sĩ (VibeMusic.Application/Interfaces/)
    │ implement
    ▼
CommentService             ← Logic bình luận (VibeMusic.Application/Services/)
ArtistService              ← Logic nghệ sĩ (VibeMusic.Application/Services/)
    │ inject
    ▼
IUnitOfWork → Repository<Comment>
              Repository<CommentLike>
              Repository<ArtistFollower>
              Repository<Artist>
    │ implement
    ▼
AppDbContext → PostgreSQL   ← (VibeMusic.Infrastructure/)
```

**Các class tham gia chính**:

| Class | Project | Vai trò |
|---|---|---|
| `CommentController` | VibeMusic | Nhận HTTP request liên quan đến bình luận |
| `ArtistController` | VibeMusic | Nhận HTTP request liên quan đến nghệ sĩ |
| `BaseController` | VibeMusic | Cung cấp `CurrentUserId`, `IsAdmin`, `SuccessResponse()` |
| `ICommentService` | VibeMusic.Application | Hợp đồng nghiệp vụ bình luận |
| `CommentService` | VibeMusic.Application | Triển khai toàn bộ logic bình luận |
| `IArtistService` | VibeMusic.Application | Hợp đồng nghiệp vụ nghệ sĩ |
| `ArtistService` | VibeMusic.Application | Triển khai logic nghệ sĩ (bao gồm follow) |
| `Comment` | VibeMusic.Domain | Entity bảng `comments` |
| `CommentLike` | VibeMusic.Domain | Entity bảng `commentlikes` |
| `ArtistFollower` | VibeMusic.Domain | Entity bảng `artist_followers` |
| `Artist` | VibeMusic.Domain | Entity bảng `artists` |
| `CommentDto` | VibeMusic.Application | DTO truyền dữ liệu bình luận (có hỗ trợ cây replies) |
| `CommentLikeStatusDto` | VibeMusic.Application | DTO trả về trạng thái like sau khi toggle |
| `ArtistDto` | VibeMusic.Application | DTO truyền dữ liệu nghệ sĩ |

---

## Cấu Trúc Dữ Liệu Bình Luận

### Entity `Comment`
```csharp
[Table("comments")]
public class Comment
{
    public int CommentId { get; set; }
    public int UserId { get; set; }          // Người viết bình luận
    public int SongId { get; set; }          // Bài hát được bình luận
    public string Content { get; set; }      // Nội dung (đã HTML encode)
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; } // null nếu chưa sửa
    public int? ParentCommentId { get; set; } // null = bình luận gốc, có giá trị = reply
}
```

**Cấu trúc cây bình luận**: Hệ thống hỗ trợ 2 cấp — bình luận gốc (`ParentCommentId = null`) và trả lời (`ParentCommentId = id của bình luận cha`). Không hỗ trợ reply lồng nhau nhiều cấp để tránh phức tạp UI.

### DTO `CommentDto`
```csharp
public class CommentDto
{
    public int CommentId { get; set; }
    public string UserName { get; set; }         // Tên hiển thị
    public string? UserAvatarUrl { get; set; }   // Ảnh đại diện
    public string Content { get; set; }          // Nội dung đã encode
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }     // Hiển thị "(đã chỉnh sửa)" nếu có
    public int? ParentCommentId { get; set; }
    public int LikeCount { get; set; }           // Số lượt thích
    public bool IsLiked { get; set; }            // User hiện tại đã like chưa
    public IEnumerable<CommentDto> Replies { get; set; } // Danh sách trả lời
}
```

---

## Use Case 1: Viết Bình Luận

### Mô tả
Customer viết bình luận mới cho một bài hát. Nội dung được kiểm tra và mã hóa HTML trước khi lưu.

### Luồng xử lý

```
POST /Comment/AddComment?songId=42&content="Bài hát hay quá!"&parentId=null
    │
    ▼
CommentController.AddComment(songId, content, parentId)
    ├── Kiểm tra CurrentUserId != null → 401 nếu chưa đăng nhập
    ├── Kiểm tra content không rỗng → 400 nếu rỗng
    ├── Kiểm tra content.Length <= 1000 → 400 nếu quá dài
    ├── encodedContent = WebUtility.HtmlEncode(content)
    │       "<script>" → "&lt;script&gt;" (chống XSS)
    └── _commentService.CreateCommentAsync(userId, songId, encodedContent, parentId=null)
            ├── Tạo entity Comment:
            │       UserId = userId
            │       SongId = songId
            │       Content = encodedContent
            │       ParentCommentId = null (bình luận gốc)
            │       CreatedAt = DateTime.UtcNow
            ├── Repository<Comment>().AddAsync(comment)
            ├── UnitOfWork.CompleteAsync() → INSERT INTO comments
            └── Load thông tin user để trả về CommentDto đầy đủ
    │
    ▼
SuccessResponse(CommentDto) → JSON với thông tin bình luận vừa tạo
```

### Bảo mật XSS
`WebUtility.HtmlEncode()` chuyển đổi các ký tự đặc biệt HTML thành entity an toàn trước khi lưu vào database. Khi hiển thị, browser render entity thành text thay vì thực thi code.

### Dữ liệu trả về
```json
{
  "commentId": 123,
  "userId": 5,
  "userName": "nguyenvana",
  "userAvatarUrl": "https://...",
  "songId": 42,
  "content": "Bài hát hay quá!",
  "createdAt": "2026-04-19T10:30:00Z",
  "parentCommentId": null,
  "likeCount": 0,
  "isLiked": false,
  "replies": []
}
```

---

## Use Case 2: Trả Lời Bình Luận

### Mô tả
Customer trả lời một bình luận đã có. Về mặt kỹ thuật, đây là cùng một use case với "Viết bình luận" — chỉ khác ở tham số `parentId`.

### Luồng xử lý

```
POST /Comment/AddComment?songId=42&content="Đồng ý!"&parentId=123
    │
    ▼
CommentController.AddComment(songId, content, parentId=123)
    │ (Validation giống Use Case 1)
    └── _commentService.CreateCommentAsync(userId, songId, encodedContent, parentId=123)
            ├── Tạo entity Comment với ParentCommentId = 123
            └── INSERT INTO comments (parentcommentid = 123)
    │
    ▼
SuccessResponse(CommentDto { ParentCommentId: 123 })
```

### Cách hiển thị cây bình luận

Khi load bình luận, `CommentService` xây dựng cây bình luận từ danh sách phẳng:

```csharp
private IEnumerable<CommentDto> BuildCommentTree(List<Comment> allComments, ...)
{
    // Bước 1: Tạo dictionary để tra cứu nhanh theo CommentId
    var dict = flatDtos.ToDictionary(d => d.CommentId);

    // Bước 2: Phân loại bình luận gốc và replies
    foreach (var dto in flatDtos)
    {
        if (dto.ParentCommentId == null)
            tree.Add(dto);                          // Bình luận gốc
        else if (dict.TryGetValue(dto.ParentCommentId.Value, out var parent))
            ((List<CommentDto>)parent.Replies).Add(dto); // Gắn vào cha
    }

    return OrderTree(tree); // Sắp xếp theo thời gian mới nhất
}
```

**Kết quả**: Frontend nhận một cây JSON có cấu trúc phân cấp, không cần xử lý thêm.

---

## Use Case 3: Sửa Bình Luận Của Mình

### Mô tả
Customer chỉnh sửa nội dung bình luận đã viết. Chỉ được sửa bình luận của chính mình.

### Luồng xử lý

```
PUT /Comment/EditComment?commentId=123&content="Bài hát rất hay!"
    │
    ▼
CommentController.EditComment(commentId, content)
    ├── Kiểm tra CurrentUserId != null
    ├── Kiểm tra content không rỗng và <= 1000 ký tự
    ├── encodedContent = WebUtility.HtmlEncode(content)
    └── _commentService.UpdateCommentAsync(commentId, userId, encodedContent)
            ├── Lấy comment từ DB: GetByIdAsync(commentId)
            ├── Kiểm tra: comment.UserId == userId
            │       Nếu không khớp → return (không throw, không cập nhật)
            ├── comment.Content = encodedContent
            ├── comment.UpdatedAt = DateTime.UtcNow  ← Đánh dấu đã chỉnh sửa
            └── UnitOfWork.CompleteAsync() → UPDATE comments SET content=..., updatedat=...
    │
    ▼
SuccessResponse({ success: true })
```

### Phân quyền im lặng
`UpdateCommentAsync` kiểm tra `comment.UserId == userId` — nếu không khớp, method **return im lặng** (không throw exception, không trả về lỗi). Đây là thiết kế phòng thủ: tránh lộ thông tin về sự tồn tại của comment cho người dùng không có quyền.

### Hiển thị trạng thái đã sửa
`UpdatedAt != null` → Frontend hiển thị nhãn "(đã chỉnh sửa)" bên cạnh bình luận.

---

## Use Case 4: Xóa Bình Luận Của Mình

### Mô tả
Customer xóa bình luận đã viết. Hệ thống thực hiện **hard delete** (xóa vật lý) — khác với playlist dùng soft delete.

### Luồng xử lý

```
DELETE /Comment/DeleteComment?commentId=123
    │
    ▼
CommentController.DeleteComment(commentId)
    ├── Kiểm tra CurrentUserId != null
    └── _commentService.DeleteCommentAsync(commentId, userId=CurrentUserId)
            ├── Lấy comment từ DB: GetByIdAsync(commentId)
            ├── Nếu comment == null → return (không làm gì)
            ├── Kiểm tra: userId != null && comment.UserId != userId → return
            │       (Admin gọi với userId=null → bỏ qua kiểm tra, xóa được mọi comment)
            ├── Repository<Comment>().Remove(comment)
            └── UnitOfWork.CompleteAsync() → DELETE FROM comments WHERE commentid=123
    │
    ▼
SuccessResponse({ success: true })
```

### Tại sao dùng Hard Delete thay vì Soft Delete?
- Bình luận bị xóa không cần khôi phục (khác với playlist/bài hát)
- Giảm kích thước bảng `comments` theo thời gian
- Đơn giản hóa query — không cần `WHERE is_deleted = false`

### Dual-role của `DeleteCommentAsync`
Tham số `userId` là nullable:
- `userId = CurrentUserId` → User xóa bình luận của mình (kiểm tra quyền sở hữu)
- `userId = null` → Admin xóa bất kỳ bình luận nào (bỏ qua kiểm tra)

Cùng một method phục vụ cả 2 use case, tránh duplicate code.

---

## Use Case 5: Thích / Bỏ Thích Bình Luận

### Mô tả
Customer toggle trạng thái thích của một bình luận. Hệ thống trả về trạng thái mới và số lượt thích cập nhật trong một lần gọi.

### Luồng xử lý

```
POST /Comment/ToggleLike?commentId=123
    │
    ▼
CommentController.ToggleLike(commentId)
    ├── Kiểm tra CurrentUserId != null
    └── _commentService.ToggleCommentLikeAndGetStatusAsync(userId, commentId)
            ├── Kiểm tra CommentLike đã tồn tại:
            │       SELECT * FROM commentlikes WHERE userid=@userId AND commentid=@commentId
            ├── Nếu đã tồn tại (đã like):
            │       Repository<CommentLike>().Remove(existing)
            │       isLiked = false
            └── Nếu chưa tồn tại (chưa like):
                    Repository<CommentLike>().AddAsync(new CommentLike { UserId, CommentId })
                    isLiked = true
            ├── UnitOfWork.CompleteAsync() → INSERT hoặc DELETE
            └── Đếm lại tổng likes:
                    SELECT COUNT(*) FROM commentlikes WHERE commentid=@commentId
            │
            ▼
            return CommentLikeStatusDto { IsLiked, LikeCount }
    │
    ▼
SuccessResponse({ isLiked: true/false, likeCount: N })
```

### Tối ưu hóa: Giảm round-trips
Method `ToggleCommentLikeAndGetStatusAsync` thực hiện toggle **và** lấy count mới trong cùng một lần gọi service. Nếu tách thành 2 calls riêng biệt (`ToggleLike` + `GetLikeCount`), sẽ cần 2 HTTP requests từ frontend.

### Cập nhật UI real-time
Frontend nhận `{ isLiked, likeCount }` và cập nhật ngay:
- Icon tim: ❤️ (đã like) hoặc 🤍 (chưa like)
- Số đếm: hiển thị `likeCount` mới

---

## Use Case 6: Theo Dõi Nghệ Sĩ

### Mô tả
Customer nhấn nút "Follow" trên trang nghệ sĩ để theo dõi. Hệ thống cập nhật `SubscriberCount` của nghệ sĩ và xóa cache.

### Luồng xử lý

```
POST /ToggleFollow/5
    │
    ▼
ArtistController.ToggleFollow(id=5)
    ├── Kiểm tra CurrentUserId != null → 401
    └── _artistService.ToggleFollowAsync(userId, artistId=5)
            ├── Kiểm tra ArtistFollower đã tồn tại:
            │       SELECT * FROM artist_followers WHERE userid=@userId AND artistid=5
            ├── Nếu chưa theo dõi (follow):
            │       INSERT INTO artist_followers (userid, artistid, followedat)
            │       artist.SubscriberCount++
            │       UnitOfWork.CompleteAsync()
            │       _cache.Remove("artist_details_5")  ← Xóa cache
            │       return true (đang theo dõi)
            └── Nếu đã theo dõi (unfollow):
                    DELETE FROM artist_followers WHERE userid=@userId AND artistid=5
                    artist.SubscriberCount = Math.Max(0, artist.SubscriberCount - 1)
                    UnitOfWork.CompleteAsync()
                    _cache.Remove("artist_details_5")  ← Xóa cache
                    return false (đã bỏ theo dõi)
    │
    ▼
SuccessResponse({ success: true, isFollowing: true/false })
```

### Cập nhật `SubscriberCount` ngay lập tức
Khi follow/unfollow, `SubscriberCount` của `Artist` entity được cập nhật trong cùng transaction. Điều này đảm bảo số liệu hiển thị trên trang nghệ sĩ luôn chính xác mà không cần query lại.

### Tại sao xóa cache?
`ArtistService` cache thông tin nghệ sĩ trong `IMemoryCache` với key `"artist_details_{id}"` (TTL 1 giờ). Sau khi follow/unfollow, `SubscriberCount` thay đổi → cache cũ không còn chính xác → phải xóa để lần load tiếp theo lấy dữ liệu mới từ DB.

---

## Use Case 7: Bỏ Theo Dõi Nghệ Sĩ

### Mô tả
Customer nhấn nút "Unfollow" để bỏ theo dõi nghệ sĩ đang theo dõi.

### Luồng xử lý

Đây là **cùng một endpoint** với Use Case 6 — `ToggleFollow` xử lý cả hai chiều:

```
POST /ToggleFollow/5  (khi đang theo dõi)
    │
    ▼
ArtistController.ToggleFollow(id=5)
    └── _artistService.ToggleFollowAsync(userId, artistId=5)
            ├── Tìm thấy ArtistFollower → xóa
            ├── artist.SubscriberCount-- (không xuống dưới 0)
            └── return false (đã bỏ theo dõi)
    │
    ▼
SuccessResponse({ success: true, isFollowing: false })
```

Frontend dựa vào `isFollowing` trong response để cập nhật nút:
- `isFollowing: true` → hiển thị nút "Đang theo dõi" (có thể click để unfollow)
- `isFollowing: false` → hiển thị nút "Theo dõi"

---

## Tính Năng Bổ Sung: Load Bình Luận Với Phân Trang

Mặc dù không xuất hiện trong sơ đồ use case, `CommentController` hỗ trợ load bình luận với phân trang:

```
GET /Comment/GetSongComments?songId=42&page=1&pageSize=20
    │ [AllowAnonymous] — khách vãng lai cũng xem được
    ▼
CommentController.GetSongComments(songId, page, pageSize)
    └── _commentService.GetSongCommentsPaginatedAsync(songId, currentUserId, page, pageSize)
            ├── Query 1: Lấy comments với phân trang
            │       SELECT * FROM comments WHERE songid=42
            │       ORDER BY createdat DESC
            │       OFFSET @skip LIMIT @pageSize
            │       + COUNT(*) for total
            ├── Query 2: Lấy likes cho tất cả comments trong trang
            │       SELECT * FROM commentlikes WHERE commentid IN (@ids)
            └── Map sang CommentDto với IsLiked và LikeCount
    │
    ▼
SuccessResponse({ comments: [...], totalCount: N, page: 1, pageSize: 20 })
```

### Tại sao tách thành 2 queries?
- Query 1 lấy comments với phân trang (chỉ lấy 20 comments)
- Query 2 lấy likes cho đúng 20 comments đó (không phải toàn bộ)
- Tránh N+1 problem: không query likes từng comment một
- Hiệu quả hơn JOIN vì likes có thể rất nhiều

---

## Bảng Tóm Tắt Use Cases

| Use Case | HTTP Method | Endpoint | Service Method | Database Operation |
|---|---|---|---|---|
| Viết bình luận | POST | `/Comment/AddComment` | `CreateCommentAsync` | INSERT |
| Trả lời bình luận | POST | `/Comment/AddComment?parentId=N` | `CreateCommentAsync` | INSERT (với parentId) |
| Sửa bình luận | PUT | `/Comment/EditComment` | `UpdateCommentAsync` | UPDATE |
| Xóa bình luận | DELETE | `/Comment/DeleteComment` | `DeleteCommentAsync` | DELETE (hard) |
| Thích/Bỏ thích bình luận | POST | `/Comment/ToggleLike` | `ToggleCommentLikeAndGetStatusAsync` | INSERT hoặc DELETE + COUNT |
| Theo dõi nghệ sĩ | POST | `/ToggleFollow/{id}` | `ToggleFollowAsync` | INSERT + UPDATE count |
| Bỏ theo dõi nghệ sĩ | POST | `/ToggleFollow/{id}` | `ToggleFollowAsync` | DELETE + UPDATE count |

---

## Cơ Chế Phân Quyền

### Bình luận
```
Viết/Trả lời: Bất kỳ user đã đăng nhập
Sửa:          Chỉ chủ sở hữu (comment.UserId == currentUserId)
Xóa:          Chủ sở hữu HOẶC Admin (userId = null bỏ qua kiểm tra)
Thích:        Bất kỳ user đã đăng nhập
Xem:          Tất cả (kể cả khách vãng lai - AllowAnonymous)
```

### Theo dõi nghệ sĩ
```
Follow/Unfollow: Bất kỳ user đã đăng nhập
Xem danh sách đang theo dõi: Chỉ chính user đó
```

### So sánh với Playlist
| Tính năng | Playlist | Bình luận |
|---|---|---|
| Xóa | Soft delete (IsDeleted = true) | Hard delete (xóa vật lý) |
| Kiểm tra quyền | Throw `UnauthorizedAccessException` | Return im lặng |
| Admin bypass | `isAdmin = true` parameter | `userId = null` parameter |

---

## Caching trong ArtistService

`ArtistService` sử dụng `IMemoryCache` để cache thông tin nghệ sĩ:

```csharp
// Cache key: "artist_details_{id}_p{page}_s{pageSize}"
// TTL: 1 giờ
// Invalidation: Khi follow/unfollow hoặc admin cập nhật thông tin

if (_cache.TryGetValue(cacheKey, out ArtistDto? cachedResult))
{
    // Luôn cập nhật IsFollowing theo user hiện tại (không cache trạng thái này)
    if (currentUserId.HasValue)
        cachedResult.IsFollowing = await IsFollowingAsync(currentUserId.Value, id);
    return cachedResult;
}
```

**Lý do không cache `IsFollowing`**: Trạng thái follow là per-user, nếu cache sẽ cần key riêng cho mỗi cặp (user, artist) — tốn bộ nhớ. Thay vào đó, chỉ cache phần dữ liệu chung (tên, bio, bài hát...) và query `IsFollowing` riêng.
