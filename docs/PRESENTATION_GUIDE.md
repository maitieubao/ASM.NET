# Hướng Dẫn Báo Cáo Dự Án VibeMusic

> **Dành cho thành viên nhóm** — Tài liệu này giúp bạn nắm được những điểm chính của dự án để trả lời câu hỏi của giáo viên một cách tự tin. Đọc kỹ phần nào bạn được phân công trình bày.

---

## PHẦN 1: Giới Thiệu Dự Án (Ai cũng cần biết)

### VibeMusic là gì?

VibeMusic là một **ứng dụng web nghe nhạc trực tuyến** — tương tự Spotify hay Zing MP3 nhưng được xây dựng từ đầu bởi nhóm. Người dùng có thể:

- Nghe nhạc stream từ YouTube
- Tạo và quản lý playlist cá nhân
- Theo dõi nghệ sĩ yêu thích
- Bình luận và tương tác với cộng đồng
- Mua gói Premium để tải nhạc về máy
- Chat với trợ lý AI để tìm nhạc bằng ngôn ngữ tự nhiên

### Công nghệ sử dụng

| Thành phần | Công nghệ | Giải thích đơn giản |
|---|---|---|
| Ngôn ngữ lập trình | C# (.NET 10) | Ngôn ngữ chính để viết backend |
| Framework web | ASP.NET Core MVC | Khung làm việc để xây dựng web app |
| Database | PostgreSQL | Nơi lưu trữ dữ liệu (bài hát, user, playlist...) |
| ORM | Entity Framework Core | Công cụ giúp code C# nói chuyện với database |
| Thanh toán | PayOS | Cổng thanh toán VietQR cho gói Premium |
| AI | Microsoft Semantic Kernel | Thư viện tích hợp AI vào ứng dụng |
| Giao diện | Bootstrap + CSS tùy chỉnh | Thiết kế giao diện đẹp, responsive |

### Tại sao chọn những công nghệ này?

- **.NET 10**: Hiệu suất cao, được Microsoft hỗ trợ lâu dài, phù hợp cho ứng dụng doanh nghiệp
- **PostgreSQL**: Miễn phí, mạnh mẽ, hỗ trợ tốt cho dữ liệu phức tạp
- **PayOS**: Cổng thanh toán Việt Nam, hỗ trợ QR code và chuyển khoản ngân hàng nội địa

---

## PHẦN 2: Kiến Trúc Dự Án (Câu hỏi hay gặp nhất)

### Câu hỏi: "Dự án được tổ chức như thế nào?"

Dự án chia thành **4 tầng** (layer), mỗi tầng có nhiệm vụ riêng:

```
┌─────────────────────────────────────┐
│  VibeMusic (Web)                    │  ← Giao diện, nhận request từ browser
├─────────────────────────────────────┤
│  VibeMusic.Infrastructure           │  ← Kết nối database, gọi API bên ngoài
├─────────────────────────────────────┤
│  VibeMusic.Application              │  ← Logic nghiệp vụ (xử lý dữ liệu)
├─────────────────────────────────────┤
│  VibeMusic.Domain                   │  ← Định nghĩa các đối tượng cốt lõi
└─────────────────────────────────────┘
```

**Giải thích bằng ví dụ thực tế:**

Khi user nhấn "Thêm bài hát vào playlist":
1. **Web layer** nhận click từ browser, gọi `PlaylistController`
2. **Application layer** kiểm tra quyền sở hữu, xử lý logic
3. **Infrastructure layer** lưu vào database PostgreSQL
4. **Domain layer** định nghĩa "Playlist là gì, có những thuộc tính gì"

### Câu hỏi: "Tại sao lại chia thành nhiều project vậy?"

**Trả lời ngắn gọn**: Để dễ bảo trì và mở rộng.

**Giải thích chi tiết hơn nếu cần**:
- Nếu muốn đổi từ PostgreSQL sang MySQL, chỉ cần sửa tầng Infrastructure, không đụng đến logic nghiệp vụ
- Nếu muốn test logic mà không cần database thật, có thể làm được vì các tầng độc lập nhau
- Đây gọi là **Clean Architecture** — một pattern phổ biến trong phát triển phần mềm chuyên nghiệp

### Câu hỏi: "MVC là gì?"

**MVC = Model - View - Controller**

