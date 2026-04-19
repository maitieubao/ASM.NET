# Implementation Plan

- [x] 1. Viết bug condition exploration test — Stop Button (Bug 2)
  - **Property 1: Bug Condition** - Heartbeat Tự Resume Sau Khi User Manual Pause
  - **CRITICAL**: Test này PHẢI FAIL trên code chưa fix — failure xác nhận bug tồn tại
  - **DO NOT attempt to fix the test or the code when it fails**
  - **GOAL**: Surface counterexample chứng minh heartbeat gọi `safePlayCurrentTrack` dù user đã pause
  - **Scoped PBT Approach**: Scope property vào concrete failing case: `audioPlayer.paused = true`, `isSongLoading = false`, `playQueue.length > 0`, user đã click Pause (icon `<i>` có class `fa-play`)
  - Setup: Mock `audioPlayer`, set `audioPlayer.paused = true`, `window.isSongLoading = false`, `window.playQueue = [{videoId: 'test'}]`, `window.currentIndex = 0`
  - Simulate user click Pause: gọi `audioPlayer.pause()`, cập nhật icon `<i>` thành `fa-play` (như `updatePlayPauseUI(false)` làm)
  - Trigger heartbeat callback (extract heartbeat logic ra function để test được)
  - Assert: `safePlayCurrentTrack` KHÔNG được gọi (từ Bug Condition trong design: `isBugCondition_StopBtn` — `$playPauseBtn.hasClass('fa-play') = false` luôn do selector sai)
  - Assert: `audioPlayer.paused` vẫn là `true` sau heartbeat
  - Chạy test trên code CHƯA fix
  - **EXPECTED OUTCOME**: Test FAILS — `safePlayCurrentTrack` bị gọi dù user đã pause (xác nhận bug)
  - Document counterexample: "Sau khi user click Pause, heartbeat lúc t+3s gọi safePlayCurrentTrack() vì `$playPauseBtn.hasClass('fa-play')` = false (selector sai — class nằm trên `<i>` bên trong, không phải button wrapper)"
  - Mark task complete khi test đã viết, chạy, và failure được document
  - _Requirements: 1.4, 1.5, 1.6_

- [x] 2. Viết bug condition exploration test — Section Cache (Bug 1)
  - **Property 1: Bug Condition** - Section Cache Miss Gây DB Query Lặp Lại
  - **CRITICAL**: Test này PHẢI FAIL trên code chưa fix — failure xác nhận bug tồn tại
  - **DO NOT attempt to fix the test or the code when it fails**
  - **GOAL**: Surface counterexample chứng minh `GetHomeSectionAsync` query DB hai lần cho cùng section
  - **Scoped PBT Approach**: Scope vào concrete failing cases: `type = "trending"` với `userId = null`, `type = "albums"` với `userId = null`, `type = "dailymix"` với `userId = 1`
  - Setup: Mock `IMemoryCache` (real hoặc mock), mock các service dependencies (`IYoutubeService`, `IAlbumService`, `IRecommendationService`)
  - Gọi `GetHomeSectionAsync("trending", null)` lần 1 → ghi nhận số lần `IYoutubeService.GetTrendingMusicAsync` được gọi
  - Gọi `GetHomeSectionAsync("trending", null)` lần 2 → ghi nhận lại
  - Assert: Tổng số lần gọi service = 1 (lần 2 phải từ cache, không gọi service) — sẽ FAIL vì không có cache
  - Lặp lại với `type = "albums"` và `type = "dailymix"` (userId = 1)
  - Chạy test trên code CHƯA fix
  - **EXPECTED OUTCOME**: Test FAILS — service được gọi 2 lần thay vì 1 lần (xác nhận bug)
  - Document counterexample: "GetHomeSectionAsync('trending', null) gọi IYoutubeService.GetTrendingMusicAsync 2 lần trong 1 giây — không có cache check ở đầu method"
  - Mark task complete khi test đã viết, chạy, và failure được document
  - _Requirements: 1.1, 1.2, 1.3_

