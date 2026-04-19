/**
 * Bug 1 — Section Cache Exploration Test
 * ========================================
 * Validates: Requirements 1.1, 1.2, 1.3
 *
 * GOAL: Surface counterexample chứng minh GetHomeSectionAsync không cache kết quả
 *       section — service được gọi 2 lần thay vì 1 lần cho cùng request.
 *
 * EXPECTED OUTCOME: Test FAILS trên code chưa fix.
 * Failure xác nhận bug tồn tại:
 *   GetHomeSectionAsync không có cache check ở đầu method → mỗi request đều
 *   tạo IServiceScope mới và gọi service → IYoutubeService.GetTrendingMusicAsync
 *   được gọi 2 lần thay vì 1 lần.
 *
 * Counterexample:
 *   - Gọi GetHomeSectionAsync("trending", null) lần 1 → service được gọi 1 lần
 *   - Gọi GetHomeSectionAsync("trending", null) lần 2 → service được gọi thêm 1 lần
 *   - Observed (buggy): tổng callCount = 2
 *   - Expected (correct): tổng callCount = 1 (lần 2 từ cache)
 *   - Root cause: không có _cache.TryGetValue() check trước switch statement
 */

'use strict';

// ─── Simulate IMemoryCache (in-process cache) ─────────────────────────────────

class MemoryCache {
    constructor() {
        this._store = new Map();
    }

    tryGetValue(key) {
        const entry = this._store.get(key);
        if (!entry) return { hit: false, value: null };
        if (entry.expiresAt && Date.now() > entry.expiresAt) {
            this._store.delete(key);
            return { hit: false, value: null };
        }
        return { hit: true, value: entry.value };
    }

    set(key, value, ttlMs) {
        this._store.set(key, {
            value,
            expiresAt: ttlMs ? Date.now() + ttlMs : null,
        });
    }
}

// ─── Simulate HomeFacade.GetHomeSectionAsync (UNFIXED) ───────────────────────
//
// Logic này phản ánh đúng code C# hiện tại trong HomeFacade.cs:
// - Không có cache check ở đầu method
// - Không có cache set ở cuối method
// - Mỗi lần gọi đều tạo scope mới và gọi service

function createUnfixedFacade(cache, youtubeService, albumService, recommendationService) {
    return {
        async getHomeSectionAsync(type, userId, refresh = false) {
            const section = { title: '', songs: [], albums: [] };

            switch (type.toLowerCase()) {
                case 'trending':
                    section.title = 'Thịnh hành hôm nay';
                    // Simulate: using (var scope = _scopeFactory.CreateScope())
                    {
                        // Simulate: var youtubeSvc = scope.ServiceProvider.GetRequiredService<IYoutubeService>()
                        const trendingSongs = await youtubeService.getTrendingMusicAsync(10, refresh);
                        section.songs = trendingSongs;
                    }
                    // BUG: Không có cache set sau khi fetch
                    break;

                case 'albums':
                    section.title = 'Album & EP phổ biến';
                    {
                        const albums = await albumService.getTrendingAlbumsAsync(10);
                        section.albums = albums;
                    }
                    // BUG: Không có cache set
                    break;

                case 'dailymix':
                    section.title = 'Hỗn hợp dành cho bạn';
                    {
                        const songs = userId != null
                            ? await recommendationService.getDailyMixVariantAsync(userId, 0, null, refresh)
                            : await youtubeService.getTrendingMusicAsync(10, refresh);
                        section.songs = songs;
                    }
                    // BUG: Không có cache set
                    break;

                default:
                    return null;
            }

            if (section.songs.length === 0 && section.albums.length === 0) return null;

            // BUG: Không có _cache.Set() ở đây — mỗi request đều query lại từ đầu
            return section;
        }
    };
}

// ─── Simulate HomeFacade.GetHomeSectionAsync (FIXED) ─────────────────────────
//
// Logic này phản ánh code C# SAU KHI FIX theo design.md:
// - Có cache check ở đầu method
// - Có cache set ở cuối method
// - Lần 2 trả về từ cache, không gọi service

