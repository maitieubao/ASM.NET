/**
 * Bug 2 — Stop Button Exploration Test
 * =====================================
 * Validates: Requirements 1.4, 1.5, 1.6
 *
 * GOAL: Surface counterexample chứng minh heartbeat gọi safePlayCurrentTrack()
 *       dù user đã manual pause.
 *
 * EXPECTED OUTCOME: Test FAILS trên code chưa fix.
 * Failure xác nhận bug tồn tại:
 *   $playPauseBtn.hasClass('fa-play') = false (selector sai — class nằm trên <i>
 *   bên trong, không phải button wrapper) → isManualPause = false → heartbeat
 *   gọi safePlayCurrentTrack() dù user đã pause.
 *
 * Counterexample:
 *   - audioPlayer.paused = true
 *   - isSongLoading = false
 *   - playQueue = [{ videoId: 'test' }]
 *   - User đã click Pause: icon <i> bên trong #playPauseBtn có class 'fa-play'
 *   - Trigger: heartbeat callback chạy
 *   - Observed (buggy): $playPauseBtn.hasClass('fa-play') = false
 *                       → isManualPause = false
 *                       → safePlayCurrentTrack() ĐƯỢC gọi  ← BUG
 *   - Expected (correct): safePlayCurrentTrack() KHÔNG được gọi
 */

'use strict';

// ─── Minimal jQuery-like mock ────────────────────────────────────────────────

/**
 * Tạo một jQuery-like element mock.
 * @param {string[]} ownClasses   - Classes trên chính element này (button wrapper)
 * @param {string[]} childClasses - Classes trên thẻ <i> bên trong
 */
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

        hasClass(cls) {
            // jQuery .hasClass() chỉ kiểm tra class của chính element này,
            // KHÔNG traverse children — đây là root cause của bug.
            return this._classes.includes(cls);
        },
        addClass(cls) { if (!this._classes.includes(cls)) this._classes.push(cls); return this; },
        removeClass(cls) { this._classes = this._classes.filter(c => c !== cls); return this; },

        // .find('i') trả về child element (thẻ <i> bên trong)
        find(selector) {
            if (selector === 'i') return this._child;
            return { hasClass: () => false };
        },
    };

    return el;
}

// ─── Simulate updatePlayPauseUI(false) ───────────────────────────────────────
// Trong ui.js, updatePlayPauseUI(false) đổi icon <i> thành 'fa-play'
// (button wrapper #playPauseBtn KHÔNG nhận class này)
function simulateUserClickPause($playPauseBtn, audioPlayer) {
    audioPlayer.paused = true;
    // Chỉ icon <i> bên trong nhận class fa-play — button wrapper không nhận
    $playPauseBtn.find('i').addClass('fa-play');
    $playPauseBtn.find('i').removeClass('fa-pause');
    // Button wrapper KHÔNG có class fa-play
}

// ─── Heartbeat logic (copy từ core.js — CHƯA FIX) ───────────────────────────
/**
 * Đây là heartbeat logic NGUYÊN BẢN từ core.js (chưa fix).
 * Dòng bug: const isManualPause = $playPauseBtn.hasClass('fa-play');
 * → Kiểm tra class trên button wrapper, nhưng class thực tế nằm trên <i> bên trong.
 */