- **Model**: Dữ liệu (bài hát, user, playlist...)
- **View**: Giao diện HTML hiển thị cho người dùng
- **Controller**: Xử lý request, lấy dữ liệu từ Model, trả về View

**Ví dụ**: Khi user vào trang `/Playlist/Index`:
- Controller `PlaylistController` nhận request
- Gọi service lấy danh sách playlist từ database (Model)
- Trả về file `Index.cshtml` (View) với dữ liệu đã có

---

## PHẦN 3: Các Tính Năng Chính (Theo Use Case)

### 3.1 Quản lý Playlist & Thư viện

**Tóm tắt**: User có thể tạo, sửa, xóa playlist và quản lý bài hát yêu thích.

**Những điểm quan trọng khi được hỏi**:

**Q: Khi xóa playlist, dữ liệu có bị mất không?**
> Không. Hệ thống dùng **soft delete** — chỉ đánh dấu `is_deleted = true` trong database, không xóa thật. Điều này giúp có thể khôi phục dữ liệu nếu cần và tránh mất dữ liệu do thao tác nhầm.

**Q: Làm sao thêm bài hát từ YouTube vào playlist?**
> User dán YouTube ID vào. Hệ thống tự động gọi YouTube API để lấy thông tin bài hát (tên, thumbnail, thời lượng), lưu vào database, rồi thêm vào playlist. Có cơ chế **retry 3 lần** nếu gặp lỗi mạng.

**Q: Bài hát yêu thích được lưu như thế nào?**
> Mỗi lần user nhấn tim, hệ thống tạo một bản ghi trong bảng `songlikes` (userId + songId). Nhấn lại thì xóa bản ghi đó. Đây gọi là **toggle pattern**.

---

### 3.2 Tính năng Social (Bình luận & Theo dõi)

**Tóm tắt**: User có thể bình luận bài hát, trả lời bình luận, thích bình luận, và theo dõi nghệ sĩ.

**Những điểm quan trọng khi được hỏi**:

**Q: Bình luận được tổ chức như thế nào?**
> Hệ thống hỗ trợ **2 cấp bình luận**: bình luận gốc và trả lời. Mỗi bình luận có trường `parentCommentId` — nếu là `null` thì là bình luận gốc, nếu có giá trị thì là reply của bình luận đó.

**Q: Tại sao bình luận được HTML encode?**
> Để chống **XSS (Cross-Site Scripting)** — một loại tấn công bảo mật. Nếu user nhập `<script>alert('hack')</script>`, hệ thống chuyển thành `&lt;script&gt;` trước khi lưu, nên khi hiển thị browser sẽ hiện text thay vì chạy code.

**Q: Khi follow nghệ sĩ, điều gì xảy ra?**
> Hệ thống tạo bản ghi trong bảng `artist_followers`, đồng thời tăng `SubscriberCount` của nghệ sĩ lên 1. Khi unfollow thì ngược lại. Sau đó xóa cache để trang nghệ sĩ hiển thị số liệu mới.

---

### 3.3 Premium & Thanh toán

**Tóm tắt**: User mua gói Premium qua PayOS để tải nhạc và truy cập nội dung độc quyền.

**Những điểm quan trọng khi được hỏi**:

**Q: Luồng thanh toán hoạt động như thế nào?**
> 1. User chọn gói → hệ thống tạo đơn hàng với mã duy nhất
> 2. Redirect sang trang PayOS để quét QR hoặc chuyển khoản
> 3. Sau khi thanh toán, PayOS gửi thông báo về hệ thống (webhook)
> 4. Hệ thống kích hoạt Premium cho user

**Q: Nếu user đóng tab trước khi redirect về, Premium có được kích hoạt không?**
> Có. Hệ thống có **2 cơ chế**: callback khi user quay về trang web, và webhook từ PayOS server gửi trực tiếp. Dù user đóng tab, webhook vẫn kích hoạt Premium.

**Q: Khi hủy gói, Premium mất ngay không?**
> Không. Hệ thống dùng **graceful cancellation** — user vẫn giữ Premium cho đến hết thời hạn đã trả tiền. Chỉ tắt tự động gia hạn. Tương tự cách Netflix hoạt động.

**Q: Tải nhạc hoạt động như thế nào?**
> Hệ thống kiểm tra Premium, sau đó gọi YouTube để lấy audio stream URL, rồi **stream trực tiếp** file về máy user dưới dạng `.mp4`. Không lưu file trên server để tiết kiệm dung lượng.