function createFixedFacade(cache, youtubeService, albumService, recommendationService) {
    function isSharedSection(type) {
        return ['trending', 'albums', 'focus', 'chill', 'sad', 'compilations'].includes(type.toLowerCase());
    }

    return {
        async getHomeSectionAsync(type, userId, refresh = false) {
            // FIX: Cache check ở đầu method
            const isUserSpecific = !isSharedSection(type) && userId != null;
            const cacheKey = isUserSpecific
                ? `section_${type.toLowerCase()}_${userId}`
                : `section_${type.toLowerCase()}_shared`;

            if (!refresh) {
                const { hit, value } = cache.tryGetValue(cacheKey);
                if (hit && value != null) {
                    return value; // Cache HIT — không gọi service
                }
            }

            const section = { title: '', songs: [], albums: [] };

            switch (type.toLowerCase()) {
                case 'trending':
                    section.title = 'Thịnh hành hôm nay';
                    {
                        const trendingSongs = await youtubeService.getTrendingMusicAsync(10, refresh);
                        section.songs = trendingSongs;
                    }
                    break;

                case 'albums':
                    section.title = 'Album & EP phổ biến';
                    {
                        const albums = await albumService.getTrendingAlbumsAsync(10);
                        section.albums = albums;
                    }
                    break;

                case 'dailymix':
                    section.title = 'Hỗn hợp dành cho bạn';
                    {
                        const songs = userId != null
                            ? await recommendationService.getDailyMixVariantAsync(userId, 0, null, refresh)
                            : await youtubeService.getTrendingMusicAsync(10, refresh);
                        section.songs = songs;
                    }
                    break;

                default:
                    return null;
            }

            if (section.songs.length === 0 && section.albums.length === 0) return null;

            // FIX: Cache set ở cuối method
            const ttlMs = isSharedSection(type) ? 10 * 60 * 1000 : 5 * 60 * 1000;
            cache.set(cacheKey, section, ttlMs);

            return section;
        }
    };
}

// ─── Test runner ─────────────────────────────────────────────────────────────

let passed = 0;
let failed = 0;
const failures = [];

function assert(condition, testName, message) {
    if (condition) {
        console.log(`  ✓ PASS: ${testName}`);
        passed++;
    } else {
        console.error(`  ✗ FAIL: ${testName}`);
        console.error(`         ${message}`);
        failed++;
        failures.push({ testName, message });
    }
}

// ─── Test Suite: UNFIXED code (expected to FAIL) ──────────────────────────────

