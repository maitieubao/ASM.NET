let currentSongId = null;
let replyToId = null;

window.showSongDetails = async function(id) {
    if(!id) return;
    const modalEl = document.getElementById('songDetailsModal');
    const thumb = document.getElementById('detailsThumb');
    const titleEl = document.getElementById('detailsTitle');
    const artistEl = document.getElementById('detailsArtist');
    const viewsEl = document.getElementById('detailsViews');
    const genreEl = document.getElementById('detailsGenre');
    const blur = document.getElementById('detailsBlur');
    const tags = document.getElementById('detailsTags');

    console.log(`[MODAL-JS] Opening details for ${id} at ${new Date().toLocaleTimeString()}`);

    try {
        const timestamp = new Date().getTime();
        const res = await fetch(`/Home/GetVideoDetails?videoUrl=https://www.youtube.com/watch?v=${id}&t=${timestamp}`);
        const result = await res.json();
        const data = result.data || result; // Handle both direct and SuccessResponse wrap
        
        currentSongId = data.songId;
        cancelReply(); // Reset reply state

        if (thumb) thumb.src = data.thumbnailUrl;
        if (titleEl) titleEl.textContent = data.title;
        if (artistEl) artistEl.textContent = data.authorName;
        if (viewsEl) viewsEl.textContent = (data.viewCount || 0).toLocaleString();
        if (genreEl) genreEl.textContent = data.genre || "Music";
        if (blur) {
            blur.style.background = `url(${data.thumbnailUrl}) center/cover`;
            blur.style.filter = 'blur(60px) brightness(0.3)';
        }
        
        if (tags) {
            tags.innerHTML = (data.tags || []).map(t => 
                `<span class="badge bg-white bg-opacity-10 text-dim rounded-pill small p-2 px-3 fw-normal">${t}</span>`
            ).join('');
        }
        
        // Load Comments
        if (currentSongId) loadComments(currentSongId);

        if (modalEl) {
            const modal = new bootstrap.Modal(modalEl);
            modal.show();
        }
    } catch(e) { console.error('[Modals] Error showing details:', e); }
};

async function loadComments(songId) {
    const container = document.getElementById('commentList');
    if (!container) return;

    try {
        const res = await fetch(`/Comment/GetSongComments?songId=${songId}`);
        const json = await res.json();
        const data = json.data || json;
        const comments = data.comments || [];
        
        document.getElementById('commentCount').textContent = data.totalCount || 0;

        if (comments.length === 0) {
            container.innerHTML = `
                <div class="text-center py-5 text-dim opacity-50">
                    <i class="fa-solid fa-face-smile fs-1 d-block mb-3"></i>
                    <p>Chưa có bình luận nào. Hãy là người đầu tiên!</p>
                </div>`;
            return;
        }

        container.innerHTML = comments.map(c => renderCommentItem(c)).join('');
    } catch (e) {
        console.error('[Modals] Failed to load comments:', e);
        container.innerHTML = `
            <div class="text-center py-4">
                <p class="text-danger small mb-2">Không thể tải bình luận lúc này.</p>
                <button class="btn btn-outline-light btn-sm rounded-pill px-3" onclick="loadComments(${songId})">
                    <i class="fa-solid fa-rotate-right me-1"></i> Thử lại
                </button>
            </div>`;
    }
}