---

### 3.4 External Platform View Count

**Tóm tắt**: Hệ thống tổng hợp view count từ 3 nguồn khác nhau — nội bộ, YouTube, và Deezer — để hiển thị con số phổ biến chính xác nhất cho mỗi bài hát.

**Kiến trúc dữ liệu**:

| Nguồn | Enum Value | Ý nghĩa |
|---|---|---|
| `Internal` | 0 | Lượt nghe thực tế trong app VibeMusic |
| `YouTube` | 1 | View count lấy từ YouTube Data API |
| `Deezer` | 2 | Popularity rank lấy từ Deezer API |

**PrioritySource** là trường nullable trên entity `Song`:
- Khi admin đặt `PrioritySource = YouTube` → hệ thống luôn hiển thị view count từ YouTube
- Khi `PrioritySource = null` → **auto mode**: hệ thống tự chọn nguồn có view count cao nhất

**Auto-select logic** (khi PrioritySource = null):
```
result = max(youtubeCount, deezerCount, internalPlayCount)
```
Nếu không có external view count nào → fallback về `PlayCount` nội bộ.

**Những điểm quan trọng khi được hỏi**:

**Q: View count lấy từ đâu?**
> Hệ thống có 3 nguồn: (1) **Internal** — lượt nghe thực tế trong app, tăng mỗi khi user play bài hát; (2) **YouTube** — view count lấy từ YouTube Data API theo `YoutubeVideoId` của bài hát; (3) **Deezer** — popularity score lấy từ Deezer API theo `DeezerTrackId`. Admin có thể chỉ định nguồn ưu tiên, hoặc để hệ thống tự chọn nguồn cao nhất.

**Q: Tại sao có 3 nguồn view count?**
> Vì mỗi nguồn phản ánh một khía cạnh khác nhau của độ phổ biến. YouTube view count thường rất cao (hàng triệu) vì là nền tảng lớn. Deezer popularity là điểm tương đối (0–100) phản ánh xu hướng trên nền tảng streaming. Internal count là số liệu thực tế của chính app. Bằng cách cho admin chọn nguồn ưu tiên, hệ thống linh hoạt hơn — ví dụ bài hát mới chưa có nhiều lượt nghe nội bộ nhưng đã viral trên YouTube thì vẫn hiển thị được con số ấn tượng.

---

### 3.5 Chức năng Admin

**Tóm tắt**: Admin có bảng điều khiển riêng để quản lý toàn bộ hệ thống.

**Những điểm quan trọng khi được hỏi**:

**Q: Admin khác user thường ở điểm nào?**
> Admin có role đặc biệt trong database. Tất cả trang admin đều có `[Authorize(Roles = "Admin")]` — nếu user thường cố truy cập sẽ bị từ chối với lỗi 403 Forbidden.

**Q: Admin có thể tự xóa tài khoản của mình không?**
> Không. Hệ thống có cơ chế **self-protection**: kiểm tra nếu admin đang cố xóa/khóa chính mình thì từ chối và hiển thị thông báo lỗi. Tránh tình huống mất quyền truy cập hệ thống.

**Q: Dashboard thống kê lấy dữ liệu từ đâu?**
> Từ database — đếm số user, tính tổng doanh thu từ bảng payments, lấy top bài hát theo lượt nghe... Dữ liệu được **cache 10 phút** để không phải query database mỗi lần admin vào trang.

**Q: Tại sao cần tính năng "Đồng bộ lượt nghe"?**
> Khi import bài hát từ YouTube, hệ thống có thể lấy ViewCount của YouTube làm PlayCount ban đầu (số ảo). Tính năng này reset về số lượt nghe thực tế trong app, đảm bảo thống kê chính xác.

---

## PHẦN 4: Các Khái Niệm Kỹ Thuật Hay Được Hỏi

### Dependency Injection (DI) là gì?

**Giải thích đơn giản**: Thay vì một class tự tạo ra các đối tượng nó cần, hệ thống sẽ "tiêm" (inject) chúng vào từ bên ngoài.

**Ví dụ thực tế**:
```
PlaylistController cần PlaylistService để xử lý logic
→ Không tự tạo: new PlaylistService()
→ Được inject tự động qua constructor
```

**Lợi ích**: Dễ thay thế, dễ test, không bị phụ thuộc cứng.

---

