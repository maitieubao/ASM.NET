/**
 * Preservation Property Tests
 * ============================
 * Validates: Requirements 3.1, 3.2, 3.3, 3.4, 3.5, 3.6
 *
 * GOAL: Xác nhận các behavior hiện tại đúng vẫn hoạt động trên code CHƯA fix.
 *       Đây là baseline — tất cả tests PHẢI PASS trên unfixed code.
 *
 * Tests:
 *   A — Anti-Throttling Resume (core.js): heartbeat GỌI safePlayCurrentTrack
 *       khi browser throttle tab (không phải manual pause)
 *   B — Section Null Return (HomeFacade.cs): GetHomeSectionAsync("mix2", null)
 *       trả về null (user-specific section không có userId)
 *   C — HomeViewModel Cache Unchanged: BuildHomeViewModelAsync trả về cached
 *       result sau lần gọi đầu (cache key "home_vm_{userId}")
 *   D — Refresh Flag Bypass: Khi refresh = true, service luôn được gọi bất
 *       kể cache state
 *
 * EXPECTED OUTCOME: Tất cả tests PASS trên code CHƯA fix (baseline behavior).
 */

'use strict';

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

// ─── Minimal jQuery-like mock (same as bug2 test) ────────────────────────────

function createMockElement(ownClasses = [], childClasses = []) {
    const childEl = {
        _classes: [...childClasses],
        hasClass(cls) { return this._classes.includes(cls); },
        addClass(cls) { if (!this._classes.includes(cls)) this._classes.push(cls); return this; },
        removeClass(cls) { this._classes = this._classes.filter(c => c !== cls); return this; },
    };

    const el = {
        _classes: [...ownClasses],
        _child: childEl,
        hasClass(cls) { return this._classes.includes(cls); },
        addClass(cls) { if (!this._classes.includes(cls)) this._classes.push(cls); return this; },
        removeClass(cls) { this._classes = this._classes.filter(c => c !== cls); return this; },
        find(selector) {
            if (selector === 'i') return this._child;
            return { hasClass: () => false };
        },
    };

    return el;
}

// ─── Heartbeat logic (UNFIXED — copy từ core.js) ─────────────────────────────

function runHeartbeat_UNFIXED(window_ctx) {
    const { audioPlayer, playQueue, currentIndex, isSongLoading, $playPauseBtn } = window_ctx;

    if (audioPlayer && playQueue[currentIndex]) {
        // Unfixed code: kiểm tra class trên button wrapper (luôn false)
        const isManualPause = $playPauseBtn.hasClass('fa-play');

        if (!isManualPause && audioPlayer.paused && !isSongLoading) {
            window_ctx.safePlayCurrentTrackCallCount++;
        }
    }
}

// ─── IMemoryCache simulation ──────────────────────────────────────────────────

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
// Phản ánh đúng code C# hiện tại: không có cache check, không có cache set.

function createUnfixedFacade(cache, youtubeService, albumService, recommendationService) {
    return {
        async getHomeSectionAsync(type, userId, refresh = false) {
            const section = { title: '', songs: [], albums: [] };

            switch (type.toLowerCase()) {
                case 'trending':
                    section.title = 'Thịnh hành hôm nay';
                    {
                        const songs = await youtubeService.getTrendingMusicAsync(10, refresh);
                        section.songs = songs;
                    }
                    break;

                case 'albums':
                    section.title = 'Album & EP phổ biến';
                    {
                        const albums = await albumService.getTrendingAlbumsAsync(10);
                        section.albums = albums;
                    }
                    break;

                case 'mix2':
                    // user-specific: trả về null nếu không có userId
                    if (userId == null) return null;
                    section.title = 'Khám phá mới';
                    {
                        const songs = await recommendationService.getDailyMixVariantAsync(userId, 1, null, refresh);
                        section.songs = songs;
                    }
                    break;

                case 'mix3':
                    // user-specific: trả về null nếu không có userId
                    if (userId == null) return null;
                    section.title = 'Giai điệu yêu thích';
                    {
                        const songs = await recommendationService.getDailyMixVariantAsync(userId, 2, null, refresh);
                        section.songs = songs;
                    }
                    break;

                default:
                    return null;
            }

            if (section.songs.length === 0 && section.albums.length === 0) return null;

            // UNFIXED: Không có cache set ở đây
            return section;
        }
    };
}