function renderCommentItem(c) {
    const isOwner = window.YTM_CONFIG && window.YTM_CONFIG.userId === c.userId;
    const hasReplies = c.replies && c.replies.length > 0;
    
    return `
        <div class="comment-item mb-4 animate-fade-in" id="comment-${c.commentId}">
            <div class="d-flex gap-3">
                <img src="${c.userAvatar || 'https://ui-avatars.com/api/?name=' + c.userName}" class="rounded-circle" style="width: 40px; height: 40px;">
                <div class="flex-grow-1">
                    <div class="d-flex align-items-center justify-content-between mb-1">
                        <span class="fw-bold text-white small">${c.userName}</span>
                        <span class="text-dim extra-small">${new Date(c.createdAt).toLocaleDateString()}</span>
                    </div>
                    <p class="text-main small mb-2 m-0 comment-content-${c.commentId}">${c.content}</p>
                    
                    <!-- Inline edit form (ẩn mặc định) -->
                    <div class="comment-edit-form-${c.commentId} d-none mt-2">
                        <textarea class="form-control bg-dark text-white border-white border-opacity-10 rounded-3 small mb-2"
                                  id="edit-input-${c.commentId}"
                                  rows="2"
                                  maxlength="1000"
                                  style="resize: none; font-size: 0.85rem;">${c.content}</textarea>
                        <div class="d-flex gap-2">
                            <button class="btn btn-accent btn-sm rounded-pill px-3 fw-bold" style="font-size: 0.75rem;"
                                    onclick="submitEditComment(${c.commentId})">Lưu</button>
                            <button class="btn btn-dark btn-sm rounded-pill px-3" style="font-size: 0.75rem;"
                                    onclick="cancelEdit(${c.commentId})">Hủy</button>
                        </div>
                    </div>

                    <div class="d-flex align-items-center gap-3">
                        <button class="btn btn-link text-dim p-0 extra-small hover-text-accent text-decoration-none" onclick="replyTo('${c.userName}', ${c.commentId})">Trả lời</button>
                        <button class="btn btn-link ${c.isLiked ? 'text-accent' : 'text-dim'} p-0 extra-small hover-text-accent text-decoration-none" onclick="toggleCommentLike(${c.commentId}, this)">
                            <i class="fa-solid fa-heart me-1"></i> ${c.likeCount || 0}
                        </button>
                        ${isOwner ? `
                        <button class="btn btn-link text-dim p-0 extra-small hover-text-accent text-decoration-none" onclick="editComment(${c.commentId})">Sửa</button>
                        <button class="btn btn-link text-dim p-0 extra-small hover-text-danger text-decoration-none" onclick="deleteComment(${c.commentId})">Xóa</button>
                        ` : ''}
                    </div>
                    
                    ${hasReplies ? `
                    <div class="replies-container ms-2 ps-3 border-start border-white border-opacity-10 mt-3">
                        ${c.replies.map(r => renderCommentItem(r)).join('')}
                    </div>
                    ` : ''}
                </div>
            </div>
        </div>`;
}

window.replyTo = function(authorName, commentId) {
    replyToId = commentId;
    const indicator = document.getElementById('replyIndicator');
    indicator.innerHTML = `Đang trả lời <b>${authorName}</b>... <button type="button" class="btn btn-link btn-sm text-dim p-0" onclick="cancelReply()">Hủy</button>`;
    indicator.classList.remove('d-none');
    document.getElementById('commentInput').focus();
};

window.cancelReply = function() {
    replyToId = null;
    const indicator = document.getElementById('replyIndicator');
    if (indicator) indicator.classList.add('d-none');
};

window.submitComment = async function(event) {
    event.preventDefault();
    if (!window.YTM_CONFIG || !window.YTM_CONFIG.isAuthenticated) {
        toastr.warning("Vui lòng đăng nhập để bình luận.");
        return;
    }

    const input = document.getElementById('commentInput');
    const content = input.value.trim();
    if (!content) return;

    try {
        const formData = new FormData();
        formData.append('songId', currentSongId);
        formData.append('content', content);
        if (replyToId) formData.append('parentId', replyToId);

        const res = await fetch('/Comment/AddComment', { method: 'POST', body: formData });
        const json = await res.json();
        
        if (json.success) {
            input.value = '';
            cancelReply();
            loadComments(currentSongId);
            toastr.success("Đã đăng bình luận!");
        } else {
            toastr.error(json.message || "Lỗi khi gửi bình luận.");
        }
    } catch (e) { 
        console.error('[Modals] Submit comment error:', e);
        toastr.error("Lỗi kết nối máy chủ. Vui lòng thử lại sau."); 
    }
};

window.toggleCommentLike = async function(commentId, btn) {
    try {
        const formData = new FormData();
        formData.append('commentId', commentId);
        const res = await fetch('/Comment/ToggleLike', { method: 'POST', body: formData });
        const json = await res.json();
        if (json.success) {
            const data = json.data;
            btn.classList.toggle('text-accent', data.isLiked);
            btn.classList.toggle('text-dim', !data.isLiked);
            btn.innerHTML = `<i class="fa-solid fa-heart me-1"></i> ${data.likeCount}`;
        }
    } catch (e) {}
};

window.deleteComment = async function(commentId) {
    if (!confirm('Xóa bình luận này?')) return;
    try {
        const res = await fetch(`/Comment/DeleteComment?commentId=${commentId}`, { method: 'DELETE' });
        if (res.ok) {
            loadComments(currentSongId);
            toastr.success("Đã xóa bình luận.");
        }
    } catch (e) {}
};