- [x] 3. Viết preservation property tests (TRƯỚC khi implement fix)
  - **Property 2: Preservation** - Anti-Throttling Resume Và Các Behavior Hiện Tại Đúng
  - **IMPORTANT**: Follow observation-first methodology — chạy trên code CHƯA fix trước
  - **Preservation Test A — Anti-Throttling Resume (core.js)**:
    - Observe: `window.isManuallyPaused` chưa tồn tại trên unfixed code, heartbeat dùng `$playPauseBtn.hasClass('fa-play')`
    - Setup: `audioPlayer.paused = true`, `window.isSongLoading = false`, `window.playQueue` có bài, KHÔNG simulate user click Pause (browser throttle scenario — `$playPauseBtn` không có class `fa-play` trên button wrapper)
    - Observe: heartbeat GỌI `safePlayCurrentTrack` (behavior đúng cần preserve)
    - Write property: For all states where `isManuallyPaused = false` AND `audioPlayer.paused = true` AND `!isSongLoading` AND `playQueue.length > 0`, heartbeat SHALL call `safePlayCurrentTrack`
    - Verify test PASSES trên unfixed code
  - **Preservation Test B — Section Null Return (HomeFacade.cs)**:
    - Observe: `GetHomeSectionAsync("mix2", null)` trả về `null` trên unfixed code
    - Write property: For all user-specific section types (`mix2`, `mix3`) with `userId = null`, method SHALL return `null`
    - Verify test PASSES trên unfixed code
  - **Preservation Test C — HomeViewModel Cache Unchanged**:
    - Observe: `BuildHomeViewModelAsync` cache key `"home_vm_{userId}"` với TTL 5 phút không bị ảnh hưởng
    - Write property: `BuildHomeViewModelAsync` vẫn trả về cached result sau lần gọi đầu
    - Verify test PASSES trên unfixed code
  - **Preservation Test D — Refresh Flag Bypass**:
    - Observe: `GetHomeSectionAsync("trending", null, refresh: true)` luôn gọi service (không cache) trên unfixed code
    - Write property: Khi `refresh = true`, service luôn được gọi bất kể cache state
    - Verify test PASSES trên unfixed code
  - Chạy tất cả preservation tests trên code CHƯA fix
  - **EXPECTED OUTCOME**: Tất cả preservation tests PASS (xác nhận baseline behavior)
  - Mark task complete khi tests đã viết, chạy, và passing trên unfixed code
  - _Requirements: 3.1, 3.2, 3.3, 3.4, 3.5, 3.6_

- [x] 4. Fix Bug 2 — Stop Button tự bật lại (`core.js`)

  - [x] 4.1 Khởi tạo `window.isManuallyPaused` flag
    - Thêm `window.isManuallyPaused = false;` ở đầu `$(function() {...})`, trước khi bind elements
    - File: `YoutubeMusicPlayer/wwwroot/js/app/player/core.js`
    - _Bug_Condition: isBugCondition_StopBtn — `$playPauseBtn.hasClass('fa-play')` luôn false do selector sai (class nằm trên `<i>`, không phải button wrapper)_
    - _Expected_Behavior: `window.isManuallyPaused` là source of truth cho manual pause state_
    - _Requirements: 2.4, 2.5, 2.6_

  - [x] 4.2 Set flag trong click handler của `$playPauseBtn`
    - Trong branch `if (audioPlayer.paused)` (user click Play): thêm `window.isManuallyPaused = false;` trước `safePlayCurrentTrack()`
    - Trong branch `else` (user click Pause): thêm `window.isManuallyPaused = true;` trước `audioPlayer.pause()`
    - File: `YoutubeMusicPlayer/wwwroot/js/app/player/core.js`
    - _Bug_Condition: Flag phải được set tại đúng nơi — click handler, không phải audio events_
    - _Expected_Behavior: Sau khi user click Pause, `window.isManuallyPaused = true`; sau khi user click Play, `window.isManuallyPaused = false`_
    - _Requirements: 2.4, 2.5_

  - [x] 4.3 Thay selector sai trong heartbeat
    - Tìm dòng: `const isManualPause = $playPauseBtn.hasClass('fa-play');`
    - Thay bằng: `const isManualPause = window.isManuallyPaused;`
    - File: `YoutubeMusicPlayer/wwwroot/js/app/player/core.js` (trong `setInterval` callback)
    - _Bug_Condition: `$playPauseBtn.hasClass('fa-play')` = false vì class nằm trên `<i>` bên trong, không phải button wrapper_
    - _Preservation: `!isManualPause && audioPlayer.paused && !window.isSongLoading` vẫn trigger anti-throttling resume khi `isManuallyPaused = false`_
    - _Requirements: 2.5, 2.6, 3.1_

  - [x] 4.4 Verify bug condition exploration test (task 1) now passes
    - **Property 1: Expected Behavior** - Heartbeat Không Resume Khi `isManuallyPaused = true`
    - **IMPORTANT**: Re-run SAME test từ task 1 — KHÔNG viết test mới
    - Test từ task 1 encode expected behavior: heartbeat KHÔNG gọi `safePlayCurrentTrack` khi user đã manual pause
    - Chạy lại test trên code ĐÃ fix
    - **EXPECTED OUTCOME**: Test PASSES — xác nhận Bug 2 đã được fix
    - _Requirements: 2.4, 2.5, 2.6_

  - [x] 4.5 Verify preservation tests (task 3) vẫn pass sau fix Bug 2
    - **Property 2: Preservation** - Anti-Throttling Resume Vẫn Hoạt Động
    - **IMPORTANT**: Re-run SAME tests từ task 3 — KHÔNG viết test mới
    - Chạy Preservation Test A (anti-throttling) trên code đã fix Bug 2
    - **EXPECTED OUTCOME**: Tests PASS — không có regression