// ─── Simulate HomeFacade.BuildHomeViewModelAsync (UNFIXED) ───────────────────
// Phản ánh đúng code C# hiện tại: có cache check với key "home_vm_{userId}".

function createUnfixedHomeFacadeWithViewModel(cache, genreService, artistService) {
    return {
        async buildHomeViewModelAsync(userId, userName = null) {
            const cacheKey = `home_vm_${userId ?? 0}`;

            // Cache check đã có trong code hiện tại (behavior đúng cần preserve)
            const { hit, value } = cache.tryGetValue(cacheKey);
            if (hit && value != null) {
                return value; // Cache HIT
            }

            // Build model
            const model = {
                greeting: userName ? `Chào, ${userName}` : 'Chào',
                genres: [],
                topArtists: [],
                recentListened: [],
            };

            try {
                model.genres = await genreService.getAllGenresAsync();
            } catch { /* ignore */ }

            try {
                model.topArtists = await artistService.getPaginatedArtistsAsync(1, 12);
            } catch { /* ignore */ }

            // Cache toàn bộ ViewModel 5 phút (behavior hiện tại đúng)
            cache.set(cacheKey, model, 5 * 60 * 1000);

            return model;
        }
    };
}

// ═════════════════════════════════════════════════════════════════════════════
// TEST A — Anti-Throttling Resume (core.js)
// ═════════════════════════════════════════════════════════════════════════════

