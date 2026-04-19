# Performance and Stop Button Fix — Bugfix Design

## Overview

Tài liệu này mô tả thiết kế fix cho hai bug độc lập trong ứng dụng YoutubeMusicPlayer:

**Bug 1 — Load dữ liệu chậm (`HomeFacade.cs`):** Phương thức `GetHomeSectionAsync` không có caching ở tầng section. Mỗi request đều tạo scope mới và thực hiện DB query từ đầu, kể cả khi dữ liệu vừa được fetch xong. Fix là thêm `IMemoryCache` check ở đầu method với TTL phân biệt theo loại section (5 phút cho user-specific, 10 phút cho shared sections).

**Bug 2 — Nút Stop tự bật lại (`core.js`):** Heartbeat interval (3 giây) kiểm tra `$playPauseBtn.hasClass('fa-play')` để phát hiện "manual pause", nhưng class `fa-play` nằm trên thẻ `<i>` bên trong button, không phải trên button wrapper. Kết quả là `isManualPause` luôn `false`, heartbeat luôn resume nhạc sau khi user pause. Fix là thay DOM class check bằng `window.isManuallyPaused` boolean flag.

---

## Glossary

- **Bug_Condition (C)**: Điều kiện kích hoạt bug — (1) request tới `GetHomeSectionAsync` khi không có cache; (2) heartbeat chạy sau khi user đã pause nhạc
- **Property (P)**: Hành vi đúng mong muốn — (1) section được trả về từ cache nếu còn hạn; (2) heartbeat không resume khi `isManuallyPaused = true`
- **Preservation**: Hành vi hiện tại đúng phải giữ nguyên — anti-throttling resume khi tab bị ẩn, HomeViewModel cache, cross-tab sync, auto-next khi bài kết thúc
- **`GetHomeSectionAsync`**: Phương thức trong `HomeFacade.cs` trả về `MusicSection?` cho từng loại section (Trending, Albums, DailyMix, v.v.)
- **`IMemoryCache`**: In-process cache đã được inject vào `HomeFacade` (field `_cache`), dùng để lưu kết quả section
- **Heartbeat**: `setInterval` 3 giây trong `core.js` — chịu trách nhiệm anti-throttling và state persistence
- **`isManuallyPaused`**: Boolean flag mới trên `window` — `true` khi user chủ động pause, `false` khi user chủ động play
- **Shared section**: Section không phụ thuộc userId (Trending, Albums, Focus, Chill, Sad, Compilations) — có thể cache chung cho mọi user
- **User-specific section**: Section phụ thuộc userId (DailyMix, Mix1, Mix2, Mix3, Contextual) — cache riêng theo `userId`

---

## Bug Details

### Bug 1 — Section Cache Miss

`GetHomeSectionAsync` không kiểm tra cache trước khi thực hiện DB query. Mỗi lần frontend gọi endpoint `/api/home/section?type=trending`, hệ thống tạo `IServiceScope` mới, gọi service tương ứng, và thực hiện full DB round-trip — kể cả khi cùng section vừa được fetch 1 giây trước bởi request khác.

**Formal Specification:**
```
FUNCTION isBugCondition_Cache(X)
  INPUT: X là request tới GetHomeSectionAsync(type, userId)
  OUTPUT: boolean

  cacheKey ← "section_" + X.type + "_" + (X.userId ?? "shared")
  RETURN _cache.TryGetValue(cacheKey) = false
     AND X.type IN ["trending", "albums", "dailymix", "mix1", "mix2", "mix3",
                    "contextual", "focus", "chill", "sad", "compilations"]
END FUNCTION
```

**Ví dụ cụ thể:**
- Request 1: `GET /api/home/section?type=trending` lúc 10:00:00 → DB query, trả về 10 bài, không cache
- Request 2: `GET /api/home/section?type=trending` lúc 10:00:01 → DB query lại, cùng kết quả, không cache
- Request 3 (sau fix): `GET /api/home/section?type=trending` lúc 10:00:01 → cache HIT, trả về ngay, 0 DB query
- Edge case: `type = "mix2"` với `userId = null` → trả về `null` (không cache vì không có dữ liệu)

### Bug 2 — Sai Selector Trong Heartbeat

