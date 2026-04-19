/**
 * interactions.js - Feedback and statistics logic
 */

window.togglePlayerLike = async function() {
    if (!window.currentSongDbId) return;
    
    try {
        const formData = new FormData();
        formData.append('songId', window.currentSongDbId);
        const res = await fetch('/Interaction/ToggleLike', { method: 'POST', body: formData });
        const json = await res.json();
        
        // Use the new sync function to update both buttons at once
        if (json.success || json) {
            const data = json.data || json;
            window.updateLikeUI(data.isLiked);
        }
    } catch(e) { console.error('[Interactions] Error toggling like:', e); }
};

// Global function to sync all heart icons across the UI
window.updateLikeUI = function(isLiked) {
    const btns = ['playerLikeBtn', 'fullPlayerLikeBtn'];
    btns.forEach(id => {
        const btn = document.getElementById(id);
        if (!btn) return;
        const icon = btn.querySelector('i');
        if (!icon) return;
        
        if (isLiked) {
            icon.className = 'fa-solid fa-heart text-danger';
        } else {
            // Check if it's the outline button or the link button to keep aesthetics
            if (id === 'fullPlayerLikeBtn') {
                icon.className = 'fa-regular fa-heart me-2';
            } else {
                icon.className = 'fa-regular fa-heart text-dim';
            }
        }
    });
};

window.hasCountedView = false;

window.startTracking = function() {
    if (window.trackingInterval) clearInterval(window.trackingInterval);
    window.lastReportedTime = 0;
    window.hasCountedView = false; // Reset for new song session

    window.trackingInterval = setInterval(() => {
        if (window.audioPlayer && !window.audioPlayer.paused && window.currentSongDbId) {
            const currentTime = window.audioPlayer.currentTime;
            const duration = window.audioPlayer.duration;

            // 1. Accumulate Listening Stats (Every 10s Delta)
            const delta = currentTime - window.lastReportedTime;
            if (delta >= 10) {
                reportProgress(delta);
                window.lastReportedTime = currentTime;
            }

            // 2. YouTube-Style View Counting (30s or 50% for short videos)
            if (!window.hasCountedView && duration > 0) {
                const isLongEnough = currentTime >= 30;
                const isHalfwayThroughShortTrack = (duration < 30 && currentTime >= (duration / 2));

                if (isLongEnough || isHalfwayThroughShortTrack) {
                    recordValidView();
                }
            }
        }
    }, 5000);
};

async function reportProgress(seconds) {
    if (!window.currentSongDbId) return;
    try {
        const formData = new FormData();
        formData.append('songId', window.currentSongDbId);
        formData.append('durationSeconds', seconds);
        await fetch('/Interaction/UpdateListeningStats', { method: 'POST', body: formData });
    } catch (e) {}
}

async function recordValidView() {
    if (!window.currentSongDbId || window.hasCountedView) return;
    window.hasCountedView = true; // Optimistic lock
    
    try {
        console.log("[Interactions] Valid view criteria met. Recording...");
        const formData = new FormData();
        formData.append('songId', window.currentSongDbId);
        await fetch('/Interaction/RecordView', { method: 'POST', body: formData });
    } catch (e) {
        window.hasCountedView = false; // Allow retry on failure
    }
}
