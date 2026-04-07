/**
 * ui.js - Common UI interactions and component management
 */

// --- NAVIGATION & DRAWERS ---
window.toggleQueue = function() {
    const qd = document.getElementById('queueDrawer');
    if (qd) qd.classList.toggle('open');
}

window.toggleMetadata = function() {
    const md = document.getElementById('fullPlayerOverlay');
    if (md) {
        md.classList.toggle('open');
        if (md.classList.contains('open')) {
            // Update the overlay with current track info
            const thumb = document.getElementById('currentThumbnail')?.src;
            const title = document.getElementById('currentTitle')?.textContent;
            const artist = document.getElementById('currentAuthor')?.textContent;
            
            if (thumb) {
                document.getElementById('fullPlayerThumb').src = thumb;
                document.getElementById('fullPlayerBackdrop').style.backgroundImage = `url(${thumb})`;
                document.getElementById('fullPlayerBackdrop').style.backgroundSize = 'cover';
                document.getElementById('fullPlayerBackdrop').style.backgroundPosition = 'center';
                document.getElementById('fullPlayerBackdrop').style.filter = 'blur(80px) brightness(0.5)';
            }
            if (title) document.getElementById('fullPlayerTitle').textContent = title;
            if (artist) document.getElementById('fullPlayerArtist').textContent = artist;

            // Hide body scroll
            document.body.style.overflow = 'hidden';
        } else {
            document.body.style.overflow = '';
        }
    }
}

// --- CAROUSEL SCROLLING ---
window.scrollCarousel = function(btn, direction) {
    const carousel = btn.parentElement.querySelector('.horizontal-carousel');
    if (carousel) {
        const scrollAmount = carousel.offsetWidth * 0.8;
        carousel.scrollBy({
            left: direction * scrollAmount,
            behavior: 'smooth'
        });
    }
}

// --- SONG DETAILS MODAL ---
window.showSongDetails = async function(id) {
    if(!id) return;
    try {
        const res = await fetch(`/Home/GetVideoDetails?videoUrl=https://www.youtube.com/watch?v=${id}`);
        const data = await res.json();
        
        const thumb = document.getElementById('detailsThumb');
        const title = document.getElementById('detailsTitle');
        const artist = document.getElementById('detailsArtist');
        const views = document.getElementById('detailsViews');
        const genre = document.getElementById('detailsGenre');
        const blur = document.getElementById('detailsBlur');
        const tags = document.getElementById('detailsTags');

        if (thumb) thumb.src = data.thumbnailUrl;
        if (title) title.textContent = data.title;
        if (artist) artist.textContent = data.authorName;
        if (views) views.textContent = data.viewCount.toLocaleString();
        if (genre) genre.textContent = data.genre;
        if (blur) {
            blur.style.background = `url(${data.thumbnailUrl}) center/cover`;
            blur.style.filter = 'blur(60px) brightness(0.3)';
        }
        
        if (tags) {
            tags.innerHTML = (data.tags || []).map(t => 
                `<span class="badge bg-white bg-opacity-10 text-dim rounded-pill small p-2 px-3 fw-normal">${t}</span>`
            ).join('');
        }
        
        const modalEl = document.getElementById('songDetailsModal');
        if (modalEl) {
            const modal = new bootstrap.Modal(modalEl);
            modal.show();
        }
    } catch(e) { console.error('Error showing details:', e); }
}

// --- PLAYLIST ADDITION ---
let currentTargetSongId = null;
let currentTargetYoutubeId = null;

window.openAddToPlaylist = async function(songId, youtubeId) {
    currentTargetSongId = songId;
    currentTargetYoutubeId = youtubeId;
    const container = document.getElementById('playlistList');
    const playlistModal = document.getElementById('addToPlaylistModal');

    if (container) {
        container.innerHTML = '<div class="text-center py-3"><i class="fa-solid fa-spinner fa-spin me-2"></i>Đang tải...</div>';
    }
    
    if (playlistModal) {
        const modal = new bootstrap.Modal(playlistModal);
        modal.show();

        try {
            const res = await fetch('/Playlist/GetPlaylistsJson');
            if (!res.ok) {
                container.innerHTML = '<div class="text-center py-3 text-danger">Vui lòng đăng nhập để sử dụng tính năng này.</div>';
                return;
            }
            const playlists = await res.json();
            if (playlists.length === 0) {
                container.innerHTML = '<div class="text-center py-3 text-muted">Bạn chưa có playlist nào. <a href="/Playlist" class="text-accent">Tạo ngay</a></div>';
            } else {
                container.innerHTML = playlists.map(p => `
                    <button onclick="addToPlaylist(${p.playlistId})" class="btn btn-dark text-start border-secondary border-opacity-25 hover-bg-accent rounded-3 p-3 mb-1 w-100">
                        <i class="fa-solid fa-plus me-2 text-accent"></i> ${p.title}
                    </button>
                `).join('');
            }
        } catch(e) {
            if (container) container.innerHTML = '<p class="text-center text-danger">Lỗi kết nối.</p>';
        }
    }
}

