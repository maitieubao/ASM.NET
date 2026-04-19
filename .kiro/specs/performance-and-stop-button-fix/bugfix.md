# Bugfix Requirements Document

## Introduction

Tài liệu này mô tả hai bug trong ứng dụng YoutubeMusicPlayer (Antigravity Music):

1. **Load dữ liệu chậm**: Trang chủ và các section nhạc (Trending, Albums, DailyMix, v.v.) tải quá chậm do thiếu caching ở tầng section và cơ chế load tuần tự không hiệu quả trong `HomeFacade`.

2. **Nút Stop bị lỗi**: Khi người dùng click nút Stop/Pause để dừng nhạc, nhạc dừng đúng nhưng sau khoảng 2–3 giây tự động phát lại. Nguyên nhân là heartbeat interval trong `core.js` kiểm tra sai điều kiện "manual pause" — nó đọc class của icon `fa-play` trên phần tử `#playPauseBtn` nhưng `$playPauseBtn.hasClass('fa-play')` luôn trả về `false` vì class thực tế nằm trên thẻ `<i>` bên trong, không phải trên button. Kết quả là heartbeat luôn coi nhạc đang bị throttle và tự resume.

---

## Bug Analysis

### Current Behavior (Defect)

**Bug 1 — Load dữ liệu chậm:**

1.1 WHEN người dùng truy cập trang chủ lần đầu THEN hệ thống load tuần tự từng section (Genres → Artists → User Personalization) mà không có caching ở tầng `GetHomeSectionAsync`, gây thời gian chờ tích lũy từ nhiều round-trip DB

1.2 WHEN frontend gọi `GetHomeSectionAsync` cho các section như Trending, Albums, DailyMix THEN hệ thống tạo scope mới và thực hiện query DB mỗi lần mà không kiểm tra cache trước, dẫn đến các request lặp lại gây chậm

1.3 WHEN nhiều người dùng cùng truy cập trang chủ trong khoảng thời gian ngắn THEN hệ thống thực hiện nhiều DB query trùng lặp cho cùng dữ liệu (Trending, Albums) vì không có shared cache ở tầng section

**Bug 2 — Nút Stop tự bật lại:**

1.4 WHEN người dùng click nút Stop/Pause để dừng nhạc THEN nhạc dừng đúng trong khoảng 2–3 giây rồi tự động phát lại mà không có tương tác nào từ người dùng

1.5 WHEN heartbeat interval (3 giây) trong `core.js` chạy sau khi người dùng đã pause nhạc THEN hệ thống đánh giá sai `isManualPause = false` vì `$playPauseBtn.hasClass('fa-play')` kiểm tra class trên phần tử button wrapper thay vì icon `<i>` bên trong, khiến điều kiện luôn sai

1.6 WHEN `isManualPause` bị đánh giá sai là `false` và `audioPlayer.paused` là `true` THEN heartbeat gọi `safePlayCurrentTrack()` để resume nhạc, bỏ qua ý định dừng nhạc của người dùng

---

### Expected Behavior (Correct)

**Bug 1 — Load dữ liệu chậm:**

2.1 WHEN người dùng truy cập trang chủ lần đầu THEN hệ thống SHALL trả về dữ liệu trang chủ trong thời gian hợp lý bằng cách cache kết quả của từng section riêng biệt trong `GetHomeSectionAsync`

2.2 WHEN frontend gọi `GetHomeSectionAsync` cho một section đã được cache THEN hệ thống SHALL trả về dữ liệu từ cache ngay lập tức mà không thực hiện thêm DB query

2.3 WHEN nhiều người dùng cùng truy cập trang chủ THEN hệ thống SHALL phục vụ dữ liệu section dùng chung (Trending, Albums) từ shared cache, giảm tải DB

**Bug 2 — Nút Stop tự bật lại:**

2.4 WHEN người dùng click nút Stop/Pause THEN hệ thống SHALL dừng nhạc và giữ trạng thái dừng cho đến khi người dùng chủ động phát lại

2.5 WHEN heartbeat interval chạy sau khi người dùng đã pause nhạc THEN hệ thống SHALL nhận diện đúng đây là "manual pause" và SHALL KHÔNG tự động resume nhạc

