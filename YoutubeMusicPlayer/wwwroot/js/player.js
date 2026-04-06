/**
 * player.js - Core playback and audio management
 */

// --- GLOBAL PLAYER STATE ---
let playQueue = [];
let currentIndex = -1;
let isShuffle = false;
let repeatMode = 0; // 0: None, 1: One, 2: All
let adSequenceActive = false;
let streamCache = {};
let currentSongDbId = null;
let lastReportedTime = 0;
let trackingInterval = null;
let isDraggingProgress = false;
let isSongLoading = false; 
let consecutiveErrorCount = 0; // Prevent infinite skipping loops

// Elements
let audioPlayer, playPauseBtn, progressSlider, currentTimeEl, durationTimeEl;

document.addEventListener('DOMContentLoaded', () => {
    console.log("[Player] Initializing...");
    audioPlayer = document.getElementById('audioPlayer');
    playPauseBtn = document.getElementById('playPauseBtn');
    progressSlider = document.getElementById('progressSlider');
    currentTimeEl = document.getElementById('currentTime');
    durationTimeEl = document.getElementById('durationTime');

    if (audioPlayer) {
        audioPlayer.volume = 0.8;
        console.log("[Player] Audio element found.");

        // Progress Slider updates
        audioPlayer.ontimeupdate = () => {
            if (!isDraggingProgress && audioPlayer.duration) {
                const progress = (audioPlayer.currentTime / audioPlayer.duration) * 100 || 0;
                if (progressSlider) progressSlider.value = progress;
                if (currentTimeEl) currentTimeEl.textContent = formatTime(audioPlayer.currentTime);
                if (durationTimeEl) durationTimeEl.textContent = formatTime(audioPlayer.duration || 0);
            }
        };

        audioPlayer.onended = () => { console.log("[Player] Track ended."); nextTrack(); };
        audioPlayer.onpause = () => updatePlayPauseUI(false);
        audioPlayer.onplay = () => updatePlayPauseUI(true);
        audioPlayer.onerror = (e) => {
            if (isSongLoading) {
                console.warn("[Player] Transition error ignored during loading.");
                return;
            }
            console.error("[Player] Audio element error:", audioPlayer.error);
            updatePlayPauseUI(false);
            setPlayerMessage('Phát nhạc gặp lỗi', 'Nguồn âm thanh không hợp lệ hoặc đã hết hạn');
        };
    } else {
        console.error("[Player] CRITICAL: Audio element NOT found!");
    }

    if (progressSlider) {
        progressSlider.oninput = () => { isDraggingProgress = true; };
        progressSlider.onchange = () => {
            if (audioPlayer && audioPlayer.duration) {
                audioPlayer.currentTime = (progressSlider.value / 100) * audioPlayer.duration;
            }
            isDraggingProgress = false;
        };
    }

    // Volume Slider
    const volumeSlider = document.getElementById('volumeSlider');
    if (volumeSlider) {
        volumeSlider.oninput = (e) => {
            if (audioPlayer) audioPlayer.volume = e.target.value;
        };
    }

    // Initialize global click handler for Play/Pause button
    const ppBtn = document.getElementById('playPauseBtn');
    if (ppBtn) {
        ppBtn.onclick = async () => {
            console.log("[Player] Play/Pause clicked.");
            if (!audioPlayer.src && playQueue[currentIndex]) {
                await loadAndPlay(playQueue[currentIndex]);
                return;
            }

            if (audioPlayer.paused) {
                await safePlayCurrentTrack();
            } else {
                audioPlayer.pause();
                updatePlayPauseUI(false);
            }
        };
    }
});

// --- CORE FUNCTIONS ---

window.updatePlayPauseUI = function(isPlaying) {
    if (!playPauseBtn) return;
    playPauseBtn.className = isPlaying ? 'fa-solid fa-circle-pause play-main' : 'fa-solid fa-circle-play play-main';
}