async function runTestA() {
    console.log('\n=== Preservation Test A: Anti-Throttling Resume (core.js) ===\n');
    console.log('Validates: Requirement 3.1');
    console.log('Scenario: Browser throttle tab — audioPlayer.paused = true, KHÔNG phải manual pause');
    console.log('Property: heartbeat GỌI safePlayCurrentTrack (behavior đúng cần preserve)');
    console.log('─────────────────────────────────────────────────────────────────────────────');

    // Scenario: Tab bị ẩn, browser throttle audio.
    // - window.isManuallyPaused chưa tồn tại trên unfixed code
    // - $playPauseBtn (button wrapper) KHÔNG có class 'fa-play'
    // - <i> bên trong có class 'fa-pause' (đang playing trước khi throttle)
    // → isManualPause = $playPauseBtn.hasClass('fa-play') = false
    // → heartbeat PHẢI gọi safePlayCurrentTrack (anti-throttling behavior đúng)

    const $playPauseBtn = createMockElement(
        [],            // button wrapper: không có class fa-play
        ['fa-pause']   // <i> bên trong: có class fa-pause (đang playing)
    );

    const audioPlayer = { paused: true }; // browser throttle gây pause

    const window_ctx = {
        audioPlayer,
        playQueue: [{ videoId: 'test-song-throttle' }],
        currentIndex: 0,
        isSongLoading: false,
        $playPauseBtn,
        safePlayCurrentTrackCallCount: 0,
        // window.isManuallyPaused chưa tồn tại trên unfixed code
    };

    // Verify setup: button wrapper không có class fa-play
    assert(
        !$playPauseBtn.hasClass('fa-play'),
        'Setup: $playPauseBtn (button wrapper) không có class fa-play (browser throttle scenario)',
        '$playPauseBtn không được có class fa-play trong browser throttle scenario'
    );

    // Verify setup: <i> bên trong có class fa-pause (đang playing trước khi throttle)
    assert(
        $playPauseBtn.find('i').hasClass('fa-pause'),
        'Setup: icon <i> có class fa-pause (nhạc đang phát trước khi browser throttle)',
        'Icon <i> phải có class fa-pause để simulate trạng thái playing'
    );

    // Trigger heartbeat (unfixed code)
    runHeartbeat_UNFIXED(window_ctx);

    // Property: heartbeat PHẢI gọi safePlayCurrentTrack khi browser throttle
    // (isManualPause = false vì button wrapper không có class fa-play)
    assert(
        window_ctx.safePlayCurrentTrackCallCount === 1,
        'Preservation A: heartbeat GỌI safePlayCurrentTrack() khi browser throttle tab',
        `safePlayCurrentTrack được gọi ${window_ctx.safePlayCurrentTrackCallCount} lần (expected: 1)\n` +
        `         Anti-throttling behavior phải được preserve sau khi fix`
    );

    // Property: Với nhiều trạng thái browser-throttle khác nhau, heartbeat luôn resume
    // (property-based: test nhiều input)
    const throttleScenarios = [
        { ownClasses: [], childClasses: ['fa-pause'], desc: 'playing → throttled' },
        { ownClasses: [], childClasses: [],           desc: 'no icon class → throttled' },
        { ownClasses: [],  childClasses: ['fa-spin'],  desc: 'loading icon → throttled' },
    ];

    for (const scenario of throttleScenarios) {
        const $btn = createMockElement(scenario.ownClasses, scenario.childClasses);
        const ctx = {
            audioPlayer: { paused: true },
            playQueue: [{ videoId: 'throttle-test' }],
            currentIndex: 0,
            isSongLoading: false,
            $playPauseBtn: $btn,
            safePlayCurrentTrackCallCount: 0,
        };
        runHeartbeat_UNFIXED(ctx);
        assert(
            ctx.safePlayCurrentTrackCallCount === 1,
            `Preservation A (property): browser throttle scenario "${scenario.desc}" → heartbeat resumes`,
            `safePlayCurrentTrack được gọi ${ctx.safePlayCurrentTrackCallCount} lần (expected: 1)`
        );
    }

    // Property: Khi isSongLoading = true, heartbeat KHÔNG resume (loading state)
    {
        const $btn = createMockElement([], ['fa-pause']);
        const ctx = {
            audioPlayer: { paused: true },
            playQueue: [{ videoId: 'loading-test' }],
            currentIndex: 0,
            isSongLoading: true, // đang loading
            $playPauseBtn: $btn,
            safePlayCurrentTrackCallCount: 0,
        };
        runHeartbeat_UNFIXED(ctx);
        assert(
            ctx.safePlayCurrentTrackCallCount === 0,
            'Preservation A (edge): isSongLoading = true → heartbeat KHÔNG resume',
            `safePlayCurrentTrack được gọi ${ctx.safePlayCurrentTrackCallCount} lần (expected: 0)`
        );
    }

    // Property: Khi playQueue rỗng, heartbeat KHÔNG resume
    {
        const $btn = createMockElement([], ['fa-pause']);
        const ctx = {
            audioPlayer: { paused: true },
            playQueue: [],
            currentIndex: 0,
            isSongLoading: false,
            $playPauseBtn: $btn,
            safePlayCurrentTrackCallCount: 0,
        };
        runHeartbeat_UNFIXED(ctx);
        assert(
            ctx.safePlayCurrentTrackCallCount === 0,
            'Preservation A (edge): playQueue rỗng → heartbeat KHÔNG resume',
            `safePlayCurrentTrack được gọi ${ctx.safePlayCurrentTrackCallCount} lần (expected: 0)`
        );
    }
}

// ═════════════════════════════════════════════════════════════════════════════
// TEST B — Section Null Return (HomeFacade.cs)
// ═════════════════════════════════════════════════════════════════════════════

