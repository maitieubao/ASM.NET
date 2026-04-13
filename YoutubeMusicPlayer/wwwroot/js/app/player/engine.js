/**
 * engine.js - Core playback engine (jQuery Refactored)
 */

window.streamCache = {};
window.isSongLoading = false;
window.consecutiveErrorCount = 0;

window.loadAndPlay = async function(track) {
    if (!track) return;
    
    // Stop current track
    if (window.audioPlayer) {
        window.audioPlayer.pause();
        if (typeof stopTracking === 'function') stopTracking();
    }
    
    if (typeof resetLyricsSync === 'function') resetLyricsSync();
    
    window.isSongLoading = true;
    if (typeof setPlayerMessage === 'function') setPlayerMessage(track.title, track.author || 'Đang tải...');
    
    audioPlayer.src = "";
    if (typeof updatePlayPauseUI === 'function') updatePlayPauseUI(false);
    window.currentSongDbId = null;
    
    $('#currentThumbnail').attr('src', track.thumbnail || 'https://ui-avatars.com/api/?name=Music&background=181818&color=6C5CE7');

    try {
        console.log("[Engine] Loading track:", track.videoId, track.title);
        if (typeof updateDynamicTheme === 'function') updateDynamicTheme(track.thumbnail);

        // Pre-update full player
        $('#fullPlayerTitle').text(track.title);
        $('#fullPlayerArtist').text(track.author || "Đang tải...");
        $('#fullPlayerThumb').attr('src', track.thumbnail);
        
        if (window.streamCache[track.videoId]) {
            audioPlayer.src = window.streamCache[track.videoId];
        } else {
            let url;
            if (track.videoId && track.videoId !== "undefined") {
                url = `/Home/GetStreamUrl?videoUrl=${encodeURIComponent('https://youtube.com/watch?v=' + track.videoId)}&title=${encodeURIComponent(track.title)}&artist=${encodeURIComponent(track.author)}`;
            } else {
                url = `/Home/GetStreamUrl?query=${encodeURIComponent(track.author + ' - ' + track.title)}&title=${encodeURIComponent(track.title)}&artist=${encodeURIComponent(track.author)}`;
            }
            
            const data = await $.getJSON(url);
            const result = data.success ? data.data : (data.Data || data);
            
            if (result && result.streamUrl) {
                if (result.videoId) track.videoId = result.videoId;
                window.streamCache[track.videoId] = result.streamUrl;
                window.currentSongDbId = result.songId || null; 
                audioPlayer.src = result.streamUrl;
            } else {
                throw new Error('No stream URL provided');
            }
        }

        const currentSpeed = localStorage.getItem('music-player-speed') || '1';
        audioPlayer.playbackRate = parseFloat(currentSpeed);
        audioPlayer.load();

        if (await window.safePlayCurrentTrack()) {
            window.isSongLoading = false;
            window.consecutiveErrorCount = 0;
            if (typeof setPlayerMessage === 'function') setPlayerMessage(track.title, track.author);
            if (typeof startTracking === 'function') startTracking();
            
            // Clean up the initiation flag after successful start
            track.isUserInitiated = false;

            // Background metadata enrichment
            if (typeof updateVisualsAndMetadata === 'function') {
                updateVisualsAndMetadata(track, { videoId: track.videoId });
            }
        }
    } catch (e) {
        console.error('[Engine] Failed to load:', e);
        window.isSongLoading = false;
        
        const errorMessage = e.message || 'Không rõ nguyên nhân';
        if (typeof setPlayerMessage === 'function') setPlayerMessage('Lỗi tải bài hát', track.title);
        if (typeof toastr !== 'undefined') toastr.error(`Không thể phát: ${track.title}. ${errorMessage}`);

        // If the user explicitly clicked this song, DO NOT automatically skip to next track.
        if (track.isUserInitiated) {
            console.log('[Engine] Aborting auto-skip because this was a user-initiated request.');
            track.isUserInitiated = false; // Reset
            return;
        }

        window.consecutiveErrorCount++;
        if (window.consecutiveErrorCount >= 5) {
            setPlayerMessage('Gặp lỗi khi phát nhạc (STOP)', 'Đã dừng tự động chuyển bài sau 5 lần thử.');
            if (typeof toastr !== 'undefined') {
                toastr.error("Quá nhiều lỗi liên tiếp. Đã dừng tự động chuyển bài để bảo vệ hệ thống.", "Lỗi Phát Nhạc", { timeOut: 0, extendedTimeOut: 0 });
            }
            window.consecutiveErrorCount = 0;
            return;
        }

        if (window.playQueue.length > 1 && window.currentIndex < window.playQueue.length - 1) {
            setTimeout(() => { if (typeof nextTrack === 'function') nextTrack(); }, 2500); 
        }
    }
};

window.safePlayCurrentTrack = async function() {
    try {
        await audioPlayer.play();
        if (typeof updatePlayPauseUI === 'function') updatePlayPauseUI(true);
        return true;
    } catch (e) {
        console.error('[Engine] safePlayCurrentTrack failed:', e);
        if (typeof updatePlayPauseUI === 'function') updatePlayPauseUI(false);
        return false;
    }
};
window.restorePlayerState = function() {
    const raw = localStorage.getItem('ytm-player-state');
    if (!raw) return;
    
    try {
        const state = JSON.parse(raw);
        if (!state.queue || state.queue.length === 0) return;
        
        console.log("[Engine] Restoring player state...");
        window.playQueue = state.queue;
        window.currentIndex = state.index || 0;
        window.isShuffle = state.isShuffle || false;
        window.repeatMode = state.repeatMode || 0;
        
        const track = window.playQueue[window.currentIndex];
        if (track) {
            // Restore UI but don't auto-play (browser restrictions)
            if (typeof setPlayerMessage === 'function') setPlayerMessage(track.title, track.author);
            if (typeof updateDynamicTheme === 'function') updateDynamicTheme(track.thumbnail);
            $('#currentThumbnail').attr('src', track.thumbnail);
            
            // Hydrate audio source so it's ready when user clicks play
            if (window.streamCache[track.videoId]) {
                audioPlayer.src = window.streamCache[track.videoId];
            } else {
                // We'll fetch the stream only when they hit play to avoid redundant network calls
            }
            
            if (typeof renderQueue === 'function') renderQueue();
            
            // Sync UI states
            $('#shuffleBtn').toggleClass('active', window.isShuffle).toggleClass('text-primary', window.isShuffle);
            // Repeat button UI requires a bit more logic but toggleRepeat handles it usually.
        }
    } catch (e) {
        console.error("[Engine] State restoration failed:", e);
    }
};