window.setPlayerMessage = function(title, author) {
    const titleEl = document.getElementById('currentTitle');
    const authorEl = document.getElementById('currentAuthor');
    if (titleEl) titleEl.textContent = title || 'Unknown Title';
    if (authorEl) authorEl.textContent = author || 'Unknown Artist';
}

window.updateDynamicTheme = function(thumbUrl) {
    if (!thumbUrl) return;
    const dynamicBG = document.getElementById('dynamicBG');
    if (dynamicBG) {
        dynamicBG.style.backgroundImage = `linear-gradient(to bottom, rgba(0,0,0,0.2), #0f0f0f), url(${thumbUrl})`;
        dynamicBG.style.backgroundSize = 'cover';
        dynamicBG.style.backgroundPosition = 'center';
        dynamicBG.style.filter = 'blur(100px) brightness(0.4)';
        dynamicBG.style.opacity = '0.5';
    }
}

window.safePlayCurrentTrack = async function() {
    if (!audioPlayer) return false;
    try {
        await audioPlayer.play();
        updatePlayPauseUI(true);
        return true;
    } catch (error) {
        console.error('Audio playback failed.', error);
        updatePlayPauseUI(false);
        setPlayerMessage('Không thể phát nhạc', 'Thử lại hoặc chọn một bài hát khác');
        return false;
    }
}

window.loadAndPlay = async function(track) {
    if (!track || !audioPlayer) return;
    
    // UI Feedback: Loading state
    isSongLoading = true;
    setPlayerMessage(track.title, 'Đang tải...');
    audioPlayer.src = "";
    updatePlayPauseUI(false);
    currentSongDbId = null;
    
    const thumbEl = document.getElementById('currentThumbnail');
    if (thumbEl) thumbEl.src = track.thumbnail || 'https://ui-avatars.com/api/?name=Music&background=181818&color=6C5CE7';

    try {
        console.log("[Player] Loading track:", track.videoId, track.title);
        if (typeof updateDynamicTheme === 'function') {
            updateDynamicTheme(track.thumbnail);
        }
        
        // Check cache first
        if (streamCache[track.videoId]) {
            console.log("[Player] Using cached stream.");
            audioPlayer.src = streamCache[track.videoId];
            const hasStarted = await safePlayCurrentTrack();
            if (hasStarted) {
                isSongLoading = false;
                consecutiveErrorCount = 0; // Success! Reset the counter
                setPlayerMessage(track.title, track.author);
            }
            return;
        }

        // Fetch new stream
        const url = `/Home/GetStreamUrl?videoUrl=${encodeURIComponent('https://youtube.com/watch?v=' + track.videoId)}&title=${encodeURIComponent(track.title)}&artist=${encodeURIComponent(track.author)}`;
        console.log("[Player] Fetching stream from:", url);
        const res = await fetch(url);
        if (!res.ok) throw new Error('Network error');
        
        const data = await res.json();
        if (data.streamUrl) {
            streamCache[track.videoId] = data.streamUrl;
            currentSongDbId = data.songId || null; 
            
            if (data.showAd && typeof playAdSequence === 'function') {
                await playAdSequence();
            }

            audioPlayer.src = data.streamUrl;
            
            const likeBtn = document.getElementById('playerLikeBtn');
            if(likeBtn) {
                if(data.isLiked) {
                    likeBtn.querySelector('i').className = 'fa-solid fa-heart text-danger';
                } else {
                    likeBtn.querySelector('i').className = 'fa-regular fa-heart text-dim';
                }
            }

            const hasStarted = await safePlayCurrentTrack();
            if (hasStarted) {
                isSongLoading = false;
                consecutiveErrorCount = 0; // Success! Reset the counter
                setPlayerMessage(track.title, track.author);
                startTracking();
                if (typeof fetchRichMetadata === 'function') {
                    fetchRichMetadata(track.videoId); 
                }
            }
        } else {
            throw new Error('No stream URL provided');
        }
    } catch (e) {
        console.error('[Player] Failed to load:', e);
        isSongLoading = false;
        consecutiveErrorCount++;
        
        if (consecutiveErrorCount >= 5) {
            console.error('[Player] Critical: Multiple failures. Stopping playback.');
            setPlayerMessage('Gặp lỗi khi phát nhạc', 'Đã dừng tự động chuyển bài sau nhiều lần thử thất bại.');
            updatePlayPauseUI(false);
            consecutiveErrorCount = 0; // Reset for next manual attempt
            return;
        }

        setPlayerMessage('Lỗi tải bài hát', 'Đang thử bài tiếp theo...');
        updatePlayPauseUI(false);
        if (playQueue.length > 1 && currentIndex < playQueue.length - 1) {
            setTimeout(nextTrack, 1500); 
        }
    }
}

