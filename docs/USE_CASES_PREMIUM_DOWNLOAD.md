# Tài Liệu Use Case: Upgrade Premium & Download

## Tổng Quan

Module **Upgrade Premium & Download** quản lý toàn bộ vòng đời của gói hội viên Premium — từ xem gói, thanh toán, tải nhạc, đến hủy gói và xem lịch sử giao dịch.

**Actors**:
- **Customer** — người dùng thường, có thể xem gói và mua
- **Premium User** — người dùng đã có gói Premium, kế thừa tất cả use cases của Customer và thêm các quyền đặc biệt

**Yêu cầu xác thực**:
- Xem gói dịch vụ: `[AllowAnonymous]` — khách vãng lai cũng xem được
- Mua gói, tải nhạc, hủy gói, xem lịch sử: `[Authorize]`

---

## Kiến Trúc Xử Lý

```
Browser (Customer / Premium User)
    │
    ▼
SubscriptionController     ← Xem gói, hủy gói, tải nhạc (VibeMusic/Controllers/)
PaymentController          ← Khởi tạo và xử lý thanh toán (VibeMusic/Controllers/)
    │ inject
    ▼
ISubscriptionService       ← Interface nghiệp vụ subscription (VibeMusic.Application/Interfaces/)
IPayOSService              ← Interface tích hợp cổng thanh toán PayOS (VibeMusic.Application/Interfaces/)
IYoutubeService            ← Interface lấy stream URL từ YouTube (VibeMusic.Application/Interfaces/)
    │ implement
    ▼
SubscriptionService        ← Logic subscription (VibeMusic.Application/Services/)
PayOSService               ← Gọi PayOS API (VibeMusic.Infrastructure/External/)
YoutubeService             ← Lấy audio stream URL (VibeMusic.Infrastructure/External/)
    │ inject
    ▼
IUnitOfWork → Repository<SubscriptionPlan>
              Repository<UserSubscription>
              Repository<Payment>
              Repository<User>
    │
    ▼
AppDbContext → PostgreSQL + PayOS API + YouTube
```

**Các class tham gia chính**:

| Class | Project | Vai trò |
|---|---|---|
| `SubscriptionController` | VibeMusic | Xem gói, hủy gói, tải nhạc |
| `PaymentController` | VibeMusic | Khởi tạo thanh toán, xử lý callback |
| `BaseController` | VibeMusic | Cung cấp `CurrentUserId`, `IsAdmin` |
| `ISubscriptionService` | VibeMusic.Application | Hợp đồng nghiệp vụ subscription |
| `SubscriptionService` | VibeMusic.Application | Toàn bộ logic subscription |
| `IPayOSService` | VibeMusic.Application | Hợp đồng tích hợp PayOS |
| `SubscriptionPlan` | VibeMusic.Domain | Entity bảng `subscriptionplans` |
| `UserSubscription` | VibeMusic.Domain | Entity bảng `usersubscriptions` |
| `Payment` | VibeMusic.Domain | Entity bảng `payments` |
| `SubscriptionPlanDto` | VibeMusic.Application | DTO thông tin gói |
| `PaymentDto` | VibeMusic.Application | DTO thông tin giao dịch |

---

## Cấu Trúc Dữ Liệu

### Entity `SubscriptionPlan`
```csharp
public class SubscriptionPlan
{
    public int PlanId { get; set; }
    public string Name { get; set; }        // "Gói 1 Tháng", "Gói 1 Năm", "Gói Trọn Đời"
    public decimal Price { get; set; }      // Giá VNĐ
    public int DurationDays { get; set; }   // 30, 365, 99999
    public string? Description { get; set; }
    public bool IsActive { get; set; }      // Soft delete
}
```

### Entity `UserSubscription`
```csharp
public class UserSubscription
{
    public int UserId { get; set; }
    public int PlanId { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }   // 9999-12-31 cho gói Trọn Đời
    public bool IsActive { get; set; }      // false = đã hủy tự động gia hạn
}
```

### Entity `Payment`
```csharp
public class Payment
{
    public int PaymentId { get; set; }
    public int UserId { get; set; }
    public int PlanId { get; set; }
    public decimal Amount { get; set; }
    public long OrderCode { get; set; }     // Mã đơn hàng duy nhất gửi cho PayOS
    public string Status { get; set; }      // "Pending", "Success", "Failed"
    public string? PayosTransactionId { get; set; }
    public DateTime PaymentDate { get; set; }
}
```