Heartbeat kiểm tra `$playPauseBtn.hasClass('fa-play')` để xác định user đã manual pause. Nhưng `#playPauseBtn` là button wrapper — class `fa-play` / `fa-pause` nằm trên thẻ `<i>` bên trong. jQuery `.hasClass()` chỉ kiểm tra class của chính element đó, không kiểm tra children. Kết quả: `isManualPause` luôn `false`.

**Formal Specification:**
```
FUNCTION isBugCondition_StopBtn(X)
  INPUT: X là trạng thái player tại thời điểm heartbeat chạy
  OUTPUT: boolean

  // Bug xảy ra khi tất cả điều kiện sau đúng:
  RETURN X.audioPlayer.paused = true
     AND X.isSongLoading = false
     AND X.playQueue.length > 0
     AND $playPauseBtn.hasClass('fa-play') = false   // ← luôn false do selector sai
     AND user_đã_manual_pause = true                 // ← ý định thực sự của user
END FUNCTION
```

**Ví dụ cụ thể:**
- User click Pause lúc 10:00:00 → `audioPlayer.pause()` được gọi, icon đổi sang `fa-play` (trên `<i>`)
- Heartbeat chạy lúc 10:00:03 → `$playPauseBtn.hasClass('fa-play')` = `false` (sai) → `isManualPause = false`
- Heartbeat thấy `!isManualPause && audioPlayer.paused` = `true` → gọi `safePlayCurrentTrack()` → nhạc tự phát lại
- Edge case: Tab bị ẩn (browser throttle) → nhạc bị pause bởi browser, không phải user → heartbeat PHẢI resume (behavior đúng, cần preserve)

---

## Expected Behavior

### Preservation Requirements

**Hành vi không được thay đổi:**
- Anti-throttling: Khi tab bị ẩn và browser throttle audio, heartbeat vẫn phải tự động resume nhạc
- HomeViewModel cache: `BuildHomeViewModelAsync` vẫn cache toàn bộ ViewModel 5 phút như hiện tại
- Cross-tab sync: `BroadcastChannel` vẫn đồng bộ play/pause/seek giữa các tab
- Auto-next: Khi bài kết thúc (`ended` event), hệ thống vẫn tự chuyển bài tiếp theo
- Play button: Khi user click Play, nhạc vẫn phát bình thường
- Section data refresh: Khi cache hết hạn, hệ thống vẫn fetch dữ liệu mới từ DB
- Artist songs cache: `GetSongsByArtistAsync` vẫn cache 30 phút như hiện tại
- Discovery cache: `GetDiscoverySongsAsync` vẫn cache 15 phút như hiện tại

**Scope:**
Mọi input không thuộc bug condition phải hoàn toàn không bị ảnh hưởng:
- Mouse click trên button Play/Pause
- Keyboard shortcuts (nếu có)
- Cross-tab sync messages
- `audioPlayer.ended` event
- `BuildHomeViewModelAsync` flow
- `SearchAllAsync` flow
- `GetSongsByArtistAsync` flow

---

## Hypothesized Root Cause

### Bug 1 — Section Cache Miss

1. **Thiếu cache check trong `GetHomeSectionAsync`**: Method hiện tại đi thẳng vào `switch` statement mà không kiểm tra `_cache` trước. `IMemoryCache` đã được inject (field `_cache`) và dùng ở các method khác (`BuildHomeViewModelAsync`, `GetSongsByArtistAsync`, `GetDiscoverySongsAsync`) nhưng bị bỏ sót ở `GetHomeSectionAsync`.

2. **Không có cache key strategy cho section**: Không có convention nào cho cache key của section. Cần phân biệt shared sections (không có userId) và user-specific sections (có userId) để tránh data leak giữa users.

3. **TTL chưa được định nghĩa**: Trending/Albums thay đổi chậm → TTL dài hơn (10 phút). DailyMix/Contextual phụ thuộc lịch sử nghe của user → TTL ngắn hơn (5 phút).

### Bug 2 — Sai Selector Trong Heartbeat

1. **Sai target element cho `.hasClass()`**: `$playPauseBtn` trỏ tới `#playPauseBtn` (button wrapper). Class `fa-play`/`fa-pause` được set trên `<i>` bên trong bởi `updatePlayPauseUI`. jQuery `.hasClass()` không traverse children — nó chỉ kiểm tra class của element được chọn.