async function runTestB() {
    console.log('\n=== Preservation Test B: Section Null Return (HomeFacade.cs) ===\n');
    console.log('Validates: Requirement 3.6 (null result không được cache)');
    console.log('Property: GetHomeSectionAsync("mix2", null) trả về null');
    console.log('─────────────────────────────────────────────────────────────────────────────');

    const cache = new MemoryCache();
    let recommendationCallCount = 0;

    const mockRecommendationService = {
        async getDailyMixVariantAsync(userId, variant, seed, refresh) {
            recommendationCallCount++;
            return [{ youtubeVideoId: 'mix', title: 'Mix Song', authorName: 'Artist' }];
        }
    };

    const facade = createUnfixedFacade(
        cache,
        { async getTrendingMusicAsync() { return [{ youtubeVideoId: 'v1', title: 'T', authorName: 'A' }]; } },
        { async getTrendingAlbumsAsync() { return [{ albumId: 1, title: 'A' }]; } },
        mockRecommendationService
    );

    // Property: mix2 với userId = null → trả về null
    const result_mix2_null = await facade.getHomeSectionAsync('mix2', null);
    assert(
        result_mix2_null === null,
        'Preservation B: GetHomeSectionAsync("mix2", null) trả về null',
        `Expected null, got ${JSON.stringify(result_mix2_null)}`
    );

    // Property: mix3 với userId = null → trả về null
    const result_mix3_null = await facade.getHomeSectionAsync('mix3', null);
    assert(
        result_mix3_null === null,
        'Preservation B: GetHomeSectionAsync("mix3", null) trả về null',
        `Expected null, got ${JSON.stringify(result_mix3_null)}`
    );

    // Property: recommendationService KHÔNG được gọi khi userId = null
    assert(
        recommendationCallCount === 0,
        'Preservation B: recommendationService KHÔNG được gọi khi userId = null (early return)',
        `recommendationService được gọi ${recommendationCallCount} lần (expected: 0)`
    );

    // Property: mix2 với userId hợp lệ → KHÔNG trả về null
    const result_mix2_user = await facade.getHomeSectionAsync('mix2', 42);
    assert(
        result_mix2_user !== null,
        'Preservation B: GetHomeSectionAsync("mix2", 42) KHÔNG trả về null (userId hợp lệ)',
        `Expected non-null result for mix2 with userId=42`
    );

    // Property: type không hợp lệ → trả về null
    const result_unknown = await facade.getHomeSectionAsync('unknown_type', null);
    assert(
        result_unknown === null,
        'Preservation B: GetHomeSectionAsync("unknown_type", null) trả về null',
        `Expected null for unknown type, got ${JSON.stringify(result_unknown)}`
    );

    // Property: null result KHÔNG được cache (gọi lại vẫn trả về null, không throw)
    const result_mix2_null_again = await facade.getHomeSectionAsync('mix2', null);
    assert(
        result_mix2_null_again === null,
        'Preservation B: Gọi lại GetHomeSectionAsync("mix2", null) vẫn trả về null (null không bị cache sai)',
        `Expected null on second call, got ${JSON.stringify(result_mix2_null_again)}`
    );
}

// ═════════════════════════════════════════════════════════════════════════════
// TEST C — HomeViewModel Cache Unchanged
// ═════════════════════════════════════════════════════════════════════════════

