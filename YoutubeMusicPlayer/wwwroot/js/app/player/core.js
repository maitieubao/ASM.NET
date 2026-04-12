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
        } else {
            audioPlayer.pause();
            if (typeof updatePlayPauseUI === 'function') updatePlayPauseUI(false);
        }
    });

    // Speed Control
    const savedSpeed = localStorage.getItem('music-player-speed') || '1';
    if (typeof setPlaybackSpeed === 'function') {
        window.setPlaybackSpeed(savedSpeed, false);
    }
    $('#playbackSpeed').val(savedSpeed);

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
});