2. **Không có explicit state flag**: Thay vì dùng DOM class làm source of truth cho trạng thái "manual pause", cần một boolean flag rõ ràng (`window.isManuallyPaused`) được set/clear tại đúng nơi (click handler của Play/Pause button).

3. **Coupling giữa UI state và logic state**: Dùng DOM class để suy ra intent của user là fragile — bất kỳ thay đổi nào về UI (đổi icon library, refactor HTML) đều có thể break logic. Flag boolean tách biệt UI khỏi business logic.

---

## Correctness Properties

Property 1: Bug Condition — Section Cache Hit

_For any_ request tới `GetHomeSectionAsync(type, userId)` mà cùng `(type, userId)` đã được gọi trước đó trong TTL, hàm sau khi fix SHALL trả về dữ liệu từ `IMemoryCache` mà không thực hiện thêm DB query hoặc tạo `IServiceScope` mới.

**Validates: Requirements 2.1, 2.2, 2.3**

Property 2: Bug Condition — Manual Pause Preserved

_For any_ trạng thái player X mà `window.isManuallyPaused = true` và `audioPlayer.paused = true`, heartbeat sau khi fix SHALL KHÔNG gọi `safePlayCurrentTrack()` hoặc `audioPlayer.play()`.

**Validates: Requirements 2.4, 2.5, 2.6**

Property 3: Preservation — Anti-Throttling Resume

_For any_ trạng thái player X mà `window.isManuallyPaused = false` và `audioPlayer.paused = true` và `!window.isSongLoading`, heartbeat sau khi fix SHALL gọi `safePlayCurrentTrack()` — giống hệt behavior của code gốc khi không có bug.

**Validates: Requirements 3.1, 3.3, 3.4**

Property 4: Preservation — Section Data Integrity

_For any_ section được trả về từ cache, dữ liệu SHALL giống hệt với dữ liệu sẽ được trả về nếu fetch trực tiếp từ DB tại thời điểm cache được tạo (không bị mutate, không bị truncate).

**Validates: Requirements 3.6**

---

## Fix Implementation

### Bug 1 — Thêm Cache vào `GetHomeSectionAsync`

**File**: `YoutubeMusicPlayer.Application/Services/HomeFacade.cs`

**Function**: `GetHomeSectionAsync`

**Specific Changes:**

1. **Thêm cache key strategy**: Phân biệt shared vs user-specific sections
   - Shared (Trending, Albums, Focus, Chill, Sad, Compilations): `"section_{type}_shared"`
   - User-specific (DailyMix, Mix1, Mix2, Mix3, Contextual): `"section_{type}_{userId}"`

2. **Thêm cache check ở đầu method** (trước `switch` statement):
   ```csharp
   string cacheKey = IsUserSpecificSection(type) && userId.HasValue
       ? $"section_{type.ToLower()}_{userId.Value}"
       : $"section_{type.ToLower()}_shared";

   if (!refresh && _cache.TryGetValue(cacheKey, out MusicSection? cached) && cached != null)
   {
       _logger.LogInformation("[HOME-FACADE] Cache HIT for section: {Type}", type);
       return cached;
   }
   ```

3. **Thêm cache set ở cuối method** (trước `return section`):
   ```csharp
   var ttl = IsSharedSection(type) ? TimeSpan.FromMinutes(10) : TimeSpan.FromMinutes(5);
   _cache.Set(cacheKey, section, ttl);
   ```

4. **Thêm helper method** để phân loại section:
   ```csharp
   private static bool IsSharedSection(string type) =>
       type.ToLower() is SectionTypes.Trending or SectionTypes.Albums
           or SectionTypes.Focus or SectionTypes.Chill
           or SectionTypes.Sad or SectionTypes.Compilations;
   ```

5. **Tôn trọng `refresh` flag**: Khi `refresh = true`, bỏ qua cache check (đã có trong logic trên) và invalidate cache cũ sau khi fetch xong.

### Bug 2 — Thay DOM Class Check bằng `window.isManuallyPaused`

**File**: `YoutubeMusicPlayer/wwwroot/js/app/player/core.js`

**Specific Changes:**