---

## Use Case 1: Xem Các Gói Dịch Vụ

### Mô tả
Customer (kể cả khách vãng lai) xem danh sách các gói Premium đang hoạt động, giá tiền, thời hạn và quyền lợi.

### Luồng xử lý

```
GET /Subscription/Index
    │ [AllowAnonymous]
    ▼
SubscriptionController.Index()
    ├── _subscriptionService.GetActivePlansAsync()
    │       └── SELECT * FROM subscriptionplans WHERE isactive = true
    ├── Nếu đã đăng nhập:
    │       ViewBag.IsPremium = _subscriptionService.IsUserPremiumAsync(userId)
    │           ├── Kiểm tra cache: "user_premium_{userId}" (TTL 2 phút)
    │           ├── Nếu cache miss: kiểm tra user.IsPremium flag
    │           └── Nếu không: kiểm tra UserSubscription còn hạn
    └── return View(plans)
    │
    ▼
View: Subscription/Index.cshtml
    ├── Hiển thị danh sách gói (thẻ card cho mỗi gói)
    ├── Nếu IsPremium = true: hiển thị nút "Hủy gói hiện tại"
    └── Nút "NÂNG CẤP NGAY" → /Payment/CreatePayment?planId=N
```

### Dữ liệu hiển thị (`SubscriptionPlanDto`)
```csharp
public class SubscriptionPlanDto
{
    public int PlanId { get; set; }
    public string Name { get; set; }       // "Gói 1 Tháng"
    public decimal Price { get; set; }     // 19000
    public int DurationDays { get; set; }  // 30
    public string? Description { get; set; }
}
```

### Gói mặc định trong hệ thống
| Gói | Giá | Thời hạn |
|---|---|---|
| Gói 1 Tháng | 19.000 VNĐ | 30 ngày |
| Gói 1 Năm | 190.000 VNĐ | 365 ngày |
| Gói Trọn Đời | 499.000 VNĐ | Vĩnh viễn (EndDate = 9999-12-31) |

### Cơ chế kiểm tra Premium với cache
`IsUserPremiumAsync` dùng `IMemoryCache` với TTL 2 phút để tránh query database mỗi request. Đây là quan trọng vì trạng thái Premium được kiểm tra ở **mọi trang** (trong `_Layout.cshtml`):
```csharp
isPremium: @(await SubService.IsUserPremiumAsync(userId))
```

---

## Use Case 2: Mua Gói Premium

### Mô tả
Customer chọn gói và thanh toán qua cổng **PayOS** (hỗ trợ QR code, chuyển khoản ngân hàng). Sau khi thanh toán thành công, hệ thống tự động kích hoạt Premium.

### Luồng xử lý đầy đủ

```
1. Customer nhấn "NÂNG CẤP NGAY" trên gói muốn mua
    │
    ▼
GET /Payment/CreatePayment?planId=2
    └── Hiển thị trang xác nhận với thông tin gói

2. Customer nhấn "Xác nhận thanh toán"
    │
    ▼
POST /Payment/CreatePayment (planId=2)
    ├── Tạo orderCode duy nhất:
    │       orderCode = DateTime.Now("yyyyMMddHHmmss") + Random(10,99)
    │       Ví dụ: 2026041910305542
    ├── Tạo Payment record với Status = "Pending":
    │       INSERT INTO payments (userid, planid, amount, ordercode, status="Pending")
    ├── Gọi PayOS API tạo link thanh toán:
    │       _payOSService.CreatePaymentLinkAsync(userId, planId, orderCode, amount, description, returnUrl, cancelUrl)
    │       returnUrl = /Payment/Success?orderCode=...
    │       cancelUrl = /Payment/Cancel
    └── Redirect → PayOS Checkout URL (QR code / chuyển khoản)

3. Customer hoàn tất thanh toán trên PayOS
    │
    ▼
GET /Payment/Success?orderCode=2026041910305542
    ├── Gọi PayOS API xác minh: GetPaymentLinkInformationAsync(orderCode)
    ├── Nếu status == "PAID" hoặc "COMPLETED":
    │       _subscriptionService.ProcessPaymentSuccessAsync(orderCode, transactionId)
    │           ├── BEGIN TRANSACTION
    │           ├── Cập nhật Payment: Status = "Success", PayosTransactionId = ...
    │           ├── Cập nhật User: IsPremium = true
    │           ├── Tạo/cập nhật UserSubscription:
    │           │       Nếu đã có sub đang active: cộng thêm DurationDays vào EndDate
    │           │       Nếu chưa có: tạo mới với StartDate = now, EndDate = now + DurationDays
    │           │       Gói Trọn Đời: EndDate = 9999-12-31
    │           └── COMMIT TRANSACTION
    └── return View("Success")

4. PayOS gửi Webhook (backup, đảm bảo không mất giao dịch)
    │
    ▼
POST /Payment/Webhook
    ├── Xác minh chữ ký webhook: _payOSService.VerifyWebhookData(webhookData)
    └── Nếu hợp lệ: ProcessPaymentSuccessAsync(orderCode, transactionId)
        (Idempotent: nếu đã xử lý rồi thì bỏ qua vì Payment.Status != "Pending")
```

