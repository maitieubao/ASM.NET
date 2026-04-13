/**
 * queue.js - Play queue management (jQuery Refactored)
 */

window.saveQueueState = function() {
    const state = {
        queue: window.playQueue,
        index: window.currentIndex,
        isShuffle: window.isShuffle,
        repeatMode: window.repeatMode,
        timestamp: Date.now()
    };
    localStorage.setItem('ytm-player-state', JSON.stringify(state));
};

window.playSingleTrack = function(videoId, title, author, thumbnail) {
    console.log("[Queue] playSingleTrack:", videoId, title);
    const track = { videoId, title, author, thumbnail, isUserInitiated: true };
    
    if (window.playQueue.length === 0) {
        window.playQueue = [track];
        window.currentIndex = 0;
    } else {
        // If the song is already in the queue, just jump to it
        const idx = window.playQueue.findIndex(t => t.videoId === videoId && videoId !== "undefined" && videoId !== null);
        if (idx === -1) {
            window.playQueue.splice(window.currentIndex + 1, 0, track);
            window.currentIndex++;
        } else {
            window.currentIndex = idx;
            window.playQueue[idx].isUserInitiated = true; // Mark as user initiated even if existing
        }
    }
    
    if (typeof loadAndPlay === 'function') loadAndPlay(window.playQueue[window.currentIndex]);
    if (typeof renderQueue === 'function') renderQueue();
    
    window.saveQueueState();

    if (author && author !== 'Nghệ sĩ' && typeof injectArtistSongs === 'function') {
        injectArtistSongs(author, videoId);
    }
};

window.playTrackInContext = function(trackList, startIndex) {
    if (!trackList || !Array.isArray(trackList) || trackList.length === 0) return;
    
    console.log("[Queue] playTrackInContext, count:", trackList.length, "startAt:", startIndex);
    
    window.playQueue = trackList.map(s => ({
        videoId: s.youtubeVideoId || s.videoId || s.YoutubeVideoId || s.VideoId || s.id,
        title: s.title || s.Title || s.name || s.Name,
        author: s.authorName || s.author || s.AuthorName || s.ArtistName || s.artist || "Playlist Track",
        thumbnail: s.thumbnailUrl || s.thumbnail || s.ThumbnailUrl || s.CoverImageUrl || s.image
    }));
    
    window.currentIndex = startIndex;
    if (typeof loadAndPlay === 'function') loadAndPlay(window.playQueue[window.currentIndex]);
    if (typeof renderQueue === 'function') renderQueue();
    window.saveQueueState();
};

window.playPlaylist = async function(data, shuffled = false, defaultThumb = null) {
    console.log("[Queue] playPlaylist", { isArray: Array.isArray(data), shuffled });
    let tracks = [];

    if (Array.isArray(data)) {
        tracks = data;
    } else if (!isNaN(data) && data !== null && data !== "") {
        try {
            const res = await $.getJSON(`/Playlist/GetPlaylistSongs/${data}`);
            // Robust extraction: Handle SuccessResponse wrapper with varying casing
            tracks = res.data || res.Data || (Array.isArray(res) ? res : []);
            
            if (!Array.isArray(tracks)) {
                console.warn("[Queue] Response format unexpected, attempting fallback to res.items/res.songs");
                tracks = res.items || res.songs || [];
            }
        } catch (e) {
            console.error("[Queue] Failed to load playlist ID:", data, e);
            if (typeof toastr !== 'undefined') toastr.error("Không thể tải danh sách.");
            return;
        }
    }

    if (!tracks || tracks.length === 0) {
        if (typeof toastr !== 'undefined') toastr.warning("Danh sách trống.");
        return;
    }

    window.playQueue = tracks.map(s => ({
        videoId: s.youtubeVideoId || s.videoId || s.YoutubeVideoId || s.VideoId || s.id,
        title: s.title || s.Title || s.name || s.Name,
        author: s.authorName || s.author || s.AuthorName || s.ArtistName || s.artist || "Nghệ sĩ",
        thumbnail: s.thumbnailUrl || s.thumbnail || s.ThumbnailUrl || s.CoverImageUrl || s.image || defaultThumb
    }));

    if (shuffled) window.playQueue.sort(() => Math.random() - 0.5);

    window.currentIndex = 0;
    if (typeof loadAndPlay === 'function') loadAndPlay(window.playQueue[0]);
    if (typeof renderQueue === 'function') renderQueue();
    window.saveQueueState();
    if (typeof toastr !== 'undefined') toastr.info(`${shuffled ? 'Đang phát ngẫu nhiên' : 'Đang phát'} ${tracks.length} bài hát.`);
};

window.nextTrack = function() {
    if (window.playQueue.length === 0) return;
    
    if (window.repeatMode === 1 && window.audioPlayer) { 
        window.audioPlayer.currentTime = 0; 
        if (typeof safePlayCurrentTrack === 'function') window.safePlayCurrentTrack(); 
        return; 
    }
    
    window.currentIndex = window.isShuffle 
        ? Math.floor(Math.random() * window.playQueue.length) 
        : window.currentIndex + 1;
    
    if (window.currentIndex < window.playQueue.length) {
        if (typeof loadAndPlay === 'function') loadAndPlay(window.playQueue[window.currentIndex]);
        if (typeof checkAndRefillQueue === 'function') checkAndRefillQueue();
    } else if (window.repeatMode === 2) { 
        window.currentIndex = 0; 
        if (typeof loadAndPlay === 'function') loadAndPlay(window.playQueue[window.currentIndex]); 
    } else { 
        if (typeof updatePlayPauseUI === 'function') updatePlayPauseUI(false); 
    }
    window.saveQueueState();
};