1. **Khởi tạo flag** ở đầu `$(function() {...})`:
   ```javascript
   window.isManuallyPaused = false;
   ```

2. **Set flag trong click handler của `$playPauseBtn`**:
   ```javascript
   $playPauseBtn.on('click', async () => {
       if (audioPlayer.paused) {
           window.isManuallyPaused = false;  // User chủ động play
           if (typeof safePlayCurrentTrack === 'function') await safePlayCurrentTrack();
           window.syncChannel.postMessage({ type: 'play', track: window.playQueue[window.currentIndex] });
       } else {
           window.isManuallyPaused = true;   // User chủ động pause
           audioPlayer.pause();
           if (typeof updatePlayPauseUI === 'function') updatePlayPauseUI(false);
           window.syncChannel.postMessage({ type: 'pause' });
       }
   });
   ```

3. **Thay thế selector sai trong heartbeat**:
   ```javascript
   // TRƯỚC (sai):
   const isManualPause = $playPauseBtn.hasClass('fa-play');
   
   // SAU (đúng):
   const isManualPause = window.isManuallyPaused;
   ```

4. **Reset flag khi load bài mới**: Trong `loadAndPlay` hoặc bất kỳ nơi nào gọi `safePlayCurrentTrack()` programmatically (không phải từ user click), đảm bảo `window.isManuallyPaused = false`.

---

## Testing Strategy

### Validation Approach

Chiến lược test theo hai giai đoạn: (1) chạy test trên code **chưa fix** để xác nhận bug tồn tại và hiểu root cause; (2) chạy test trên code **đã fix** để xác nhận fix đúng và không có regression.

---

### Exploratory Bug Condition Checking

**Goal**: Surface counterexamples chứng minh bug tồn tại TRƯỚC khi implement fix. Xác nhận hoặc bác bỏ root cause analysis.

**Test Plan — Bug 1:**
Gọi `GetHomeSectionAsync` hai lần liên tiếp với cùng `type` và `userId`, đo số lần DB query được thực hiện. Trên code chưa fix, cả hai lần đều query DB.

**Test Cases — Bug 1:**
1. **Trending Double Call**: Gọi `GetHomeSectionAsync("trending", null)` hai lần → expect lần 2 không query DB (sẽ fail trên unfixed code)
2. **Albums Double Call**: Gọi `GetHomeSectionAsync("albums", null)` hai lần → expect lần 2 không query DB (sẽ fail)
3. **DailyMix User-Specific**: Gọi `GetHomeSectionAsync("dailymix", userId: 1)` hai lần → expect lần 2 không query DB (sẽ fail)
4. **Cross-User Isolation**: Gọi `GetHomeSectionAsync("dailymix", userId: 1)` rồi `GetHomeSectionAsync("dailymix", userId: 2)` → expect kết quả khác nhau (edge case)

**Test Plan — Bug 2:**
Mock `audioPlayer.paused = true`, `window.isSongLoading = false`, `window.playQueue` có bài. Simulate user đã click Pause (icon `<i>` có class `fa-play`). Trigger heartbeat callback. Trên code chưa fix, `safePlayCurrentTrack` sẽ bị gọi.

**Test Cases — Bug 2:**
1. **Manual Pause Not Resumed**: User click Pause → heartbeat chạy → assert `safePlayCurrentTrack` KHÔNG được gọi (sẽ fail trên unfixed code)
2. **Browser Throttle Still Resumed**: Simulate browser throttle (pause không phải từ user) → heartbeat chạy → assert `safePlayCurrentTrack` ĐƯỢC gọi (phải pass cả trước và sau fix)
3. **Play After Pause**: User click Pause → user click Play → heartbeat chạy → assert `safePlayCurrentTrack` KHÔNG được gọi thêm lần nữa bởi heartbeat

**Expected Counterexamples:**
- Bug 1: DB query count = 2 cho cả hai lần gọi (thay vì 1 lần fetch + 1 lần cache hit)
- Bug 2: `safePlayCurrentTrack` được gọi bởi heartbeat dù user đã manual pause

---

### Fix Checking

**Goal**: Xác nhận rằng với mọi input thuộc bug condition, hàm sau khi fix cho ra kết quả đúng.