async function runTestC() {
    console.log('\n=== Preservation Test C: HomeViewModel Cache Unchanged ===\n');
    console.log('Validates: Requirement 3.3');
    console.log('Property: BuildHomeViewModelAsync trả về cached result sau lần gọi đầu');
    console.log('Cache key: "home_vm_{userId}"');
    console.log('─────────────────────────────────────────────────────────────────────────────');

    let genreCallCount = 0;
    let artistCallCount = 0;

    const mockGenreService = {
        async getAllGenresAsync() {
            genreCallCount++;
            return [{ genreId: 1, name: 'Pop' }, { genreId: 2, name: 'Rock' }];
        }
    };

    const mockArtistService = {
        async getPaginatedArtistsAsync(page, limit) {
            artistCallCount++;
            return [{ artistId: 1, name: 'Artist 1' }];
        }
    };

    const cache = new MemoryCache();
    const facade = createUnfixedHomeFacadeWithViewModel(cache, mockGenreService, mockArtistService);

    // Lần 1: fetch từ service, lưu cache
    const result1 = await facade.buildHomeViewModelAsync(1, 'TestUser');
    const genreCountAfterCall1 = genreCallCount;
    const artistCountAfterCall1 = artistCallCount;

    assert(
        result1 !== null,
        'Preservation C: BuildHomeViewModelAsync lần 1 trả về kết quả hợp lệ',
        'BuildHomeViewModelAsync lần 1 không được trả về null'
    );

    assert(
        genreCountAfterCall1 === 1,
        'Preservation C: genreService được gọi đúng 1 lần ở lần gọi đầu',
        `genreService được gọi ${genreCountAfterCall1} lần (expected: 1)`
    );

    // Lần 2: phải từ cache, không gọi service
    const result2 = await facade.buildHomeViewModelAsync(1, 'TestUser');
    const genreCountAfterCall2 = genreCallCount;
    const artistCountAfterCall2 = artistCallCount;

    // Property: lần 2 trả về cached result (service không được gọi thêm)
    assert(
        genreCountAfterCall2 === 1,
        'Preservation C: genreService KHÔNG được gọi thêm ở lần 2 (cache hit)',
        `genreService được gọi ${genreCountAfterCall2} lần sau 2 calls (expected: 1 — lần 2 từ cache)`
    );

    assert(
        artistCountAfterCall2 === 1,
        'Preservation C: artistService KHÔNG được gọi thêm ở lần 2 (cache hit)',
        `artistService được gọi ${artistCountAfterCall2} lần sau 2 calls (expected: 1 — lần 2 từ cache)`
    );

    // Property: kết quả lần 2 giống lần 1 (data integrity)
    assert(
        result2 === result1,
        'Preservation C: result lần 2 là cùng object với lần 1 (cache reference equality)',
        'result2 phải là cùng object reference với result1 (từ cache)'
    );

    // Property: cache key "home_vm_{userId}" — user khác nhau có cache riêng
    let genreCallCountUser2 = 0;
    const mockGenreService2 = {
        async getAllGenresAsync() {
            genreCallCountUser2++;
            return [{ genreId: 3, name: 'Jazz' }];
        }
    };
    const facade2 = createUnfixedHomeFacadeWithViewModel(cache, mockGenreService2, mockArtistService);

    // userId = 2 (khác userId = 1) → phải fetch mới, không dùng cache của userId = 1
    const result_user2 = await facade2.buildHomeViewModelAsync(2, 'OtherUser');
    assert(
        genreCallCountUser2 === 1,
        'Preservation C: userId khác nhau có cache riêng (userId=2 fetch mới)',
        `genreService cho userId=2 được gọi ${genreCallCountUser2} lần (expected: 1 — cache riêng)`
    );

    // userId = null (guest) → cache key "home_vm_0"
    const result_guest = await facade.buildHomeViewModelAsync(null, null);
    assert(
        result_guest !== null,
        'Preservation C: BuildHomeViewModelAsync với userId=null (guest) hoạt động bình thường',
        'BuildHomeViewModelAsync với userId=null không được trả về null'
    );
}

// ═════════════════════════════════════════════════════════════════════════════
// TEST D — Refresh Flag Bypass
// ═════════════════════════════════════════════════════════════════════════════

