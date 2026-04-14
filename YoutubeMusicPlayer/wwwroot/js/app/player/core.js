/**
 * core.js - Player initialization and event binding (jQuery Refactored)
 */

$(function() {
    console.log("[PlayerCore] Initializing with jQuery...");
    
    // Bind elements to window for global access
    window.$audioPlayer = $('#audioPlayer');
    window.audioPlayer = $audioPlayer[0]; // Keep raw reference for native audio API
    
    window.$playPauseBtn = $('#playPauseBtn');
    window.$progressSlider = $('#progressSlider');
    window.$currentTimeEl = $('#currentTime');
    window.$durationTimeEl = $('#durationTime');

    if (audioPlayer) {
        audioPlayer.volume = 0.8;
        console.log("[PlayerCore] Audio element found.");

        $audioPlayer.on('timeupdate', () => {
            const currentTime = audioPlayer.currentTime;
            
            // 1. Sync lyrics immediately
            if (typeof updateLyricsSync === 'function') {
                updateLyricsSync(currentTime);
            }

            // 2. Update Progress
            if (!window.isDraggingProgress && audioPlayer.duration) {
                const progress = (currentTime / audioPlayer.duration) * 100 || 0;
                $progressSlider.val(progress);
                
                if (typeof formatTime === 'function') {
                    $currentTimeEl.text(formatTime(currentTime));
                    $durationTimeEl.text(formatTime(audioPlayer.duration || 0));
                }
            }
        });

        $audioPlayer.on('ended', () => { 
            console.log("[PlayerCore] Track ended."); 
            if (typeof nextTrack === 'function') nextTrack(); 
        });
        
        $audioPlayer.on('pause', () => { if (typeof updatePlayPauseUI === 'function') updatePlayPauseUI(false); });
        $audioPlayer.on('play', () => { if (typeof updatePlayPauseUI === 'function') updatePlayPauseUI(true); });
        
        $audioPlayer.on('error', () => {
            if (window.isSongLoading) return;
            console.error("[PlayerCore] Audio element error:", audioPlayer.error);
            if (typeof updatePlayPauseUI === 'function') updatePlayPauseUI(false);
            if (typeof setPlayerMessage === 'function') {
                setPlayerMessage('Phát nhạc gặp lỗi', 'Nguồn âm thanh không hợp lệ hoặc đã hết hạn');
            }
        });
    }

    $progressSlider.on('input', () => { window.isDraggingProgress = true; });
    $progressSlider.on('change', () => {
        if (audioPlayer && audioPlayer.duration) {
            audioPlayer.currentTime = ($progressSlider.val() / 100) * audioPlayer.duration;
        }
        window.isDraggingProgress = false;
    });

    $('#volumeSlider').on('input', function() {
        if (audioPlayer) audioPlayer.volume = $(this).val();
    });

    $playPauseBtn.on('click', async () => {
        if (!audioPlayer.src && window.playQueue[window.currentIndex]) {
            if (typeof loadAndPlay === 'function') await loadAndPlay(window.playQueue[window.currentIndex]);
            return;
        }

        if (audioPlayer.paused) {
            if (typeof safePlayCurrentTrack === 'function') await safePlayCurrentTrack();
            // Sync play to other tabs
            window.syncChannel.postMessage({ type: 'play', track: window.playQueue[window.currentIndex] });
        } else {
            audioPlayer.pause();
            if (typeof updatePlayPauseUI === 'function') updatePlayPauseUI(false);
            // Sync pause to other tabs
            window.syncChannel.postMessage({ type: 'pause' });
        }
    });

    // Speed Control
    const savedSpeed = localStorage.getItem('music-player-speed') || '1';
    window.setPlaybackSpeed = function(speed, showToast = true) {
        if (!audioPlayer) return;
        const s = parseFloat(speed);
        audioPlayer.playbackRate = s;
        localStorage.setItem('music-player-speed', speed);
        $('#playbackSpeed').val(speed);
        if (showToast && typeof toastr !== 'undefined') {
            toastr.info(`Tốc độ phát: ${speed}x`);
        }
    };

    window.setPlaybackSpeed(savedSpeed, false);

    // Bridge for external views
    window.playerModule = {
        playDirect: (streamUrl, metadata) => {
            console.log("[PlayerBridge] playDirect called:", metadata.title);
            if (!audioPlayer) return;
            
            const track = {
                videoId: metadata.videoId || 'direct-stream',
                title: metadata.title,
                author: metadata.author,
                thumbnail: metadata.thumbnail
            };
            
            window.playQueue = [track];
            window.currentIndex = 0;
            
            audioPlayer.src = streamUrl;
            audioPlayer.load();
            
            if (typeof safePlayCurrentTrack === 'function') window.safePlayCurrentTrack();
            else audioPlayer.play();
            
            if (typeof updateVisualsAndMetadata === 'function') {
                window.updateVisualsAndMetadata(track, { streamUrl, videoId: track.videoId });
            }
    if (typeof renderQueue === 'function') renderQueue();
        }
    };

    // 4. Persistence Restore
    if (typeof restorePlayerState === 'function') {
        window.restorePlayerState();
    }

    // 5. Periodic State Sync & ANTI-THROTTLING (HEARTBEAT)
    setInterval(() => {
        if (window.audioPlayer && window.playQueue[window.currentIndex]) {
            // A. Anti-Throttling Logic: If it should be playing but is paused abnormally
            // We only do this if it's NOT a manual pause and NOT loading
            const isManualPause = $playPauseBtn.hasClass('fa-play'); // UI shows Play icon means it's paused by user
            
            if (!isManualPause && audioPlayer.paused && !window.isSongLoading) {
                console.log("[Heartbeat] Detected abnormal pause (likely tab throttling). Resuming...");
                if (typeof safePlayCurrentTrack === 'function') {
                    window.safePlayCurrentTrack().catch(e => console.warn("[StayAwake] Auto-resume failed:", e));
                } else {
                    audioPlayer.play().catch(() => {});
                }
            }

            // B. State Persistence
            if (!audioPlayer.paused) {
                const raw = localStorage.getItem('ytm-player-state');
                if (raw) {
                    const state = JSON.parse(raw);
                    state.currentTime = window.audioPlayer.currentTime;
                    localStorage.setItem('ytm-player-state', JSON.stringify(state));
                    
                    // Also broadcast for other tabs
                    window.syncChannel.postMessage({ type: 'sync_time', time: audioPlayer.currentTime });
                }
            }
        }
    }, 3000);

    // 6. Cross-Tab Synchronization (BroadcastChannel)
    window.syncChannel = new BroadcastChannel('ytm-player-sync');
    
    // REMOVED auto-sync on audio events to prevent browser tab-switching pause issues
    
    window.syncChannel.onmessage = (event) => {
        const { type, time, track } = event.data;
        console.log("[Sync] received:", type);

        if (type === 'play') {
            if (audioPlayer.paused) {
                if (track && (!window.playQueue[window.currentIndex] || window.playQueue[window.currentIndex].videoId !== track.videoId)) {
                    // Update state to match sender if different
                     if (typeof restorePlayerState === 'function') window.restorePlayerState();
                }
                audioPlayer.play().catch(() => {});
            }
        } else if (type === 'pause') {
            if (!audioPlayer.paused) audioPlayer.pause();
        } else if (type === 'sync_time') {
            if (Math.abs(audioPlayer.currentTime - time) > 5) {
                audioPlayer.currentTime = time;
            }
        }
    };
});