**Pseudocode — Bug 1:**
```
FOR ALL X WHERE isBugCondition_Cache(X) DO
  result1 ← GetHomeSectionAsync_fixed(X.type, X.userId)  // fetch + cache
  result2 ← GetHomeSectionAsync_fixed(X.type, X.userId)  // cache hit
  ASSERT result2.source = "cache"
  ASSERT result1.songs = result2.songs  // data integrity
  ASSERT dbQueryCount(result2) = 0
END FOR
```

**Pseudocode — Bug 2:**
```
FOR ALL X WHERE isBugCondition_StopBtn(X) DO
  window.isManuallyPaused ← true  // user đã pause
  triggerHeartbeat()
  ASSERT safePlayCurrentTrack WAS NOT CALLED
  ASSERT audioPlayer.paused = true  // vẫn paused
END FOR
```

---

### Preservation Checking

**Goal**: Xác nhận rằng với mọi input KHÔNG thuộc bug condition, behavior giống hệt code gốc.

**Pseudocode:**
```
FOR ALL X WHERE NOT isBugCondition(X) DO
  ASSERT F_original(X) = F_fixed(X)
END FOR
```

**Testing Approach**: Property-based testing được khuyến nghị cho preservation checking vì:
- Tự động sinh nhiều test case trên toàn bộ input domain
- Bắt được edge case mà unit test thủ công có thể bỏ sót
- Đảm bảo mạnh rằng behavior không thay đổi cho mọi non-buggy input

**Test Cases — Preservation:**
1. **Anti-Throttling Preserved**: `window.isManuallyPaused = false`, `audioPlayer.paused = true` → heartbeat PHẢI gọi `safePlayCurrentTrack` (behavior gốc đúng)
2. **HomeViewModel Cache Unchanged**: `BuildHomeViewModelAsync` vẫn cache 5 phút, không bị ảnh hưởng bởi section cache
3. **Artist Songs Cache Unchanged**: `GetSongsByArtistAsync` vẫn cache 30 phút
4. **Section Refresh Flag**: Khi `refresh = true`, cache bị bỏ qua và dữ liệu mới được fetch
5. **Cross-Tab Sync Preserved**: `syncChannel.postMessage` vẫn được gọi đúng khi user click Play/Pause
6. **Null Return Preserved**: `GetHomeSectionAsync("mix2", null)` vẫn trả về `null` (không cache null result)

---

### Unit Tests

- Test `GetHomeSectionAsync` với mock `IMemoryCache` — verify cache set được gọi sau lần fetch đầu
- Test `GetHomeSectionAsync` với cache đã có — verify không tạo `IServiceScope` mới
- Test TTL: shared sections nhận TTL 10 phút, user-specific nhận TTL 5 phút
- Test `refresh = true` bỏ qua cache và invalidate entry cũ
- Test heartbeat với `window.isManuallyPaused = true` — verify `safePlayCurrentTrack` không được gọi
- Test heartbeat với `window.isManuallyPaused = false` và `audioPlayer.paused = true` — verify `safePlayCurrentTrack` được gọi
- Test click handler set `window.isManuallyPaused = true` khi pause, `false` khi play

### Property-Based Tests

- Sinh ngẫu nhiên `(type, userId)` pairs — verify rằng lần gọi thứ 2 trong TTL luôn là cache hit
- Sinh ngẫu nhiên chuỗi play/pause actions từ user — verify `isManuallyPaused` luôn phản ánh đúng action cuối cùng
- Sinh ngẫu nhiên các trạng thái player (paused/playing, loading/not loading) — verify heartbeat chỉ resume khi `isManuallyPaused = false`
- Verify data integrity: section từ cache luôn bằng section từ DB tại thời điểm cache được tạo

### Integration Tests

- Test full flow: frontend gọi `/api/home/section?type=trending` hai lần → verify response time lần 2 < 50ms
- Test user isolation: hai user khác nhau gọi `GetHomeSectionAsync("dailymix", userId)` → verify nhận dữ liệu khác nhau
- Test play → pause → wait 5s → verify nhạc vẫn paused (không tự resume)
- Test play → pause → play → verify nhạc phát lại bình thường
- Test tab switch: ẩn tab → nhạc bị throttle → hiện tab → verify nhạc resume (anti-throttling vẫn hoạt động)
- Test cache expiry: sau TTL, verify section được fetch lại từ DB
