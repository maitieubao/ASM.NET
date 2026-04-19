/**
 * Ultra-Fast Playback Warmup Script V2 (Aggressive)
 * Handles pre-fetching stream URLs when user interactions or visibility change.
 */
(function() {
    const warmedVideoIds = new Set();
    let warmupTimeout = null;

    // Level 2: Intersection Observer for automatic warmup when elements become visible
    const observerOptions = {
        root: null, // use the viewport
        rootMargin: '0px',
        threshold: 0.1 // 10% of the element is visible
    };

    const warmupObserver = new IntersectionObserver((entries, observer) => {
        entries.forEach(entry => {
            if (entry.isIntersecting) {
                const videoId = entry.target.getAttribute('data-video-id');
                if (videoId && !warmedVideoIds.has(videoId)) {
                    // Small delay to prioritize visible items that stay visible
                    setTimeout(() => {
                        if (entry.isIntersecting) {
                            warmUpStream(videoId, entry.target);
                        }
                    }, 500);
                }
                // Once we attempt warmup, we can stop observing this specific element
                // to save resources, but we only stop if we succeeded or tried.
                // For now, let's keep observing until warmed.
            }
        });
    }, observerOptions);

    // Initial and dynamic observation
    function observeSongs() {
        const songElements = document.querySelectorAll('[data-video-id]');
        songElements.forEach(el => warmupObserver.observe(el));
    }

    // Traditional mouseenter listener for instant hover warming
    document.addEventListener('mouseenter', function(e) {
        const target = e.target.closest('[data-video-id]');
        if (!target) return;

        const videoId = target.getAttribute('data-video-id');
        if (!videoId || warmedVideoIds.has(videoId)) return;

        clearTimeout(warmupTimeout);
        warmupTimeout = setTimeout(() => {
            warmUpStream(videoId, target);
        }, 100); // Faster hover response (100ms)
    }, true);

    /**
     * Calls the warmup API to pre-cache the stream URL
     */
    async function warmUpStream(videoId, element) {
        if (warmedVideoIds.has(videoId)) return;
        
        console.log(`[WARMUP-V2] Pre-fetching visible/hovered: ${videoId}`);
        warmedVideoIds.add(videoId);

        try {
            const response = await fetch(`/Home/GetStreamWarmup?videoId=${videoId}`);
            if (response.ok) {
                console.log(`[WARMUP-V2] Ready: ${videoId}`);
            }
        } catch (error) {
            console.error(`[WARMUP-V2] Error: ${videoId}`, error);
            warmedVideoIds.delete(videoId); 
        }
    }

    // Watch for DOM changes (new sections loading via AJAX)
    const mutationObserver = new MutationObserver((mutations) => {
        observeSongs();
    });

    window.addEventListener('load', () => {
        observeSongs();
        mutationObserver.observe(document.body, { childList: true, subtree: true });
    });
})();

