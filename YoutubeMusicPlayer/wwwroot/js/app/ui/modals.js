/**
 * modals.js - Logic for song details and playlist addition modals
 */

let currentTargetSongId = null;
let currentTargetYoutubeId = null;

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
    } catch(e) { console.error('[Modals] Error showing details:', e); }
};

window.showAddToPlaylistModal = async function(youtubeId, songId = null) {
    if (!youtubeId && !songId) {
        if (window.playQueue && window.currentIndex >= 0 && window.playQueue[window.currentIndex]) {
            youtubeId = window.playQueue[window.currentIndex].videoId;
        } else {
            if (typeof toastr !== 'undefined') toastr.warning("Không có bài hát nào được chọn.");
            return;
        }
    }
    await window.openAddToPlaylist(songId, youtubeId);
};

window.openAddToPlaylist = async function(songId, youtubeId) {
    if (!window.YTM_CONFIG || !window.YTM_CONFIG.isAuthenticated) {
        if (typeof toastr !== 'undefined') toastr.warning("Vui lòng đăng nhập để sử dụng tính năng này.");
        return;
    }

    currentTargetSongId = songId;
    currentTargetYoutubeId = youtubeId;
    const container = document.getElementById('playlistListContainer');
    const playlistModalEl = document.getElementById('addToPlaylistModal');

    if (container) {
        container.innerHTML = '<div class="text-center py-4 opacity-50"><i class="fa-solid fa-circle-notch fa-spin fs-4"></i></div>';
    }
    
    if (playlistModalEl) {
        const modal = new bootstrap.Modal(playlistModalEl);
        modal.show();

        try {
            const res = await fetch('/Playlist/GetPlaylistsJson');
            if (res.status === 401) {
                container.innerHTML = '<div class="text-center py-3 text-danger small">Phiên đăng nhập hết hạn.</div>';
                return;
            }
            const json = await res.json();
            const playlists = json.data || json;
            
            if (!playlists || playlists.length === 0) {
                container.innerHTML = '<div class="text-center py-4 text-dim small">Bạn chưa có playlist nào.</div>';
            } else {
                container.innerHTML = playlists.map(p => `
                    <button onclick="addToPlaylist(${p.playlistId})" class="btn btn-dark text-start border-white border-opacity-5 hover-bg-accent rounded-3 p-3 w-100 d-flex align-items-center mb-2">
                        <div class="bg-accent bg-opacity-10 rounded-2 p-2 me-3">
                            <i class="fa-solid fa-plus text-accent"></i>
                        </div>
                        <span class="fw-bold">${p.title}</span>
                    </button>
                `).join('');
            }
        } catch(e) {
            if (container) container.innerHTML = '<p class="text-center text-danger small">Lỗi kết nối máy chủ.</p>';
        }
    }
};

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

        const res = await fetch(url, { method: 'POST', body: params });
        const data = await res.json();

        if (res.ok && (data.success || data)) {
            const modalEl = document.getElementById('addToPlaylistModal');
            if (modalEl) {
                const modal = bootstrap.Modal.getInstance(modalEl);
                if (modal) modal.hide();
            }
            if (typeof toastr !== 'undefined') toastr.success("Đã thêm vào playlist!");
        } else {
            if (typeof toastr !== 'undefined') toastr.error(data.message || "Lỗi khi thêm vào playlist.");
        }
    } catch(e) { if (typeof toastr !== 'undefined') toastr.error("Lỗi khi thực hiện thao tác."); }
};

window.createNewPlaylistFromModal = async function() {
    const input = document.getElementById('newPlaylistTitle');
    const title = input?.value?.trim();
    
    if (!title) {
        if (typeof toastr !== 'undefined') toastr.info("Vui lòng nhập tên playlist.");
        return;
    }

    try {
        const formData = new FormData();
        formData.append('title', title);
        
        const token = document.querySelector('input[name="__RequestVerificationToken"]')?.value;
        if (token) formData.append('__RequestVerificationToken', token);

        const res = await fetch('/Playlist/Create', {
            method: 'POST',
            body: formData
        });

        if (res.ok) {
            if (typeof toastr !== 'undefined') toastr.success(`Đã tạo playlist "${title}"`);
            if (input) input.value = '';
            window.openAddToPlaylist(currentTargetSongId, currentTargetYoutubeId);
            if (typeof loadSidebarPlaylists === 'function') window.loadSidebarPlaylists();
        } else {
            if (typeof toastr !== 'undefined') toastr.error("Không thể tạo playlist mới.");
        }
    } catch(e) { if (typeof toastr !== 'undefined') toastr.error("Lỗi hệ thống."); }
};
