/**
 * sync.js - Logic for real-time lyrics synchronization (jQuery Refactored)
 */

let lastActiveIndex = -1;
let lastUserScrollTime = 0;
const SYNC_OFFSET = 0.200; // Anticipate next line for perceived snappy response

window.updateLyricsSync = function(currentTime) {
    const $lyContainer = $('#lyricsContent');
    if (!$lyContainer.length || !$lyContainer.hasClass('is-synchronized')) return;

    const $lines = $lyContainer.find('.lyrics-line');
    if ($lines.length === 0) return;

    // Apply sync offset
    const adjustedTime = currentTime + SYNC_OFFSET;

    let activeIndex = -1;
    let low = 0;
    let high = $lines.length - 1;

    // Binary Search matching
    while (low <= high) {
        let mid = Math.floor((low + high) / 2);
        const start = parseFloat($lines.eq(mid).data('start') || "0");
        
        if (adjustedTime >= start) {
            activeIndex = mid;
            low = mid + 1;
        } else {
            high = mid - 1;
        }
    }

    if (activeIndex !== -1) {
        if (activeIndex !== lastActiveIndex) {
            $lines.removeClass('active');
            const $activeLine = $lines.eq(activeIndex).addClass('active');
            $lyContainer.addClass('has-active-line');
            
            // Native smooth scroll - prioritized for mobile and desktop smoothness
            if (Date.now() - lastUserScrollTime > 3500) {
                const activeEl = $activeLine[0];
                if (activeEl) {
                    activeEl.scrollIntoView({ 
                        behavior: 'smooth', 
                        block: 'center' 
                    });
                }
            }
            lastActiveIndex = activeIndex;
        }
    } else if (lastActiveIndex !== -1) {
        $lines.removeClass('active');
        $lyContainer.removeClass('has-active-line');
        lastActiveIndex = -1;
        
        const $container = $('.lyrics-container');
        if ($container.length && (Date.now() - lastUserScrollTime > 3000)) {
            $container.scrollTop(0);
        }
    }
};

window.resetLyricsSync = function() {
    console.log("[Sync] Resetting lyrics state...");
    lastActiveIndex = -1;
    const $lyContainer = $('#lyricsContent');
    if ($lyContainer.length) {
        $lyContainer.removeClass('has-active-line').find('.lyrics-line').removeClass('active');
        const $container = $lyContainer.closest('.lyrics-container');
        if ($container.length) $container.scrollTop(0);
    }
};

$(function() {
    const $lyContainer = $('#lyricsContent');
    const $container = $lyContainer.closest('.lyrics-container');
    
    if ($container.length) {
        $container.on('mousedown wheel touchstart', () => { 
            lastUserScrollTime = Date.now(); 
        });
    }
});