### Tại sao dùng cả Success callback và Webhook?
- **Success callback** (`/Payment/Success`): Xử lý ngay khi user quay về — trải nghiệm tức thì
- **Webhook** (`/Payment/Webhook`): Backup từ PayOS server — đảm bảo không mất giao dịch nếu user đóng tab trước khi redirect

### Tính idempotent của `ProcessPaymentSuccessAsync`
```csharp
var payment = await _unitOfWork.Repository<Payment>()
    .FirstOrDefaultAsync(p => p.OrderCode == orderCode && p.Status == "Pending");

if (payment == null) return; // Đã xử lý rồi → bỏ qua
```
Nếu cả Success callback và Webhook đều gọi, lần thứ 2 sẽ không tìm thấy Payment với Status = "Pending" → return sớm, không duplicate.

### Gia hạn gói (stacking)
Nếu user đã có Premium và mua thêm, hệ thống **cộng dồn** thời gian:
```csharp
existingSub.EndDate = existingSub.EndDate.AddDays(plan.DurationDays);
```
Không reset về ngày hiện tại — user không mất thời gian còn lại.

---

## Use Case 3: Tải Nhạc (Premium Only)

### Mô tả
Premium User tải bài hát về máy dưới dạng file `.mp4` (audio). Tính năng này chỉ dành cho Premium User — user thường bị từ chối với HTTP 403.

### Luồng xử lý

```
User nhấn nút tải xuống trên bài hát
    │
    ▼
JavaScript: handlePremiumDownload('/Subscription/Download?youtubeId=dQw4w9WgXcQ&title=Song+Name')
    ├── Nếu window.YTM_CONFIG.isPremium = true:
    │       window.location.href = '/Subscription/Download?...'
    └── Nếu không phải Premium:
            toastr.warning("Chỉ hội viên Premium mới có thể tải nhạc.")
    │
    ▼
GET /Subscription/Download?youtubeId=dQw4w9WgXcQ&title=Song+Name
    │ [Authorize]
    ▼
SubscriptionController.Download(youtubeId, title)
    ├── Kiểm tra CurrentUserId != null
    ├── Kiểm tra Premium: _subscriptionService.IsUserPremiumAsync(userId)
    │       Nếu không phải Premium → return Forbid() (HTTP 403)
    ├── Lấy audio stream URL từ YouTube:
    │       streamUrl = await _youtubeService.GetAudioStreamUrlAsync(videoUrl)
    │       Nếu streamUrl rỗng → return NotFound()
    ├── Tạo HTTP request đến YouTube với User-Agent giả browser
    ├── Stream dữ liệu về client:
    │       Response.Headers["Content-Disposition"] = "attachment; filename=Song_Name.mp4"
    │       Response.Headers["Content-Type"] = "audio/mp4"
    └── return File(stream, "audio/mp4", safeTitle)
    │
    ▼
Browser tự động tải file .mp4
```