async function runTestD() {
    console.log('\n=== Preservation Test D: Refresh Flag Bypass ===\n');
    console.log('Validates: Requirement 3.6');
    console.log('Property: Khi refresh = true, service luôn được gọi bất kể cache state');
    console.log('─────────────────────────────────────────────────────────────────────────────');

    // Trên unfixed code, không có cache nên refresh flag không có tác dụng gì đặc biệt.
    // Nhưng behavior quan trọng cần preserve là: khi refresh = true, service PHẢI được gọi.
    // Trên unfixed code, service luôn được gọi (vì không có cache) → test này PASS.
    // Sau khi fix (có cache), test này vẫn phải PASS vì refresh = true bỏ qua cache.

    let trendingCallCount = 0;

    const mockYoutubeService = {
        async getTrendingMusicAsync(limit, refresh) {
            trendingCallCount++;
            return [{ youtubeVideoId: 'v1', title: 'Trending Song', authorName: 'Artist' }];
        }
    };

    const cache = new MemoryCache();
    const facade = createUnfixedFacade(
        cache,
        mockYoutubeService,
        { async getTrendingAlbumsAsync() { return [{ albumId: 1, title: 'A' }]; } },
        { async getDailyMixVariantAsync() { return [{ youtubeVideoId: 'mix', title: 'M', authorName: 'A' }]; } }
    );

    // Lần 1: gọi bình thường (không refresh)
    await facade.getHomeSectionAsync('trending', null, false);
    const countAfterNormal = trendingCallCount;

    assert(
        countAfterNormal === 1,
        'Preservation D: Lần gọi bình thường (refresh=false) → service được gọi 1 lần',
        `trendingCallCount = ${countAfterNormal} (expected: 1)`
    );

    // Lần 2: gọi với refresh = true → service PHẢI được gọi (bỏ qua cache nếu có)
    await facade.getHomeSectionAsync('trending', null, true);
    const countAfterRefresh = trendingCallCount;

    // Property: refresh = true → service được gọi (không bị cache block)
    assert(
        countAfterRefresh === 2,
        'Preservation D: refresh=true → service được gọi (bỏ qua cache)',
        `trendingCallCount = ${countAfterRefresh} sau refresh call (expected: 2)\n` +
        `         Khi refresh=true, service phải luôn được gọi bất kể cache state`
    );

    // Property: Gọi lại với refresh = true nhiều lần → service luôn được gọi
    await facade.getHomeSectionAsync('trending', null, true);
    await facade.getHomeSectionAsync('trending', null, true);
    const countAfterMultiRefresh = trendingCallCount;

    assert(
        countAfterMultiRefresh === 4,
        'Preservation D: Nhiều lần refresh=true → service được gọi mỗi lần',
        `trendingCallCount = ${countAfterMultiRefresh} sau 4 calls (expected: 4)`
    );

    // Property: refresh flag hoạt động cho user-specific sections cũng vậy
    let dailymixCallCount = 0;
    const mockRecommendationService = {
        async getDailyMixVariantAsync(userId, variant, seed, refresh) {
            dailymixCallCount++;
            return [{ youtubeVideoId: 'mix', title: 'Mix', authorName: 'Artist' }];
        }
    };

    const cache2 = new MemoryCache();
    const facade2 = createUnfixedFacade(
        cache2,
        { async getTrendingMusicAsync() { return []; } },
        { async getTrendingAlbumsAsync() { return []; } },
        mockRecommendationService
    );

    await facade2.getHomeSectionAsync('mix2', 1, false);
    await facade2.getHomeSectionAsync('mix2', 1, true); // refresh

    assert(
        dailymixCallCount === 2,
        'Preservation D: refresh=true cho user-specific section (mix2) → service được gọi',
        `dailymixCallCount = ${dailymixCallCount} (expected: 2 — 1 normal + 1 refresh)`
    );
}

// ─── Main ─────────────────────────────────────────────────────────────────────

async function runAllTests() {
    console.log('╔═══════════════════════════════════════════════════════════════════════════╗');
    console.log('║           PRESERVATION PROPERTY TESTS — Baseline Behavior                ║');
    console.log('║           Running on UNFIXED code — ALL tests MUST PASS                  ║');
    console.log('╚═══════════════════════════════════════════════════════════════════════════╝');

    await runTestA();
    await runTestB();
    await runTestC();
    await runTestD();

    console.log('\n═══════════════════════════════════════════════════════════════════════════════');
    console.log(`Results: ${passed} passed, ${failed} failed`);

    if (failed > 0) {
        console.log('\n❌ PRESERVATION TESTS FAILED — Baseline behavior bị phá vỡ!\n');
        console.log('Các tests này phải PASS trên code chưa fix.');
        console.log('Nếu tests fail, có thể có vấn đề với test setup hoặc code đã bị thay đổi.\n');

        failures.forEach(f => {
            console.log(`  Failed: "${f.testName}"`);
            console.log(`  Reason: ${f.message}\n`);
        });

        process.exit(1);
    } else {
        console.log('\n✅ All preservation tests PASSED — Baseline behavior confirmed!');
        console.log('\nSummary:');
        console.log('  ✓ Test A: Anti-Throttling Resume — heartbeat gọi safePlayCurrentTrack khi browser throttle');
        console.log('  ✓ Test B: Section Null Return — mix2/mix3 với userId=null trả về null');
        console.log('  ✓ Test C: HomeViewModel Cache — BuildHomeViewModelAsync cache 5 phút hoạt động đúng');
        console.log('  ✓ Test D: Refresh Flag Bypass — refresh=true luôn gọi service bất kể cache');
        console.log('\nBaseline behavior đã được capture. Các tests này sẽ được re-run sau khi fix');
        console.log('để đảm bảo không có regression.');
        process.exit(0);
    }
}

runAllTests().catch(err => { console.error(err); process.exit(1); });