2.6 WHEN `isManualPause` được kiểm tra THEN hệ thống SHALL đọc class từ phần tử icon `<i>` bên trong `#playPauseBtn` (hoặc dùng một biến trạng thái riêng) để xác định chính xác trạng thái pause do người dùng

---

### Unchanged Behavior (Regression Prevention)

3.1 WHEN trình duyệt throttle tab (tab bị ẩn/không active) và nhạc đang phát THEN hệ thống SHALL CONTINUE TO tự động resume nhạc để chống throttling của trình duyệt

3.2 WHEN người dùng click nút Play để phát nhạc THEN hệ thống SHALL CONTINUE TO phát nhạc bình thường

3.3 WHEN trang chủ được load lần thứ hai trong vòng 5 phút THEN hệ thống SHALL CONTINUE TO trả về HomeViewModel từ cache (< 0.1s)

3.4 WHEN người dùng chuyển tab và quay lại THEN hệ thống SHALL CONTINUE TO đồng bộ trạng thái phát nhạc qua BroadcastChannel

3.5 WHEN nhạc kết thúc tự nhiên (sự kiện `ended`) THEN hệ thống SHALL CONTINUE TO tự động chuyển sang bài tiếp theo trong queue

3.6 WHEN section data đã hết hạn cache THEN hệ thống SHALL CONTINUE TO fetch dữ liệu mới từ DB và cập nhật cache

---

## Bug Condition Pseudocode

### Bug 2 — Stop Button (Primary Bug)

**Bug Condition Function:**
```pascal
FUNCTION isBugCondition(X)
  INPUT: X là trạng thái player tại thời điểm heartbeat chạy
  OUTPUT: boolean

  // Bug xảy ra khi:
  // 1. Nhạc đang paused (audioPlayer.paused = true)
  // 2. Không đang loading bài hát (isSongLoading = false)
  // 3. Có bài hát trong queue
  // 4. isManualPause bị tính sai = false (do lỗi selector)
  RETURN X.audioPlayer.paused = true
     AND X.isSongLoading = false
     AND X.playQueue.length > 0
     AND $playPauseBtn.hasClass('fa-play') = false  // ← Lỗi: selector sai
END FUNCTION
```

**Property: Fix Checking**
```pascal
// Property: Sau khi fix, heartbeat KHÔNG được resume khi user đã manual pause
FOR ALL X WHERE isBugCondition(X) DO
  // Sau fix: isManualPause phải được tính đúng
  isManualPause ← $playPauseBtn.find('i').hasClass('fa-play')
               OR window.isManuallyPaused = true
  ASSERT isManualPause = true
  ASSERT safePlayCurrentTrack() KHÔNG được gọi
END FOR
```

**Property: Preservation Checking**
```pascal
// Property: Khi tab bị throttle (nhạc đang phát bị pause bởi browser), heartbeat VẪN resume
FOR ALL X WHERE NOT isBugCondition(X) DO
  // X.audioPlayer.paused = true nhưng isManualPause = false (browser throttle)
  ASSERT F(X) = F'(X)  // Behavior giống nhau: resume nhạc
END FOR
```

### Bug 1 — Section Cache

**Bug Condition Function:**
```pascal
FUNCTION isBugCondition(X)
  INPUT: X là request tới GetHomeSectionAsync(type, userId)
  OUTPUT: boolean

  // Bug xảy ra khi section đã được load gần đây nhưng không có cache
  RETURN cache.TryGetValue("section_" + X.type) = false
     AND X.type IN ["trending", "albums", "dailymix", "mix1", "mix2", "mix3",
                    "contextual", "focus", "chill", "sad", "compilations"]
END FUNCTION
```

**Property: Fix Checking**
```pascal
FOR ALL X WHERE isBugCondition(X) DO
  // Lần đầu: fetch từ DB, lưu cache
  result1 ← GetHomeSectionAsync'(X.type, X.userId)
  // Lần hai (trong TTL): phải từ cache
  result2 ← GetHomeSectionAsync'(X.type, X.userId)
  ASSERT result2.source = "cache"
  ASSERT result2.dbQueryCount = 0
END FOR
```