// Hiển thị inline editor cho bình luận
window.editComment = function(commentId) {
    // Ẩn nội dung hiện tại, hiện form sửa
    const contentEl = document.querySelector(`.comment-content-${commentId}`);
    const editForm  = document.querySelector(`.comment-edit-form-${commentId}`);
    if (!contentEl || !editForm) return;

    contentEl.classList.add('d-none');
    editForm.classList.remove('d-none');

    // Focus vào cuối textarea
    const textarea = document.getElementById(`edit-input-${commentId}`);
    if (textarea) {
        textarea.focus();
        textarea.setSelectionRange(textarea.value.length, textarea.value.length);
    }
};

// Hủy chỉnh sửa, khôi phục nội dung cũ
window.cancelEdit = function(commentId) {
    const contentEl = document.querySelector(`.comment-content-${commentId}`);
    const editForm  = document.querySelector(`.comment-edit-form-${commentId}`);
    if (!contentEl || !editForm) return;

    editForm.classList.add('d-none');
    contentEl.classList.remove('d-none');
};

// Gửi nội dung đã sửa lên server
window.submitEditComment = async function(commentId) {
    const textarea = document.getElementById(`edit-input-${commentId}`);
    if (!textarea) return;

    const content = textarea.value.trim();
    if (!content) {
        toastr.warning("Nội dung không được để trống.");
        return;
    }
    if (content.length > 1000) {
        toastr.warning("Bình luận tối đa 1000 ký tự.");
        return;
    }

    try {
        const params = new URLSearchParams();
        params.append('commentId', commentId);
        params.append('content', content);

        const res = await fetch('/Comment/EditComment', {
            method: 'PUT',
            body: params
        });

        const json = await res.json();

        if (res.ok && json.success) {
            // Cập nhật nội dung hiển thị ngay lập tức (không cần reload toàn bộ)
            const contentEl = document.querySelector(`.comment-content-${commentId}`);
            const editForm  = document.querySelector(`.comment-edit-form-${commentId}`);
            if (contentEl) contentEl.textContent = content;
            if (editForm)  editForm.classList.add('d-none');
            if (contentEl) contentEl.classList.remove('d-none');
            toastr.success("Đã cập nhật bình luận.");
        } else {
            toastr.error(json.message || "Không thể cập nhật bình luận.");
        }
    } catch (e) {
        toastr.error("Lỗi kết nối máy chủ.");
    }
};

window.showAddToPlaylistModal = async function(youtubeId, songId = null) {
    if (!youtubeId && !songId) {
        if (window.playQueue && window.currentIndex >= 0 && window.playQueue[window.currentIndex]) {
            youtubeId = window.playQueue[window.currentIndex].videoId;
            // Lấy songId từ currentSongDbId nếu có
            if (!songId && window.currentSongDbId) songId = window.currentSongDbId;
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
        } else if (currentTargetYoutubeId) {
            url = "/Playlist/AddSongByYoutubeId";
            params.append("youtubeId", currentTargetYoutubeId);
        } else {
            if (typeof toastr !== 'undefined') toastr.error("Không xác định được bài hát để thêm.");
            return;
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
        } else if (!res.ok && currentTargetSongId && currentTargetYoutubeId) {
            // Fallback: songId không hợp lệ, thử lại bằng youtubeId
            console.warn('[Playlist] AddSong failed, retrying with youtubeId...');
            const fallbackParams = new URLSearchParams();
            fallbackParams.append("playlistId", playlistId);
            fallbackParams.append("youtubeId", currentTargetYoutubeId);
            const fallbackRes = await fetch("/Playlist/AddSongByYoutubeId", { method: 'POST', body: fallbackParams });
            const fallbackData = await fallbackRes.json();
            if (fallbackRes.ok && (fallbackData.success || fallbackData)) {
                const modalEl = document.getElementById('addToPlaylistModal');
                if (modalEl) { const modal = bootstrap.Modal.getInstance(modalEl); if (modal) modal.hide(); }
                if (typeof toastr !== 'undefined') toastr.success("Đã thêm vào playlist!");
            } else {
                if (typeof toastr !== 'undefined') toastr.error(fallbackData.message || "Lỗi khi thêm vào playlist.");
            }
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