function runHeartbeat_UNFIXED(window_ctx) {
    const { audioPlayer, playQueue, currentIndex, isSongLoading, $playPauseBtn } = window_ctx;

    if (audioPlayer && playQueue[currentIndex]) {
        // ← BUG: hasClass kiểm tra button wrapper, không phải <i> bên trong
        const isManualPause = $playPauseBtn.hasClass('fa-play');

        if (!isManualPause && audioPlayer.paused && !isSongLoading) {
            // Heartbeat gọi safePlayCurrentTrack — đây là hành vi SAI khi user đã pause
            window_ctx.safePlayCurrentTrackCallCount++;
        }
    }
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

// ─── Test Case 1: Bug Condition — Manual Pause Not Resumed ───────────────────
console.log('\n=== Bug 2 Exploration Test: Stop Button ===\n');
console.log('Test Case 1: Manual Pause — heartbeat KHÔNG được gọi safePlayCurrentTrack');
console.log('─────────────────────────────────────────────────────────────────────────');

{
    // Setup: Trạng thái sau khi user click Pause
    // - #playPauseBtn là button wrapper (không có class fa-play)
    // - <i> bên trong có class fa-play (set bởi updatePlayPauseUI(false))
    const $playPauseBtn = createMockElement(
        [],           // button wrapper: KHÔNG có class fa-play
        ['fa-play']   // <i> bên trong: CÓ class fa-play
    );

    const audioPlayer = { paused: true };

    const window_ctx = {
        audioPlayer,
        playQueue: [{ videoId: 'test-song-001' }],
        currentIndex: 0,
        isSongLoading: false,
        $playPauseBtn,
        safePlayCurrentTrackCallCount: 0,
    };

    // Simulate user click Pause (icon đã được set bởi updatePlayPauseUI)
    simulateUserClickPause($playPauseBtn, audioPlayer);

    // Verify setup: icon <i> có class fa-play
    assert(
        $playPauseBtn.find('i').hasClass('fa-play'),
        'Setup: icon <i> có class fa-play sau khi user click Pause',
        'Icon <i> phải có class fa-play để simulate trạng thái paused'
    );

    // Verify bug condition: button wrapper KHÔNG có class fa-play
    assert(
        !$playPauseBtn.hasClass('fa-play'),
        'Bug Condition: $playPauseBtn.hasClass("fa-play") = false (selector sai)',
        '$playPauseBtn (button wrapper) không có class fa-play — class nằm trên <i> bên trong'
    );

    // Trigger heartbeat (unfixed code)
    runHeartbeat_UNFIXED(window_ctx);

    // Assert: safePlayCurrentTrack KHÔNG được gọi (expected behavior)
    // → Trên code CHƯA FIX, assertion này SẼ FAIL vì heartbeat gọi safePlayCurrentTrack
    assert(
        window_ctx.safePlayCurrentTrackCallCount === 0,
        'Heartbeat KHÔNG gọi safePlayCurrentTrack() khi user đã manual pause',
        `COUNTEREXAMPLE FOUND: safePlayCurrentTrack() được gọi ${window_ctx.safePlayCurrentTrackCallCount} lần ` +
        `dù user đã pause!\n` +
        `         Root cause: $playPauseBtn.hasClass('fa-play') = false (selector sai)\n` +
        `         → isManualPause = false → heartbeat resume nhạc dù user đã pause`
    );

    // Assert: audioPlayer vẫn paused
    assert(
        audioPlayer.paused === true,
        'audioPlayer.paused vẫn là true sau heartbeat',
        `audioPlayer.paused = ${audioPlayer.paused} (expected: true)`
    );
}

// ─── Test Case 2: Verify Bug Root Cause ──────────────────────────────────────
console.log('\nTest Case 2: Xác nhận root cause — class nằm trên <i>, không phải button wrapper');
console.log('─────────────────────────────────────────────────────────────────────────────────');

{
    const $playPauseBtn = createMockElement([], ['fa-play']);

    // Selector sai (unfixed): kiểm tra button wrapper
    const wrongCheck = $playPauseBtn.hasClass('fa-play');
    // Selector đúng (fixed): kiểm tra <i> bên trong
    const correctCheck = $playPauseBtn.find('i').hasClass('fa-play');

    assert(
        wrongCheck === false,
        'Selector sai: $playPauseBtn.hasClass("fa-play") = false (button wrapper không có class)',
        `Expected false, got ${wrongCheck}`
    );

    assert(
        correctCheck === true,
        'Selector đúng: $playPauseBtn.find("i").hasClass("fa-play") = true (class nằm trên <i>)',
        `Expected true, got ${correctCheck}`
    );

    assert(
        wrongCheck !== correctCheck,
        'Bug confirmed: selector sai cho kết quả ngược với selector đúng',
        `wrongCheck (${wrongCheck}) phải khác correctCheck (${correctCheck})`
    );
}

// ─── Test Case 3: Preservation — Browser Throttle Vẫn Resume ─────────────────
console.log('\nTest Case 3: Preservation — Browser throttle (không phải manual pause) vẫn resume');
console.log('──────────────────────────────────────────────────────────────────────────────────');

{
    // Scenario: Tab bị ẩn, browser throttle audio (pause không phải từ user)
    // → button wrapper KHÔNG có class fa-play, <i> cũng KHÔNG có class fa-play
    const $playPauseBtn = createMockElement(
        [],           // button wrapper: không có class fa-play
        ['fa-pause']  // <i> bên trong: có class fa-pause (đang playing trước khi throttle)
    );

    const audioPlayer = { paused: true }; // browser throttle gây pause

    const window_ctx = {
        audioPlayer,
        playQueue: [{ videoId: 'test-song-002' }],
        currentIndex: 0,
        isSongLoading: false,
        $playPauseBtn,
        safePlayCurrentTrackCallCount: 0,
    };

    runHeartbeat_UNFIXED(window_ctx);

    // Trong browser throttle scenario, heartbeat PHẢI gọi safePlayCurrentTrack (behavior đúng)
    assert(
        window_ctx.safePlayCurrentTrackCallCount === 1,
        'Preservation: heartbeat GỌI safePlayCurrentTrack() khi browser throttle (không phải manual pause)',
        `safePlayCurrentTrack được gọi ${window_ctx.safePlayCurrentTrackCallCount} lần (expected: 1)`
    );
}

// ─── Summary ─────────────────────────────────────────────────────────────────
console.log('\n═══════════════════════════════════════════════════════════════════════════════');
console.log(`Results: ${passed} passed, ${failed} failed`);

if (failed > 0) {
    console.log('\n⚠️  TEST FAILURES DETECTED — Bug condition confirmed!\n');
    console.log('Counterexample documented:');
    console.log('  Setup:');
    console.log('    - audioPlayer.paused = true');
    console.log('    - isSongLoading = false');
    console.log('    - playQueue = [{ videoId: "test-song-001" }]');
    console.log('    - User đã click Pause: icon <i> bên trong #playPauseBtn có class "fa-play"');
    console.log('    - Button wrapper #playPauseBtn KHÔNG có class "fa-play"');
    console.log('  Trigger: heartbeat callback chạy (setInterval 3s)');
    console.log('  Observed (buggy):');
    console.log('    - $playPauseBtn.hasClass("fa-play") = false  ← selector sai');
    console.log('    - isManualPause = false                      ← sai');
    console.log('    - safePlayCurrentTrack() ĐƯỢC gọi           ← BUG: nhạc tự resume');
    console.log('  Expected (correct):');
    console.log('    - isManualPause = true');
    console.log('    - safePlayCurrentTrack() KHÔNG được gọi');
    console.log('  Root cause:');
    console.log('    - core.js dòng: const isManualPause = $playPauseBtn.hasClass("fa-play")');
    console.log('    - jQuery .hasClass() chỉ kiểm tra class của chính element,');
    console.log('      không traverse children');
    console.log('    - Class "fa-play" nằm trên <i> bên trong, không phải button wrapper');
    console.log('  Fix:');
    console.log('    - Thay bằng: const isManualPause = window.isManuallyPaused;');
    console.log('    - Set window.isManuallyPaused = true khi user click Pause');
    console.log('    - Set window.isManuallyPaused = false khi user click Play');

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