window.playSingleTrack = function(videoId, title, author, thumbnail) {
    const track = { videoId, title, author: author, thumbnail };
    
    if (playQueue.length === 0) {
        playQueue = [track];
        currentIndex = 0;
    } else {
        const idx = playQueue.findIndex(t => t.videoId === videoId);
        if (idx === -1) {
            playQueue.splice(currentIndex + 1, 0, track);
            currentIndex = currentIndex + 1;
        } else {
            currentIndex = idx;
        }
    }
    
    loadAndPlay(track);
    if (typeof renderQueue === 'function') renderQueue();
    
    // Auto-inject more from same artist
    if (author && author !== 'Nghệ sĩ') {
        injectArtistSongs(author, videoId);
    }
}

window.injectArtistSongs = async function(artistName, currentVideoId) {
    if (!artistName) return;
    try {
        const res = await fetch(`/Home/GetSongsByArtist?name=${encodeURIComponent(artistName)}`);
        const data = await res.json();
        
        if (data && data.length > 0) {
            const existingIds = new Set(playQueue.map(q => q.videoId));
            const existingTitles = new Set(playQueue.map(q => q.title.toLowerCase().trim()));
            
            const newTracks = [];
            for (const item of data) {
                const vId = item.youtubeVideoId;
                const title = item.title;
                const author = item.authorName;
                const thumb = item.thumbnailUrl;
                
                if (vId === currentVideoId) continue;
                if (existingIds.has(vId)) continue;
                if (existingTitles.has(title.toLowerCase().trim())) continue;
                
                newTracks.push({
                    videoId: vId,
                    title: title,
                    author: author,
                    thumbnail: thumb
                });
                
                if (newTracks.length >= 10) break;
            }
            
            if (newTracks.length > 0) {
                // Insert after current index
                playQueue.splice(currentIndex + 1, 0, ...newTracks);
                if (typeof renderQueue === 'function') renderQueue();
            }
        }
    } catch (e) {
        console.error('Error injecting artist songs:', e);
    }
}

window.playPlaylist = async function(id) {
    try {
        const res = await fetch(`/Playlist/GetPlaylistSongs?id=${id}`);
        const songs = await res.json();
        if (songs && songs.length > 0) {
            playQueue = songs.map(s => ({
                videoId: s.youtubeVideoId,
                title: s.title,
                author: s.authorName,
                thumbnail: s.thumbnailUrl
            }));
            currentIndex = 0;
            loadAndPlay(playQueue[0]);
            if (typeof renderQueue === 'function') renderQueue();
        } else {
            alert("Playlist này chưa có bài hát nào!");
        }
    } catch(e) { console.error("Error playing playlist:", e); }
}

window.playArtistAlbum = async function(artistId) {
    try {
        const res = await fetch(`/Home/GetArtistSongs?artistId=${artistId}`);
        const songs = await res.json();
        if (songs && songs.length > 0) {
            playQueue = songs.map(s => ({
                videoId: s.youtubeVideoId,
                title: s.title,
                author: s.authorName,
                thumbnail: s.thumbnailUrl
            }));
            currentIndex = 0;
            loadAndPlay(playQueue[0]);
            if (typeof renderQueue === 'function') renderQueue();
        }
    } catch(e) { console.error("Error playing album:", e); }
}