### Tại sao dùng streaming thay vì download trước?
- File nhạc có thể lớn (5-50MB) — streaming tránh tốn RAM server
- `HttpCompletionOption.ResponseHeadersRead` bắt đầu stream ngay khi nhận header, không chờ download xong
- Timeout 20 phút để xử lý file lớn

### Bảo mật tên file
```csharp
string safeTitle = string.Join("_", title.Split(Path.GetInvalidFileNameChars()));
if (!safeTitle.EndsWith(".mp4")) safeTitle += ".mp4";
```
Loại bỏ ký tự không hợp lệ trong tên file để tránh path traversal.

### Nơi hiển thị nút tải
- `Views/Playlist/Details.cshtml` — trong danh sách bài hát của playlist
- `Views/Playlist/LikedSongs.cshtml` — trong danh sách yêu thích
- `Views/Album/Details.cshtml` — trong danh sách bài hát của album

---

## Use Case 4: Hủy Gói Dịch Vụ

### Mô tả
Premium User hủy gói đang hoạt động. Hệ thống thực hiện **graceful cancellation** — user vẫn giữ quyền Premium cho đến hết thời hạn đã trả tiền.

### Luồng xử lý

```
POST /Subscription/CancelSubscription
    │ [Authorize]
    ▼
SubscriptionController.CancelSubscription()
    └── _subscriptionService.CancelSubscriptionAsync(userId)
            ├── Tìm UserSubscription đang active:
            │       SELECT * FROM usersubscriptions WHERE userid=@userId AND isactive=true
            ├── Nếu không tìm thấy → return false
            ├── existingSub.IsActive = false  ← Tắt tự động gia hạn
            └── UnitOfWork.CompleteAsync() → UPDATE usersubscriptions SET isactive=false
    ├── Nếu success: TempData["Message"] = "Đã hủy gói thành công..."
    └── Nếu thất bại: TempData["Error"] = "Không tìm thấy gói Premium..."
    │
    ▼
RedirectToAction("Index") → Quay về trang gói dịch vụ
```

### Graceful Cancellation — Tại sao không xóa ngay?
`IsActive = false` chỉ tắt **tự động gia hạn**, không thu hồi quyền Premium ngay lập tức. `IsUserPremiumAsync` vẫn kiểm tra `EndDate`:

```csharp
var sub = await db.Repository<UserSubscription>()
    .FirstOrDefaultAsync(s => s.UserId == userId &&
        (s.EndDate.Date >= DateTime.UtcNow.Date || s.IsActive));
```

Điều này có nghĩa:
- User hủy gói ngày 15/04, gói hết hạn ngày 30/04
- User vẫn có Premium từ 15/04 đến 30/04
- Sau 30/04, `EndDate < UtcNow` → không còn Premium

Đây là chuẩn ngành (tương tự Netflix, Spotify).

---

## Use Case 5: Nghe Nhạc Offline (Gộp vào Tải Nhạc)

### Mô tả
VibeMusic là **web application** — toàn bộ luồng phát nhạc phụ thuộc vào kết nối internet để stream audio từ YouTube. Do đó, tính năng "nghe nhạc offline" theo nghĩa truyền thống (phát nhạc trong app mà không cần internet) là **không khả thi về mặt kỹ thuật** và không được triển khai.

Thay vào đó, nhu cầu nghe nhạc không cần internet được đáp ứng thông qua **Use Case 3 — Tải nhạc**:

- Premium User tải file `.mp4` (audio stream chất lượng cao) về thiết bị
- File tải về có thể phát bằng bất kỳ media player nào (Windows Media Player, VLC, điện thoại...) mà không cần mở app hay kết nối internet

**Kết luận**: Use case "Nghe nhạc offline" được xem là **phần mở rộng của Use Case Tải nhạc**, không phải một tính năng độc lập trong hệ thống.

---

## Use Case 6: Xem Lịch Sử Giao Dịch

### Mô tả
Customer xem toàn bộ lịch sử thanh toán của mình, bao gồm mã đơn hàng, tên gói, số tiền, ngày thanh toán và trạng thái.

### Luồng xử lý