window.prevTrack = function() {
    if (!window.audioPlayer) return;
    
    // If song played for more than 3 seconds, just restart it
    if (window.audioPlayer.currentTime > 3) {
        window.audioPlayer.currentTime = 0;
        if (typeof safePlayCurrentTrack === 'function') window.safePlayCurrentTrack();
        return;
    }
    
    if (window.currentIndex > 0) {
        window.currentIndex--;
        if (typeof loadAndPlay === 'function') loadAndPlay(window.playQueue[window.currentIndex]);
    } else if (window.repeatMode === 2 && window.playQueue.length > 0) {
        // Repeat All enabled: wrap to end
        window.currentIndex = window.playQueue.length - 1;
        if (typeof loadAndPlay === 'function') loadAndPlay(window.playQueue[window.currentIndex]);
    }
};

window.toggleShuffle = function() {
    window.isShuffle = !window.isShuffle;
    const $btn = $('#shuffleBtn');
    $btn.toggleClass('active', window.isShuffle);
    $btn.toggleClass('text-primary', window.isShuffle);
    
    if (window.isShuffle && window.playQueue.length > 1) {
        // Shuffle the remaining queue visually
        const currentTrack = window.playQueue[window.currentIndex];
        const others = window.playQueue.filter((_, i) => i !== window.currentIndex);
        
        // Fisher-Yates shuffle
        for (let i = others.length - 1; i > 0; i--) {
            const j = Math.floor(Math.random() * (i + 1));
            [others[i], others[j]] = [others[j], others[i]];
        }
        
        window.playQueue = [currentTrack, ...others];
        window.currentIndex = 0;
        if (typeof renderQueue === 'function') renderQueue();
        if (typeof toastr !== 'undefined') toastr.success("Đã xáo trộn danh sách chờ.");
    }
};

window.toggleRepeat = function() {
    window.repeatMode = (window.repeatMode + 1) % 3;
    const $btn = $('#repeatBtn');
    if ($btn.length) {
        // Reset classes
        $btn.removeClass('fa-repeat fa-repeat-1 text-primary active');
        
        if (window.repeatMode === 0) {
            $btn.addClass('fa-solid fa-repeat text-dim');
        } else if (window.repeatMode === 1) {
            $btn.addClass('fa-solid fa-repeat-1 active text-primary');
            if (typeof toastr !== 'undefined') toastr.info("Đang lặp lại 1 bài");
        } else {
            $btn.addClass('fa-solid fa-repeat active text-primary');
            if (typeof toastr !== 'undefined') toastr.info("Đang lặp lại danh sách");
        }
    }
};

window.removeFromQueue = function(index) {
    window.playQueue.splice(index, 1);
    if (index === window.currentIndex) {
        if (window.playQueue.length > 0) {
            window.currentIndex = index % window.playQueue.length;
            if (typeof loadAndPlay === 'function') loadAndPlay(window.playQueue[window.currentIndex]);
        } else {
            window.currentIndex = -1;
            if (audioPlayer) audioPlayer.src = '';
            if (typeof setPlayerMessage === 'function') setPlayerMessage('Sẵn sàng phát nhạc', 'Chọn bài hát yêu thích.');
        }
    } else if (index < window.currentIndex) {
        window.currentIndex--;
    }
    if (typeof renderQueue === 'function') renderQueue();
};

window.injectArtistSongs = function(artistName, currentVideoId) {
    if (!artistName) return;
    $.getJSON(`/Home/GetSongsByArtist?name=${encodeURIComponent(artistName)}`)
        .done(function(data) {
            const list = data.data || data;
            if (list && list.length > 0) {
                const existingIds = new Set(window.playQueue.map(q => q.videoId));
                const existingTitles = new Set(window.playQueue.map(q => q.title.toLowerCase().trim()));
                const newTracks = [];
                
                for (const item of list) {
                    const vId = item.youtubeVideoId;
                    const title = item.title;
                    if (vId === currentVideoId || existingIds.has(vId) || existingTitles.has(title.toLowerCase().trim())) continue;
                    newTracks.push({ videoId: vId, title, author: item.authorName, thumbnail: item.thumbnailUrl });
                    if (newTracks.length >= 10) break;
                }
                
                if (newTracks.length > 0) {
                    window.playQueue.splice(window.currentIndex + 1, 0, ...newTracks);
                    if (typeof renderQueue === 'function') renderQueue();
                }
            }
        });
};

window.checkAndRefillQueue = function() {
    if (window.playQueue.length - window.currentIndex <= 3) {
        const track = window.playQueue[window.playQueue.length - 1];
        if (!track) return;
        $.getJSON(`/Home/GetRecommendations?videoId=${track.videoId}`)
            .done(function(data) {
                if (data && data.length > 0) {
                    const existing = new Set(window.playQueue.map(q => q.videoId));
                    data.forEach(v => {
                        if (!existing.has(v.youtubeVideoId)) {
                            window.playQueue.push({
                                videoId: v.youtubeVideoId, title: v.title, author: v.authorName,
                                thumbnail: v.thumbnailUrl, isRecommended: true
                            });
                        }
                    });
                    if (typeof renderQueue === 'function') renderQueue();
                }
            });
    }
};