### Repository Pattern là gì?

**Giải thích đơn giản**: Một lớp trung gian giữa code và database. Thay vì viết SQL trực tiếp khắp nơi, tất cả thao tác database đi qua Repository.

**Ví dụ**:
- `Repository<Song>.GetByIdAsync(id)` → `SELECT * FROM songs WHERE songid = @id`
- `Repository<Song>.AddAsync(song)` → `INSERT INTO songs (...)`

**Lợi ích**: Code sạch hơn, dễ thay đổi database sau này.

---

### Unit of Work là gì?

**Giải thích đơn giản**: Quản lý một "phiên làm việc" với database. Tất cả thay đổi được gom lại và lưu một lần duy nhất.

**Ví dụ**: Khi mua Premium:
1. Cập nhật Payment → "Paid"
2. Cập nhật User → IsPremium = true
3. Tạo UserSubscription mới
→ Tất cả 3 thao tác được lưu cùng lúc. Nếu bước 2 lỗi, bước 1 cũng bị rollback — không có tình trạng "tiền đã trừ nhưng Premium chưa kích hoạt".

---

### Soft Delete là gì?

**Giải thích đơn giản**: Không xóa thật, chỉ đánh dấu "đã xóa".

**Cách hoạt động**: Thêm cột `is_deleted` vào bảng. Khi "xóa", chỉ set `is_deleted = true`. Khi query, luôn thêm `WHERE is_deleted = false`.

**Tại sao dùng**: Có thể khôi phục dữ liệu, giữ lịch sử, tránh lỗi do xóa nhầm.

---

### Cache là gì và tại sao dùng?

**Giải thích đơn giản**: Lưu kết quả tính toán vào bộ nhớ tạm. Lần sau hỏi cùng câu hỏi đó, trả lời ngay mà không cần tính lại.

**Ví dụ trong dự án**: Dashboard admin query nhiều bảng để tính thống kê — tốn thời gian. Kết quả được cache 10 phút. Trong 10 phút đó, mọi admin vào trang đều nhận kết quả ngay lập tức.

**Khi nào xóa cache**: Khi dữ liệu thay đổi (admin nhấn "Làm mới", hoặc có giao dịch mới).

---

### API là gì?

**Giải thích đơn giản**: Cách hai hệ thống "nói chuyện" với nhau qua internet.

**Dự án dùng các API**:
- **YouTube API**: Lấy thông tin bài hát, stream audio
- **Deezer API**: Lấy metadata nhạc (ảnh bìa, thể loại...)
- **PayOS API**: Tạo link thanh toán, xác nhận giao dịch
- **Wikipedia API**: Lấy tiểu sử nghệ sĩ

---

### Authentication vs Authorization

**Authentication** (Xác thực): "Bạn là ai?" → Đăng nhập bằng email/password hoặc Google

**Authorization** (Phân quyền): "Bạn được làm gì?" → User thường vs Admin

**Ví dụ**:
- User đăng nhập thành công → Authentication OK
- User cố vào trang `/AdminSong/Create` → Authorization FAIL → 403 Forbidden

---

## PHẦN 5: Câu Hỏi Thường Gặp Khi Báo Cáo

### "Điểm khó nhất trong dự án là gì?"

> Tích hợp thanh toán PayOS với cơ chế webhook — phải đảm bảo Premium được kích hoạt dù user đóng tab giữa chừng. Giải pháp là xử lý cả callback URL và webhook song song, với cơ chế idempotent (xử lý trùng lặp an toàn).

---

### "Dự án có điểm gì nổi bật so với ứng dụng thông thường?"

> Ba điểm nổi bật:
> 1. **Tích hợp AI**: Trợ lý ảo hiểu ngôn ngữ tự nhiên tiếng Việt, có thể tạo playlist tự động từ lịch sử nghe
> 2. **Kiến trúc Clean Architecture**: Tổ chức code chuyên nghiệp, dễ mở rộng
> 3. **Xác minh nghệ sĩ qua Deezer**: Hệ thống tự động so khớp nghệ sĩ với database Deezer để xác thực thông tin

---

### "Database được thiết kế như thế nào?"