```
GET /Subscription/Transactions
    │ [Authorize]
    ▼
SubscriptionController.Transactions()
    └── _subscriptionService.GetUserPaymentsAsync(userId)
            └── Query với JOIN:
                    SELECT p.*, sp.name as PlanName
                    FROM payments p
                    LEFT JOIN subscriptionplans sp ON p.planid = sp.planid
                    WHERE p.userid = @userId
                    ORDER BY p.paymentdate DESC
    │
    ▼
View: Subscription/Transactions.cshtml
    └── Bảng hiển thị:
        ├── Mã đơn (#OrderCode)
        ├── Gói sử dụng (PlanName)
        ├── Số tiền (Amount VNĐ)
        ├── Ngày thanh toán
        └── Trạng thái (badge màu):
                Success → xanh lá
                Pending → vàng
                Failed  → đỏ
```

### Tối ưu hóa N+1
`GetUserPaymentsAsync` dùng `Include(p => p.Plan)` để JOIN bảng `subscriptionplans` trong một query duy nhất, tránh N+1 problem khi load tên gói cho mỗi payment:

```csharp
var payments = await _unitOfWork.Repository<Payment>()
    .Query()
    .Include(p => p.Plan)          // JOIN subscriptionplans
    .Where(p => p.UserId == userId)
    .OrderByDescending(p => p.PaymentDate)
    .ToListAsync();
```

### Trạng thái giao dịch
| Status | Ý nghĩa | Màu |
|---|---|---|
| `Pending` | Đã tạo đơn, chờ thanh toán | Vàng |
| `Success` | Thanh toán thành công, Premium đã kích hoạt | Xanh lá |
| `Failed` | Thanh toán thất bại hoặc hết hạn | Đỏ |

---

## Bảng Tóm Tắt Use Cases

| Use Case | Actor | HTTP Method | Endpoint | Service Method | Yêu cầu |
|---|---|---|---|---|---|
| Xem gói dịch vụ | Customer | GET | `/Subscription/Index` | `GetActivePlansAsync` | Không cần đăng nhập |
| Mua gói Premium | Customer | POST | `/Payment/CreatePayment` | `CreateInitialPaymentAsync` | Đăng nhập |
| Tải nhạc | Premium User | GET | `/Subscription/Download` | `IsUserPremiumAsync` + YouTube stream | Premium |
| Hủy gói | Premium User | POST | `/Subscription/CancelSubscription` | `CancelSubscriptionAsync` | Đăng nhập |
| Nghe offline | Premium User | — | (gộp vào Tải nhạc — file `.mp4` tải về) | — | Premium |
| Xem lịch sử | Customer | GET | `/Subscription/Transactions` | `GetUserPaymentsAsync` | Đăng nhập |

---

## Luồng Thanh Toán Đầy Đủ (Sequence Diagram)

```
Customer    VibeMusic    PayOS API    Webhook
    │            │            │           │
    │──POST──────►            │           │
    │  CreatePayment          │           │
    │            │──CreateLink►           │
    │            │◄──CheckoutURL──────────│
    │◄──Redirect─│            │           │
    │            │            │           │
    │──Thanh toán trên PayOS──►           │
    │            │            │──Webhook──►
    │            │            │           │──ProcessSuccess──►DB
    │◄──Redirect─────────────►            │
    │  /Payment/Success       │           │
    │            │──Verify────►           │
    │            │◄──PAID─────│           │
    │            │──ProcessSuccess──►DB   │
    │◄──View Success──────────│           │
```

---

## Cơ Chế Bảo Mật

### 1. Kiểm tra Premium hai lớp
- **Frontend**: `window.YTM_CONFIG.isPremium` — ẩn/hiện nút tải
- **Backend**: `IsUserPremiumAsync(userId)` trong `Download()` — từ chối HTTP 403 nếu không phải Premium

Frontend check chỉ là UX, backend check mới là bảo mật thực sự.

### 2. Xác minh webhook
```csharp
if (_payOSService.VerifyWebhookData(webhookData))
```
PayOS ký webhook bằng HMAC — hệ thống xác minh chữ ký trước khi xử lý, tránh giả mạo webhook.

### 3. OrderCode duy nhất
```csharp
long orderCode = long.Parse(DateTime.Now.ToString("yyyyMMddHHmmss")) + new Random().Next(10, 99);
```
Kết hợp timestamp + random để đảm bảo không trùng lặp, tránh PayOS từ chối đơn hàng duplicate.