window.addToPlaylist = async function(playlistId) {
    try {
        let url = "/Playlist/AddSong";
        let params = new URLSearchParams();
        params.append("playlistId", playlistId);
        
        if (currentTargetSongId) {
            params.append("songId", currentTargetSongId);
        } else {
            url = "/Playlist/AddSongByYoutubeId";
            params.append("youtubeId", currentTargetYoutubeId);
        }

        const res = await fetch(url, {
            method: 'POST',
            body: params
        });

        if (res.ok) {
            const modalEl = document.getElementById('addToPlaylistModal');
            if (modalEl) {
                const modal = bootstrap.Modal.getInstance(modalEl);
                if (modal) modal.hide();
            }
            alert("Đã thêm vào playlist!");
        } else {
            alert("Lỗi khi thêm vào playlist.");
        }
    } catch(e) { alert("Lỗi khi thực hiện thao tác."); }
}

// --- QUEUE MANAGEMENT ---
window.renderQueue = function() {
    const container = document.getElementById('queueContainer');
    if (!container) return;
    
    if (playQueue.length === 0) {
        container.innerHTML = '<div class="p-5 text-center text-dim">Danh sách chờ trống.</div>';
        return;
    }
    
    container.innerHTML = playQueue.map((t, i) => `
        <div class="queue-item ${i === currentIndex ? 'active' : ''}" onclick="currentIndex=${i}; loadAndPlay(playQueue[currentIndex]); renderQueue();">
            <img src="${t.thumbnail}" class="queue-item-thumb" alt="">
            <div class="queue-item-info">
                <div class="queue-item-title">${t.title}</div>
                <div class="queue-item-artist">${t.author}</div>
            </div>
            ${i === currentIndex ? '<div class="playing-bars"><div class="bar"></div><div class="bar"></div><div class="bar"></div></div>' : ''}
            <button class="btn btn-link text-white-50 p-1 ms-2 hover-text-danger" onclick="event.stopPropagation(); removeFromQueue(${i})">
                <i class="fa-solid fa-xmark"></i>
            </button>
        </div>
    `).join('');

    // Scroll active item into view
    const active = container.querySelector('.queue-item.active');
    if (active) active.scrollIntoView({ behavior: 'smooth', block: 'center' });
}

window.removeFromQueue = function(index) {
    playQueue.splice(index, 1);
    if (index === currentIndex) {
        if (playQueue.length > 0) {
            currentIndex = index % playQueue.length;
            loadAndPlay(playQueue[currentIndex]);
        } else {
            currentIndex = -1;
            if (audioPlayer) audioPlayer.src = '';
            setPlayerMessage('Sẵn sàng phát nhạc', 'Chọn bài hát bạn yêu thích');
        }
    } else if (index < currentIndex) {
        currentIndex--;
    }
    renderQueue();
}

// --- AD SEQUENCE ---
window.playAdSequence = async function() {
    if (adSequenceActive) return;
    adSequenceActive = true;
    const overlay = document.getElementById('adOverlay');
    const counter = document.getElementById('adCountdown');
    
    if (overlay && counter) {
        overlay.classList.remove('d-none');
        overlay.classList.add('d-flex');
        overlay.style.display = 'flex';
        
        let timeLeft = 5;
        counter.textContent = timeLeft;
        
        return new Promise(resolve => {
            const timer = setInterval(() => {
                timeLeft--;
                counter.textContent = timeLeft;
                if (timeLeft <= 0) {
                    clearInterval(timer);
                    overlay.classList.add('d-none');
                    overlay.classList.remove('d-flex');
                    overlay.style.display = 'none';
                    adSequenceActive = false;
                    resolve();
                }
            }, 1000);
        });
    }
}

// --- RICH METADATA ---
window.fetchRichMetadata = async function(videoId) {
    const lc = document.getElementById('lyricsContent');
    const bc = document.getElementById('bioContent');
    
    if (lc) lc.innerHTML = '<div class="text-center py-5 opacity-50"><i class="fa-solid fa-spinner fa-spin me-2"></i>Đang tìm lời bài hát...</div>';
    if (bc) bc.innerHTML = 'Đang tải tiểu sử...';

    try {
        const r = await fetch(`/Home/GetRichMetadata?videoId=${videoId}`);
        const data = await r.json();
        
        if (lc) {
            lc.textContent = data.lyrics || "Hiện tại chưa có lời bài hát cho tác phẩm này.";
            // Scroll to top
            lc.parentElement.scrollTop = 0;
        }
        if (bc) bc.textContent = data.bio || "Thông tin nghệ sĩ đang được cập nhật...";
    } catch (e) {
        if (lc) lc.textContent = "Không tìm thấy lời bài hát.";
        if (bc) bc.textContent = "Không tìm thấy tiểu sử nghệ sĩ.";
    }
}
