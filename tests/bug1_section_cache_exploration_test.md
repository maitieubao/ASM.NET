# Bug 1 — Section Cache Exploration Test
## Validates: Requirements 1.1, 1.2, 1.3

**GOAL**: Surface counterexample chứng minh `GetHomeSectionAsync` query DB hai lần cho cùng section.

**EXPECTED OUTCOME**: Test FAILS trên code chưa fix.
Failure xác nhận bug tồn tại: không có cache check ở đầu `GetHomeSectionAsync` → mỗi request đều tạo `IServiceScope` mới và gọi service → `IYoutubeService.GetTrendingMusicAsync` được gọi 2 lần thay vì 1 lần.

---

## Bug Condition (Formal)

```pascal
FUNCTION isBugCondition(X)
  INPUT: X là request tới GetHomeSectionAsync(type, userId)
  OUTPUT: boolean

  cacheKey ← "section_" + X.type + "_" + (X.userId ?? "shared")
  RETURN _cache.TryGetValue(cacheKey) = false
     AND X.type IN ["trending", "albums", "dailymix", "mix1", "mix2", "mix3",
                    "contextual", "focus", "chill", "sad", "compilations"]
END FUNCTION
```

---

## Counterexample

### Test Case 1: Trending Double Call

**Setup:**
- `IYoutubeService.GetTrendingMusicAsync` là mock, đếm số lần được gọi
- `IMemoryCache` là real `MemoryCache` (không mock — để xác nhận cache không được dùng)
- `IServiceScopeFactory` inject mock `IYoutubeService` vào scope

**Execution:**
```
callCount = 0

// Lần 1
result1 = await GetHomeSectionAsync("trending", null)
// → Tạo IServiceScope mới
// → Gọi youtubeSvc.GetTrendingMusicAsync(10, false)
// → callCount = 1
// → KHÔNG set cache

// Lần 2 (ngay sau đó, trong TTL nếu có cache)
result2 = await GetHomeSectionAsync("trending", null)
// → Tạo IServiceScope mới (lại)
// → Gọi youtubeSvc.GetTrendingMusicAsync(10, false) (lại)
// → callCount = 2  ← BUG: phải là 1 nếu có cache
```

**Assert (sẽ FAIL trên code chưa fix):**
```
ASSERT callCount == 1
// Observed (buggy): callCount = 2
// Expected (correct): callCount = 1 (lần 2 từ cache, không gọi service)
```

**Root cause:**
`GetHomeSectionAsync` đi thẳng vào `switch` statement mà không kiểm tra `_cache` trước.
`IMemoryCache` đã được inject (`_cache`) và dùng ở `BuildHomeViewModelAsync`, `GetSongsByArtistAsync`, `GetDiscoverySongsAsync` — nhưng bị bỏ sót ở `GetHomeSectionAsync`.

---

### Test Case 2: Albums Double Call

**Setup:** Tương tự Test Case 1, nhưng với `IAlbumService.GetTrendingAlbumsAsync`

**Execution:**
```
albumCallCount = 0

result1 = await GetHomeSectionAsync("albums", null)
// → albumCallCount = 1

result2 = await GetHomeSectionAsync("albums", null)
// → albumCallCount = 2  ← BUG
```

**Assert (sẽ FAIL):**
```
ASSERT albumCallCount == 1
// Observed: albumCallCount = 2
```

---

### Test Case 3: DailyMix User-Specific Double Call

**Setup:** Mock `IRecommendationService.GetDailyMixVariantAsync`, userId = 1

**Execution:**
```
dailymixCallCount = 0

result1 = await GetHomeSectionAsync("dailymix", userId: 1)
// → dailymixCallCount = 1

result2 = await GetHomeSectionAsync("dailymix", userId: 1)
// → dailymixCallCount = 2  ← BUG
```

**Assert (sẽ FAIL):**
```
ASSERT dailymixCallCount == 1
// Observed: dailymixCallCount = 2
```

---

## C# Test Code (xUnit + Moq)

Không có test project trong solution. Code dưới đây là pseudocode C# để document test logic.
Nếu cần chạy thực tế, tạo project `YoutubeMusicPlayer.Tests` với xUnit + Moq.

