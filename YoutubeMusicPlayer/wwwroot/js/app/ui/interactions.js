/**
 * interactions.js - Feedback and statistics logic
 */

window.togglePlayerLike = async function() {
    if (!window.currentSongDbId) return;
    const btn = document.getElementById('playerLikeBtn');
    if (!btn) return;
    const icon = btn.querySelector('i');
    
    try {
        const formData = new FormData();
        formData.append('songId', window.currentSongDbId);
        const res = await fetch('/Interaction/ToggleLike', { method: 'POST', body: formData });
        const json = await res.json();
        const data = json.data || json;
        if(json.success || json) {
            if(data.isLiked) {
                if (icon) icon.className = 'fa-solid fa-heart text-danger';
            } else {
                if (icon) icon.className = 'fa-regular fa-heart text-dim';
            }
        }
    } catch(e) { console.error('[Interactions] Error toggling like:', e); }
};

window.startTracking = function() {
    if (window.trackingInterval) clearInterval(window.trackingInterval);
    window.lastReportedTime = 0;
    window.trackingInterval = setInterval(() => {
        if (window.audioPlayer && !window.audioPlayer.paused && window.currentSongDbId) {
            const delta = window.audioPlayer.currentTime - window.lastReportedTime;
            if (delta >= 10) {
                reportProgress(delta);
                window.lastReportedTime = window.audioPlayer.currentTime;
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