async function runTests() {

console.log('\n=== Bug 1 Exploration Test: Section Cache ===\n');
console.log('Running on UNFIXED code — tests expected to FAIL\n');

// ─── Test Case 1: Trending Double Call ───────────────────────────────────────
console.log('Test Case 1: GetHomeSectionAsync("trending", null) — service KHÔNG được gọi 2 lần');
console.log('────────────────────────────────────────────────────────────────────────────────');

await (async () => {
    let trendingCallCount = 0;

    const mockYoutubeService = {
        async getTrendingMusicAsync(limit, refresh) {
            trendingCallCount++;
            return [
                { youtubeVideoId: 'vid1', title: 'Song 1', authorName: 'Artist 1' },
                { youtubeVideoId: 'vid2', title: 'Song 2', authorName: 'Artist 2' },
            ];
        }
    };

    const cache = new MemoryCache();
    const facade = createUnfixedFacade(
        cache,
        mockYoutubeService,
        { async getTrendingAlbumsAsync() { return []; } },
        { async getDailyMixVariantAsync() { return []; } }
    );

    // Lần 1
    const result1 = await facade.getHomeSectionAsync('trending', null);
    const countAfterCall1 = trendingCallCount;

    // Lần 2 (ngay sau đó — trong TTL nếu có cache)
    const result2 = await facade.getHomeSectionAsync('trending', null);
    const countAfterCall2 = trendingCallCount;

    assert(
        countAfterCall1 === 1,
        'Lần 1: IYoutubeService.GetTrendingMusicAsync được gọi đúng 1 lần',
        `Expected callCount = 1 sau lần gọi đầu, got ${countAfterCall1}`
    );

    // Assert: Tổng số lần gọi service = 1 (lần 2 phải từ cache)
    // → Trên code CHƯA FIX, assertion này SẼ FAIL vì callCount = 2
    assert(
        countAfterCall2 === 1,
        'Lần 2: IYoutubeService.GetTrendingMusicAsync KHÔNG được gọi thêm (cache hit)',
        `COUNTEREXAMPLE FOUND: IYoutubeService.GetTrendingMusicAsync được gọi ${countAfterCall2} lần ` +
        `(expected: 1)\n` +
        `         Root cause: GetHomeSectionAsync không có _cache.TryGetValue() check\n` +
        `         → Mỗi request đều tạo IServiceScope mới và gọi service`
    );

    assert(
        result1 !== null && result2 !== null,
        'Cả hai lần gọi đều trả về kết quả hợp lệ',
        'Một trong hai lần gọi trả về null'
    );
})();

// ─── Test Case 2: Albums Double Call ─────────────────────────────────────────
console.log('\nTest Case 2: GetHomeSectionAsync("albums", null) — service KHÔNG được gọi 2 lần');
console.log('──────────────────────────────────────────────────────────────────────────────');

await (async () => {
    let albumCallCount = 0;

    const mockAlbumService = {
        async getTrendingAlbumsAsync(limit) {
            albumCallCount++;
            return [
                { albumId: 1, title: 'Album 1', coverImageUrl: 'http://example.com/1.jpg' },
            ];
        }
    };

    const cache = new MemoryCache();
    const facade = createUnfixedFacade(
        cache,
        { async getTrendingMusicAsync() { return []; } },
        mockAlbumService,
        { async getDailyMixVariantAsync() { return []; } }
    );

    await facade.getHomeSectionAsync('albums', null);
    await facade.getHomeSectionAsync('albums', null);

    // Assert: Tổng số lần gọi service = 1
    // → Trên code CHƯA FIX, assertion này SẼ FAIL vì albumCallCount = 2
    assert(
        albumCallCount === 1,
        'IAlbumService.GetTrendingAlbumsAsync KHÔNG được gọi 2 lần',
        `COUNTEREXAMPLE FOUND: IAlbumService.GetTrendingAlbumsAsync được gọi ${albumCallCount} lần ` +
        `(expected: 1)\n` +
        `         Root cause: không có cache check ở đầu GetHomeSectionAsync`
    );
})();

// ─── Test Case 3: DailyMix User-Specific Double Call ─────────────────────────
console.log('\nTest Case 3: GetHomeSectionAsync("dailymix", userId=1) — service KHÔNG được gọi 2 lần');
console.log('────────────────────────────────────────────────────────────────────────────────');

await (async () => {
    let dailymixCallCount = 0;

    const mockRecommendationService = {
        async getDailyMixVariantAsync(userId, variant, seed, refresh) {
            dailymixCallCount++;
            return [
                { youtubeVideoId: 'mix1', title: 'Mix Song 1', authorName: 'Artist 1' },
            ];
        }
    };

    const cache = new MemoryCache();
    const facade = createUnfixedFacade(
        cache,
        { async getTrendingMusicAsync() { return []; } },
        { async getTrendingAlbumsAsync() { return []; } },
        mockRecommendationService
    );

    await facade.getHomeSectionAsync('dailymix', 1);
    await facade.getHomeSectionAsync('dailymix', 1);

    // Assert: Tổng số lần gọi service = 1
    // → Trên code CHƯA FIX, assertion này SẼ FAIL vì dailymixCallCount = 2
    assert(
        dailymixCallCount === 1,
        'IRecommendationService.GetDailyMixVariantAsync KHÔNG được gọi 2 lần (userId=1)',
        `COUNTEREXAMPLE FOUND: IRecommendationService.GetDailyMixVariantAsync được gọi ${dailymixCallCount} lần ` +
        `(expected: 1)\n` +
        `         Root cause: không có cache check ở đầu GetHomeSectionAsync`
    );
})();

// ─── Summary ─────────────────────────────────────────────────────────────────
console.log('\n═══════════════════════════════════════════════════════════════════════════════');
console.log(`Results: ${passed} passed, ${failed} failed`);

if (failed > 0) {
    console.log('\n⚠️  TEST FAILURES DETECTED — Bug condition confirmed!\n');
    console.log('Counterexample documented:');
    console.log('  Setup:');
    console.log('    - IYoutubeService.GetTrendingMusicAsync là mock, đếm số lần gọi');
    console.log('    - IMemoryCache là real MemoryCache (không mock)');
    console.log('  Execution:');
    console.log('    - Gọi GetHomeSectionAsync("trending", null) lần 1');
    console.log('      → IYoutubeService.GetTrendingMusicAsync được gọi (callCount = 1)');
    console.log('    - Gọi GetHomeSectionAsync("trending", null) lần 2');
    console.log('      → IYoutubeService.GetTrendingMusicAsync được gọi LẠI (callCount = 2)  ← BUG');
    console.log('  Observed (buggy):');
    console.log('    - callCount = 2 cho cả hai lần gọi');
    console.log('    - Mỗi lần gọi đều tạo IServiceScope mới và query service');
    console.log('  Expected (correct):');
    console.log('    - callCount = 1 (lần 2 từ cache, không gọi service)');
    console.log('  Root cause:');
    console.log('    - HomeFacade.cs: GetHomeSectionAsync đi thẳng vào switch statement');
    console.log('      mà không kiểm tra _cache trước');
    console.log('    - IMemoryCache đã được inject (_cache) và dùng ở BuildHomeViewModelAsync,');
    console.log('      GetSongsByArtistAsync, GetDiscoverySongsAsync — nhưng bị bỏ sót ở');
    console.log('      GetHomeSectionAsync');
    console.log('  Fix:');
    console.log('    - Thêm cache check ở đầu GetHomeSectionAsync (trước switch statement)');
    console.log('    - Thêm cache set ở cuối GetHomeSectionAsync (trước return section)');
    console.log('    - Shared sections (Trending, Albums, ...): TTL 10 phút');
    console.log('    - User-specific sections (DailyMix, Mix1, ...): TTL 5 phút');

    failures.forEach(f => {
        console.log(`\n  Failed: "${f.testName}"`);
        console.log(`  Reason: ${f.message}`);
    });

    // Exit with non-zero code to signal test failure
    process.exit(1);
} else {
    console.log('\n✅ All tests passed.');
    process.exit(0);
}

} // end runTests

runTests().catch(err => { console.error(err); process.exit(1); });