- [x] 5. Fix Bug 1 — Section Cache Miss (`HomeFacade.cs`)

  - [x] 5.1 Thêm helper method `IsSharedSection`
    - Thêm private static method vào class `HomeFacade`:
      ```csharp
      private static bool IsSharedSection(string type) =>
          type.ToLower() is SectionTypes.Trending or SectionTypes.Albums
              or SectionTypes.Focus or SectionTypes.Chill
              or SectionTypes.Sad or SectionTypes.Compilations;
      ```
    - File: `YoutubeMusicPlayer.Application/Services/HomeFacade.cs`
    - _Bug_Condition: Shared sections không phụ thuộc userId — có thể cache chung cho mọi user_
    - _Requirements: 2.3_

  - [x] 5.2 Thêm cache check ở đầu `GetHomeSectionAsync`
    - Thêm cache key strategy và early return trước `switch` statement:
      ```csharp
      bool isUserSpecific = !IsSharedSection(type) && userId.HasValue;
      string cacheKey = isUserSpecific
          ? $"section_{type.ToLower()}_{userId!.Value}"
          : $"section_{type.ToLower()}_shared";

      if (!refresh && _cache.TryGetValue(cacheKey, out MusicSection? cached) && cached != null)
      {
          _logger.LogInformation("[HOME-FACADE] Cache HIT for section: {Type}", type);
          return cached;
      }
      ```
    - File: `YoutubeMusicPlayer.Application/Services/HomeFacade.cs`
    - _Bug_Condition: isBugCondition_Cache — `_cache.TryGetValue(cacheKey) = false` cho mọi request hiện tại_
    - _Expected_Behavior: Lần gọi thứ 2 trong TTL trả về từ cache, không tạo IServiceScope mới_
    - _Preservation: `refresh = true` bỏ qua cache check (điều kiện `!refresh`)_
    - _Requirements: 2.1, 2.2, 2.3_

  - [x] 5.3 Thêm cache set ở cuối `GetHomeSectionAsync` (trước `return section`)
    - Tìm đoạn cuối method, trước `return section;` (sau Level 3 Active Cache Mapping block)
    - Thêm:
      ```csharp
      var ttl = IsSharedSection(type) ? TimeSpan.FromMinutes(10) : TimeSpan.FromMinutes(5);
      _cache.Set(cacheKey, section, ttl);
      ```
    - Lưu ý: `cacheKey` đã được khai báo ở task 5.2 — đảm bảo scope đúng
    - Shared sections (Trending, Albums, Focus, Chill, Sad, Compilations): TTL 10 phút
    - User-specific sections (DailyMix, Mix1, Mix2, Mix3, Contextual): TTL 5 phút
    - File: `YoutubeMusicPlayer.Application/Services/HomeFacade.cs`
    - _Bug_Condition: Không có cache set → mỗi request đều query DB_
    - _Expected_Behavior: Sau lần fetch đầu, section được cache với TTL phù hợp_
    - _Preservation: Null result (mix2/mix3 với userId=null) không được cache — method return null trước khi đến cache set_
    - _Requirements: 2.1, 2.2, 2.3, 3.6_

  - [x] 5.4 Verify bug condition exploration test (task 2) now passes
    - **Property 1: Expected Behavior** - Section Cache Hit Sau Lần Fetch Đầu
    - **IMPORTANT**: Re-run SAME test từ task 2 — KHÔNG viết test mới
    - Test từ task 2 encode expected behavior: lần gọi thứ 2 không gọi service
    - Chạy lại test trên code ĐÃ fix
    - **EXPECTED OUTCOME**: Test PASSES — xác nhận Bug 1 đã được fix
    - _Requirements: 2.1, 2.2, 2.3_

  - [x] 5.5 Verify preservation tests (task 3) vẫn pass sau fix Bug 1
    - **Property 2: Preservation** - Null Return, HomeViewModel Cache, Refresh Flag Vẫn Đúng
    - **IMPORTANT**: Re-run SAME tests từ task 3 — KHÔNG viết test mới
    - Chạy Preservation Test B (null return), C (HomeViewModel cache), D (refresh flag) trên code đã fix Bug 1
    - **EXPECTED OUTCOME**: Tests PASS — không có regression

- [x] 6. Checkpoint — Đảm bảo tất cả tests pass
  - Chạy toàn bộ test suite: exploration tests (tasks 1, 2) và preservation tests (task 3)
  - Verify: Exploration test Bug 2 (task 1) PASS sau fix
  - Verify: Exploration test Bug 1 (task 2) PASS sau fix
  - Verify: Tất cả preservation tests (task 3) PASS sau cả hai fix
  - Build project để đảm bảo không có compile error: `dotnet build YoutubeMusicPlayer.slnx`
  - Nếu có test nào fail, hỏi user trước khi tiếp tục
  - _Requirements: 2.1, 2.2, 2.3, 2.4, 2.5, 2.6, 3.1, 3.2, 3.3, 3.4, 3.5, 3.6_
