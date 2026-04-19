# 🎵 YoutubeMusicPlayer - Nền tảng Âm nhạc Đỉnh cao

Chào mừng bạn đến với **YoutubeMusicPlayer**, một ứng dụng nghe nhạc trực tuyến hiện đại, mạnh mẽ và đầy tính thẩm mỹ, được xây dựng trên nền tảng **ASP.NET Core 10** và kiến trúc **Clean Architecture**.

---

## 🔥 Các Tính Năng Nổi Bật

Chúng tôi chia hệ thống tính năng thành các nhóm chức năng chính để bạn dễ dàng khám phá:

### 🎧 1. Trải nghiệm Nghe nhạc Đắm chìm (Immersive UI)
*   **Stream nhạc từ YouTube:** Tiếp cận kho nhạc khổng lồ toàn cầu với tốc độ nhanh chóng.
*   **Trình phát nhạc Glassmorphism:** Giao diện điều khiển hiện đại, mờ ảo và sang trọng.
*   **Đồng bộ Lời bài hát (Lyrics Sync):** Lời nhạc chạy theo thời gian thực với hiệu ứng làm nổi bật và cuộn tự động chuyên nghiệp.
*   **Điều khiển thông minh:** Hỗ trợ đầy đủ các tính năng Shuffle (phát ngẫu nhiên), Repeat (phát lại), và điều chỉnh tốc độ phát từ 0.5x đến 2.0x.
*   **Hẹn giờ tắt nhạc (Sleep Timer):** Tự động dừng nhạc sau một khoảng thời gian định trước, giúp bạn đi vào giấc ngủ dễ dàng.
*   **Chế độ Cửa sổ nhỏ (Picture-in-Picture):** Tiếp tục nghe nhạc và xem lời ngay cả khi đang duyệt các trang khác.

### 🌟 2. Khám phá & Gợi ý Thông minh
*   **Cá nhân hóa Trang chủ:** Lời chào thay đổi theo thời gian thực (Sáng/Trưa/Chiều/Tối) và hiển thị tên người dùng.
*   **Tìm kiếm Đa nguồn (Ultimate Search):** Tích hợp tìm kiếm từ YouTube, Database nội bộ, Deezer và iTunes để đưa ra kết quả chính xác nhất.
*   **Gợi ý theo Tâm trạng (Moods):** Danh sách nhạc được phân loại theo cảm xúc: Chill, Tập trung, Sôi động, Tâm trạng...
*   **Khám phá Nghệ sĩ & Thể loại:** Hệ thống phân loại nghệ sĩ xác thực (Verified) và hơn 50+ thể loại âm nhạc khác nhau.
*   **Dữ liệu dự phòng (Fallback):** Đảm bảo trang chủ luôn có nội dung "Thịnh hành" ngay cả khi mạng gặp sự cố.

### 📚 3. Quản lý Thư viện Cá nhân
*   **Danh sách phát (Playlist):** Tạo, chỉnh sửa và quản lý các danh sách nhạc của riêng bạn một cách dễ dàng.
*   **Yêu thích & Theo dõi:** Lưu trữ những bài hát tâm đắc và theo dõi những nghệ sĩ bạn yêu mến.
*   **Lịch sử nghe nhạc:** Ghi lại hành trình âm nhạc của bạn để dễ dàng tìm lại những giai điệu cũ.
*   **Đăng nhập Google:** Bảo mật và tiện lợi với hệ thống xác thực từ Google.

### 💎 4. Tính năng Premium & Thanh toán
*   **Nâng cấp Premium:** Tích hợp cổng thanh toán **PayOS** hỗ trợ VietQR, giao dịch nhanh chóng và an toàn.
*   **Tải nhạc chất lượng cao:** Đặc quyền dành cho thành viên Premium - tải bài hát trực tiếp về máy dưới định dạng MP4 Audio chuẩn.
*   **Trải nghiệm không giới hạn:** Loại bỏ các giới hạn tính năng dành cho người dùng thường.

### 🤖 5. Trợ lý AI & Công nghệ
*   **Trợ lý ảo AI:** Tích hợp Semantic Kernel giúp bạn tìm kiếm nhạc và quản lý playlist bằng ngôn ngữ tự nhiên.
*   **Kiến trúc sạch (Clean Architecture):** Mã nguồn dễ bảo trì, mở rộng và có hiệu suất cao.
*   **Xử lý nền (Background Processing):** Hệ thống hàng đợi giúp nạp dữ liệu và xử lý video mà không gây gián đoạn trải nghiệm người dùng.

---

## 🏛️ Sơ đồ Kiến trúc (Solution Map)

Dự án được phân chia nghiêm ngặt theo các lớp:
*   **YoutubeMusicPlayer (Web):** Lớp hiển thị, giao diện người dùng và Controller.
*   **YoutubeMusicPlayer.Application:** Chứa logic nghiệp vụ xử lý nhạc, thanh toán và AI.
*   **YoutubeMusicPlayer.Domain:** Định nghĩa các thực thể cốt lõi (Song, Artist, User, Payment...).
*   **YoutubeMusicPlayer.Infrastructure:** Kết nối Database PostgreSQL và các dịch vụ bên ngoài (YouTube, PayOS, Deezer).

---

## 🚀 Hướng dẫn Cài đặt Nhanh

1.  **Yêu cầu:** Cài đặt .NET 10 SDK và PostgreSQL.
2.  **Cấu hình:** Cập nhật chuỗi kết nối Database và API Keys (PayOS, Google Auth) trong `appsettings.json`.
3.  **Khởi tạo:**
    ```bash
    dotnet restore
    dotnet build
    dotnet run --project YoutubeMusicPlayer
    ```

---
*Phát triển bởi đội ngũ Antigravity với đam mê mang lại trải nghiệm âm nhạc tốt nhất cho người dùng Việt.*