> Database có khoảng 15+ bảng chính. Các bảng quan trọng:
> - `users` — thông tin người dùng
> - `songs` — bài hát
> - `artists` — nghệ sĩ
> - `playlists` + `playlistsongs` — playlist và quan hệ nhiều-nhiều với bài hát
> - `payments` + `usersubscriptions` — thanh toán và gói Premium
> - `comments` + `commentlikes` — bình luận và lượt thích
> - `artist_followers` — quan hệ theo dõi nghệ sĩ

---

### "Làm sao đảm bảo bảo mật?"

> Bốn lớp bảo mật:
> 1. **Authentication**: Đăng nhập qua cookie session hoặc Google OAuth
> 2. **Authorization**: Role-based (`[Authorize(Roles = "Admin")]`)
> 3. **Anti-CSRF**: Mọi form POST đều có token chống giả mạo request
> 4. **XSS Prevention**: Nội dung user nhập được HTML encode trước khi lưu

---

### "Tại sao dùng Clean Architecture thay vì cấu trúc đơn giản hơn?"

> Vì dự án có nhiều tích hợp bên ngoài (YouTube, PayOS, Deezer, AI). Nếu để tất cả trong một project, khi YouTube thay đổi API thì phải sửa code khắp nơi. Với Clean Architecture, chỉ cần sửa tầng Infrastructure, phần còn lại không bị ảnh hưởng.

---

### "Trợ lý AI hoạt động như thế nào?"

> Dùng **Microsoft Semantic Kernel** — một framework kết nối AI với ứng dụng. Khi user nhắn tin, hệ thống phân tích ý định (tìm nhạc? tạo playlist? xem lịch sử?) rồi gọi đúng function tương ứng. AI không trực tiếp truy cập database — chỉ gọi các "plugin" được định nghĩa sẵn.

---

## PHẦN 6: Phân Công Trình Bày (Gợi ý)

Nếu nhóm cần phân chia phần trình bày:

| Phần | Nội dung | Thời gian gợi ý |
|---|---|---|
| **Giới thiệu** | Dự án là gì, công nghệ sử dụng, kiến trúc tổng quan | 3-5 phút |
| **Demo User** | Đăng nhập, nghe nhạc, tạo playlist, bình luận, follow nghệ sĩ | 5-7 phút |
| **Demo Premium** | Xem gói, thanh toán, tải nhạc | 3-4 phút |
| **Demo Admin** | Dashboard, quản lý bài hát/nghệ sĩ, xử lý báo cáo | 4-5 phút |
| **Kỹ thuật** | Clean Architecture, Database design, bảo mật | 3-5 phút |
| **Kết luận** | Điểm nổi bật, khó khăn, hướng phát triển | 2-3 phút |

---

## PHẦN 7: Từ Điển Thuật Ngữ Nhanh

| Thuật ngữ | Giải thích đơn giản |
|---|---|
| **Backend** | Phần xử lý logic, database — người dùng không thấy trực tiếp |
| **Frontend** | Giao diện người dùng thấy và tương tác |
| **API** | Cách hai hệ thống giao tiếp với nhau |
| **Database** | Nơi lưu trữ dữ liệu có cấu trúc |
| **ORM** | Công cụ giúp code tương tác với database mà không cần viết SQL |
| **Cache** | Bộ nhớ tạm để tăng tốc độ |
| **Soft Delete** | Xóa ảo — đánh dấu đã xóa thay vì xóa thật |
| **Webhook** | Thông báo tự động từ hệ thống bên ngoài khi có sự kiện |
| **Stream** | Truyền dữ liệu liên tục thay vì tải toàn bộ rồi mới dùng |
| **Token** | Mã xác thực dùng một lần để bảo mật |
| **Role** | Vai trò của user (Admin, Customer...) |
| **CRUD** | Create, Read, Update, Delete — 4 thao tác cơ bản với dữ liệu |
| **MVC** | Model-View-Controller — mô hình tổ chức code web phổ biến |
| **Dependency Injection** | Cách cung cấp các đối tượng cần thiết cho một class |
| **Repository Pattern** | Lớp trung gian giữa code và database |
| **Unit of Work** | Quản lý nhóm thao tác database thực hiện cùng lúc |
| **Clean Architecture** | Cách tổ chức dự án thành các tầng độc lập |
| **Idempotent** | Thực hiện nhiều lần cho kết quả giống thực hiện một lần |
| **Pagination** | Phân trang — chia danh sách dài thành nhiều trang nhỏ |
| **Toggle** | Chuyển đổi qua lại giữa 2 trạng thái (bật/tắt) |