window.nextTrack = function() {
    if (playQueue.length === 0) return;
    if (repeatMode === 1 && audioPlayer) { audioPlayer.currentTime = 0; safePlayCurrentTrack(); return; }
    
    currentIndex = isShuffle ? Math.floor(Math.random() * playQueue.length) : currentIndex + 1;
    
    if (currentIndex < playQueue.length) {
        loadAndPlay(playQueue[currentIndex]);
        if (typeof checkAndRefillQueue === 'function') checkAndRefillQueue();
    }
    else if (repeatMode === 2) { 
        currentIndex = 0; 
        loadAndPlay(playQueue[currentIndex]); 
    }
    else { 
        updatePlayPauseUI(false); 
    }
}

window.prevTrack = function() {
    if (currentIndex > 0) {
        currentIndex--;
        loadAndPlay(playQueue[currentIndex]);
    }
}

window.toggleShuffle = function() {
    isShuffle = !isShuffle;
    const btn = document.getElementById('shuffleBtn');
    if (btn) btn.classList.toggle('active', isShuffle);
}

window.toggleRepeat = function() {
    repeatMode = (repeatMode + 1) % 3;
    const btn = document.getElementById('repeatBtn');
    if (btn) {
        btn.className = repeatMode === 1 ? 'fa-solid fa-repeat-1 text-primary' : 'fa-solid fa-repeat';
        if (repeatMode === 2) btn.classList.add('text-primary');
        else if (repeatMode === 0) btn.classList.remove('text-primary');
    }
}

async function checkAndRefillQueue() {
    if (playQueue.length - currentIndex <= 3) {
        const track = playQueue[playQueue.length - 1];
        if (!track) return;
        try {
            const res = await fetch(`/Home/GetRecommendations?videoId=${track.videoId}`);
            const data = await res.json();
            if (data && data.length > 0) {
                const existing = new Set(playQueue.map(q => q.videoId));
                data.forEach(v => {
                    if (!existing.has(v.youtubeVideoId)) {
                        playQueue.push({
                            videoId: v.youtubeVideoId, title: v.title, author: v.authorName,
                            thumbnail: v.thumbnailUrl, isRecommended: true
                        });
                    }
                });
                if (typeof renderQueue === 'function') renderQueue();
            }
        } catch(e) {}
    }
}

window.togglePlayerLike = async function() {
    if (!currentSongDbId) return;
    const btn = document.getElementById('playerLikeBtn');
    if (!btn) return;
    const icon = btn.querySelector('i');
    
    try {
        const formData = new FormData();
        formData.append('songId', currentSongDbId);
        const res = await fetch('/Interaction/ToggleLike', { method: 'POST', body: formData });
        const data = await res.json();
        if(data.success) {
            if(data.isLiked) {
                if (icon) icon.className = 'fa-solid fa-heart text-danger';
            } else {
                if (icon) icon.className = 'fa-regular fa-heart text-dim';
            }
        }
    } catch(e) { console.error('Error toggling like:', e); }
}

function formatTime(s) {
    if (isNaN(s)) return "0:00";
    const m = Math.floor(s / 60);
    const r = Math.floor(s % 60);
    return `${m}:${r < 10 ? '0' : ''}${r}`;
}

function startTracking() {
    if (trackingInterval) clearInterval(trackingInterval);
    lastReportedTime = 0;
    trackingInterval = setInterval(() => {
        if (audioPlayer && !audioPlayer.paused && currentSongDbId) {
            const delta = audioPlayer.currentTime - lastReportedTime;
            if (delta >= 10) {
                reportProgress(delta);
                lastReportedTime = audioPlayer.currentTime;
            }
        }
    }, 5000);
}

async function reportProgress(seconds) {
    if (!currentSongDbId) return;
    try {
        const formData = new FormData();
        formData.append('songId', currentSongDbId);
        formData.append('durationSeconds', seconds);
        await fetch('/Interaction/UpdateListeningStats', { method: 'POST', body: formData });
    } catch (e) {}
}