```csharp
// File: YoutubeMusicPlayer.Tests/HomeFacade_Bug1_SectionCacheExplorationTest.cs
// Validates: Requirements 1.1, 1.2, 1.3
// EXPECTED OUTCOME: Tests FAIL on unfixed code (confirms bug exists)

using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using YoutubeMusicPlayer.Application.Services;
using YoutubeMusicPlayer.Application.Interfaces;
using YoutubeMusicPlayer.Domain.Entities;

public class HomeFacade_Bug1_SectionCacheExplorationTest
{
    /// <summary>
    /// Bug Condition: GetHomeSectionAsync("trending", null) gọi IYoutubeService.GetTrendingMusicAsync
    /// 2 lần thay vì 1 lần — không có cache check ở đầu method.
    ///
    /// EXPECTED: Test FAILS on unfixed code.
    /// Counterexample: callCount = 2 (observed) vs callCount = 1 (expected)
    /// </summary>
    [Fact]
    public async Task GetHomeSectionAsync_Trending_ShouldNotCallServiceTwice_BugCondition()
    {
        // Arrange
        var callCount = 0;

        var mockYoutubeService = new Mock<IYoutubeService>();
        mockYoutubeService
            .Setup(s => s.GetTrendingMusicAsync(It.IsAny<int>(), It.IsAny<bool>()))
            .ReturnsAsync(() => {
                callCount++;
                return new List<YoutubeVideoDetails>
                {
                    new() { YoutubeVideoId = "vid1", Title = "Song 1", AuthorName = "Artist 1" }
                };
            });

        var serviceProvider = new ServiceCollection()
            .AddSingleton(mockYoutubeService.Object)
            // ... other mocked services
            .BuildServiceProvider();

        var scopeFactory = serviceProvider.GetRequiredService<IServiceScopeFactory>();
        var cache = new MemoryCache(new MemoryCacheOptions());
        var logger = Mock.Of<ILogger<HomeFacade>>();

        var facade = new HomeFacade(
            Mock.Of<IArtistService>(),
            Mock.Of<IGenreService>(),
            Mock.Of<IInteractionService>(),
            Mock.Of<ISongService>(),
            mockYoutubeService.Object,
            Mock.Of<IAlbumService>(),
            Mock.Of<IRecommendationService>(),
            Mock.Of<IDeezerService>(),
            Mock.Of<IITunesService>(),
            scopeFactory,
            cache,
            logger
        );

        // Act
        var result1 = await facade.GetHomeSectionAsync("trending", null);
        var result2 = await facade.GetHomeSectionAsync("trending", null);

        // Assert — WILL FAIL on unfixed code (callCount = 2, not 1)
        // Counterexample: GetHomeSectionAsync("trending", null) gọi
        // IYoutubeService.GetTrendingMusicAsync 2 lần trong 1 giây
        // — không có cache check ở đầu method
        Assert.Equal(1, callCount);
        // Observed (buggy): callCount = 2
        // Expected (correct): callCount = 1 (lần 2 từ cache)
    }
}
```

---

## Observed Counterexample (Buggy Behavior)

```
Input:
  type    = "trending"
  userId  = null
  refresh = false (default)

Call 1 → GetHomeSectionAsync("trending", null):
  [No cache check]
  switch("trending"):
    using (var scope = _scopeFactory.CreateScope())  ← Scope #1 created
    {
        var youtubeSvc = scope.ServiceProvider.GetRequiredService<IYoutubeService>();
        var trendingSongs = await youtubeSvc.GetTrendingMusicAsync(10, false);  ← DB/API call #1
        section.Songs = trendingSongs;
    }
  [No cache set]
  return section;

Call 2 → GetHomeSectionAsync("trending", null):
  [No cache check — same as Call 1]
  switch("trending"):
    using (var scope = _scopeFactory.CreateScope())  ← Scope #2 created
    {
        var youtubeSvc = scope.ServiceProvider.GetRequiredService<IYoutubeService>();
        var trendingSongs = await youtubeSvc.GetTrendingMusicAsync(10, false);  ← DB/API call #2  ← BUG
        section.Songs = trendingSongs;
    }
  [No cache set]
  return section;

Total IYoutubeService.GetTrendingMusicAsync calls: 2  ← BUG (expected: 1)
```

---

## Expected Behavior (After Fix)

```
Call 1 → GetHomeSectionAsync("trending", null):
  cacheKey = "section_trending_shared"
  _cache.TryGetValue("section_trending_shared") → MISS
  [fetch from service, callCount = 1]
  _cache.Set("section_trending_shared", section, TTL=10min)
  return section;

Call 2 → GetHomeSectionAsync("trending", null):
  cacheKey = "section_trending_shared"
  _cache.TryGetValue("section_trending_shared") → HIT
  return cached;  ← No service call, no IServiceScope created

Total IYoutubeService.GetTrendingMusicAsync calls: 1  ← CORRECT
```

---

## Fix Location

**File**: `YoutubeMusicPlayer.Application/Services/HomeFacade.cs`
**Method**: `GetHomeSectionAsync`
**Line**: Before `switch (type.ToLower())` statement

**Fix**:
```csharp
// Add at the beginning of GetHomeSectionAsync, before switch statement:
bool isUserSpecific = !IsSharedSection(type) && userId.HasValue;
string cacheKey = isUserSpecific
    ? $"section_{type.ToLower()}_{userId!.Value}"
    : $"section_{type.ToLower()}_shared";

if (!refresh && _cache.TryGetValue(cacheKey, out MusicSection? cached) && cached != null)
{
    _logger.LogInformation("[HOME-FACADE] Cache HIT for section: {Type}", type);
    return cached;
}

// ... existing switch statement ...

// Add before final return section:
var ttl = IsSharedSection(type) ? TimeSpan.FromMinutes(10) : TimeSpan.FromMinutes(5);
_cache.Set(cacheKey, section, ttl);
return section;
```
